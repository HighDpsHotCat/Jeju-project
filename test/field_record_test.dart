import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:jeju_project/explore/field_record.dart';
import 'package:path/path.dart' as p;

void main() {
  final now = DateTime.utc(2026, 11, 12, 5);
  test('a fix is current only when valid and measured in the 15s before the shutter', () {
    FieldStamp sample(int seconds) => FieldStamp(
      now,
      GpsFix(now.add(Duration(seconds: seconds)), 33.4, 126.5, 5.2),
    );
    expect(sample(-15).status, 'current');
    expect(sample(-16).status, 'stale');
    expect(sample(1).status, 'stale');
    expect(FieldStamp(now, null).toJson()['latitude'], isNull);
    expect(
      FieldStamp(now, GpsFix(now, 100, 126.5, -1)).toJson()['gps_at'],
      isNull,
    );
  });

  test('committed records reopen with the right photos; failed copies leave no visible record', () async {
    final dir = await Directory.systemTemp.createTemp('jeju-record-test');
    addTearDown(() => dir.delete(recursive: true));
    final archiveDir = Directory(p.join(dir.path, 'field_captures'));
    final archive = FieldArchive(archiveDir);
    Future<FieldShot> shot(String name, int byte, GpsFix? fix) async {
      final src = await File(p.join(dir.path, name)).writeAsBytes([byte]);
      return FieldShot(XFile(src.path), FieldStamp(now, fix));
    }

    final a = await shot('a.jpg', 11, GpsFix(now, 33.4, 126.5, 4.7));
    final b = await shot('b.jpg', 22, null);
    await Future.wait([archive.save(a), archive.save(b)]);
    await File(a.photo.path).delete();
    await expectLater(archive.save(a), throwsA(isA<FileSystemException>()));
    await archiveDir.createTemp(
      '.pending-',
    ); // Interrupted write from an earlier session.
    final reopened = await FieldArchive(archiveDir).list();
    expect(reopened, hasLength(2));
    for (final record in reopened) {
      final bytes = await record.photo.readAsBytes();
      final metadata = jsonDecode(
        await File(p.join(p.dirname(record.photo.path), 'record.json'))
            .readAsString(),
      );
      expect(
        p.joinAll([
          dir.path,
          ...p.posix.split(metadata['photo_ref'] as String),
        ]),
        record.photo.path,
      );
      expect(metadata['captured_at'], now.toIso8601String());
      if (bytes.single == 11) {
        expect(record.stamp.fix!.accuracy, 4.7);
        expect(record.stamp.fix!.latitude, 33.4);
        expect(record.stamp.status, 'current');
      } else {
        expect(bytes.single, 22);
        expect(record.stamp.status, 'unavailable');
      }
    }
  });
}
