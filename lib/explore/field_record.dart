import 'dart:convert';
import 'dart:io';

import 'package:image_picker/image_picker.dart' show XFile;
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

class GpsFix {
  const GpsFix(this.at, this.latitude, this.longitude, this.accuracy);
  final DateTime at;
  final double latitude, longitude, accuracy;

  bool get valid =>
      latitude.isFinite &&
      latitude.abs() <= 90 &&
      longitude.isFinite &&
      longitude.abs() <= 180 &&
      accuracy.isFinite &&
      accuracy >= 0;
}

class FieldStamp {
  const FieldStamp(this.capturedAt, this.fix);
  final DateTime capturedAt;
  final GpsFix? fix;
  // A preview freshness indicator, independent of Jeju boundary eligibility.
  static const maxFixAge = Duration(seconds: 15);

  String get status {
    final sample = fix;
    if (sample == null || !sample.valid) return 'unavailable';
    final age = capturedAt.difference(sample.at);
    return !age.isNegative && age <= maxFixAge ? 'current' : 'stale';
  }

  Map<String, Object?> toJson() {
    final sample = status == 'unavailable' ? null : fix;
    return {
      'capture_source': 'camera',
      'captured_at': capturedAt.toUtc().toIso8601String(),
      'gps_status': status,
      'gps_at': sample?.at.toUtc().toIso8601String(),
      'latitude': sample?.latitude,
      'longitude': sample?.longitude,
      'accuracy_m': sample?.accuracy,
    };
  }

  factory FieldStamp.fromJson(Map<String, dynamic> json) => FieldStamp(
    DateTime.parse(json['captured_at'] as String),
    json['gps_at'] == null
        ? null
        : GpsFix(
            DateTime.parse(json['gps_at'] as String),
            (json['latitude'] as num).toDouble(),
            (json['longitude'] as num).toDouble(),
            (json['accuracy_m'] as num).toDouble(),
          ),
  );
}

class FieldShot {
  const FieldShot(this.photo, this.stamp);
  final XFile photo;
  final FieldStamp stamp;
}

class FieldArchive {
  FieldArchive(this.directory);
  final Directory directory;

  static Future<FieldArchive> open() async => FieldArchive(
    Directory(
      p.join((await getApplicationSupportDirectory()).path, 'field_captures'),
    ),
  );

  // Publish both files together. Readers ignore incomplete staging directories.
  Future<void> save(FieldShot shot) async {
    await directory.create(recursive: true);
    final stage = await directory.createTemp('.pending-');
    try {
      final id =
          '${shot.stamp.capturedAt.toUtc().microsecondsSinceEpoch}-${p.basename(stage.path).substring(9)}';
      final photoName = 'photo${p.extension(shot.photo.name).toLowerCase()}';
      await shot.photo.saveTo(p.join(stage.path, photoName));
      await File(p.join(stage.path, 'record.json')).writeAsString(
        jsonEncode({
          'photo_id': id,
          'photo_ref': p.posix.join('field_captures', id, photoName),
          ...shot.stamp.toJson(),
        }),
        flush: true,
      );
      await stage.rename(p.join(directory.path, id));
    } finally {
      if (await stage.exists()) await stage.delete(recursive: true);
    }
  }

  Future<List<FieldShot>> list() async {
    if (!await directory.exists()) return [];
    final records = <FieldShot>[];
    await for (final item in directory.list(followLinks: false)) {
      if (item is! Directory || p.basename(item.path).startsWith('.')) continue;
      final json = jsonDecode(
        await File(p.join(item.path, 'record.json')).readAsString(),
      ) as Map<String, dynamic>;
      final photoName = p.posix.basename(json['photo_ref'] as String);
      records.add(
        FieldShot(
          XFile(p.join(item.path, photoName)),
          FieldStamp.fromJson(json),
        ),
      );
    }
    records.sort((a, b) => b.stamp.capturedAt.compareTo(a.stamp.capturedAt));
    return records;
  }
}
