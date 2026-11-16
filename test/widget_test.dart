import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:jeju_project/explore/catalog.dart';
import 'package:jeju_project/main.dart';
import 'package:shared_preferences/shared_preferences.dart';

const animals = [
  Animal(
    'JJ-000837',
    '1',
    '노루',
    'Capreolus pygargus',
    'Mammalia',
    GuideRegion.jeju,
  ),
  Animal(
    'JJ-004873',
    '2',
    '동박새',
    'Zosterops japonicus',
    'Aves',
    GuideRegion.jeju,
  ),
  Animal(
    'JJ-003382',
    '3',
    '호랑나비',
    'Papilio xuthus',
    'Insecta',
    GuideRegion.jeju,
  ),
  Animal(
    'JJ-004729',
    '',
    '남방큰돌고래',
    'Tursiops aduncus',
    'Mammalia',
    GuideRegion.jeju,
  ),
  Animal(
    'JJ-000837',
    '1',
    '노루',
    'Capreolus pygargus',
    'Mammalia',
    GuideRegion.national,
  ),
  Animal(
    '',
    '4',
    '반달가슴곰',
    'Ursus thibetanus',
    'Mammalia',
    GuideRegion.national,
  ),
];

Future<SharedPreferences> start(
  WidgetTester tester, {
  Set<String> discoveries = const {},
  Size size = const Size(390, 805),
}) async {
  tester.view.devicePixelRatio = 1;
  tester.view.physicalSize = size;
  addTearDown(tester.view.resetDevicePixelRatio);
  addTearDown(tester.view.resetPhysicalSize);
  SharedPreferences.setMockInitialValues({});
  final prefs = await SharedPreferences.getInstance();
  await tester.pumpWidget(
    MyApp(
      preferences: prefs,
      loadAnimals: () async => animals,
      loadDiscoveries: (_) async => discoveries,
    ),
  );
  await tester.pumpAndSettle();
  return prefs;
}

Future<void> tapText(WidgetTester tester, String text) async {
  await tester.tap(find.text(text).last);
  await tester.pumpAndSettle();
}

