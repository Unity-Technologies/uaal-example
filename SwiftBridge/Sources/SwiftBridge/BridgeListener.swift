//
//  BridgeListener.swift
//
//  Created by Jonathan Thorpe on 29/05/2023.
//

import Foundation
import Combine

public struct BridgePayload : Codable {
    
    static let notificationName = NSNotification.Name(rawValue: "BrideIncomingPayloadNotification")
    static let notificationPayloadKey = "BrideIncomingPayloadKey"
    
    let path : String
    let content : String
}

public class BridgeListener {
    
    public var notifications : AnyPublisher<BridgePayload, Never> {
        subject.eraseToAnyPublisher()
    }
    
    private var subscription : AnyCancellable?
    private let subject = PassthroughSubject<BridgePayload, Never>()
    
    private init() {
        subscription = NotificationCenter.default.publisher(for: BridgePayload.notificationName).sink { value in
            guard let userInfo = value.userInfo else {
                return
            }
            guard let raw = userInfo[BridgePayload.notificationPayloadKey] else {
                return
            }
            guard let notification = raw as? BridgePayload else {
                return
            }
            self.subject.send(notification)
        }
    }
    
    deinit {
        subscription?.cancel()
    }
    
}


extension Publisher {
    
    func decode<T:Decodable>(path: String) -> AnyPublisher<T, Self.Failure> where Output == BridgePayload {
        self
            .filter { $0.path == path}
            .compactMap { try? JSONDecoder().decode(T.self, from: Data($0.content.utf8)) }
            .eraseToAnyPublisher()
    }
}
