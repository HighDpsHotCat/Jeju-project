import 'package:flutter/foundation.dart';
import 'package:sqflite/sqflite.dart';

/// The field recorder owns this database. The guide only reads confirmed cards.
Future<Set<String>> readDiscoveredSpecies(String? activeUserId) async {
  if (kIsWeb ||
      !const {
        TargetPlatform.android,
        TargetPlatform.iOS,
        TargetPlatform.macOS,
      }.contains(defaultTargetPlatform)) {
    return {};
  }
  final path = '${await getDatabasesPath()}/jeju_catalog.sqlite3';
  if (!await databaseExists(path)) return {};
  final db = await openReadOnlyDatabase(path, singleInstance: false);
  try {
    if (activeUserId == null) {
      final profiles = await db.query(
        'device_profile',
        columns: ['user_id'],
        limit: 2,
      );
      if (profiles.length != 1) return {};
      activeUserId = profiles.single['user_id'] as String;
    }
    final cards = await db.query(
      'species_card',
      columns: ['species_id'],
      where: 'user_id = ?',
      whereArgs: [activeUserId],
    );
    return cards.map((card) => card['species_id'] as String).toSet();
  } finally {
    await db.close();
  }
}
