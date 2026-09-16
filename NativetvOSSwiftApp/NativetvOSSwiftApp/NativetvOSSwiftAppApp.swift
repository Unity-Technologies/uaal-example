import SwiftUI
import UIKit
import UnityAPI
import UnityFramework
import Observation

@Observable
class Helper: NSObject, NativeCallsProtocol {
    static let shared = Helper()
    var lastCircleColor: Color? = nil
    var isUnityRunning = false
    var hasUnityQuit = false
    var unityWindow: UIWindow?

    private override init() {
        super.init()
        let nc = NotificationCenter.default
        nc.addObserver(forName: UnityNotifications.unityDidInitializeRuntime, object: nil, queue: .main) { _ in
            FrameworkLibAPI.registerAPIforNativeCalls(self)
            self.isUnityRunning = true
        }
        nc.addObserver(forName: UnityNotifications.unityDidUnload, object: nil, queue: .main) { _ in
            self.unityWindow?.isHidden = true
            self.isUnityRunning = false
            self.overlayInstalled = false
        }
        nc.addObserver(forName: UnityNotifications.unityDidQuit, object: nil, queue: .main) { _ in
            self.unityWindow?.isHidden = true
            self.isUnityRunning = false
            self.hasUnityQuit = true
        }
    }

    func startUnity() {
        guard !hasUnityQuit, !isUnityRunning else { return }

        UnityPlayer.shared.terminatesOnQuit = false
        UnitySetDataBundleDirWithBundleId("com.unity3d.framework")
        UnityPlayer.shared.startEngine()
        isUnityRunning = true

        guard let windowScene = UIApplication.shared.connectedScenes
            .compactMap({ $0 as? UIWindowScene })
            .first
        else { return }

        let window = UIWindow(windowScene: windowScene)
        let controller = TvOSEventViewController(
            rootView: UnityRenderingView().ignoresSafeArea(),
            responder: UnityPlayer.shared.renderingView
        )
        controller.view.backgroundColor = .clear
        window.rootViewController = controller
        unityWindow = window
        window.makeKeyAndVisible()

        UnityPlayer.shared.sceneDidBecomeActive(windowScene)

        installUnityOverlayButtons()
    }

    func showHostMainWindow(_ color: String!) {
        unityWindow?.isHidden = true
        lastCircleColor = Self.parseColor(color)
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

    private var overlayInstalled = false
    func installUnityOverlayButtons() {
        guard !overlayInstalled else { return }
        guard let rootView = unityWindow else { return }
        overlayInstalled = true

        let btnSize = CGSize(width: 240, height: 50)
        let x: CGFloat = 130
        var y: CGFloat = 300
        let spacing: CGFloat = 60

        rootView.addOverlayButton("Show Main", center: CGPoint(x: x, y: y), size: btnSize, color: .green) {
            [weak self] in self?.showHostMainWindow("")
        }
        y += spacing
        rootView.addOverlayButton("Send Msg", center: CGPoint(x: x, y: y), size: btnSize, color: .yellow) {
            UnityPlayer.shared.sendMessage(toGameObject: "Cube", method: "ChangeColor", argument: "yellow")
        }
        y += spacing
        rootView.addOverlayButton("Unload", center: CGPoint(x: x, y: y), size: btnSize, color: .red) {
            UnityPlayer.shared.unload()
        }
        y += spacing
        rootView.addOverlayButton("Quit", center: CGPoint(x: x, y: y), size: btnSize, color: .red) {
            UnityPlayer.shared.quit()
        }
    }
}

struct ContentView: View {
    var helper = Helper.shared
    @State private var alertTitle = ""
    @State private var alertMessage = ""
    @State private var showingAlert = false

    var body: some View {
        VStack(spacing: 20) {
            Circle()
                .fill(helper.lastCircleColor ?? .gray)
                .frame(width: 120, height: 120)

            Button("Init Unity") {
                if helper.hasUnityQuit {
                    showAlert("Unity cannot be initialized after quit", "Use unload instead")
                } else if helper.isUnityRunning {
                    showAlert("Unity already initialized", "Unload Unity first")
                } else {
                    helper.startUnity()
                }
            }

            Button("Show Unity") {
                if !helper.isUnityRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    helper.unityWindow?.isHidden = false
                    helper.unityWindow?.makeKeyAndVisible()
                }
            }

            Button("Unload Unity") {
                if !helper.isUnityRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    UnityPlayer.shared.unload()
                }
            }

            Button("Quit Unity") {
                if !helper.isUnityRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    UnityPlayer.shared.quit()
                }
            }
        }
        .font(.title)
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

struct UnityRenderingView: UIViewRepresentable {
    func makeUIView(context: Context) -> some UIView {
        UnityPlayer.shared.renderingView!
    }
    func updateUIView(_ uiView: UIViewType, context: Context) {}
}

private extension UIView {
    @discardableResult
    func addOverlayButton(
        _ title: String,
        center: CGPoint,
        size: CGSize = CGSize(width: 100, height: 44),
        color: UIColor,
        action: @escaping () -> Void
    ) -> UIButton {
        let button = UIButton(type: .system)
        button.setTitle(title, for: .normal)
        button.frame = CGRect(origin: .zero, size: size)
        button.center = center
        button.backgroundColor = color
        button.addAction(UIAction { _ in action() }, for: .primaryActionTriggered)
        addSubview(button)
        return button
    }
}

@main
struct NativetvOSSwiftAppApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
        }
    }
}
