import 'dart:convert';

import 'package:archive/archive.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

enum GuideRegion {
  jeju('제주 목격 생물'),
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
    this.taxon,
    this.region,
  );

  final String jejuId;
  final String nationalId;
  final String korean;
  final String scientific;
  final String taxon;
  final GuideRegion region;

  String get name => korean.isEmpty ? scientific : korean;
  String? get photo {
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

  bool isDiscovered(Set<String> ids) =>
      (jejuId.isNotEmpty && ids.contains(jejuId)) ||
      (nationalId.isNotEmpty &&
          (ids.contains(nationalId) || ids.contains('KTSN-$nationalId')));

  bool matches(AnimalGroup group, String query) {
    if (group != AnimalGroup.all && group.taxon != taxon) return false;
    return query.isEmpty ||
        korean.contains(query) ||
        scientific.toLowerCase().contains(query) ||
        jejuId.toLowerCase().contains(query) ||
        (nationalId.isNotEmpty && 'ktsn-$nationalId'.contains(query));
  }
}

Future<List<Animal>> loadGuideCatalog() async {
  final data = await rootBundle.load('assets/catalog/animals.tsv.gz');
  return compute(decodeGuideCatalog, Uint8List.sublistView(data));
}

List<Animal> decodeGuideCatalog(Uint8List compressed) {
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
        );
      })
      .toList(growable: false);
}
