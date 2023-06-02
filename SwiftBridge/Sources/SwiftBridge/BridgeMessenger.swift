//
//  BridgeMessenger.swift
//
//  Created by Jonathan Thorpe on 29/05/2023.
//

import Foundation

public protocol BridgeMessenger {
    func sendMessage(path: String, content: String) throws
}

public extension BridgeMessenger {
    
    func sendMessage(message: BridgeMessage) throws {
        try self.sendMessage(path: message.path, content: message.content)
    }
    
    func sendMessage(path: String, data: Data) throws {
        try sendMessage(path: path, content: String(decoding: data, as: UTF8.self))
    }
    
}


