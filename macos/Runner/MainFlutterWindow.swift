import Cocoa
import FlutterMacOS

class MainFlutterWindow: NSWindow {
  override func awakeFromNib() {
    let flutterViewController = FlutterViewController()
    let windowFrame = self.frame
    self.contentViewController = flutterViewController
    self.setFrame(windowFrame, display: true)

    let yard = FlutterMethodChannel(name: "jeju/yard", binaryMessenger: flutterViewController.engine.binaryMessenger)
    yard.setMethodCallHandler { call, result in
      guard call.method == "collectionDirectory" else {
        result(FlutterMethodNotImplemented)
        return
      }
      let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
      result(support?.appendingPathComponent("JejuYard").path)
    }

    RegisterGeneratedPlugins(registry: flutterViewController)

    super.awakeFromNib()
  }
}
