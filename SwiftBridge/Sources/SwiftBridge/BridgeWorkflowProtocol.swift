//
//  BridgeWorkflow.swift
//  
//
//  Created by Jonathan Thorpe on 05/06/2023.
//

import Foundation

public enum WorkflowError : Error {
    case noBridge
    case invalidProcedure(String)
    case runtime(type: String, message: String)
    case unknown
}

struct WorkflowCompletion : Codable {
    static let path = "/workflow/completed"
    let identifier : String
    let result : String
}

struct WorkflowFailure : Codable {
    static let path = "/workflow/failed"
    static let cancellationType = "CancelledWorkflow"
    static let invalidType = "InvalidWorkflow"
    let identifier : String
    let type : String
    let message : String
    
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
    
    static func from(identifier: String, error : Error) -> WorkflowFailure {
        switch error {
        case is CancellationError:
            return WorkflowFailure(identifier: identifier, type: WorkflowFailure.cancellationType, message: "")
        case let WorkflowError.invalidProcedure(procedure):
            return WorkflowFailure(identifier: identifier, type: WorkflowFailure.invalidType, message: procedure)
        case let WorkflowError.runtime(type, message):
            return WorkflowFailure(identifier: identifier, type: type, message: message)
        default:
            return WorkflowFailure(identifier: identifier, type: "", message: "")
        }
    }
}

struct WorkflowRequest : Codable {
    static let path = "/workflow/request"
    let identifier : String
    let procedure : String
    let payload : String
}

struct WorkflowCancellation : Codable  {
    static let path = "/workflow/cancel"
    let identifier : String
}
