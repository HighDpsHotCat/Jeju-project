import 'dart:convert';

import 'package:archive/archive.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

enum GuideRegion {
  jeju('제주 생물'),
  national('전국 도감');

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

const _yardModels = {
  'JJ-000837': ('roe_deer', '노루'),
  'JJ-002293': ('bulbul', '직박구리'),
  'JJ-004873': ('white_eye', '동박새'),
  'JJ-003658': ('magpie', '까치'),
  'JJ-003671': ('cabbage_white', '배추흰나비'),
  'JJ-000821': ('corgi', '웰시코기'),
  'JJ-001834': ('tabby', '노란 고양이'),
  'JJ-002426': ('gull', '갈매기'),
  'JJ-003483': ('sparrow', '참새'),
  'JJ-003382': ('swallowtail', '호랑나비'),
  'JJ-004729': ('dolphin', '남방큰돌고래'),
};

const contestSpeciesIds = {
  'JJ-001834', // 고양이
  'JJ-004729', // 남방큰돌고래
  'JJ-003483', // 참새
  'JJ-000821', // 개 (웰시코기)
};

// Additional records for profiles that previously enabled example mode.
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
    this.taxon,
    this.region, {
    this.guidePhoto,
  });

  final Map<String, dynamic>? guidePhoto;
  final String jejuId;
  final String nationalId;
  final String korean;
  final String scientific;
  final String taxon;
  final GuideRegion region;

  String get id => jejuId.isEmpty ? nationalId : jejuId;
  String get modelKey => _yardModels[jejuId]?.$1 ?? '';
  String get yardName => _yardModels[jejuId]?.$2 ?? name;
  String get name => korean.isNotEmpty
      ? korean
      : region == GuideRegion.jeju
      ? '국명 미확인 · $jejuId'
      : scientific;
  String? get photo {
    if (guidePhoto != null) {
      final id = region == GuideRegion.jeju ? jejuId : nationalId;
      return 'assets/guide_photos/$id.jpg';
    }
    final feature = _featured[jejuId];
    return feature == null ? null : 'assets/art/${feature.$1}.jpg';
  }

  String get description =>
      _featured[jejuId]?.$2 ??
      (region == GuideRegion.jeju ? '제주 목격 기록' : scientific);

  String get groupName => switch (taxon) {
    'Mammalia' => '포유류',
    'Aves' => '조류',
    'Insecta' => '곤충',
    'Reptilia' => '파충류',
    'Amphibia' => '양서류',
    _ => taxon.isEmpty ? '기타 동물' : taxon,
  };

  bool isDiscovered(Set<String> ids) =>
      (jejuId.isNotEmpty && ids.contains(jejuId)) ||
      (nationalId.isNotEmpty &&
          (ids.contains(nationalId) || ids.contains('KTSN-$nationalId')));

  bool matches(AnimalGroup group, String query) {
    if (group != AnimalGroup.all && group.taxon != taxon) return false;
    return query.isEmpty ||
        korean.contains(query) ||
        (jejuId == 'JJ-000821' && '강아지 웰시코기'.contains(query)) ||
        scientific.toLowerCase().contains(query) ||
        jejuId.toLowerCase().contains(query) ||
        (nationalId.isNotEmpty && 'ktsn-$nationalId'.contains(query));
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
          GuideRegion.values.byName(fields[5]),
          guidePhoto: photos[fields[5] == 'national' ? fields[1] : fields[0]],
        );
      })
      .toList(growable: false);
}
