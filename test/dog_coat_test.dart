import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:jeju_project/explore/dog_coat.dart';
import 'package:jeju_project/explore/encounter.dart';
import 'package:jeju_project/explore/yard_snapshot.dart';

void main() {
  test('a sampled fur patch votes for one coat or gives no suggestion', () {
    DogCoat? sample(int r, int g, int b) {
      final patch = ByteData(4 * 25);
      for (var offset = 0; offset < patch.lengthInBytes; offset += 4) {
        patch.setUint32(offset, (r << 24) | (g << 16) | (b << 8) | 255);
      }
      patch.setUint32(12 * 4, 0xffffffff);
      return sampleDogCoat(patch, 5, 5, .5, .5);
    }

    expect(sample(124, 74, 46), DogCoat.brown);
    expect(sample(228, 223, 217), DogCoat.white);
    expect(sample(38, 35, 36), DogCoat.black);
    expect(sample(40, 158, 60), isNull);
  });

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
