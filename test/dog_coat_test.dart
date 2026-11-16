import 'dart:convert';
import 'dart:typed_data';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:jeju_project/explore/dog_mask.dart';

import 'package:flutter_test/flutter_test.dart';
import 'package:jeju_project/explore/dog_coat.dart';
import 'package:jeju_project/explore/encounter.dart';
import 'package:jeju_project/explore/yard_snapshot.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  test('recognizes coats inside dog masks and abstains on a cat', () async {
    final model = ByteData.sublistView(
      await File('assets/models/dog-mask.bin').readAsBytes(),
    );
    for (final entry in {
      'BREED-bichon': DogCoat.white,
      'BREED-shiba': DogCoat.brown,
      'BREED-french-bulldog': DogCoat.black,
      'CAT-bengal': null,
    }.entries) {
      final bytes = await File('assets/guide_photos/${entry.key}.jpg')
          .readAsBytes();
      final codec = await ui.instantiateImageCodec(
        bytes,
        targetWidth: dogInputSize,
        targetHeight: dogInputSize,
      );
      final image = (await codec.getNextFrame()).image;
      final rgba = await image.toByteData(format: ui.ImageByteFormat.rawRgba);
      image.dispose();
      codec.dispose();
      expect(
        recognizeDogCoat(Uint8List.sublistView(rgba!), model),
        entry.value,
        reason: entry.key,
      );
    }
  }, timeout: const Timeout(Duration(minutes: 2)));

  test('yard includes only photographed coats and their first dates', () {
    final json = encodeYardSnapshot(
      profile: 'local',
      examples: false,
      discoveries: {},
      catalog: [],
      dogCoats: {'black': FirstEncounter('2026-11-16', '')},
    );
    final animals = jsonDecode(json)['animals'] as List<dynamic>;
    expect(animals.single['id'], 'JJ-000821/dog_black');
    expect(animals.single['firstMetDate'], '2026-11-16');
  });
}
