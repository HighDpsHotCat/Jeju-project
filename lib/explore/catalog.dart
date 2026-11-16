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

const jejuGuidePageSize = 20;

const _guidePriority = {
  'JJ-000837': 0, // 노루
  'JJ-004873': 1, // 동박새
  'JJ-003382': 2, // 호랑나비
  'JJ-004729': 3, // 남방큰돌고래
  'JJ-000821': 4, // 강아지
  'JJ-001834': 5, // 고양이
};

int _guideRank(Animal animal) =>
    _guidePriority[animal.jejuId] ??
    _guidePriority.length +
        switch (animal.taxon) {
          'Mammalia' => 0,
          'Aves' => 1,
          'Insecta' => 2,
          _ => 3,
        };

int compareGuideAnimals(Animal a, Animal b) {
  final rank = _guideRank(a).compareTo(_guideRank(b));
  return rank != 0 ? rank : _compareNames(a, b);
}

int _compareNames(Animal a, Animal b) {
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
  'JJ-000837': ('roe_deer', '노루'),
  'JJ-002293': ('bulbul', '직박구리'),
  'JJ-004873': ('white_eye', '동박새'),
  'JJ-003658': ('magpie', '까치'),
  'JJ-003671': ('cabbage_white', '배추흰나비'),
  'JJ-004503': ('chipmunk', '다람쥐'),
  '120000053353': ('water_deer', '고라니'),
  '120000053303': ('raccoon_dog', '너구리'),
  'JJ-001174': ('crow', '까마귀'),
  'JJ-000261': ('mallard', '청둥오리'),
  'JJ-002187': ('swallow', '제비'),
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

typedef GuidePhoto = ({String asset, Map<String, dynamic>? credit});

typedef PetBreed = ({String id, String name, String english, GuidePhoto photo});

String? breedSpeciesId(String id) {
  if (id.startsWith('BREED-')) return 'JJ-000821';
  if (id.startsWith('CAT-')) return 'JJ-001834';
  return null;
}

class Animal {
  const Animal(
    this.jejuId,
    this.nationalId,
    this.korean,
    this.scientific,
    this.taxon, {
    this.breeds = const [],
    this.jejuPhoto,
    this.nationalPhoto,
  });

  final List<PetBreed> breeds;
  final GuidePhoto? jejuPhoto;
  final GuidePhoto? nationalPhoto;
  final String jejuId;
  final String nationalId;
  final String korean;
  final String scientific;
  final String taxon;

  String get id => jejuId.isEmpty ? nationalId : jejuId;
  String get name => switch (jejuId) {
    'JJ-000821' => '강아지',
    'JJ-001834' => '고양이',
    _ => korean.isEmpty ? scientific : korean,
  };
  String get modelKey => _yardModels[id]?.$1 ?? '';
  String get yardName => _yardModels[id]?.$2 ?? name;
  GuidePhoto? photoFor(GuideRegion region) {
    final photo = region == GuideRegion.national
        ? nationalPhoto ?? jejuPhoto
        : jejuPhoto ?? nationalPhoto;
    if (photo != null) return photo;
    final feature = _featured[jejuId];
    return feature == null
        ? null
        : (asset: 'assets/art/${feature.$1}.jpg', credit: null);
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
    // The bundled national checklist has no IDs for domestic dogs or cats.
    if (region == GuideRegion.national &&
        nationalId.isEmpty &&
        jejuId != 'JJ-000821' &&
        jejuId != 'JJ-001834') {
      return false;
    }
    if (group != AnimalGroup.all && group.taxon != taxon) return false;
    return query.isEmpty ||
        name.contains(query) ||
        (jejuId == 'JJ-000821' && query == '개') ||
        breeds.any(
          (breed) =>
              breed.name.contains(query) ||
              breed.english.toLowerCase().contains(query),
        ) ||
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
  GuidePhoto? lookupPhoto(String id) {
    final credit = photos[id] as Map<String, dynamic>?;
    return credit == null
        ? null
        : (asset: 'assets/guide_photos/$id.jpg', credit: credit);
  }

  final breeds = <String, List<PetBreed>>{};
  for (final id in photos.keys) {
    final parent = breedSpeciesId(id);
    if (parent == null) continue;
    final credit = photos[id] as Map<String, dynamic>;
    (breeds[parent] ??= []).add((
      id: id,
      name: credit['name'] as String,
      english: credit['subject'] as String,
      photo: lookupPhoto(id)!,
    ));
  }
  for (final list in breeds.values) {
    list.sort((a, b) => a.name.compareTo(b.name));
  }
  final tsv = utf8.decode(GZipDecoder().decodeBytes(compressed));
  return const LineSplitter()
      .convert(tsv)
      .map((line) {
        final fields = line.split('\t');
        final localPhoto = lookupPhoto(fields[0]);
        final credit = localPhoto?.credit;
        final name = fields[2].isEmpty && credit?['scientific'] == fields[3]
            ? (credit?['name'] as String? ?? fields[2])
            : fields[2];
        return Animal(
          fields[0],
          fields[1],
          name,
          fields[3],
          fields[4],
          breeds: breeds[fields[0]] ?? const [],
          jejuPhoto: localPhoto,
          nationalPhoto: lookupPhoto(fields[1]),
        );
      })
      .toList(growable: false);
}
