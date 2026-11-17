import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:image_picker/image_picker.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'encounter.dart';
import 'identification.dart';
import 'style.dart';

const pendingObservationPaths = 'camera.pendingObservationPaths';

class CameraScreen extends StatefulWidget {
  const CameraScreen({
    super.key,
    required this.registerPhoto,
    this.recovered = const [],
    this.preferences,
    this.spotOwner,
  });
  final String? spotOwner;
  final Future<String> Function(
    String speciesId,
    FirstEncounter encounter,
    Map<String, String>? receipt,
  )
  registerPhoto;
  final List<XFile> recovered;
  final SharedPreferences? preferences;
  @override
  State<CameraScreen> createState() => _CameraScreenState();
}

class _CameraScreenState extends State<CameraScreen> {
  final _picker = ImagePicker();
  final _photos = <({XFile file, Uint8List bytes, Map<String, double>? fix})>[];
  int _selected = 0;
  String? _error;
  String? _notice;
  bool _picking = false;
  bool _identifying = false;
  http.Client? _client;
  String? _speciesId;

  bool get _busy => _picking || _identifying;
  bool get _canAdd => !_busy && _photos.length < maxObservationPhotos;

  void _clearResult() {
    _speciesId = null;
    _notice = null;
    _error = null;
  }

  @override
  void dispose() {
    _client?.close();
    super.dispose();
  }

