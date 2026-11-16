import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:jeju_project/explore/catalog.dart';
import 'package:jeju_project/explore/yard_snapshot.dart';

void main() {
  test('national dog breeds keep individual photos and discovery IDs', () {
    final credits = jsonDecode(
      File('assets/guide_photos/credits.json').readAsStringSync(),
    ) as Map<String, dynamic>;
    final catalog = decodeGuideCatalog(
      File('assets/catalog/animals.tsv.gz').readAsBytesSync(),
      credits,
    );
    final breeds = catalog.where((a) => a.breedId != null).toList();
    expect(breeds.length, 20);
    expect(breeds.map((a) => a.id).toSet().length, breeds.length);
    for (final breed in breeds) {
      expect(breed.region, GuideRegion.national);
      expect(breed.nationalId, isEmpty);
      expect(breed.jejuId, isEmpty);
      expect(breed.matches(AnimalGroup.mammals, breed.name), isTrue);
      expect(
        breed.matches(AnimalGroup.mammals, breed.breedEnglish!.toLowerCase()),
        isTrue,
      );
      expect(breed.matches(AnimalGroup.birds, '강아지'), isFalse);
      expect(File(breed.photo!).existsSync(), isTrue);
    }
    final selected = breeds.first;
    final discoveries = {selected.id};
    expect(breeds.where((a) => a.isDiscovered(discoveries)), [selected]);
    final snapshot = jsonDecode(
      encodeYardSnapshot(
        profile: 'guest',
        examples: false,
        discoveries: discoveries,
        catalog: catalog,
      ),
    );
    expect(snapshot['animals'].single['id'], selected.id);
    expect(snapshot['animals'].single['name'], selected.name);
  });
}
