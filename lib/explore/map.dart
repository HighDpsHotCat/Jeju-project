import 'package:flutter/material.dart';

import 'style.dart';

const mapRoute = '/map';

class MapPendingScreen extends StatelessWidget {
  const MapPendingScreen({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('지도')),
    body: const Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.map_outlined, color: leaf, size: 44),
          SizedBox(height: 12),
          Text('지도를 준비하고 있어요.'),
        ],
      ),
    ),
  );
}
