import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:jeju_project/explore/field_record.dart';
import 'package:jeju_project/explore/identification.dart';

void main() {
  final head = SpeciesHead({
    'version': 'test',
    'margin': .12,
    'weights': [
      [8.0, -4.0, -4.0],
      [-4.0, 8.0, -4.0],
      [-4.0, -4.0, 8.0],
    ],
    'bias': [0.0, 0.0, 0.0],
    'species': [
      for (final entry in [
        ('a', [1.0, 0.0, 0.0]),
        ('b', [0.0, 1.0, 0.0]),
      ])
        {
          'id': entry.$1,
          'name': entry.$1,
          'scientific': entry.$1,
          'group': 'Aves',
          'centroid': entry.$2,
          'minScore': .6,
          'minCos': .2,
        },
    ],
  });

  test('unknown and ambiguous inputs never receive an automatic species confirmation', () {
    final suggested = head.classify([4, 0, 0]);
    expect(suggested.decision, 'suggested');
    expect(suggested.confirmedId, isNull);
    expect(suggested.candidates.single.id, 'a');
    expect(head.classify([3, 3, 0]).decision, 'review');
    expect(head.classify([0, 0, 4]).candidates, isEmpty);
    expect(head.classify([0, 0, 0]).decision, 'unknown');
  });

  test('legacy photo can be identified, corrected and reopened without rewriting the observation', () async {
    final dir = await Directory.systemTemp.createTemp('jeju-identification-');
    addTearDown(() => dir.delete(recursive: true));
    final archive = FieldArchive(Directory('${dir.path}/records'));
    final origin = await File('${dir.path}/test.jpg').writeAsBytes([42]);
    final stamp = FieldStamp(DateTime.utc(2026), null);
    await archive.save(FieldShot(XFile(origin.path), stamp));
    final old = (await archive.list()).single;
    expect(old.identification, isNull);
    final result = head.classify([4, 0, 0]);
    final saved = await archive.updateResult(old, result.choose('a'));
    expect((await archive.list()).single.identification!.confirmedId, 'a');
    await archive.updateResult(saved, result.choose(null));
    final latest = (await archive.list()).single;
    expect(latest.identification!.dismissed, isTrue);
    expect(latest.identification!.visibleCandidates, isEmpty);
    expect(latest.identification!.candidates.single.id, 'a');
    expect(latest.stamp.capturedAt, stamp.capturedAt);
    expect(latest.photo.path, old.photo.path);
    expect(await latest.photo.readAsBytes(), [42]);
  });
}
