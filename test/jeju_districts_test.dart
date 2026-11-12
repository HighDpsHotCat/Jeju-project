import 'dart:convert';

import 'package:archive/archive.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:jeju_project/explore/jeju_districts.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test(
    'bundled districts include city neighborhoods and outlying islands',
    () async {
      final lookup = await loadJejuAreaLookup();
      expect(lookup(33.489, 126.4979), '제주시 연동');
      expect(lookup(33.482, 126.53), '제주시 이도2동');
      expect(lookup(33.4507, 126.918), '서귀포시 성산읍');
      expect(lookup(33.2492, 126.562), '서귀포시 중앙동');
      expect(lookup(33.5068, 126.9518), '제주시 우도면');
      expect(lookup(33.118, 126.269), '서귀포시 대정읍'); // 마라도
      expect(lookup(33.409, 126.225), '제주시 한림읍'); // 비양도
      expect(lookup(33.95, 126.319), '제주시 추자면');
      expect(lookup(33.795, 126.7), ''); // 해상
      expect(lookup(37.568, 126.978), ''); // 제주 행정경계 밖
      expect(lookup(double.nan, 126.51), '');
      expect(await loadJejuAreaLookup(), same(lookup));
    },
  );

  test(
    'polygon interior holes and shared borders cannot assign a district',
    () {
      const outer = [0, 0, 4000000, 0, 4000000, 4000000, 0, 4000000, 0, 0];
      const hole = [
        1000000,
        1000000,
        3000000,
        1000000,
        3000000,
        3000000,
        1000000,
        3000000,
        1000000,
        1000000,
      ];
      const adjacent = [
        4000000,
        0,
        6000000,
        0,
        6000000,
        4000000,
        4000000,
        4000000,
        4000000,
        0,
      ];
      final fixture = [
        {
          'name': '서쪽',
          'polygons': [
            [outer, hole],
          ],
        },
        {
          'name': '동쪽',
          'polygons': [
            [adjacent],
          ],
        },
      ];
      final lookup = decodeJejuAreas(
        GZipEncoder().encodeBytes(utf8.encode(jsonEncode(fixture))),
      );
      expect(lookup(.5, .5), '서쪽');
      expect(lookup(2, 2), '');
      expect(lookup(1, 2), '');
      expect(lookup(2, 4), '');
      expect(lookup(2, 5), '동쪽');
    },
  );
}
