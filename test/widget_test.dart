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
  test('the yard models use stable species IDs and Korean names', () async {
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
      for (final a in models) a.id,
      'recorder-only',
    });
    expect(
      {for (final row in rows) row['modelKey']: row['name']},
      {
        'roe_deer': '노루',
        'bulbul': '직박구리',
        'white_eye': '동박새',
        'magpie': '까치',
        'cabbage_white': '배추흰나비',
        'chipmunk': '다람쥐',
        'water_deer': '고라니',
        'raccoon_dog': '너구리',
        'crow': '까마귀',
        'mallard': '청둥오리',
        'swallow': '제비',
        'corgi': '웰시코기',
        'tabby': '노란 고양이',
        'gull': '갈매기',
        'sparrow': '참새',
        'swallowtail': '호랑나비',
        'dolphin': '남방큰돌고래',
        '': 'recorder-only',
      },
    );
    expect(rows, hasLength(18));
  });

  testWidgets(
    'pet details collect individual breeds by profile and count each species once',
    (tester) async {
      final catalog = decodeGuideCatalog(
        File('assets/catalog/animals.tsv.gz').readAsBytesSync(),
        jsonDecode(File('assets/guide_photos/credits.json').readAsStringSync())
            as Map<String, dynamic>,
      ).where((a) => a.breeds.isNotEmpty).toList();
      expect(catalog, hasLength(2));
      SharedPreferences.setMockInitialValues({'device_profile.user_id': 'a'});
      final prefs = await SharedPreferences.getInstance();
      await tester.pumpWidget(
        MyApp(
          preferences: prefs,
          loadAnimals: () async => catalog,
          loadRecords: (user) async => GuideRecords(user, {}),
        ),
      );
      await tester.pumpAndSettle();
      await tapText(tester, '도감');

      Future<void> openPet(String query, String name) async {
        await tester.enterText(find.byType(TextField).first, query);
        await tester.pumpAndSettle();
        await tapText(tester, name);
      }

      Future<void> close() async {
        Navigator.of(tester.element(find.text('품종 수집'))).pop();
        await tester.pumpAndSettle();
      }

      Future<void> collect(String name) async {
        await tester.ensureVisible(find.text(name));
        await tapText(tester, name);
        await tester.ensureVisible(find.text('직접 관찰한 품종으로 수집'));
        await tapText(tester, '직접 관찰한 품종으로 수집');
      }

      await openPet('강아지', '개');
      expect(find.text('개 · 품종 수집 0/20'), findsOneWidget);
      await collect('골든 리트리버');
      await collect('닥스훈트');
      expect(find.text('개 · 품종 수집 2/20'), findsOneWidget);
      await close();
      await openPet('고양이', '고양이');
      expect(find.text('고양이 · 품종 수집 0/20'), findsOneWidget);
      await collect('노르웨이 숲고양이');
      expect(find.text('고양이 · 품종 수집 1/20'), findsOneWidget);
      await close();
      expect(find.text('2종'), findsOneWidget);
      await openPet('골든 리트리버', '개');
      await tapText(tester, '골든 리트리버');
      expect(
        tester.widget<FilledButton>(find.byType(FilledButton).last).onPressed,
        isNull,
      );
      Navigator.of(tester.element(find.text('골든 리트리버').last)).pop();
      await tester.pumpAndSettle();
      await close();
      final saved = readManualEncounters(
        prefs.getString('guide.encounters.user:a'),
      );
      expect(saved.keys.toSet(), {
        'JJ-000821',
        'BREED-golden',
        'BREED-dachshund',
        'JJ-001834',
        'CAT-norwegian-forest',
      });
      await prefs.setString('device_profile.user_id', 'b');
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
      await tester.pumpAndSettle();
      await openPet('강아지', '개');
      expect(find.text('개 · 품종 수집 0/20'), findsOneWidget);
    },
  );

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
      final credits = jsonDecode(
        await File('assets/guide_photos/credits.json').readAsString(),
      ) as Map<String, dynamic>;
      final catalog = decodeGuideCatalog(
        await File('assets/catalog/animals.tsv.gz').readAsBytes(),
        credits,
      );
      final displayed = <String>{};
      for (final animal in catalog) {
        for (final (region, id) in [
          (GuideRegion.jeju, animal.jejuId),
          (GuideRegion.national, animal.nationalId),
        ]) {
          if (!credits.containsKey(id)) continue;
          final photo = animal.photoFor(region)!;
          expect(photo.asset, 'assets/guide_photos/$id.jpg');
          expect(photo.credit, credits[id]);
          expect(File(photo.asset).existsSync(), isTrue);
          displayed.add(id);
        }
      }
      for (final parent in catalog.where((a) => a.breeds.isNotEmpty)) {
        expect(parent.breeds, hasLength(20));
        for (final breed in parent.breeds) {
          expect(breedSpeciesId(breed.id), parent.id);
          expect(breed.photo.credit, credits[breed.id]);
          expect(File(breed.photo.asset).existsSync(), isTrue);
          expect(displayed.add(breed.id), isTrue);
        }
      }
      expect(displayed, credits.keys.toSet());
      final local = catalog
          .where((animal) => animal.jejuId.isNotEmpty)
          .map((animal) => animal.jejuId);
      final national = catalog
          .where((animal) => animal.nationalId.isNotEmpty)
          .map((animal) => animal.nationalId);
      expect(local.toSet(), hasLength(4797));
      expect(national.toSet(), hasLength(32460));
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
      final ordered = catalog.where((a) => a.nationalId.isNotEmpty).toList()
        ..sort(compareNationalAnimals);
      expect(ordered.take(4).map((a) => a.name), ['참새', '다람쥐', '까치', '청설모']);
      for (final animal in ordered.take(80)) {
        expect(
          animal.photoFor(GuideRegion.national)?.credit?['source'],
          startsWith('https://'),
          reason: animal.name,
        );
        expect(
          File(animal.photoFor(GuideRegion.national)!.asset).existsSync(),
          isTrue,
          reason: animal.name,
        );
      }
      final dolphin = ordered.indexWhere((a) => a.name == '남방큰돌고래');
      expect(dolphin, greaterThanOrEqualTo(12));
      expect(ordered[dolphin].taxon, 'Mammalia');
      expect(dolphin, lessThan(ordered.indexWhere((a) => a.name == '가마우지')));
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
      expect(find.text('3종'), findsOneWidget);
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
    expect(find.byType(AppBar), findsNothing);
    expect(find.byType(BackButton), findsNothing);
    tester.widget<YardPlayer>(find.byType(YardPlayer)).onExit();
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
