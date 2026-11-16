package com.example.jeju_project

import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "jeju/yard").setMethodCallHandler { call, result ->
            if (call.method == "collectionDirectory") {
                result.success(getExternalFilesDir(null)?.absolutePath)
            } else {
                result.notImplemented()
            }
        }
    }
}
