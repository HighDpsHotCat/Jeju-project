import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:jeju_project/explore/catalog.dart';
import 'package:jeju_project/main.dart';
import 'package:shared_preferences/shared_preferences.dart';

const animals = [
  Animal('JJ-000837', '1', '노루', 'Capreolus pygargus', 'Mammalia'),
  Animal('JJ-004873', '2', '동박새', 'Zosterops japonicus', 'Aves'),
  Animal('JJ-003382', '3', '호랑나비', 'Papilio xuthus', 'Insecta'),
  Animal('JJ-004729', '', '남방큰돌고래', 'Tursiops aduncus', 'Mammalia'),
  Animal('', '4', '반달가슴곰', 'Ursus thibetanus', 'Mammalia'),
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
      );
      final local = catalog
          .where((animal) => animal.jejuId.isNotEmpty)
          .map((animal) => animal.jejuId);
      final national = catalog
          .where((animal) => animal.nationalId.isNotEmpty)
          .map((animal) => animal.nationalId);
      expect(local.toSet(), hasLength(4875));
      expect(national.toSet(), hasLength(34875));
      expect(local.toSet().containsAll(exampleSpeciesIds), isTrue);
      expect(catalog.take(4).map((animal) => animal.name), [
        '노루',
        '동박새',
        '호랑나비',
        '남방큰돌고래',
      ]);
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
    await start(tester, discoveries: {'JJ-000837'});
    expect(find.text('1종'), findsOneWidget);
    await tapText(tester, '발견한 동물');
    expect(find.text('1종'), findsOneWidget);
    expect(find.text('등록 완료'), findsOneWidget);
    await tapText(tester, '조류');
    expect(find.text('동박새'), findsOneWidget);
    expect(find.text('노루'), findsNothing);
    await tapText(tester, '전체');
    await tapText(tester, '제주 · 전국');
    await tester.tap(
      find.widgetWithText(CheckedPopupMenuItem<GuideRegion>, '전국'),
    );
    await tester.pumpAndSettle();
    expect(find.text('남방큰돌고래'), findsNothing);
    await tester.enterText(find.byType(TextField), 'ursus');
    await tester.pumpAndSettle();
    await tapText(tester, '반달가슴곰');
    expect(find.text('국가생물종목록  4'), findsOneWidget);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), '없는 동물');
    await tester.pumpAndSettle();
    expect(find.text('조건에 맞는 동물이 없어요.'), findsOneWidget);
    await tapText(tester, '전체 도감 보기');
    expect(find.text('노루'), findsOneWidget);
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
      expect(find.text('12종'), findsOneWidget);
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
