//
//  BridgeListener.swift
//
//  Created by Jonathan Thorpe on 29/05/2023.
//

import Foundation
import Combine

public class BridgeNotification : NSObject {
    static let notificationName = NSNotification.Name(rawValue: "BrideIncomingPayloadNotification")
    static let notificationPathKey = "BrideIncomingPathKey"
    static let notificationContentKey = "BrideIncomingContentKey"
}

public struct BridgePayload : Codable {
    public let path : String
    public let content : String
    // need to define explicitely for use outside package
    public init(path: String, content: String) {
        self.path = path
        self.content = content
    }
}

public protocol BridgeListener {
    var notifications : AnyPublisher<BridgePayload, Never> { get }
}

public class DefaultBridgeListener : BridgeListener {
    
    public var notifications : AnyPublisher<BridgePayload, Never> {
        subject.eraseToAnyPublisher()
    }
    
    private var subscription : AnyCancellable?
    private let subject = PassthroughSubject<BridgePayload, Never>()
    
    private init() {
        subscription = NotificationCenter.default.publisher(for: BridgeNotification.notificationName).sink { value in
            guard let userInfo = value.userInfo else {
                return
            }
            guard let path = userInfo[BridgeNotification.notificationPathKey] as? String else {
                return
            }
            guard let content = userInfo[BridgeNotification.notificationContentKey] as? String else {
                return
            }
            self.subject.send(BridgePayload(path: path, content: content))
        }
    }
    
    deinit {
        subscription = nil
    }
}


public extension Publisher {
    
    func decode<T:Decodable>(path: String) -> AnyPublisher<T, Self.Failure> where Output == BridgePayload {
        self
            .filter { $0.path == path}
            .compactMap { try? JSONDecoder().decode(T.self, from: Data($0.content.utf8)) }
            .eraseToAnyPublisher()
    }
}
