//
//  Bridge.swift
//  
//
//  Created by Jonathan Thorpe on 30/05/2023.
//

import Foundation
import Combine

protocol WorkflowImplementation {
    func perform(identifier: String, payload: String) async
}

public class Bridge {
    
    struct WorkflowResult : Codable {
        static let path = "/workflow/completed"
        let identifier : String
        let result : String
    }

    struct WorkflowFailure : Codable {
        static let path = "/workflow/failed"
        let identifier : String
        let type : String
        let message : String
    }

    struct WorkflowRequest : Codable {
        static let path = "/workflow/request"
        let identifier : String
        let payload : String
    }

    struct WorkflowCancellation : Codable  {
        static let path = "/workflow/cancel"
        let identifier : String
    }
    
    let messenger : BridgeMessenger
    let listener : BridgeListener
    
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()
    
    private var incomingWorkflowTasks : [String:Task<Void, Never>] = [:]
    private var incomingWorkflowImplementations : [String:WorkflowImplementation] = [:]
    private let requestSubscription : AnyCancellable
    private let cancelSubscription : AnyCancellable
    
    init (messenger: BridgeMessenger, listener: BridgeListener) {
        self.messenger = messenger
        self.listener = listener
        self.requestSubscription = listener.notifications
            .decode(path: WorkflowRequest.path)
            .sink { (request : WorkflowRequest) in
            
        }
        self.cancelSubscription = listener.notifications
            .decode(path: WorkflowCancellation.path)
            .sink { (cancellation : WorkflowCancellation) in
            
        }
    }
    
    //MARK: Outgoing
    
    public enum OutgoingWorkflowError : Error {
        case notImplemented
        case failedEncoding
    }
    
    private var outgoingWorkflowResults : AnyPublisher<WorkflowResult, Never> {
        listener.notifications.decode(path: WorkflowResult.path)
    }
    
    func performWorkflow<TPayload, TResult>(payload: TPayload) async throws -> TResult
    where TPayload : Encodable, TResult : Decodable {
        throw OutgoingWorkflowError.notImplemented
    }
    
    private func performWorkflow<TPayload> (payload: TPayload) async throws -> WorkflowResult where TPayload : Encodable {
        let identifier = UUID().uuidString
        return try await withTaskCancellationHandler(operation: {
            return try await withCheckedThrowingContinuation { (continuation : CheckedContinuation<WorkflowResult, Error>) in
                let subscription = outgoingWorkflowResults
                    .first { $0.identifier == identifier }
                    .sink { continuation.resume(returning: $0) }
                do {
                    guard let payload = String(data: try encoder.encode(payload), encoding: .utf8) else {
                        throw OutgoingWorkflowError.failedEncoding
                    }
                    let request = WorkflowRequest(identifier: identifier, payload: payload)
                    messenger.sendPayload(path: WorkflowRequest.path, data: try encoder.encode(request))
                } catch {
                    subscription.cancel()
                    continuation.resume(throwing: error)
                }
            }
        }, onCancel: {
            if let data = try? self.encoder.encode(WorkflowCancellation(identifier: identifier)) {
                messenger.sendPayload(path: WorkflowCancellation.path, data:data)
            }

        })
    }
    
    //MARK: Incoming
    
    public enum IncomingWorkflowError : Error {
        case notImplemented
        case failedEncoding
        case noBridge
        case implementationConflict
    }
    
    func register<TPayload: Decodable, TResult: Encodable> (
        path: String,
        callback : @escaping (TPayload) async throws -> TResult) throws {
            guard incomingWorkflowImplementations[path] == nil else {
                throw IncomingWorkflowError.implementationConflict
            }
            incomingWorkflowImplementations[path] = CallbackContainer(callback: callback, bridge: self)
    }
    
    private class CallbackContainer<TPayload: Decodable, TResult: Encodable> : WorkflowImplementation {
        
        private let callback : (TPayload) async throws -> TResult
        private let bridge : Bridge
                
        init(callback: @escaping (TPayload) async throws -> TResult, bridge: Bridge) {
            self.callback = callback
            self.bridge = bridge
        }
        
        func perform(identifier: String, payload: String) async {
            do {
                let decoded = try bridge.decoder.decode(TPayload.self, from: Data(payload.utf8))
                let value = try await callback(decoded)
                let encoded = try bridge.encoder.encode(value)
                let result = WorkflowResult(identifier: identifier, result: String(decoding: encoded, as: UTF8.self))
                bridge.messenger.sendPayload(path: WorkflowResult.path, data: try bridge.encoder.encode(result))
            } catch is CancellationError {
                if let cancellation = try? bridge.encoder.encode(WorkflowCancellation(identifier: identifier)) {
                    bridge.messenger.sendPayload(path: WorkflowCancellation.path, data: cancellation)
                }
            } catch {
                if let failure = try? bridge.encoder.encode(WorkflowFailure(identifier: identifier, type: "\(error)", message: "")) {
                    bridge.messenger.sendPayload(path: WorkflowFailure.path, data: failure)
                }
            }
        }
    }
}
