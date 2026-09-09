  // Assets/Plugins/iOS/NativeCallProxy.swift
  import Foundation

  @objc public protocol NativeCallsProtocol {
      func showHostMainWindow(_ color: String!)
  }
  
  @objc public class FrameworkLibAPI: NSObject {
      fileprivate static var api: NativeCallsProtocol?

      @objc public static func registerAPIforNativeCalls(_ api: NativeCallsProtocol) {
          self.api = api
      }
  }

  // C symbol that IL2CPP calls via [DllImport("__Internal")]
  @_cdecl("showHostMainWindow")
  func showHostMainWindow(_ color: UnsafePointer<CChar>?) {
      let str = color.map { String(cString: $0) }
      FrameworkLibAPI.api?.showHostMainWindow(str)
  }