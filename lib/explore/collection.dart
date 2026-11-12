import 'package:flutter/foundation.dart';
import 'package:sqflite/sqflite.dart';

import 'encounter.dart';
import 'jeju_districts.dart';

class GuideRecords {
  const GuideRecords(this.userId, this.species, [this.firstMet = const {}]);
  final String? userId;
  final Set<String> species;
  final Map<String, FirstEncounter> firstMet;

  String get profile => userId == null ? 'local' : 'user:$userId';
}

Future<GuideRecords> readGuideRecords(String? activeUserId) async {
  if (kIsWeb ||
      !const {
        TargetPlatform.android,
        TargetPlatform.iOS,
        TargetPlatform.macOS,
      }.contains(defaultTargetPlatform)) {
    return GuideRecords(activeUserId, {});
  }
  final path = '${await getDatabasesPath()}/jeju_catalog.sqlite3';
  if (!await databaseExists(path)) return GuideRecords(activeUserId, {});
  final db = await openReadOnlyDatabase(path, singleInstance: false);
  try {
    return await db.transaction(
      (reader) => loadGuideProfile(reader, activeUserId),
    );
  } finally {
    await db.close();
  }
}

/// Only confirmed general observations contribute to the recorder's species_card
/// view. The recorder writes captured_at in fixed-width ISO 8601 UTC.
Future<GuideRecords> loadGuideProfile(
  DatabaseExecutor db,
  String? activeUserId,
) async {
  if (activeUserId == null) {
    final profiles = await db.query(
      'device_profile',
      columns: ['user_id'],
      limit: 2,
    );
    if (profiles.length != 1) return const GuideRecords(null, {});
    activeUserId = profiles.single['user_id'] as String;
  }
  final columns = (await db.rawQuery('PRAGMA table_info(observation)'))
      .map((col) => col['name'])
      .toSet();
  if (!columns.containsAll({
    'species_id',
    'user_id',
    'mode',
    'identification',
    'captured_at',
    'observation_id',
  })) {
    final cards = await db.query(
      'species_card',
      columns: ['species_id'],
      where: 'user_id = ?',
      whereArgs: [activeUserId],
    );
    return GuideRecords(
      activeUserId,
      cards.map((card) => card['species_id'] as String).toSet(),
    );
  }
  final hasGps = columns.containsAll({'latitude', 'longitude'});
  final rows = await db.rawQuery(firstObservationSql(hasGps: hasGps), [
    activeUserId,
  ]);
  String Function(double, double)? locate;
  if (hasGps &&
      rows.any((r) => r['latitude'] != null && r['longitude'] != null)) {
    try {
      locate = await loadJejuAreaLookup();
    } catch (error) {
      debugPrint('Could not load offline district boundaries: $error');
    }
  }
  final first = {
    for (final row in rows)
      row['species_id'] as String: encounterFromObservation(row, locate),
  };
  return GuideRecords(activeUserId, first.keys.toSet(), first);
}

String firstObservationSql({required bool hasGps}) =>
    '''
  SELECT c.species_id, o.captured_at ${hasGps ? ', o.latitude, o.longitude' : ''}
  FROM species_card c
  LEFT JOIN observation o ON o.observation_id = (
    SELECT x.observation_id FROM observation x
    WHERE x.user_id = c.user_id AND x.species_id = c.species_id
      AND x.mode = 'general' AND x.identification = 'confirmed'
    ORDER BY x.captured_at, x.observation_id LIMIT 1
  )
  WHERE c.user_id = ?
''';

FirstEncounter encounterFromObservation(
  Map<String, Object?> row,
  String Function(double, double)? locate,
) {
  final captured = row['captured_at'];
  final moment = captured is String ? DateTime.tryParse(captured) : null;
  final day = moment != null && moment.isUtc
      ? calendarDay(moment.toUtc().add(const Duration(hours: 9)))
      : '';
  final lat = row['latitude'], lon = row['longitude'];
  final area = lat is num && lon is num && lat.isFinite && lon.isFinite
      ? locate?.call(lat.toDouble(), lon.toDouble())
      : null;
  return FirstEncounter(day, area, capturedAt: moment);
}
