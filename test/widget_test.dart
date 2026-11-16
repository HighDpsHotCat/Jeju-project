import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:jeju_project/explore/catalog.dart';
import 'package:jeju_project/main.dart';
import 'package:jeju_project/explore/collection.dart';
import 'package:jeju_project/explore/encounter.dart';
import 'package:jeju_project/explore/yard_file.dart';
import 'package:jeju_project/explore/yard_snapshot.dart';
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
  Map<String, Object> initial = const {},
  Future<void> Function(String)? publish,
  WidgetBuilder? mapBuilder,
  Size size = const Size(390, 805),
}) async {
  tester.view.devicePixelRatio = 1;
  tester.view.physicalSize = size;
  addTearDown(tester.view.resetDevicePixelRatio);
  addTearDown(tester.view.resetPhysicalSize);
  SharedPreferences.setMockInitialValues(initial);
  final prefs = await SharedPreferences.getInstance();
  await tester.pumpWidget(
    MyApp(
      preferences: prefs,
      loadAnimals: () async => animals,
      loadRecords: (user) async => GuideRecords(user, discoveries),
      publishYard: publish,
      mapBuilder: mapBuilder,
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
  test('the six yard models use stable species IDs and Korean names', () async {
    final catalog = decodeGuideCatalog(
      await File('assets/catalog/animals.tsv.gz').readAsBytes(),
    );
    final models = catalog.where((animal) => animal.modelKey.isNotEmpty);
    final ids = {
      for (final a in models) ...[a.jejuId, a.nationalId],
      'recorder-only',
    };
    final data = jsonDecode(
      encodeYardSnapshot(
        profile: 'user:one',
        examples: false,
        discoveries: ids,
        catalog: catalog,
      ),
    );
    final rows = (data['animals'] as List).cast<Map>();
    expect(rows.map((row) => row['id']).toSet(), {
      for (final a in models) a.jejuId,
      'recorder-only',
    });
    expect(
      {for (final row in rows) row['modelKey']: row['name']},
      {
        'corgi': '웰시코기',
        'tabby': '노란 고양이',
        'gull': '갈매기',
        'sparrow': '참새',
        'swallowtail': '호랑나비',
        'dolphin': '남방큰돌고래',
        '': 'recorder-only',
      },
    );
    expect(rows, hasLength(7));
  });

  testWidgets(
    'manual registration persists by profile and exports the visible collection',
    (tester) async {
      String? snapshot;
      String? clipboard;
      final messenger = tester.binding.defaultBinaryMessenger;
      messenger.setMockMethodCallHandler(SystemChannels.platform, (call) async {
        if (call.method == 'Clipboard.setData') {
          clipboard = call.arguments['text'] as String?;
        }
        return null;
      });
      addTearDown(
        () => messenger.setMockMethodCallHandler(SystemChannels.platform, null),
      );
      final prefs = await start(
        tester,
        discoveries: {'1', 'JJ-000837'},
        initial: {
          'device_profile.user_id': 'field-a',
          'guide.manual.user:field-b': <String>['JJ-004729'],
        },
        publish: (value) async => snapshot = value,
      );
      expect(jsonDecode(snapshot!)['animals'], hasLength(1));
      await tapText(tester, '도감');
      await tapText(tester, '호랑나비');
      await tester.tap(find.byType(DropdownButtonFormField<String>).first);
      await tester.pumpAndSettle();
      await tapText(tester, '제주시');
      final districtPicker = find.byType(DropdownButtonFormField<String>).last;
      await tester.ensureVisible(districtPicker);
      await tester.tap(districtPicker);
      await tester.pumpAndSettle();
      final areaOption = find.text('한림읍').last;
      await tester.ensureVisible(areaOption);
      await tester.tap(areaOption);
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('직접 관찰한 동물로 등록'));
      await tapText(tester, '직접 관찰한 동물로 등록');
      expect(
        readManualEncounters(prefs.getString('guide.encounters.user:field-a'))
            .keys,
        ['JJ-003382'],
      );
      expect(prefs.getStringList('guide.manual.user:field-b'), ['JJ-004729']);
      expect(find.text('2종'), findsOneWidget);
      await tapText(tester, '마당으로 보내기');
      expect(clipboard, snapshot);
      final data = jsonDecode(snapshot!);
      expect(data['version'], 1);
      expect(data['profile'], 'user:field-a');
      expect(data['examples'], false);
      expect(data['animals'], [
        {'id': 'JJ-000837', 'name': '노루', 'modelKey': ''},
        {
          'id': 'JJ-003382',
          'name': '호랑나비',
          'modelKey': 'swallowtail',
          'firstMetDate': calendarDay(DateTime.now()),
          'firstMetLocation': '제주시 한림읍',
        },
      ]);
      await prefs.setString('device_profile.user_id', 'field-b');
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
      await tester.pumpAndSettle();
      expect(jsonDecode(snapshot!)['profile'], 'user:field-b');
      expect(jsonDecode(snapshot!)['animals'].last, {
        'id': 'JJ-004729',
        'name': '남방큰돌고래',
        'modelKey': 'dolphin',
      });
    },
  );

  test('native macOS bridge atomically replaces a populated roster with an empty one', () async {
    TestWidgetsFlutterBinding.ensureInitialized();
    final directory = await Directory.systemTemp.createTemp('jeju-yard-test');
    addTearDown(() => directory.delete(recursive: true));
    const channel = MethodChannel('jeju/yard');
    final messenger =
        TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger;
    messenger.setMockMethodCallHandler(channel, (call) async {
      expect(call.method, 'collectionDirectory');
      return directory.path;
    });
    addTearDown(() => messenger.setMockMethodCallHandler(channel, null));
    final file = File('${directory.path}/yard-collection.json');
    for (final ids in [
      <String>{'JJ-003382'},
      <String>{},
    ]) {
      final json = encodeYardSnapshot(
        profile: 'local',
        examples: false,
        discoveries: ids,
        catalog: animals,
      );
      await writeYardSnapshot(json);
      expect(await file.readAsString(), json);
      expect(await directory.list().length, 1);
    }
  }, skip: !Platform.isMacOS);

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
    await tester.tap(find.byTooltip('메인으로 돌아가기'));
    await tester.pumpAndSettle();
    expect(find.text('나의 탐험'), findsOneWidget);
  });

  testWidgets(
    'example mode is explicit and leaves the real collection intact',
    (tester) async {
      String? snapshot;
      final prefs = await start(
        tester,
        publish: (value) async => snapshot = value,
      );
      expect(find.text('0종'), findsOneWidget);
      await tapText(tester, '설정');
      await tapText(tester, '예시 기록 보기');
      await tapText(tester, '적용');
      expect(prefs.getBool('explorer.examples'), isTrue);
      expect(jsonDecode(snapshot!)['examples'], true);
      await tapText(tester, '도감');
      expect(find.text('12종'), findsOneWidget);
      expect(find.text('발견한 동물 · 예시'), findsOneWidget);
      expect(find.text('등록 완료'), findsNWidgets(3));
      await tapText(tester, '마당');
      await tapText(tester, '설정');
      await tapText(tester, '예시 기록 보기');
      await tapText(tester, '적용');
      expect(find.text('0종'), findsOneWidget);
      expect(jsonDecode(snapshot!)['examples'], false);
      expect(jsonDecode(snapshot!)['animals'], isEmpty);
    },
  );

  testWidgets('character, camera and guide work on a narrow enlarged display', (
    tester,
  ) async {
    tester.platformDispatcher.textScaleFactorTestValue = 1.6;
    addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
    final prefs = await start(tester, size: const Size(320, 640));
    await tapText(tester, '설정');
    await tester.ensureVisible(find.text('나의 캐릭터'));
    await tapText(tester, '나의 캐릭터');
    await tester.enterText(find.byType(TextFormField), '한라');
    await tapText(tester, '저장');
    expect(prefs.getString('explorer.name'), '한라');
    await tapText(tester, '적용');
    await tapText(tester, '카메라');
    expect(find.text('관찰 카메라'), findsOneWidget);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    await tapText(tester, '도감');
    expect(find.text('동물 도감'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('main actions and yard entry return to the originating tab', (
    tester,
  ) async {
    await start(tester);
    final main = tester.getCenter(find.text('메인'));
    final camera = tester.getCenter(find.text('카메라'));
    expect(camera.dx, closeTo(main.dx, 1));
    expect(camera.dy, lessThan(main.dy));
    expect(tester.getCenter(find.text('도감')).dx, lessThan(main.dx));
    expect(tester.getCenter(find.text('마당')).dx, greaterThan(main.dx));
    await tapText(tester, '지도');
    expect(find.text('지도를 준비하고 있어요.'), findsOneWidget);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();
    await tapText(tester, '마당');
    expect(find.text('나의 캐릭터'), findsNothing);
    await tapText(tester, '마당 입장');
    expect(find.text('제주 마당'), findsOneWidget);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();
    expect(find.text('마당 입장'), findsOneWidget);
    await tapText(tester, '발견한 동물');
    await tester.tap(find.byTooltip('마당으로 돌아가기'));
    await tester.pumpAndSettle();
    expect(find.text('나의 마당'), findsOneWidget);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(find.text('지도'), findsOneWidget);
  });

  testWidgets('the map button opens the supplied team screen', (tester) async {
    await start(
      tester,
      mapBuilder: (_) => const Scaffold(body: Text('팀 지도 화면')),
    );
    await tapText(tester, '지도');
    expect(find.text('팀 지도 화면'), findsOneWidget);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(find.text('나의 탐험'), findsOneWidget);
  });
}