  Future<void> _identify() async {
    if (_busy || _photos.isEmpty) return;
    final client = http.Client();
    _client = client;
    setState(() {
      _identifying = true;
      _clearResult();
    });
    try {
      Map<String, String>? receipt;
      final result = await identifyPhotos(
        [for (final photo in _photos) photo.bytes],
        client,
        spotOwner: widget.spotOwner,
        fieldFixes: [for (final p in _photos) p.fix],
        onSpotReceipt: (value) => receipt = value,
      );
      if (!mounted || ModalRoute.of(context)?.isCurrent != true) return;
      final id = result.speciesId;
      final encounter = result.encounter;
      final notice = id != null && encounter != null
          ? await widget.registerPhoto(id, encounter, receipt)
          : result.notice;
      if (mounted) {
        setState(() {
          _speciesId = id;
          _notice = notice;
        });
      }
    } on IdentificationException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } catch (_) {
      if (mounted) {
        setState(() => _error = '판별 서버와 연결이 끊겼어요. 연결을 확인하고 다시 시도해 주세요.');
      }
    } finally {
      client.close();
      _client = null;
      if (mounted) setState(() => _identifying = false);
    }
  }

  bool get _hasCamera =>
      (!kIsWeb ||
          defaultTargetPlatform == TargetPlatform.iOS ||
          defaultTargetPlatform == TargetPlatform.android) &&
      _picker.supportsImageSource(ImageSource.camera);

  @override
  void initState() {
    super.initState();
    if (widget.recovered.isNotEmpty) _restore();
  }

  Future<void> _stage(XFile file, [Map<String, double>? fix]) async {
    if (await file.length() > maxPhotoBytes) {
      throw const IdentificationException('사진 한 장당 12MB 이하로 선택해 주세요.');
    }
    final bytes = await file.readAsBytes();
    if (!mounted) return;
    setState(() {
      _photos.add((file: file, bytes: bytes, fix: fix));
      _selected = _photos.length - 1;
      _clearResult();
    });
  }

  Future<void> _restore() async {
    setState(() => _picking = true);
    var skipped = false;
    for (final file in widget.recovered.take(maxObservationPhotos)) {
      if (!mounted) return;
      try {
        await _stage(file);
      } catch (_) {
        skipped = true;
      }
    }
    if (mounted) {
      setState(() {
        _picking = false;
        if (skipped) _error = '일부 임시 사진을 복원하지 못했어요. 다시 추가해 주세요.';
      });
    }
  }

  Future<void> _pick({ImageSource? source}) async {
    if (!_canAdd) return;
    setState(() {
      _picking = true;
      _error = null;
    });
    // Android can recreate the activity while the native picker is open.
    final prefs = !kIsWeb && defaultTargetPlatform == TargetPlatform.android
        ? widget.preferences
        : null;
    try {
      if (_photos.isNotEmpty) {
        await prefs?.setStringList(pendingObservationPaths, [
          for (final photo in _photos) photo.file.path,
        ]);
      }
      final selectedSource =
          source ?? (_hasCamera ? ImageSource.camera : ImageSource.gallery);
      final file = await _picker.pickImage(
        source: selectedSource,
        requestFullMetadata:
            requireJejuPhotoLocation || widget.spotOwner != null,
      );
      if (file != null && mounted) {
        Map<String, double>? fix;
        if (selectedSource == ImageSource.camera && widget.spotOwner != null) {
          try {
            var permission = await Geolocator.checkPermission();
            if (permission == LocationPermission.denied) {
              permission = await Geolocator.requestPermission();
            }
            if (permission == LocationPermission.whileInUse ||
                permission == LocationPermission.always) {
              final pos = await Geolocator.getCurrentPosition(
                locationSettings: const LocationSettings(
                  accuracy: LocationAccuracy.high,
                  timeLimit: Duration(seconds: 8),
                ),
              );
              if (pos.accuracy <= 100) {
                fix = {'lat': pos.latitude, 'lon': pos.longitude};
              }
            }
          } catch (_) {
            /* A photo without a location still qualifies for the personal guide. */
          }
        }
        if (mounted) await _stage(file, fix);
      }
    } on IdentificationException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } on PlatformException catch (error) {
      if (mounted) {
        setState(
          () => _error =
              error.code.contains('denied') || error.code.contains('restricted')
              ? '기기 설정에서 카메라 또는 사진 접근 권한을 확인해 주세요.'
              : '사진을 열지 못했어요. 다시 시도해 주세요.',
        );
      }
    } catch (_) {
      if (mounted) setState(() => _error = '사진을 불러오지 못했어요. 다시 시도해 주세요.');
    } finally {
      if (mounted) setState(() => _picking = false);
      await prefs?.remove(pendingObservationPaths);
    }
  }

  void _removeSelected() {
    setState(() {
      _photos.removeAt(_selected);
      _selected = _photos.isEmpty ? 0 : _selected.clamp(0, _photos.length - 1);
      _clearResult();
    });
  }

  void _startOver() {
    setState(() {
      _photos.clear();
      _selected = 0;
      _clearResult();
    });
  }

  @override
  Widget build(BuildContext context) {
    final hasPhotos = _photos.isNotEmpty;
    return Scaffold(
      appBar: AppBar(title: const Text('관찰 카메라')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 500),
            child: ListView(
              shrinkWrap: true,
              padding: const EdgeInsets.fromLTRB(24, 8, 24, 24),
              children: [
                Row(
                  children: [
                    const Expanded(
                      child: Text(
                        '관찰 사진',
                        style: TextStyle(
                          fontSize: 23,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                    Text('${_photos.length} / $maxObservationPhotos장'),
                  ],
                ),
                const SizedBox(height: 8),
                const Text(
                  '현장에서 만난 동물과 주변이 함께 보이게 여러 각도에서 찍어 주세요. 사진을 모은 뒤 다음을 누르면 AI가 함께 판별해요.',
                  style: TextStyle(color: muted, height: 1.5),
                ),
                if (widget.spotOwner != null)
                  const Text('촬영 위치가 있는 희귀 동물은 분양 지도에 24시간 동안 대략적인 위치로 표시돼요.'),
                if (requireJejuPhotoLocation)
                  const Text('제주에서 촬영한 GPS 원본 사진을 사용해 주세요.'),
                const SizedBox(height: 16),
                ClipRRect(
                  borderRadius: BorderRadius.circular(20),
                  child: AspectRatio(
                    aspectRatio: 1.6,
                    child: ColoredBox(
                      color: const Color(0xFFE8ECDD),
                      child: !hasPhotos
                          ? const Icon(
                              Icons.photo_camera_outlined,
                              size: 52,
                              color: leaf,
                            )
                          : Image.memory(
                              _photos[_selected].bytes,
                              cacheWidth: 900,
                              fit: BoxFit.contain,
                              semanticLabel: '${_selected + 1}번째 관찰 사진 미리보기',
                              errorBuilder: (_, _, _) =>
                                  const Center(child: Text('미리보기를 표시할 수 없어요.')),
                            ),
                    ),
                  ),
                ),
                if (hasPhotos) ...[
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 4,
                    runSpacing: 4,
                    children: [
                      for (var i = 0; i < _photos.length; i++)
                        Semantics(
                          button: true,
                          selected: i == _selected,
                          label: '${i + 1}번째 사진 보기',
                          child: InkWell(
                            onTap: _busy
                                ? null
                                : () => setState(() => _selected = i),
                            child: Container(
                              padding: const EdgeInsets.all(2),
                              decoration: BoxDecoration(
                                border: Border.all(
                                  color: i == _selected
                                      ? leaf
                                      : Colors.transparent,
                                  width: 2,
                                ),
                              ),
                              child: Image.memory(
                                _photos[i].bytes,
                                width: 44,
                                height: 44,
                                cacheWidth: 120,
                                fit: BoxFit.cover,
                                excludeFromSemantics: true,
                                errorBuilder: (_, _, _) =>
                                    const SizedBox.square(
                                      dimension: 44,
                                      child: Icon(Icons.photo_outlined),
                                    ),
                              ),
                            ),
                          ),
                        ),
                    ],
                  ),
                  Align(
                    alignment: Alignment.centerRight,
                    child: TextButton.icon(
                      onPressed: _busy ? null : _removeSelected,
                      icon: const Icon(Icons.delete_outline),
                      label: const Text('선택한 사진 삭제'),
                    ),
                  ),
                ],
                OutlinedButton.icon(
                  onPressed: _canAdd ? () => _pick() : null,
                  icon: Icon(
                    _hasCamera
                        ? Icons.photo_camera_outlined
                        : Icons.add_photo_alternate_outlined,
                  ),
                  label: Text(_hasCamera ? '사진 촬영' : '사진 추가'),
                ),
                if (_hasCamera)
                  TextButton(
                    onPressed: _canAdd
                        ? () => _pick(source: ImageSource.gallery)
                        : null,
                    child: const Text('앨범에서 사진 추가'),
                  ),
                const SizedBox(height: 8),
                FilledButton(
                  onPressed: !hasPhotos || _busy ? null : _identify,
                  child: Text(_identifying ? '판별 중…' : '다음'),
                ),
                if (_identifying) const LinearProgressIndicator(),
                if (_error != null || _notice != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 12),
                    child: Text(
                      _error ?? _notice!,
                      style: TextStyle(
                        color: _error != null
                            ? Theme.of(context).colorScheme.error
                            : null,
                      ),
                    ),
                  ),
                if (_notice != null && !_busy) ...[
                  if (_speciesId != null)
                    TextButton(
                      onPressed: () => Navigator.pop(context, _speciesId),
                      child: const Text('도감에서 보기'),
                    ),
                  TextButton(
                    onPressed: _startOver,
                    child: const Text('새 관찰 시작'),
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
