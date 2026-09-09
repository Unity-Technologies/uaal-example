import SwiftUI
import UIKit
import UnityAPI
import UnityFramework
import Observation

@Observable
class Helper: NSObject , NativeCallsProtocol {
    static let shared = Helper()
    var lastCircleColor: Color? = nil

    private override init() {
        super.init()
        UaaLAPI.shared.onReadyForNativeCalls = {
            FrameworkLibAPI.registerAPIforNativeCalls(self)
        }
    }

    func showHostMainWindow(_ color: String!) {
        // Callback from unity managed code
        // [Show Host Main With Color Button] -> il2cpp -> extern C void showHostMainWindow() in NativeCallProxy.mm -> api (we set it with FrameworkLibAPI.registerAPIforNativeCalls) -> showHostMainWindow -> Helper:NSObject this method
        UaaLAPI.shared.hideUnityWindow()
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
    public func installUnityOverlayButtons() {
        guard !overlayInstalled else { return }
        guard let rootView = UaaLAPI.shared.unityRootView else { return }
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
            UaaLAPI.shared.sendMessage(toGameObject: "Cube", functionName: "ChangeColor", message: "yellow")
        }
        y += spacing
        rootView.addOverlayButton("Unload", center: CGPoint(x: x, y: y), size: btnSize, color: .red) {
            UaaLAPI.shared.unload()
        }
        y += spacing
        rootView.addOverlayButton("Quit", center: CGPoint(x: x, y: y), size: btnSize, color: .red) {
            UaaLAPI.shared.quit()
        }
    }
}

struct ContentView: View {
    var helper = Helper.shared
    var uaal = UaaLAPI.shared
    @State private var alertTitle = ""
    @State private var alertMessage = ""
    @State private var showingAlert = false
    
    var body: some View {
        VStack(spacing: 20) {
            Circle()
                .fill(helper.lastCircleColor ?? .gray)
                .frame(width: 120, height: 120)

            Button("Init Unity") {
                if uaal.hasQuit {
                    showAlert("Unity cannot be initialized after quit", "Use unload instead")
                } else if uaal.isRunning {
                    showAlert("Unity already initialized", "Unload Unity first")
                } else {
                    uaal.runEmbedded()
                    helper.installUnityOverlayButtons()
                }
            }

            Button("Show Unity") {
                if !uaal.isRunning {
                    showAlert("Unity is not initialized", "Initialize Unity first")
                } else {
                    uaal.showUnityWindow()
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
