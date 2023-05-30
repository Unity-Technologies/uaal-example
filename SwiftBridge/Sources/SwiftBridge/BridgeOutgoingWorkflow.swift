//
//  BridgeOutgoingWorkflow.swift
//  
//
//  Created by Jonathan Thorpe on 30/05/2023.
//

import Foundation
import Combine

extension Bridge {
    
    public enum OutgoingWorkflowError : Error {
        case notImplemented
    }
    
    private struct OutgoingWorkflowResult : Codable {
        static let path = "/workflow/result"
        let success : Bool
        let result : String
    }

    private struct OutgoingWorkflowFailure : Codable {
        let type : String
        let message : String
    }

    private struct OutgoingWorkflowRequest {
        static let path = "/workflow/request"
        struct Wrapper<TPayload> : Encodable where TPayload : Encodable {
            let identifier : String
            let payload : TPayload
        }
    }
    
    private struct OutgoingWorkflowCancellation {
        static let path = "/workflow/cancel"
    }
    
    private var outgoingWorkflowResults : AnyPublisher<OutgoingWorkflowResult, Never> {
        listener.notifications.decode(path: OutgoingWorkflowResult.path)
    }
    
    func performWorkflow<TPayload, TResult>(payload: TPayload) async throws -> TResult
    where TPayload : Encodable, TResult : Decodable {
        throw OutgoingWorkflowError.notImplemented
    }
    
    private func performWorkflow<TPayload> (payload: TPayload) async throws -> OutgoingWorkflowResult where TPayload : Encodable {
        let identifier = UUID().uuidString
        return try await withTaskCancellationHandler(operation: {
            return try await withCheckedThrowingContinuation { (continuation : CheckedContinuation<OutgoingWorkflowResult, Error>) in
                do {
                    let wrapper = OutgoingWorkflowRequest.Wrapper(identifier: identifier, payload: payload)
                    let content = try encoder.encode(payload)
                    messenger.sendPayload(path: OutgoingWorkflowRequest.path, data: try encoder.encode(payload))
                } catch {
                    continuation.resume(throwing: error)
                }
            }
        }, onCancel: {
            messenger.sendPayload(path: OutgoingWorkflowCancellation.path, content: identifier)
        })
    }
}
