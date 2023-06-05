//
//  BridgeWorkflowController.swift
//
//  Created by Jonathan Thorpe on 02/06/2023.
//

import Foundation
import Combine

extension WorkflowFailure {
    func toError() -> Error {
        switch type {
        case WorkflowFailure.invalidType:
            return WorkflowError.invalidProcedure(message)
        case WorkflowFailure.cancellationType:
            return CancellationError()
        default:
            return WorkflowError.runtime(type: type, message: message)
        }
    }
}

public class BridgeWorkflowPerformer {
    
    private let bridge : Bridge
    
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()
    
    private var subscriptions = Set<AnyCancellable>()
    private var continuations : [String:CheckedContinuation<WorkflowCompletion, Error>] = [:]
    
    init(bridge: Bridge) {
        self.bridge = bridge
        self.bridge.publishContent(path: WorkflowCompletion.path).sink { [weak self] (completion : WorkflowCompletion) in
            guard let continuation = self?.continuations[completion.identifier] else { return }
            continuation.resume(returning: completion)
            self?.continuations.removeValue(forKey: completion.identifier)
        }.store(in: &subscriptions)
        self.bridge.publishContent(path: WorkflowFailure.path).sink { [weak self] (failure : WorkflowFailure) in
            guard let continuation = self?.continuations[failure.identifier] else { return }
            continuation.resume(throwing: failure.toError())
            self?.continuations.removeValue(forKey: failure.identifier)
        }.store(in: &subscriptions)
    }
    
    public func perform<TPayload, TResult>(procedure: String, payload: TPayload) async throws -> TResult
    where TPayload : Encodable, TResult : Decodable {
        let completion = try await performWorkflow(procedure: procedure, payload: payload)
        return try decoder.decode(TResult.self, from: Data(completion.result.utf8))
    }
    
    private func performWorkflow<TPayload>(procedure: String, payload: TPayload) async throws -> WorkflowCompletion
    where TPayload : Encodable {
        let identifier = UUID().uuidString
        return try await withTaskCancellationHandler(operation: {
            return try await withCheckedThrowingContinuation { (continuation : CheckedContinuation<WorkflowCompletion, Error>) in
                continuations[identifier] = continuation
                do {
                    let payload = String(decoding: try encoder.encode(payload), as: UTF8.self)
                    let request = WorkflowRequest(identifier: identifier, procedure: procedure, payload: payload)
                    try bridge.sendMessage(path: WorkflowRequest.path, data: try encoder.encode(request))
                } catch {
                    continuation.resume(throwing: error)
                    continuations.removeValue(forKey: identifier)
                }
            }
        }, onCancel: {
            // note : if we want immediate cancellation we can throw CancellationError on the continuation here
            if let data = try? self.encoder.encode(WorkflowCancellation(identifier: identifier)) {
                try? bridge.sendMessage(path: WorkflowCancellation.path, data:data)
            }
        })
    }
    
}
