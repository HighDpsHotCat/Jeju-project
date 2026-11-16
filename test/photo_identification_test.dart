import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:jeju_project/explore/identification.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const candidates = [
    {
      'name': '참새',
      'scientific': 'Passer montanus',
      'jeju_ids': ['JJ-003483'],
    },
    {
      'name': '제비',
      'scientific': 'Hirundo rustica',
      'jeju_ids': ['JJ-OTHER'],
    },
  ];

  Future<PhotoIdentification> identify(
    Object payload, {
    bool strict = false,
    int count = 1,
  }) => identifyPhotos(
    [
      for (var i = 0; i < count; i++) Uint8List.fromList([1]),
    ],
    MockClient((request) async {
      expect(request.url.queryParameters['capture'], strict ? '1' : 'date');
      return http.Response.bytes(utf8.encode(jsonEncode(payload)), 200);
    }),
    requireJejuLocation: strict,
  );

  test(
    'AI selects the top species with no GPS or with a mainland photo',
    () async {
      for (final capture in [
        null,
        {'date': '2025-03-04'},
        {'latitude': 37.568, 'longitude': 126.978},
      ]) {
        final result = await identify({
          'decision': 'accepted',
          'candidates': candidates,
          'captures': [capture],
        });
        expect(result.speciesId, 'JJ-003483');
        expect(result.encounter, isNotNull);
        expect(result.encounter!.location, isEmpty);
        expect(result.encounter!.date, capture?['date'] ?? '');
        expect(result.notice, isNull);
      }
    },
  );

  test(
    'only an explicit accepted identification can register a species',
    () async {
      for (final payload in [
        candidates,
        {'candidates': candidates},
        {'decision': 'no_animal', 'candidates': candidates},
        {'decision': 'uncertain', 'candidates': candidates},
        {'decision': 'accepted', 'candidates': []},
        {
          'decision': 'accepted',
          'candidates': [
            {'jeju_ids': []},
          ],
        },
      ]) {
        final result = await identify(payload);
        expect(result.speciesId, isNull);
        expect(result.encounter, isNull);
        expect(result.notice, isNotEmpty);
      }
    },
  );

  test('a suspected recapture blocks the batch and identifies the photo to replace', () async {
    final result = await identify({
      'decision': 'recapture',
      'photo_index': 1,
      'candidates': candidates,
    }, count: 3);
    expect(result.speciesId, isNull);
    expect(result.encounter, isNull);
    expect(result.notice, contains('2번째 사진'));
  });

  test('a geotag on Udo qualifies with its original encounter date', () async {
    final result = await identify({
      'decision': 'accepted',
      'candidates': candidates,
      'captures': [
        {'latitude': 33.5068, 'longitude': 126.9518, 'date': '2025-03-04'},
      ],
    }, strict: true);
    expect(result.encounter?.fields, {
      'firstMetLocation': '제주시 우도면',
      'firstMetDate': '2025-03-04',
    });
    expect(result.notice, isNull);
  });

  test(
    'offshore, mainland and missing GPS keep candidates without registration',
    () async {
      for (final payload in [
        {
          'decision': 'accepted',
          'candidates': candidates,
          'captures': [
            {'latitude': 33.795, 'longitude': 126.7},
          ],
        },
        {
          'decision': 'accepted',
          'candidates': candidates,
          'captures': [
            {'latitude': 37.568, 'longitude': 126.978},
          ],
        },
        {
          'decision': 'accepted',
          'candidates': candidates,
          'captures': [null],
        },
      ]) {
        final result = await identify(payload, strict: true);
        expect(result.speciesId, 'JJ-003483');
        expect(result.encounter, isNull);
        expect(result.notice, isNotNull);
      }
    },
  );
  test(
    'strict location mode checks every photo in an accepted batch',
    () async {
      final result = await identify(
        {
          'decision': 'accepted',
          'candidates': candidates,
          'captures': [
            {'latitude': 33.5068, 'longitude': 126.9518},
            null,
          ],
        },
        strict: true,
        count: 2,
      );
      expect(result.encounter, isNull);
      expect(result.notice, isNotEmpty);
    },
  );

  test('incomplete batch responses cannot register and 0 or 6 photos cannot upload', () async {
    final result = await identify({
      'decision': 'accepted',
      'candidates': candidates,
      'captures': [null],
    }, count: 2);
    expect(result.encounter, isNull);
    for (final count in [0, 6]) {
      await expectLater(
        identify({}, count: count),
        throwsA(isA<IdentificationException>()),
      );
    }
  });
}
