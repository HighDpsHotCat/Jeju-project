import 'dart:convert';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import 'encounter.dart';
import 'map.dart';

class AdoptionProfile {
  AdoptionProfile(this.token, this.preferences, this.profile);
  final String token, profile;
  final SharedPreferences preferences;
  String get owner => sha256.convert(utf8.encode(token)).toString();
  Map<String, String> get headers => {'Authorization': 'Bearer $token'};
  String get _pendingPrefix => 'adoption.pending.$profile.';

  static Future<AdoptionProfile> open(
    SharedPreferences prefs,
    String profile,
  ) async {
    final key = 'adoption.profile.$profile';
    var token = prefs.getString(key);
    if (token == null) {
      final random = Random.secure();
      token = List.generate(
        32,
        (_) => random.nextInt(256).toRadixString(16).padLeft(2, '0'),
      ).join();
      if (!await prefs.setString(key, token)) {
        throw StateError('Could not save profile');
      }
    }
    return AdoptionProfile(token, prefs, profile);
  }

  Future<String?> share(String id, Map<String, String> receipt) async {
    final key = '$_pendingPrefix$id';
    await preferences.setString(key, jsonEncode(receipt));
    return _send(key);
  }

  Future<String?> _send(String key) async {
    final receipt = preferences.getString(key);
    if (receipt == null) return null;
    final response = await http
        .post(
          observationMapUrl.resolve('/api/adoption/spots'),
          headers: {...headers, 'Content-Type': 'application/json'},
          body: receipt,
        )
        .timeout(const Duration(seconds: 8));
    if (response.statusCode == 403) {
      await preferences.remove(key);
      return null;
    }
    if (response.statusCode != 200) {
      throw StateError('Spot service unavailable');
    }
    final result = jsonDecode(utf8.decode(response.bodyBytes));
    await preferences.remove(key);
    return switch (result['state']) {
      'shared' => '분양 지도에 24시간 표시돼요.',
      'facility' => '동물 전시시설에서 촬영해 분양 스팟은 생성되지 않았어요.',
      'location' => '제주 촬영 위치를 확인할 수 없어 분양 스팟은 생성되지 않았어요.',
      _ => null,
    };
  }

  Future<Map<String, FirstEncounter>> sync() async {
    for (final key in preferences.getKeys().where(
      (k) => k.startsWith(_pendingPrefix),
    )) {
      await _send(key);
    }
    final res = await http
        .get(
          observationMapUrl.resolve('/api/adoption/collection'),
          headers: headers,
        )
        .timeout(const Duration(seconds: 8));
    if (res.statusCode != 200) throw StateError('Spot service unavailable');
    final data = jsonDecode(utf8.decode(res.bodyBytes));
    return {
      for (final row in data['claims'])
        if (row case {'species': String id, 'claimed': int timestamp})
          id: FirstEncounter(
            calendarDay(
              DateTime.fromMillisecondsSinceEpoch(
                timestamp,
                isUtc: true,
              ).add(const Duration(hours: 9)),
            ),
            null,
          ),
    };
  }
}
