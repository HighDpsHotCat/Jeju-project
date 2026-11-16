import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:jeju_project/explore/catalog.dart';
import 'package:jeju_project/explore/collection.dart';
import 'package:jeju_project/explore/encounter.dart';
import 'package:jeju_project/explore/yard_snapshot.dart';

void main() {
  test('districts, incomplete addresses and unknown days export only coarse fields', () {
    expect(FirstEncounter('2025-02-29', '제주특별자치도 제주시 연동 123-4 202호').fields, {
      'firstMetLocation': '제주시 연동',
    });
    expect(coarseJejuArea('서귀포시 성산읍 고성리 123'), '서귀포시 성산읍');
    expect(coarseJejuArea('제주시 102동 501호'), '제주시');
    expect(coarseJejuArea('33.012,126.012'), '');
    expect(coarseJejuArea('서귀포시 연동 123'), '서귀포시');
    expect(coarseJejuArea('연동'), '연동');
    expect(FirstEncounter.unknown.fields, isEmpty);
    expect(FirstEncounter('2024-02-29', null).fields, {
      'firstMetDate': '2024-02-29',
    });

    final early = FirstEncounter('2026-03-02', null);
    final late = FirstEncounter('2026-04-01', '제주시 연동');
    expect(late.earliest(early).fields, {'firstMetDate': '2026-03-02'});
    expect(late.earliest(FirstEncounter.unknown).fields, isEmpty);
    final morning = encounterFromObservation({
      'captured_at': '2026-03-02T00:30:00Z',
      'latitude': 1,
      'longitude': 2,
    }, (_, _) => '서귀포시 서홍동');
    final afternoon = encounterFromObservation({
      'captured_at': '2026-03-02T01:00:00Z',
      'latitude': 3,
      'longitude': 4,
    }, (_, _) => '제주시 연동');
    expect(afternoon.earliest(morning).location, '서귀포시 서홍동');
    expect(
      encodeYardSnapshot(
        profile: 'local',
        examples: false,
        discoveries: {'X'},
        catalog: const <Animal>[],
        encounters: {'X': early},
      ),
      contains('"firstMetDate":"2026-03-02"'),
    );
    expect(
      encodeYardSnapshot(
        profile: 'local',
        examples: false,
        discoveries: {'X'},
        catalog: const <Animal>[],
      ),
      isNot(contains('firstMet')),
    );
  });

  bool sqliteAvailable;
  try {
    sqliteAvailable = Process.runSync('sqlite3', ['-version']).exitCode == 0;
  } on ProcessException {
    sqliteAvailable = false;
  }

  test('recorder SQL selects earliest confirmed general observation within the same profile', () {
    // Production recorder normalizes captured_at to a fixed-width UTC string.
    const schema = '''
      CREATE TABLE observation(observation_id TEXT PRIMARY KEY,user_id TEXT,species_id TEXT,mode TEXT,identification TEXT,captured_at TEXT,latitude REAL,longitude REAL);
      CREATE VIEW species_card AS SELECT user_id, species_id, count(*) AS observation_count FROM observation WHERE mode='general' AND identification='confirmed' GROUP BY user_id,species_id;
      INSERT INTO observation VALUES
      ('later','me','cat','general','confirmed','2026-06-20T01:00:00.000000+00:00',33.2,126.2),
      ('early','me','cat','general','confirmed','2026-06-18T16:00:00.000000+00:00',33.1,126.1),
      ('other-user','them','cat','general','confirmed','2026-05-01T01:00:00.000000+00:00',33.3,126.3),
      ('research','me','cat','research_only','confirmed','2026-05-01T01:00:00.000000+00:00',33.4,126.4),
      ('uncertain','me',NULL,'general','unknown','2026-04-01T01:00:00.000000+00:00',33.5,126.5);
    ''';
    final result = Process.runSync('sqlite3', [
      '-json',
      ':memory:',
      schema + firstObservationSql(hasGps: true).replaceFirst('?', "'me'"),
    ]);
    expect(result.exitCode, 0, reason: result.stderr.toString());
    final rows = jsonDecode(result.stdout as String) as List;
    final encounter = encounterFromObservation(
      Map<String, Object?>.from(rows.single),
      (lat, lon) {
        expect(lat, 33.1);
        expect(lon, 126.1);
        return '제주시 연동 999번지';
      },
    );
    expect(encounter.fields, {
      'firstMetDate': '2026-06-19',
      'firstMetLocation': '제주시 연동',
    });
    final json = writeManualEncounters({'cat': encounter});
    expect(json, isNot(contains('126.1')));
    expect(json, isNot(contains('999')));
    expect(readManualEncounters(json)['cat']!.fields, encounter.fields);
    final noLocation = encounterFromObservation(
      Map<String, Object?>.from(rows.single),
      null,
    );
    expect(noLocation.fields, {'firstMetDate': '2026-06-19'});
  }, skip: !sqliteAvailable);
}
