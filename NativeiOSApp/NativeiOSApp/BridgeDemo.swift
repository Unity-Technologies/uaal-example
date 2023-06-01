//
//  BridgeDemo.swift
//  NativeiOSApp
//
//  Created by Jonathan Thorpe on 01/06/2023.
//  Copyright © 2023 unity. All rights reserved.
//

import Foundation
import UIKit
import SwiftBridge

@objc public class BridgeDemo : NSObject {
    @objc public func start() {
        
    }
}

enum UnityBridgeMessengerError : Error {
    case notInitialized
}

class UnityBridgeMessenger : BridgeMessenger {
    
    let gameObject : String
    let method : String
    
    let encoder = JSONEncoder()
    
    init(gameObject: String, method: String) {
        self.gameObject = gameObject
        self.method = method
    }
    
    func sendPayload(path: String, content: String) throws {
        guard let appDelegate = UIApplication.shared.delegate as? AppDelegate else {
            throw UnityBridgeMessengerError.notInitialized
        }
        let payload = BridgePayload(path: path, content: content)
        let message = String(decoding: try encoder.encode(payload), as: UTF8.self)
        appDelegate.sendMessageToGO(withName: gameObject, functionName: method, message: message)
    }
    
}


