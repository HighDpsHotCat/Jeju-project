import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

import 'encounter.dart';
import 'jeju_districts.dart';

const maxObservationPhotos = 5;
const maxPhotoBytes = 12 * 1024 * 1024;

const requireJejuPhotoLocation = bool.fromEnvironment(
  'REQUIRE_JEJU_PHOTO_LOCATION',
);

typedef PhotoIdentification = ({
  String? speciesId,
  FirstEncounter? encounter,
  String? notice,
});

class IdentificationException implements Exception {
  const IdentificationException(this.message);
  final String message;
}

Uri get _endpoint {
  const configured = String.fromEnvironment('BIOCLIP_SERVER');
  if (configured.isNotEmpty) {
    return Uri.parse(configured).resolve('/api/identify');
  }
  if (kIsWeb) return Uri.base.resolve('/api/identify');
  final host = defaultTargetPlatform == TargetPlatform.android
      ? '10.0.2.2'
      : '127.0.0.1';
  return Uri.http('$host:8765', '/api/identify');
}

Future<PhotoIdentification> identifyPhotos(
  List<Uint8List> photos,
  http.Client client, {
  bool requireJejuLocation = requireJejuPhotoLocation,
}) async {
  if (photos.isEmpty || photos.length > maxObservationPhotos) {
    throw const IdentificationException('관찰 사진을 1~5장 선택해 주세요.');
  }
  final request = http.MultipartRequest(
    'POST',
    _endpoint.replace(
      queryParameters: {'capture': requireJejuLocation ? '1' : 'date'},
    ),
  );
  for (var i = 0; i < photos.length; i++) {
    if (photos[i].length > maxPhotoBytes) {
      throw const IdentificationException('사진 한 장당 12MB 이하로 선택해 주세요.');
    }
    request.files.add(
      http.MultipartFile.fromBytes('photos', photos[i], filename: 'photo-$i'),
    );
  }
  final response = await client
      .send(request)
      .then(http.Response.fromStream)
      .timeout(const Duration(seconds: 90));
  if (response.statusCode != 200) {
    throw IdentificationException(switch (response.statusCode) {
      413 => '사진이 너무 커요. 크기를 줄여 다시 시도해 주세요.',
      415 || 422 => '사진을 읽을 수 없어요. 다른 사진을 선택해 주세요.',
      _ => '판별 서버에 연결하지 못했어요. 잠시 후 다시 시도해 주세요.',
    });
  }
  final data = jsonDecode(utf8.decode(response.bodyBytes));
  // A ranked list alone is insufficient, including responses from older servers.
  if (data
      case {
        'decision': 'accepted',
        'candidates': [{'jeju_ids': [String id, ...]}, ...],
        'captures': List captures,
      }
      when id.isNotEmpty && captures.length == photos.length) {
    FirstEncounter? earliest;
    for (final capture in captures) {
      final result = await _acceptedPhoto(id, capture, requireJejuLocation);
      final encounter = result.encounter;
      if (encounter == null) return result;
      earliest = earliest?.earliest(encounter) ?? encounter;
    }
    return (speciesId: id, encounter: earliest, notice: null);
  }
  if (data case {'decision': 'recapture'}) {
    final index = data['photo_index'];
    final label = index is int && index >= 0 && index < photos.length
        ? '${index + 1}번째 사진에서'
        : '관찰 사진에서';
    return (
      speciesId: null,
      encounter: null,
      notice:
          '$label 화면이나 인쇄물을 다시 찍은 흔적이 보여 등록을 보류했어요. 해당 사진을 삭제하고 실제 동물과 주변이 함께 보이게 다시 찍어 주세요.',
    );
  }
  final noAnimal = data is Map && data['decision'] == 'no_animal';
  return (
    speciesId: null,
    encounter: null,
    notice: noAnimal
        ? '사진에서 동물을 확인하지 못했어요. 동물이 잘 보이는 사진으로 다시 시도해 주세요.'
        : '종을 확실히 구분하기 어려워 등록하지 않았어요. 동물의 특징이 더 잘 보이는 사진을 사용해 주세요.',
  );
}

Future<PhotoIdentification> _acceptedPhoto(
  String speciesId,
  dynamic capture,
  bool requireJejuLocation,
) async {
  if (!requireJejuLocation) {
    return (
      speciesId: speciesId,
      encounter: FirstEncounter(capture is Map ? capture['date'] : null, null),
      notice: null,
    );
  }
  FirstEncounter? encounter;
  String? notice;
  if (capture case {'latitude': num latitude, 'longitude': num longitude}) {
    final locate = await loadJejuAreaLookup();
    final area = locate(latitude.toDouble(), longitude.toDouble());
    if (area.isNotEmpty) {
      encounter = FirstEncounter(capture['date'], area);
    } else {
      notice = '촬영 위치가 제주 지역 안으로 확인되지 않아 자동 등록하지 않았어요.';
    }
  } else {
    notice = '촬영 위치를 확인할 수 없어 자동 등록하지 않았어요. GPS가 포함된 원본 사진을 선택해 주세요.';
  }
  return (speciesId: speciesId, encounter: encounter, notice: notice);
}
