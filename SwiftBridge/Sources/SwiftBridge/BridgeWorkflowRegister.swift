//
//  File.swift
//  
//
//  Created by Jonathan Thorpe on 05/06/2023.
//

import Foundation
import Combine

private protocol WorkflowImplementation { }

private protocol SyncWorkflowImplementation : WorkflowImplementation {
    func perform(payload: String) throws -> String
}

private protocol AsyncWorkflowImplementation : WorkflowImplementation {
    func perform(payload: String) async throws -> String
}

public enum BridgeWorkflowRegisterError : Error {
    case procedureConflict(String)
}

public class BridgeWorkflowRegister {
    
    private let bridge : Bridge
    
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()
    
    // actual running tasks launched from WorkflowImplementation
    private var incomingWorkflowTasks : [String:Task<Void, Error>] = [:]
    // registered implementations used to create tasks for a procedure
    private var incomingWorkflowImplementations : [String:WorkflowImplementation] = [:]
    
    private var subscriptions = Set<AnyCancellable>()
    
    init(bridge: Bridge) {
        self.bridge = bridge
        self.bridge.publishContent(path: WorkflowRequest.path)
            .sink { [weak self] (request : WorkflowRequest) in
                guard let self = self else {
                    return
                }
                guard let implementation = self.incomingWorkflowImplementations[request.procedure] else {
                    let error = WorkflowError.invalidProcedure(request.procedure)
                    try? self.reportFailure(identifier: request.identifier, error: error)
                    return
                }
                switch implementation {
                case let syncImplementation as SyncWorkflowImplementation:
                    do {
                        let result = try syncImplementation.perform(payload: request.payload)
                        try self.reportCompletion(identifier: request.identifier, result: result)
                    } catch {
                        try? self.reportFailure(identifier: request.identifier, error: error)
                    }
                    break
                case let asyncImplementation as AsyncWorkflowImplementation:
                    self.incomingWorkflowTasks[request.identifier] = Task { () -> Void in
                        defer { self.incomingWorkflowTasks.removeValue(forKey: request.identifier) }
                        do {
                            let result = try await asyncImplementation.perform(payload: request.payload)
                            try self.reportCompletion(identifier: request.identifier, result: result)
                        } catch {
                            try? self.reportFailure(identifier: request.identifier, error: error)
                        }
                    }
                    break
                default:
                    break
                }
            }.store(in: &subscriptions)
        self.bridge.publishContent(path: WorkflowCancellation.path)
            .sink { [weak self] (cancellation : WorkflowCancellation) in
                guard let task = self?.incomingWorkflowTasks[cancellation.identifier] else { return }
                task.cancel()
                self?.incomingWorkflowTasks.removeValue(forKey: cancellation.identifier)
            }.store(in: &subscriptions)
    }
    
    //MARK: Outgoing
    
    public func performWorkflow<TPayload, TResult>(procedure: String, payload: TPayload) async throws -> TResult
    where TPayload : Encodable, TResult : Decodable {
        throw CancellationError()
    }
    
    private func performWorkflow<TPayload>(procedure: String, payload: TPayload) async throws -> WorkflowCompletion
    where TPayload : Encodable {
        let identifier = UUID().uuidString
        var cancellables = Set<AnyCancellable>()
        return try await withTaskCancellationHandler(operation: {
            return try await withCheckedThrowingContinuation { (continuation : CheckedContinuation<WorkflowCompletion, Error>) in
                bridge.publishContent(path: WorkflowCompletion.path)
                    .first { (result : WorkflowCompletion) in result.identifier == identifier }
                    .sink { continuation.resume(returning: $0) }
                    .store(in: &cancellables)
                bridge.publishContent(path: WorkflowFailure.path)
                    .first { (failure : WorkflowFailure) in failure.identifier == identifier }
                    .sink { continuation.resume(throwing: $0.toError()) }
                    .store(in: &cancellables)
                do {
                    let payload = String(decoding: try encoder.encode(payload), as: UTF8.self)
                    let request = WorkflowRequest(identifier: identifier, procedure: procedure, payload: payload)
                    try bridge.sendMessage(path: WorkflowRequest.path, data: try encoder.encode(request))
                } catch {
                    continuation.resume(throwing: error)
                }
            }
        }, onCancel: {
            if let data = try? self.encoder.encode(WorkflowCancellation(identifier: identifier)) {
                try? bridge.sendMessage(path: WorkflowCancellation.path, data:data)
            }
        })
    }
    
    //MARK: Incoming
    
    public func register<TPayload: Decodable, TResult: Encodable> (
        procedure: String,
        callback : @escaping (TPayload) async throws -> TResult) throws {
            guard incomingWorkflowImplementations[procedure] == nil else {
                throw BridgeWorkflowControllerError.procedureConflict(procedure)
            }
            incomingWorkflowImplementations[procedure] = AsyncCallbackContainer(callback: callback, controller: self)
    }
    
    public func register<TPayload: Decodable, TResult: Encodable> (
        procedure: String,
        callback : @escaping (TPayload) throws -> TResult) throws {
            guard incomingWorkflowImplementations[procedure] == nil else {
                throw BridgeWorkflowControllerError.procedureConflict(procedure)
            }
            incomingWorkflowImplementations[procedure] = SyncCallbackContainer(callback: callback, controller: self)
    }
    
    private func reportFailure(identifier: String, error: Error) throws {
        let failure = WorkflowFailure.from(identifier: identifier, error: error)
        try bridge.sendMessage(path: WorkflowFailure.path, data: try encoder.encode(failure))
    }
    
    private func reportCompletion(identifier: String, result: String) throws {
        let completion = WorkflowCompletion(identifier: identifier, result: result)
        try bridge.sendMessage(path: WorkflowCompletion.path, data: try encoder.encode(result))
    }
    
    private class AsyncCallbackContainer<TPayload: Decodable, TResult: Encodable> : AsyncWorkflowImplementation {
        
        private let callback : (TPayload) async throws -> TResult
        private let controller : BridgeWorkflowController
                
        init(callback: @escaping (TPayload) async throws -> TResult, controller: BridgeWorkflowController) {
            self.callback = callback
            self.controller = controller
        }
        
        func perform(payload: String) async throws -> String {
            let decoded = try controller.decoder.decode(TPayload.self, from: Data(payload.utf8))
            let value = try await callback(decoded)
            let encoded = try controller.encoder.encode(value)
            return String(decoding: encoded, as: UTF8.self)
        }
    }
    
    private class SyncCallbackContainer<TPayload: Decodable, TResult: Encodable> : SyncWorkflowImplementation {
        
        private let callback : (TPayload) throws -> TResult
        private let controller : BridgeWorkflowController
                
        init(callback: @escaping (TPayload) throws -> TResult, controller: BridgeWorkflowController) {
            self.callback = callback
            self.controller = controller
        }
        
        func perform(payload: String) throws -> String {
            let decoded = try controller.decoder.decode(TPayload.self, from: Data(payload.utf8))
            let value = try callback(decoded)
            let encoded = try controller.encoder.encode(value)
            return String(decoding: encoded, as: UTF8.self)
        }
    }
}
