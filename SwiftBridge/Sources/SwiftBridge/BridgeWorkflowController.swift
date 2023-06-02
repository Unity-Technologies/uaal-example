//
//  File.swift
//
//  Created by Jonathan Thorpe on 02/06/2023.
//

import Foundation
import Combine

fileprivate protocol WorkflowImplementation {
    func perform(identifier: String, payload: String) async throws
}

class BridgeWorkflowController {
    
    private struct WorkflowResult : Codable {
        static let path = "/workflow/completed"
        let identifier : String
        let result : String
    }

    private struct WorkflowFailure : Codable {
        static let path = "/workflow/failed"
        let identifier : String
        let type : String
        let message : String
    }

    private struct WorkflowRequest : Codable {
        static let path = "/workflow/request"
        let identifier : String
        let procedure : String
        let payload : String
    }

    private struct WorkflowCancellation : Codable  {
        static let path = "/workflow/cancel"
        let identifier : String
    }
    
    private let bridge : Bridge
    
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()
    
    // actual running tasks launched from WorkflowImplementation
    private var incomingWorkflowTasks : [String:Task<Void, Error>] = [:]
    // registered implementations used to create tasks for a procedure
    private var incomingWorkflowImplementations : [String:WorkflowImplementation] = [:]
    private var requestSubscription : AnyCancellable?
    private var cancelSubscription : AnyCancellable?
    
    init(bridge: Bridge) {
        self.bridge = bridge
        self.requestSubscription = self.bridge.publishContent(path: WorkflowRequest.path)
            .sink { [weak self] (request : WorkflowRequest) in
                guard let self = self else {
                    return
                }
                guard let implementation = self.incomingWorkflowImplementations[request.procedure] else {
                    return
                }
                self.incomingWorkflowTasks[request.identifier] = Task { () -> Void in
                    defer { self.incomingWorkflowTasks.removeValue(forKey: request.identifier) }
                    try await implementation.perform(identifier: request.identifier, payload: request.payload)
                }
        }
        self.cancelSubscription = self.bridge.publishContent(path: WorkflowCancellation.path)
            .sink { [weak self] (cancellation : WorkflowCancellation) in
                guard let task = self?.incomingWorkflowTasks[cancellation.identifier] else { return }
                task.cancel()
                self?.incomingWorkflowTasks.removeValue(forKey: cancellation.identifier)
            }
    }
    
    deinit {
        requestSubscription = nil
        cancelSubscription = nil
    }
    
    //MARK: Outgoing
    
    public enum OutgoingWorkflowError : Error {
        case notImplemented
        case failedEncoding
    }
    
    public func performWorkflow<TPayload, TResult>(procedure: String, payload: TPayload) async throws -> TResult
    where TPayload : Encodable, TResult : Decodable {
        throw OutgoingWorkflowError.notImplemented
    }
    
    private func performWorkflow<TPayload>(procedure: String, payload: TPayload) async throws -> WorkflowResult
    where TPayload : Encodable {
        let identifier = UUID().uuidString
        return try await withTaskCancellationHandler(operation: {
            return try await withCheckedThrowingContinuation { (continuation : CheckedContinuation<WorkflowResult, Error>) in
                let subscription = bridge.publishContent(path: WorkflowResult.path)
                    .first { (result : WorkflowResult) in result.identifier == identifier }
                    .sink { continuation.resume(returning: $0) }
                do {
                    guard let payload = String(data: try encoder.encode(payload), encoding: .utf8) else {
                        throw OutgoingWorkflowError.failedEncoding
                    }
                    let request = WorkflowRequest(identifier: identifier, procedure: procedure, payload: payload)
                    try bridge.sendMessage(path: WorkflowRequest.path, data: try encoder.encode(request))
                } catch {
                    subscription.cancel()
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
    
    public enum IncomingWorkflowError : Error {
        case notImplemented
        case failedEncoding
        case noBridge
        case procedureConflict
    }
    
    public func register<TPayload: Decodable, TResult: Encodable> (
        procedure: String,
        callback : @escaping (TPayload) async throws -> TResult) throws {
            guard incomingWorkflowImplementations[procedure] == nil else {
                throw IncomingWorkflowError.procedureConflict
            }
            incomingWorkflowImplementations[procedure] = CallbackContainer(callback: callback, controller: self)
    }
    
    private class CallbackContainer<TPayload: Decodable, TResult: Encodable> : WorkflowImplementation {
        
        private let callback : (TPayload) async throws -> TResult
        private let controller : BridgeWorkflowController
                
        init(callback: @escaping (TPayload) async throws -> TResult, controller: BridgeWorkflowController) {
            self.callback = callback
            self.controller = controller
        }
        
        func perform(identifier: String, payload: String) async throws {
            do {
                let decoded = try controller.decoder.decode(TPayload.self, from: Data(payload.utf8))
                let value = try await callback(decoded)
                let encoded = try controller.encoder.encode(value)
                let result = WorkflowResult(identifier: identifier, result: String(decoding: encoded, as: UTF8.self))
                let data = try controller.encoder.encode(result)
                try controller.bridge.sendMessage(path: WorkflowResult.path, data: data)
            } catch is CancellationError {
                let cancellation = WorkflowCancellation(identifier: identifier)
                if let data = try? controller.encoder.encode(cancellation) {
                    try controller.bridge.sendMessage(path: WorkflowCancellation.path, data: data)
                }
            } catch {
                let failure = WorkflowFailure(identifier: identifier, type: "\(error)", message: "")
                if let data = try? controller.encoder.encode(failure) {
                    try controller.bridge.sendMessage(path: WorkflowFailure.path, data: data)
                }
            }
        }
    }
}
