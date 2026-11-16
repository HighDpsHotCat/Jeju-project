import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import 'yard_player_native.dart'
    if (dart.library.js_interop) 'yard_player_web.dart';

class YardScreen extends StatelessWidget {
  const YardScreen({super.key, required this.snapshot});
  final ValueListenable<String?> snapshot;

  @override
  Widget build(BuildContext context) => Scaffold(
    body: SafeArea(
      child: ValueListenableBuilder<String?>(
        valueListenable: snapshot,
        builder: (context, value, _) => YardPlayer(
          snapshot: value,
          onExit: () {
            if (ModalRoute.of(context)?.isCurrent == true) {
              Navigator.pop(context);
            }
          },
        ),
      ),
    ),
  );
}
