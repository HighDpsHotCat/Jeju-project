import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:webview_flutter/webview_flutter.dart';

class YardPlayer extends StatefulWidget {
  const YardPlayer({super.key, required this.snapshot, required this.onExit});
  final String? snapshot;
  final VoidCallback onExit;

  @override
  State<YardPlayer> createState() => _YardPlayerState();
}

class _YardPlayerState extends State<YardPlayer> {
  WebViewController? _controller;
  bool _ready = false;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    if (WebViewPlatform.instance != null) _load();
  }

  Future<void> _load() async {
    try {
      final controller = WebViewController();
      await controller.setJavaScriptMode(JavaScriptMode.unrestricted);
      await controller.addJavaScriptChannel(
        'YardHost',
        onMessageReceived: (message) {
          if (!mounted) return;
          switch (message.message) {
            case 'yard-ready':
              _ready = true;
              _sendSnapshot();
            case 'yard-exit':
              widget.onExit();
          }
        },
      );
      if (!mounted) return;
      setState(() {
        _controller = controller;
        _ready = false;
        _failed = false;
      });
      await controller.loadFlutterAsset('assets/yard/index.html');
    } catch (error) {
      debugPrint('Could not open the yard player: $error');
      if (mounted) setState(() => _failed = true);
    }
  }

  void _sendSnapshot() {
    final json = widget.snapshot;
    if (_ready && json != null) {
      _controller
          ?.runJavaScript('loadGuideSnapshot(${jsonEncode(json)})')
          .catchError((Object error) {
            debugPrint('Could not send the guide to the yard: $error');
          });
    }
  }

  @override
  void didUpdateWidget(YardPlayer oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.snapshot != oldWidget.snapshot) _sendSnapshot();
  }

  @override
  void dispose() {
    // Unload the page even if the user leaves while Unity is still starting.
    unawaited(
      _controller
          ?.loadRequest(Uri.parse('about:blank'))
          .catchError((Object _) {}),
    );
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (WebViewPlatform.instance == null) {
      return const Center(child: Text('웹 버전에서 3D 마당을 열 수 있어요.'));
    }
    if (_failed) {
      return Center(
        child: FilledButton(onPressed: _load, child: const Text('마당 다시 열기')),
      );
    }
    final controller = _controller;
    return controller == null
        ? const Center(child: CircularProgressIndicator())
        : WebViewWidget(controller: controller);
  }
}