void main() {
  test(
    'bundled Jeju and national indexes keep source IDs and example cards',
    () async {
      final catalog = decodeGuideCatalog(
        await File('assets/catalog/animals.tsv.gz').readAsBytes(),
        jsonDecode(
          await File('assets/guide_photos/credits.json').readAsString(),
        ) as Map<String, dynamic>,
      );
      final local = catalog
          .where((animal) => animal.region == GuideRegion.jeju)
          .map((animal) => animal.jejuId);
      final national = catalog
          .where((animal) => animal.region == GuideRegion.national)
          .map((animal) => animal.nationalId);
      expect(local.toSet(), hasLength(4797));
      expect(national.toSet(), hasLength(32460));
      expect(
        catalog,
        hasLength(local.toSet().length + national.toSet().length),
      );
      expect(local.contains(''), isFalse);
      expect(national.contains(''), isFalse);
      expect(local, isNot(contains('JJ-000305'))); // 국명이 없는 응애
      expect(catalog.any((animal) => animal.name.contains('회충')), isFalse);
      expect(catalog.any((animal) => animal.name == '작은소피참진드기'), isFalse);
      expect(catalog.any((animal) => animal.name == '무당거미'), isTrue);
      expect(
        catalog.any((animal) => animal.scientific == 'Bathypathes patula'),
        isFalse,
      );
      expect(catalog.any((animal) => animal.name == '구멍의산호'), isFalse);
      expect(catalog.any((animal) => animal.name == '산호랑나비'), isTrue);
      final ordered = catalog
          .where((a) => a.region == GuideRegion.national)
          .toList();
      expect(ordered.take(4).map((a) => a.name), ['참새', '다람쥐', '까치', '청설모']);
      for (final animal in ordered.take(80)) {
        expect(
          animal.guidePhoto?['source'],
          startsWith('https://'),
          reason: animal.name,
        );
        expect(File(animal.photo!).existsSync(), isTrue, reason: animal.name);
      }
      final dolphin = ordered.indexWhere((a) => a.name == '남방큰돌고래');
      expect(dolphin, greaterThanOrEqualTo(12));
      expect(ordered[dolphin].taxon, 'Mammalia');
      expect(dolphin, lessThan(ordered.indexWhere((a) => a.name == '가마우지')));
      expect(local.toSet().containsAll(exampleSpeciesIds), isTrue);
      expect(
        catalog
            .where((a) => a.region == GuideRegion.jeju)
            .take(4)
            .map((animal) => animal.name),
        ['참새', '직박구리', '멧비둘기', '까치'],
      );
      expect(
        catalog.firstWhere((animal) => animal.jejuId == 'JJ-003554').name,
        '가마우지',
      );
      expect(catalog.every((animal) => animal.name.isNotEmpty), isTrue);
      expect(catalog.any((animal) => animal.name == '[국명없음]'), isFalse);
    },
  );

  testWidgets('discovery counts, region, group, search and detail agree', (
    tester,
  ) async {
    await start(tester, discoveries: {'JJ-000837', 'KTSN-4'});
    expect(find.text('2종'), findsOneWidget);
    await tapText(tester, '발견한 동물');
    expect(find.text('1종'), findsOneWidget);
    expect(find.text('등록 완료'), findsOneWidget);
    await tapText(tester, '조류');
    expect(find.text('동박새'), findsOneWidget);
    expect(find.text('노루'), findsNothing);
    await tapText(tester, '포유류');
    await tester.enterText(find.byType(TextField), 'ursus');
    await tester.pumpAndSettle();
    expect(find.text('조건에 맞는 동물이 없어요.'), findsOneWidget);
    await tapText(tester, '제주 목격');
    await tester.tap(
      find.widgetWithText(CheckedPopupMenuItem<GuideRegion>, '전국 도감'),
    );
    await tester.pumpAndSettle();
    expect(find.text('남방큰돌고래'), findsNothing);
    expect(find.text('2종'), findsOneWidget);
    expect(find.text('등록 완료'), findsOneWidget);
    expect(find.text('노루'), findsNothing);
    await tapText(tester, '반달가슴곰');
    expect(find.text('국가생물종목록  4'), findsOneWidget);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), '없는 동물');
    await tester.pumpAndSettle();
    expect(find.text('조건에 맞는 동물이 없어요.'), findsOneWidget);
    await tapText(tester, '검색·분류 초기화');
    expect(find.text('노루'), findsOneWidget);
    expect(find.text('남방큰돌고래'), findsNothing);
    await tapText(tester, '전국 도감');
    await tester.tap(
      find.widgetWithText(CheckedPopupMenuItem<GuideRegion>, '제주 목격 생물'),
    );
    await tester.pumpAndSettle();
    expect(find.text('반달가슴곰'), findsNothing);
    expect(find.text('1종'), findsOneWidget);
    await tester.tap(find.byTooltip('마당으로 돌아가기'));
    await tester.pumpAndSettle();
    expect(find.text('나의 탐험'), findsOneWidget);
  });

  testWidgets(
    'example mode is explicit and leaves the real collection intact',
    (tester) async {
      final prefs = await start(tester);
      expect(find.text('0종'), findsOneWidget);
      await tapText(tester, '설정');
      await tapText(tester, '예시 기록 보기');
      await tapText(tester, '적용');
      expect(prefs.getBool('explorer.examples'), isTrue);
      await tapText(tester, '도감');
      expect(find.text('3종'), findsOneWidget);
      expect(find.text('발견한 동물 · 예시'), findsOneWidget);
      expect(find.text('등록 완료'), findsNWidgets(3));
      await tapText(tester, '마당');
      await tapText(tester, '설정');
      await tapText(tester, '예시 기록 보기');
      await tapText(tester, '적용');
      expect(find.text('0종'), findsOneWidget);
    },
  );

  testWidgets('character, camera and guide work on a narrow enlarged display', (
    tester,
  ) async {
    tester.platformDispatcher.textScaleFactorTestValue = 1.6;
    addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
    final prefs = await start(tester, size: const Size(320, 640));
    await tester.ensureVisible(find.text('나의 캐릭터'));
    await tapText(tester, '나의 캐릭터');
    await tester.enterText(find.byType(TextFormField), '한라');
    await tapText(tester, '저장');
    expect(prefs.getString('explorer.name'), '한라');
    await tapText(tester, '카메라');
    expect(find.text('관찰 카메라'), findsOneWidget);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    await tapText(tester, '도감');
    expect(find.text('동물 도감'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
