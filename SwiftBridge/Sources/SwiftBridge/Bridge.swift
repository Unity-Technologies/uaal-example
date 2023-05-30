//
//  Bridge.swift
//  
//
//  Created by Jonathan Thorpe on 30/05/2023.
//

import Foundation



public class Bridge {
    
    let messenger : BridgeMessenger
    let listener : BridgeListener
    
    let encoder = JSONEncoder()
    let decoder = JSONDecoder()
    
    init (messenger: BridgeMessenger, listener: BridgeListener) {
        self.messenger = messenger
        self.listener = listener
    }
}
