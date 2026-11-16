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
  'JJ-000532': ('grasshopper', '섬서구메뚜기'),
  'JJ-001114': ('ladybird', '칠성무당벌레'),
  'JJ-004705': ('rhino_beetle', '장수풍뎅이'),
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

const _dogBreeds = {
  'BREED-golden': (korean: '골든 리트리버', english: 'Golden Retriever'),
  'BREED-dachshund': (korean: '닥스훈트', english: 'Dachshund'),
  'BREED-labrador': (korean: '래브라도 리트리버', english: 'Labrador Retriever'),
  'BREED-maltese': (korean: '말티즈', english: 'Maltese'),
  'BREED-border-collie': (korean: '보더 콜리', english: 'Border Collie'),
  'BREED-beagle': (korean: '비글', english: 'Beagle'),
  'BREED-bichon': (korean: '비숑 프리제', english: 'Bichon Frisé'),
  'BREED-samoyed': (korean: '사모예드', english: 'Samoyed'),
  'BREED-shiba': (korean: '시바견', english: 'Shiba Inu'),
  'BREED-husky': (korean: '시베리안 허스키', english: 'Siberian Husky'),
  'BREED-shih-tzu': (korean: '시추', english: 'Shih Tzu'),
  'BREED-yorkshire': (korean: '요크셔 테리어', english: 'Yorkshire Terrier'),
  'BREED-pembroke': (korean: '웰시코기 (펨브로크)', english: 'Pembroke Welsh Corgi'),
  'BREED-german-shepherd': (korean: '저먼 셰퍼드', english: 'German Shepherd'),
  'BREED-jindo': (korean: '진돗개', english: 'Korean Jindo'),
  'BREED-chihuahua': (korean: '치와와', english: 'Chihuahua'),
  'BREED-pug': (korean: '퍼그', english: 'Pug'),
  'BREED-pomeranian': (korean: '포메라니안', english: 'Pomeranian'),
  'BREED-poodle': (korean: '푸들', english: 'Poodle'),
  'BREED-french-bulldog': (korean: '프렌치 불도그', english: 'French Bulldog'),
};

const _catBreeds = {
  'CAT-norwegian-forest': (
    korean: '노르웨이 숲고양이',
    english: 'Norwegian Forest Cat',
  ),
  'CAT-devon-rex': (korean: '데본 렉스', english: 'Devon Rex'),
  'CAT-ragdoll': (korean: '랙돌', english: 'Ragdoll'),
  'CAT-russian-blue': (korean: '러시안 블루', english: 'Russian Blue'),
  'CAT-munchkin': (korean: '먼치킨', english: 'Munchkin'),
  'CAT-maine-coon': (korean: '메인쿤', english: 'Maine Coon'),
  'CAT-birman': (korean: '버만', english: 'Birman'),
  'CAT-burmese': (korean: '버미즈', english: 'Burmese'),
  'CAT-bengal': (korean: '벵갈', english: 'Bengal'),
  'CAT-british-shorthair': (korean: '브리티시 쇼트헤어', english: 'British Shorthair'),
  'CAT-siamese': (korean: '샴', english: 'Siamese'),
  'CAT-somali': (korean: '소말리', english: 'Somali'),
  'CAT-scottish-fold': (korean: '스코티시 폴드', english: 'Scottish Fold'),
  'CAT-sphynx': (korean: '스핑크스', english: 'Sphynx'),
  'CAT-siberian': (korean: '시베리안', english: 'Siberian'),
  'CAT-american-shorthair': (
    korean: '아메리칸 쇼트헤어',
    english: 'American Shorthair',
  ),
  'CAT-abyssinian': (korean: '아비시니안', english: 'Abyssinian'),
  'CAT-exotic-shorthair': (korean: '엑조틱 쇼트헤어', english: 'Exotic Shorthair'),
  'CAT-turkish-angora': (korean: '터키시 앙고라', english: 'Turkish Angora'),
  'CAT-persian': (korean: '페르시안', english: 'Persian'),
};

enum _BreedGroup {
  dog('개', 'Canis lupus familiaris', _dogBreeds, {
    '개',
    '강아지',
    '반려견',
    '견종',
    '개품종',
  }),
  cat('고양이', 'Felis catus', _catBreeds, {'고양이', '반려묘', '묘종', '고양이품종'});

  const _BreedGroup(this.label, this.scientific, this.breeds, this.searchTerms);
  final String label;
  final String scientific;
  final Map<String, ({String korean, String english})> breeds;
  final Set<String> searchTerms;
}

class Animal {
  const Animal(
    this.jejuId,
    this.nationalId,
    this.korean,
    this.scientific,
    this.taxon,
    this.region, {
    this.guidePhoto,
    this.breedId,
  });

  final Map<String, dynamic>? guidePhoto;
  final String? breedId;
  _BreedGroup? get _breedGroup {
    if (breedId == null) return null;
    for (final group in _BreedGroup.values) {
      if (group.breeds.containsKey(breedId)) return group;
    }
    return null;
  }

  String? get breedEnglish => _breedGroup?.breeds[breedId]?.english;
  String get classification =>
      _breedGroup == null ? groupName : '$groupName · ${_breedGroup!.label} 품종';
  final String jejuId;
  final String nationalId;
  final String korean;
  final String scientific;
  final String taxon;
  final GuideRegion region;

  String get id => breedId ?? (jejuId.isEmpty ? nationalId : jejuId);
  String get modelKey => _yardModels[id]?.$1 ?? '';
  String get yardName => _yardModels[id]?.$2 ?? name;
  String get name => korean.isNotEmpty
      ? korean
      : region == GuideRegion.jeju
      ? '국명 미확인 · $jejuId'
      : scientific;
  String? get photo {
    if (guidePhoto != null) {
      final id = breedId ?? (region == GuideRegion.jeju ? jejuId : nationalId);
      return 'assets/guide_photos/$id.jpg';
    }
    final feature = _featured[jejuId];
    return feature == null ? null : 'assets/art/${feature.$1}.jpg';
  }

  String get description =>
      breedEnglish ??
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
      (breedId != null && ids.contains(breedId)) ||
      (jejuId.isNotEmpty && ids.contains(jejuId)) ||
      (nationalId.isNotEmpty &&
          (ids.contains(nationalId) || ids.contains('KTSN-$nationalId')));

  bool matches(AnimalGroup group, String query) {
    if (group != AnimalGroup.all && group.taxon != taxon) return false;
    if (breedEnglish case final english?) {
      final compact = query.replaceAll(' ', '');
      if (korean.replaceAll(' ', '').contains(compact) ||
          english.toLowerCase().replaceAll(' ', '').contains(compact) ||
          _breedGroup!.searchTerms.contains(compact)) {
        return true;
      }
    }
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
  final species = const LineSplitter().convert(tsv).map((line) {
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
  });
  return [
    ...species,
    for (final group in _BreedGroup.values)
      for (final entry in group.breeds.entries)
        Animal(
          '',
          '',
          entry.value.korean,
          group.scientific,
          'Mammalia',
          GuideRegion.national,
          breedId: entry.key,
          guidePhoto: photos[entry.key],
        ),
  ];
}
