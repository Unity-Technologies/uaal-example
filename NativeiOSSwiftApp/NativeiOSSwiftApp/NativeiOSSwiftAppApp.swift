import SwiftUI
import UIKit
import UnityAPI
import Observation

@Observable
	class UnityUaaL: NSObject, NativeCallsProtocol {
    static let shared = UnityUaaL()
    var isShowingHost = false
    var hostColor: Color?

    private override init() {
        super.init()
        UaaLAPI.shared.onReadyForNativeCalls = {
            FrameworkLibAPI.registerAPIforNativeCalls(UnityUaaL.shared)
        }
        UaaLAPI.shared.onDidUnload = { [weak self] in
            self?.isShowingHost = false
        }
        UaaLAPI.shared.onDidQuit = { [weak self] in
            self?.isShowingHost = false
        }
    }

    func runEmbedded() {
        UaaLAPI.shared.runEmbedded()
        if UaaLAPI.shared.isRunning {
            installUnityOverlayButtons()
        }
    }

    func showHostMainWindow(_ color: String!) {
        UaaLAPI.shared.hideUnityWindow()
        if let parsed = Self.parseColor(color) {
            hostColor = parsed
        }
        isShowingHost = true
    }

    func showUnity() {
        UaaLAPI.shared.showUnityWindow()
        isShowingHost = false
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

    private func installUnityOverlayButtons() {
        guard !overlayInstalled else { return }
        guard let rootView = UaaLAPI.shared.unityRootView else { return }
        overlayInstalled = true

        rootView.addOverlayButton("Show Main", center: CGPoint(x: 50, y: 300), color: .green) { [weak self] in
            self?.showHostMainWindow("")
        }
        rootView.addOverlayButton("Send Msg", center: CGPoint(x: 150, y: 300), color: .yellow) {
            UaaLAPI.shared.sendMessage(toGameObject: "Cube", functionName: "ChangeColor", message: "yellow")
        }
        rootView.addOverlayButton("Unload", center: CGPoint(x: 250, y: 300), color: .red) {
            UaaLAPI.shared.unload()
        }
        rootView.addOverlayButton("Quit", center: CGPoint(x: 250, y: 350), color: .red) {
            UaaLAPI.shared.quit()
        }
    }
}

struct ContentView: View {
    var unity = UnityUaaL.shared
    var uaal = UaaLAPI.shared
    @State private var alertTitle = ""
    @State private var alertMessage = ""
    @State private var showingAlert = false

    var statusColor: Color {
        if uaal.hasQuit { return .red }
        if uaal.isRunning { return .green }
        return .gray
    }

    var body: some View {
        VStack(spacing: 20) {
            Circle()
                .fill(unity.hostColor ?? statusColor)
                .frame(width: 120, height: 120)

            Button("Init Unity") {
                if uaal.hasQuit {
                    showAlert("Unity cannot be initialized after quit", "Use unload instead")
                } else if uaal.isRunning {
                    showAlert("Unity already initialized", "Unload Unity first")
                } else {
                    unity.runEmbedded()
                }
            }

            Button("Show Unity") {
                if !uaal.isRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    unity.showUnity()
                }
            }

            Button("Unload Unity") {
                if !uaal.isRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    uaal.unload()
                }
            }

            Button("Quit Unity") {
                if !uaal.isRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    uaal.quit()
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
struct NativeiOSSwiftAppApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
        }
    }
}
