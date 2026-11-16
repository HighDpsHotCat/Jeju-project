import 'package:flutter/material.dart';
import 'package:webview_flutter/webview_flutter.dart';

const mapRoute = '/map';
final observationMapUrl = Uri.https(
  'jeju-wildlife-field-guide.gimhoyeon.chatgpt.site',
  mapRoute,
);

class ObservationMapPage extends StatefulWidget {
  const ObservationMapPage({super.key, required this.url});
  final Uri url;

  @override
  State<ObservationMapPage> createState() => _ObservationMapPageState();
}

class _ObservationMapPageState extends State<ObservationMapPage> {
  WebViewController? controller;
  bool failed = false;

  @override
  void initState() {
    super.initState();
    _open();
  }

  Future<void> _open() async {
    try {
      final controller = this.controller ??= WebViewController();
      await controller.setJavaScriptMode(JavaScriptMode.unrestricted);
      await controller.setNavigationDelegate(
        NavigationDelegate(
          onWebResourceError: (error) {
            if (mounted && error.isForMainFrame == true) {
              setState(() => failed = true);
            }
          },
          onNavigationRequest: (request) {
            final uri = Uri.tryParse(request.url);
            if (request.isMainFrame &&
                uri?.host == widget.url.host &&
                uri?.path == '/' &&
                !uri!.hasQuery) {
              if (mounted) Navigator.of(context).pop();
              return NavigationDecision.prevent;
            }
            return NavigationDecision.navigate;
          },
        ),
      );
      await controller.loadRequest(widget.url);
    } catch (_) {
      if (mounted) setState(() => failed = true);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: SafeArea(
      child: failed
          ? Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text('관찰 지도를 열 수 없어요.'),
                  TextButton(
                    onPressed: () {
                      setState(() => failed = false);
                      _open();
                    },
                    child: const Text('다시 시도'),
                  ),
                  TextButton(
                    onPressed: () => Navigator.pop(context),
                    child: const Text('메인으로 돌아가기'),
                  ),
                ],
              ),
            )
          : controller == null
          ? const Center(child: CircularProgressIndicator())
          : WebViewWidget(controller: controller!),
    ),
  );
}
