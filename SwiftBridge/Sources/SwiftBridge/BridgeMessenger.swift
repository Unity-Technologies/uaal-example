//
//  BridgeMessenger.swift
//
//  Created by Jonathan Thorpe on 29/05/2023.
//

import Foundation

public protocol BridgeMessenger {
    func sendPayload(path: String, content: String) throws
}

public extension BridgeMessenger {
    
    func sendPayload(payload: BridgePayload) throws {
        try self.sendPayload(path: payload.path, content: payload.content)
    }
    
    func sendPayload(path: String, data: Data) throws {
        try sendPayload(path: path, content: String(decoding: data, as: UTF8.self))
    }
    
}


