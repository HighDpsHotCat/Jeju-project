import 'package:flutter/foundation.dart';
import 'package:sqflite/sqflite.dart';

class GuideRecords {
  const GuideRecords(this.userId, this.species);
  final String? userId;
  final Set<String> species;

  String get profile => userId == null ? 'local' : 'user:$userId';
}

/// The field recorder owns the confirmed records. Manual guide entries are
/// stored separately and scoped to the same resolved profile.
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
    if (activeUserId == null) {
      final profiles = await db.query(
        'device_profile',
        columns: ['user_id'],
        limit: 2,
      );
      if (profiles.length != 1) return const GuideRecords(null, {});
      activeUserId = profiles.single['user_id'] as String;
    }
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
  } finally {
    await db.close();
  }
}
