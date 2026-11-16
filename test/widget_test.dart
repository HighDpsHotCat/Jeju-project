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
import 'package:jeju_project/explore/yard_player_native.dart';
import 'package:jeju_project/explore/yard_snapshot.dart';
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
    'JJ-000821',
    '',
    '개',
    'Canis familiaris',
    'Mammalia',
    GuideRegion.jeju,
  ),
  Animal('JJ-001834', '', '고양이', 'Felis catus', 'Mammalia', GuideRegion.jeju),
  Animal('JJ-003483', '5', '참새', 'Passer montanus', 'Aves', GuideRegion.jeju),
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
  test('the eleven yard models use stable species IDs and Korean names', () async {
    final catalog = decodeGuideCatalog(
      await File('assets/catalog/animals.tsv.gz').readAsBytes(),
    );
    final models = catalog.where(
      (animal) =>
          animal.region == GuideRegion.jeju && animal.modelKey.isNotEmpty,
    );
    final ids = {for (final a in models) a.jejuId, 'recorder-only'};
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
        'roe_deer': '노루',
        'bulbul': '직박구리',
        'white_eye': '동박새',
        'magpie': '까치',
        'cabbage_white': '배추흰나비',
        'sparrow': '참새',
        'swallowtail': '호랑나비',
        'dolphin': '남방큰돌고래',
        '': 'recorder-only',
      },
    );
    expect(rows, hasLength(12));
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
        discoveries: {'KTSN-1', 'JJ-000837'},
        initial: {
          'device_profile.user_id': 'field-a',
          'guide.manual.user:field-b': <String>['JJ-004729'],
        },
        publish: (value) async => snapshot = value,
      );
      expect(jsonDecode(snapshot!)['animals'], hasLength(5));
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
      expect(find.text('6종'), findsOneWidget);
      await tapText(tester, '마당으로 보내기');
      expect(clipboard, snapshot);
      final data = jsonDecode(snapshot!);
      expect(data['version'], 1);
      expect(data['profile'], 'user:field-a');
      expect(data['examples'], false);
      final exported = (data['animals'] as List).cast<Map>();
      expect(
        {
          for (final a in exported.where((a) => a['startInYard'] == true))
            a['id']: a['modelKey'],
        },
        {
          'JJ-000821': 'corgi',
          'JJ-001834': 'tabby',
          'JJ-003483': 'sparrow',
          'JJ-004729': 'dolphin',
        },
      );
      expect(exported.where((a) => a['startInYard'] != true), [
        {'id': 'JJ-000837', 'name': '노루', 'modelKey': 'roe_deer'},
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
      expect(
        (jsonDecode(snapshot!)['animals'] as List).singleWhere(
          (a) => a['id'] == 'JJ-004729',
        ),
        {
          'id': 'JJ-004729',
          'name': '남방큰돌고래',
          'modelKey': 'dolphin',
          'startInYard': true,
        },
      );
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
      final jeju = catalog.where((a) => a.region == GuideRegion.jeju);
      expect(jeju.skip(80).every((a) => a.guidePhoto == null), isTrue);
      for (final animal in [...ordered.take(80), ...jeju.take(80)]) {
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
    await start(tester, discoveries: {'JJ-000837'});
    expect(find.text('1종'), findsNothing);
    await tapText(tester, '도감');
    expect(find.text('5종'), findsOneWidget);
    expect(find.text('등록 완료'), findsWidgets);
    await tapText(tester, '조류');
    expect(find.text('동박새'), findsOneWidget);
    expect(find.text('노루'), findsNothing);
    await tapText(tester, '전체');
    await tapText(tester, '제주 생물');
    await tester.tap(
      find.widgetWithText(CheckedPopupMenuItem<GuideRegion>, '전국 도감'),
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
    expect(find.text('제주 탐험'), findsOneWidget);
  });

  testWidgets(
    'example mode is explicit and leaves the real collection intact',
    (tester) async {
      String? snapshot;
      await start(
        tester,
        initial: {'explorer.examples': true},
        publish: (value) async => snapshot = value,
      );
      expect(jsonDecode(snapshot!)['examples'], true);
      await tapText(tester, '도감');
      expect(find.text('7종'), findsOneWidget);
      expect(find.text('발견한 동물 · 예시'), findsOneWidget);
      expect(find.text('등록 완료'), findsWidgets);
      await tapText(tester, '마당');
      expect(find.text('발견한 동물 · 예시 16종'), findsOneWidget);
    },
  );

  testWidgets('credits, camera and guide work on a narrow enlarged display', (
    tester,
  ) async {
    tester.platformDispatcher.textScaleFactorTestValue = 1.6;
    addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
    await start(tester, size: const Size(320, 640));
    final creditBytes = await tester.runAsync(
      () => File('assets/guide_photos/credits.json').readAsBytes(),
    );
    final messenger = tester.binding.defaultBinaryMessenger;
    messenger.setMockMessageHandler('flutter/assets', (message) async {
      final key = utf8.decode(
        message!.buffer.asUint8List(
          message.offsetInBytes,
          message.lengthInBytes,
        ),
      );
      if (key != 'assets/guide_photos/credits.json') return null;
      return ByteData.sublistView(creditBytes!);
    });
    addTearDown(() => messenger.setMockMessageHandler('flutter/assets', null));
    await tester.tap(find.byTooltip('저작권 및 출처'));
    await tester.pumpAndSettle();
    expect(find.text('저작권 및 출처'), findsOneWidget);
    expect(find.byType(ExpansionTile), findsWidgets);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();
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
    await tapText(tester, '마당');
    expect(find.text('카메라'), findsNothing);
    expect(find.text('나의 캐릭터'), findsNothing);
    await tapText(tester, '마당 입장');
    expect(find.byType(AppBar), findsNothing);
    expect(find.byType(BackButton), findsNothing);
    tester.widget<YardPlayer>(find.byType(YardPlayer)).onExit();
    await tester.pumpAndSettle();
    expect(find.text('마당 입장'), findsOneWidget);
    await tapText(tester, '발견한 동물 4종');
    expect(find.text('카메라'), findsNothing);
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
    expect(find.text('제주 탐험'), findsOneWidget);
  });
}
