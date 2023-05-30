//
//  BridgeMessenger.swift
//
//  Created by Jonathan Thorpe on 29/05/2023.
//

import Foundation

protocol BridgeMessenger {
    func sendPayload(path: String, content: String)
}

extension BridgeMessenger {
    
    func sendPayload(payload: BridgePayload) {
        self.sendPayload(path: payload.path, content: payload.content)
    }
    
    func sendPayload(path: String, data: Data) {
        sendPayload(path: path, content: String(decoding: data, as: UTF8.self))
    }
    
}


