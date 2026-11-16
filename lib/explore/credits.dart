import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

class PhotoCreditsPage extends StatefulWidget {
  const PhotoCreditsPage({super.key});

  @override
  State<PhotoCreditsPage> createState() => _PhotoCreditsPageState();
}

class _PhotoCreditsPageState extends State<PhotoCreditsPage> {
  late final photos = _loadPhotos();

  Future<List<Map<String, dynamic>>> _loadPhotos() async {
    final bytes = await rootBundle.load('assets/guide_photos/credits.json');
    final json = utf8.decode(Uint8List.sublistView(bytes));
    final records = jsonDecode(json) as Map<String, dynamic>;
    return records.values.cast<Map<String, dynamic>>().toList()
      ..sort((a, b) => (a['name'] as String).compareTo(b['name'] as String));
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('저작권 및 출처')),
    body: FutureBuilder(
      future: photos,
      builder: (context, snapshot) {
        if (snapshot.hasError)
          return const Center(child: Text('사진 출처를 불러오지 못했어요.'));
        final rows = snapshot.data;
        if (rows == null)
          return const Center(child: CircularProgressIndicator());
        return ListView.builder(
          itemCount: rows.length,
          itemBuilder: (context, index) {
            final photo = rows[index];
            return ExpansionTile(
              title: Text(photo['name']),
              subtitle: Text(
                "${photo['displayCredit'] ?? photo['author']} · ${photo['license']}",
              ),
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
                  child: SelectableText(
                    "원본  ${photo['source']}\n이용 조건  ${photo['licenseUrl']}",
                  ),
                ),
              ],
            );
          },
        );
      },
    ),
  );
}
