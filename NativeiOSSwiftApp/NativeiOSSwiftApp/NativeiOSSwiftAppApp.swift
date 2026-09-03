import SwiftUI
import UIKit
import UnityAPI
import Observation

@Observable
class UnityUaaL: NSObject, NativeCallsProtocol {
    static let shared = UnityUaaL()
    private var runCount: Int = 0
    private(set) var appDelegate: AppDelegate?
    var isRunning = false
    var isShowingHost = false
    var hasQuit = false
    var hostColor: Color?

    private override init() {
        super.init()
        NotificationCenter.default.addObserver(
            forName: UnityNotifications.unityDidUnload,
            object: nil, queue: .main
        ) { [weak self] _ in
            self?.appDelegate?.hideUnityWindow()
            self?.isRunning = false
            self?.isShowingHost = false
        }
        NotificationCenter.default.addObserver(
            forName: UnityNotifications.unityDidQuit,
            object: nil, queue: .main
        ) { [weak self] _ in
            self?.appDelegate?.hideUnityWindow()
            self?.isRunning = false
            self?.isShowingHost = false
            self?.hasQuit = true
        }
    }

    func runEmbedded() {
        if runCount == 0 {
            UnityAPI.UnitySetDataBundleDirWithBundleId("com.unity3d.framework")

            let delegate = AppDelegate()
            appDelegate = delegate

            let application = UIApplication.shared

            guard delegate.application(
                application,
                willFinishLaunchingWithOptions: nil
            ) else {
                fatalError("[UnityFramework] willFinishLaunching failed")
            }

            guard delegate.application(
                application,
                didFinishLaunchingWithOptions: nil
            ) else {
                fatalError("[UnityFramework] didFinishLaunching failed")
            }

            FrameworkLibAPI.registerAPIforNativeCalls(self)
            delegate.setEmbeddedMode()
            delegate.startEmbeddedRendering()
            installUnityOverlayButtons(on: delegate)
        } else {
            appDelegate?.resumeFromUnload()
        }

        runCount += 1
        isRunning = true
    }

    func unload() {
        guard isRunning else { return }
        appDelegate?.unloadApplication()
    }

    func showHostMainWindow(_ color: String!) {
        appDelegate?.hideUnityWindow()
        hostColor = Self.parseColor(color)
        isShowingHost = true
    }

    private static func parseColor(_ name: String?) -> Color? {
        switch name?.lowercased() {
        case "red": return .red
        case "blue": return .blue
        case "yellow": return .yellow
        case "green": return .green
        default: return nil
        }
    }

    func showUnity() {
        appDelegate?.showUnityWindow()
        isShowingHost = false
    }

    private func installUnityOverlayButtons(on delegate: AppDelegate) {
        guard let rootView = delegate.unityRootView else { return }

        let showMainBtn = UIButton(type: .system)
        showMainBtn.setTitle("Show Main", for: .normal)
        showMainBtn.frame = CGRect(x: 0, y: 0, width: 100, height: 44)
        showMainBtn.center = CGPoint(x: 50, y: 300)
        showMainBtn.backgroundColor = .green
        showMainBtn.addAction(UIAction { [weak self] _ in
            self?.showHostMainWindow("")
        }, for: .primaryActionTriggered)
        rootView.addSubview(showMainBtn)

        let sendBtn = UIButton(type: .system)
        sendBtn.setTitle("Send Msg", for: .normal)
        sendBtn.frame = CGRect(x: 0, y: 0, width: 100, height: 44)
        sendBtn.center = CGPoint(x: 150, y: 300)
        sendBtn.backgroundColor = .yellow
        sendBtn.addAction(UIAction { [weak self] _ in
            self?.appDelegate?.sendMessage(toGameObject: "Cube", functionName: "ChangeColor", message: "yellow")
        }, for: .primaryActionTriggered)
        rootView.addSubview(sendBtn)

        let unloadBtn = UIButton(type: .system)
        unloadBtn.setTitle("Unload", for: .normal)
        unloadBtn.frame = CGRect(x: 0, y: 0, width: 100, height: 44)
        unloadBtn.center = CGPoint(x: 250, y: 300)
        unloadBtn.backgroundColor = .red
        unloadBtn.addAction(UIAction { [weak self] _ in
            self?.unload()
        }, for: .primaryActionTriggered)
        rootView.addSubview(unloadBtn)

        let quitBtn = UIButton(type: .system)
        quitBtn.setTitle("Quit", for: .normal)
        quitBtn.frame = CGRect(x: 0, y: 0, width: 100, height: 44)
        quitBtn.center = CGPoint(x: 250, y: 350)
        quitBtn.backgroundColor = .red
        quitBtn.addAction(UIAction { [weak self] _ in
            self?.appDelegate?.quitApplication()
        }, for: .primaryActionTriggered)
        rootView.addSubview(quitBtn)
    }
}

struct ContentView: View {
    var unity = UnityUaaL.shared
    @State private var alertTitle = ""
    @State private var alertMessage = ""
    @State private var showingAlert = false

    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "globe")
                .imageScale(.large)
                .foregroundStyle(.tint)
            Text("Hello, world!")

            if let color = unity.hostColor, unity.isShowingHost {
                Circle()
                    .fill(color)
                    .frame(width: 80, height: 80)
            }

            Button("Init Unity") {
                if unity.hasQuit {
                    showAlert("Unity cannot be initialized after quit", "Use unload instead")
                } else if unity.isRunning {
                    showAlert("Unity already initialized", "Unload Unity first")
                } else {
                    unity.runEmbedded()
                }
            }

            Button("Show Unity") {
                if !unity.isRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    unity.showUnity()
                }
            }

            Button("Unload Unity") {
                if !unity.isRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    unity.unload()
                }
            }

            Button("Quit Unity") {
                if !unity.isRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    unity.appDelegate?.quitApplication()
                }
            }
        }
        .padding()
        .alert(alertTitle, isPresented: $showingAlert) {
            Button("Ok") {}
        } message: {
            Text(alertMessage)
        }
    }

    private func showAlert(_ title: String, _ message: String) {
        alertTitle = title
        alertMessage = message
        showingAlert = true
    }
}

class HostAppDelegate: NSObject, UIApplicationDelegate {
    func applicationWillTerminate(_ application: UIApplication) {
        UnityUaaL.shared.appDelegate?.applicationWillTerminate(application)
    }
}

@main
struct NativeiOSSwiftAppApp: App {
    @UIApplicationDelegateAdaptor(HostAppDelegate.self) var hostDelegate

    var body: some Scene {
        WindowGroup {
            ContentView()
        }
    }
}
