import 'dart:async';
import 'dart:js_interop';
import 'dart:ui_web' as ui_web;

import 'package:flutter/widgets.dart';
import 'package:web/web.dart' as web;

class YardPlayer extends StatefulWidget {
  const YardPlayer({super.key, required this.snapshot, required this.onExit});
  final String? snapshot;
  final VoidCallback onExit;

  @override
  State<YardPlayer> createState() => _YardPlayerState();
}

class _YardPlayerState extends State<YardPlayer> {
  web.HTMLIFrameElement? _frame;
  late final StreamSubscription<web.MessageEvent> _messages;
  bool _ready = false;

  @override
  void initState() {
    super.initState();
    _messages = web.window.onMessage.listen((event) {
      if (_frame == null ||
          event.origin != web.window.location.origin ||
          event.source != _frame!.contentWindow ||
          !event.data.isA<JSString>()) {
        return;
      }
      switch ((event.data as JSString).toDart) {
        case 'yard-ready':
          _ready = true;
          _sendSnapshot();
        case 'yard-exit':
          widget.onExit();
      }
    });
  }

  void _sendSnapshot() {
    final json = widget.snapshot;
    if (_ready && json != null) {
      _frame?.contentWindow?.postMessage(
        json.toJS,
        web.window.location.origin.toJS,
      );
    }
  }

  @override
  void didUpdateWidget(YardPlayer oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.snapshot != oldWidget.snapshot) _sendSnapshot();
  }

  @override
  void dispose() {
    _messages.cancel();
    _frame?.src = 'about:blank';
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => HtmlElementView.fromTagName(
    tagName: 'iframe',
    onElementCreated: (element) {
      _frame = (element as web.HTMLIFrameElement)
        ..title = '제주 마당'
        ..style.border = '0'
        ..src = ui_web.assetManager.getAssetUrl('assets/yard/index.html');
    },
  );
}
