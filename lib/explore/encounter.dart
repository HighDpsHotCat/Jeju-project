import 'dart:convert';

const jejuDistricts = {
  '제주시': [
    '한림읍',
    '애월읍',
    '구좌읍',
    '조천읍',
    '한경면',
    '추자면',
    '우도면',
    '일도1동',
    '일도2동',
    '이도1동',
    '이도2동',
    '삼도1동',
    '삼도2동',
    '용담1동',
    '용담2동',
    '건입동',
    '화북동',
    '삼양동',
    '봉개동',
    '아라동',
    '오라동',
    '연동',
    '노형동',
    '외도동',
    '이호동',
    '도두동',
  ],
  '서귀포시': [
    '대정읍',
    '남원읍',
    '성산읍',
    '안덕면',
    '표선면',
    '송산동',
    '정방동',
    '중앙동',
    '천지동',
    '효돈동',
    '영천동',
    '동홍동',
    '서홍동',
    '대륜동',
    '대천동',
    '중문동',
    '예래동',
  ],
};

String coarseJejuArea(Object? raw) {
  if (raw is! String) return '';
  final tokens = raw
      .split(RegExp(r'[\s,()]+'))
      .where((s) => s.isNotEmpty)
      .toList();
  final cityIndex = tokens.indexWhere(jejuDistricts.containsKey);
  if (cityIndex >= 0) {
    final city = tokens[cityIndex];
    if (cityIndex + 1 < tokens.length &&
        jejuDistricts[city]!.contains(tokens[cityIndex + 1])) {
      return '$city ${tokens[cityIndex + 1]}';
    }
    return city;
  }
  if (tokens.length == 1 &&
      jejuDistricts.values.any((list) => list.contains(tokens.single))) {
    return tokens.single;
  }
  return '';
}

String calendarDay(DateTime day) =>
    '${day.year.toString().padLeft(4, '0')}-${day.month.toString().padLeft(2, '0')}-${day.day.toString().padLeft(2, '0')}';

class FirstEncounter {
  const FirstEncounter._(this.date, this.location, [this._capturedAt]);
  static const unknown = FirstEncounter._('', '');

  factory FirstEncounter(Object? day, Object? area, {DateTime? capturedAt}) {
    var valid = '';
    if (day is String && RegExp(r'^\d{4}-\d{2}-\d{2}$').hasMatch(day)) {
      final value = DateTime.tryParse(day);
      if (value != null && calendarDay(value) == day) valid = day;
    }
    return FirstEncounter._(valid, coarseJejuArea(area), capturedAt);
  }

  final String date, location;
  final DateTime? _capturedAt;
  Map<String, String> get fields => {
    if (date.isNotEmpty) 'firstMetDate': date,
    if (location.isNotEmpty) 'firstMetLocation': location,
  };

  // Unknown-day legacy registrations may predate a dated record. Never silently
  // substitute a later sighting or borrow its area for the first encounter.
  FirstEncounter earliest(FirstEncounter other) {
    final comparison = date.compareTo(other.date);
    if (comparison != 0) return comparison < 0 ? this : other;
    final a = _capturedAt, b = other._capturedAt;
    if (a != null && b != null) return a.isAfter(b) ? other : this;
    return location == other.location ? this : FirstEncounter._(date, '');
  }
}

Map<String, FirstEncounter> readManualEncounters(String? json) {
  if (json == null) return {};
  final raw = jsonDecode(json) as Map<String, dynamic>;
  return {
    for (final item in raw.entries)
      if (item.key.isNotEmpty && item.value is Map)
        item.key: FirstEncounter(
          item.value['firstMetDate'],
          item.value['firstMetLocation'],
        ),
  };
}

String writeManualEncounters(Map<String, FirstEncounter> encounters) =>
    jsonEncode(encounters.map((id, record) => MapEntry(id, record.fields)));
