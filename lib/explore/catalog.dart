import 'dart:convert';

import 'package:archive/archive.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

enum GuideRegion {
  combined('제주 · 전국'),
  jeju('제주'),
  national('전국');

  const GuideRegion(this.label);
  final String label;
}

enum AnimalGroup {
  all('전체', ''),
  mammals('포유류', 'Mammalia'),
  birds('조류', 'Aves'),
  insects('곤충', 'Insecta');

  const AnimalGroup(this.label, this.taxon);
  final String label;
  final String taxon;
}

const _featured = {
  'JJ-000837': ('roe_deer', '숲과 초원에 사는 동물'),
  'JJ-004873': ('white_eye', '눈 주변의 하얀 테가 특징'),
  'JJ-003382': ('swallowtail', '노란 날개와 검은 무늬'),
  'JJ-004729': ('dolphin', '제주 바다에서 만나는 동물'),
};

// Everyday wildlife shown first in the national guide.
const _commonWildlife = [
  '참새',
  '다람쥐',
  '까치',
  '청설모',
  '직박구리',
  '멧비둘기',
  '고라니',
  '박새',
  '너구리',
  '까마귀',
  '흰뺨검둥오리',
  '제비',
];

int _nationalRank(Animal animal) {
  if (animal.taxon == 'Mammalia' || animal.taxon == 'Aves') {
    final index = _commonWildlife.indexOf(animal.name);
    if (index >= 0) return index;
  }
  return _commonWildlife.length +
      switch (animal.taxon) {
        'Mammalia' => 0,
        'Aves' => 1,
        'Insecta' => 2,
        _ => 3,
      };
}

int compareNationalAnimals(Animal a, Animal b) {
  final rank = _nationalRank(a).compareTo(_nationalRank(b));
  if (rank != 0) return rank;
  final aUnnamed = a.name == a.scientific;
  final bUnnamed = b.name == b.scientific;
  if (aUnnamed != bUnnamed) return aUnnamed ? 1 : -1;
  final name = a.name.compareTo(b.name);
  if (name != 0) return name;
  final scientific = a.scientific.compareTo(b.scientific);
  return scientific != 0 ? scientific : a.nationalId.compareTo(b.nationalId);
}

// Domestic breeds share their species-level discovery with the local yard model.
const _yardModels = {
  'JJ-000821': ('corgi', '웰시코기'),
  'JJ-001834': ('tabby', '노란 고양이'),
  'JJ-002426': ('gull', '갈매기'),
  'JJ-003483': ('sparrow', '참새'),
  'JJ-003382': ('swallowtail', '호랑나비'),
  'JJ-004729': ('dolphin', '남방큰돌고래'),
};

// Used only when the explorer explicitly turns on the example in Settings.
const exampleSpeciesIds = {
  'JJ-000837',
  'JJ-004873',
  'JJ-003382',
  'JJ-003554',
  'JJ-002293',
  'JJ-003570',
  'JJ-003658',
  'JJ-004398',
  'JJ-003478',
  'JJ-003671',
  'JJ-002187',
  'JJ-000261',
};

class Animal {
  const Animal(
    this.jejuId,
    this.nationalId,
    this.korean,
    this.scientific,
    this.taxon, {
    this.guidePhoto,
  });

  final Map<String, dynamic>? guidePhoto;
  final String jejuId;
  final String nationalId;
  final String korean;
  final String scientific;
  final String taxon;

  String get id => jejuId.isEmpty ? nationalId : jejuId;
  String get name => switch (jejuId) {
    'JJ-000821' => '개',
    'JJ-001834' => '고양이',
    _ => korean.isEmpty ? scientific : korean,
  };
  String get modelKey => _yardModels[jejuId]?.$1 ?? '';
  String get yardName => _yardModels[jejuId]?.$2 ?? name;
  String? get photo {
    if (guidePhoto != null) return 'assets/guide_photos/$nationalId.jpg';
    final feature = _featured[jejuId];
    return feature == null ? null : 'assets/art/${feature.$1}.jpg';
  }

  String get description =>
      _featured[jejuId]?.$2 ?? (korean.isEmpty ? groupName : scientific);

  String get groupName => switch (taxon) {
    'Mammalia' => '포유류',
    'Aves' => '조류',
    'Insecta' => '곤충',
    'Reptilia' => '파충류',
    'Amphibia' => '양서류',
    _ => taxon.isEmpty ? '기타 동물' : taxon,
  };

  bool matches(GuideRegion region, AnimalGroup group, String query) {
    if (region == GuideRegion.jeju && jejuId.isEmpty) return false;
    if (region == GuideRegion.national && nationalId.isEmpty) return false;
    if (group != AnimalGroup.all && group.taxon != taxon) return false;
    return query.isEmpty ||
        name.contains(query) ||
        yardName.contains(query) ||
        scientific.toLowerCase().contains(query) ||
        jejuId.toLowerCase().contains(query) ||
        nationalId.contains(query);
  }
}

Future<List<Animal>> loadGuideCatalog() async {
  final data = await rootBundle.load('assets/catalog/animals.tsv.gz');
  final credits = await rootBundle.loadString(
    'assets/guide_photos/credits.json',
  );
  return compute(_decodeWithPhotos, (Uint8List.sublistView(data), credits));
}

List<Animal> _decodeWithPhotos((Uint8List, String) input) =>
    decodeGuideCatalog(input.$1, jsonDecode(input.$2) as Map<String, dynamic>);

List<Animal> decodeGuideCatalog(
  Uint8List compressed, [
  Map<String, dynamic> photos = const {},
]) {
  final tsv = utf8.decode(GZipDecoder().decodeBytes(compressed));
  return const LineSplitter()
      .convert(tsv)
      .map((line) {
        final fields = line.split('\t');
        return Animal(
          fields[0],
          fields[1],
          fields[2],
          fields[3],
          fields[4],
          guidePhoto: photos[fields[1]],
        );
      })
      .toList(growable: false);
}
