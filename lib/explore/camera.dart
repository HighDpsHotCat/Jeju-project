import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';

import 'style.dart';
import 'field_record.dart';
import 'field_shutter.dart';
import 'catalog.dart';
import 'guide.dart';
import 'identification.dart';

class CameraScreen extends StatefulWidget {
  const CameraScreen({super.key, this.recovered});
  final XFile? recovered;
  @override
  State<CameraScreen> createState() => _CameraScreenState();
}

class _CameraScreenState extends State<CameraScreen> {
  final _picker = ImagePicker();
  Uint8List? _photo;
  String? _error;
  bool _busy = false;
  FieldShot? _shot;
  bool _saved = false;
  PhotoIdentification? _result;
  bool _classifying = false;
  bool _identifyError = false;
  int _request = 0;
  late final _catalog = loadGuideCatalog();

  bool get _fieldCamera =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.iOS ||
          defaultTargetPlatform == TargetPlatform.android);

  bool get _hasCamera =>
      (!kIsWeb ||
          defaultTargetPlatform == TargetPlatform.iOS ||
          defaultTargetPlatform == TargetPlatform.android) &&
      _picker.supportsImageSource(ImageSource.camera);

  @override
  void initState() {
    super.initState();
    if (widget.recovered != null) _pick(recovered: widget.recovered);
  }

  Future<void> _pick({XFile? recovered, bool gallery = false}) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      FieldShot? shot;
      XFile? file = recovered;
      if (file == null && _fieldCamera && !gallery) {
        shot = await Navigator.push<FieldShot>(
          context,
          MaterialPageRoute(builder: (_) => const FieldShutter()),
        );
        file = shot?.photo;
      } else {
        file ??= await _picker.pickImage(
          source: !gallery && _hasCamera
              ? ImageSource.camera
              : ImageSource.gallery,
          maxWidth: 1800,
          maxHeight: 1800,
          requestFullMetadata: false,
        );
      }
      if (file == null) return;
      final bytes = await file.readAsBytes();
      if (mounted) {
        setState(() {
          _photo = bytes;
          _shot = shot;
          _saved = false;
          _result = null;
        });
        unawaited(_identify(bytes));
      }
    } on PlatformException catch (error) {
      if (mounted) {
        setState(
          () => _error =
              error.code.contains('denied') || error.code.contains('restricted')
              ? '기기 설정에서 카메라 또는 사진 접근 권한을 확인해 주세요.'
              : '사진을 열지 못했어요. 카메라 상태를 확인하고 다시 시도해 주세요.',
        );
      }
    } catch (_) {
      if (mounted) setState(() => _error = '사진을 불러오지 못했어요. 다시 시도해 주세요.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _identify(Uint8List photo) async {
    final token = ++_request;
    setState(() {
      _classifying = true;
      _identifyError = false;
    });
    try {
      final result = await SpeciesIdentifier.instance.identify(photo);
      if (mounted && token == _request) setState(() => _result = result);
    } catch (e) {
      debugPrint('Species identification failed: $e');
      if (mounted && token == _request) setState(() => _identifyError = true);
    } finally {
      if (mounted && token == _request) setState(() => _classifying = false);
    }
  }

  Future<void> _details(SpeciesCandidate candidate) async {
    final entries = await _catalog;
    if (!mounted) return;
    final entry = entries.where((a) => a.jejuId == candidate.id).firstOrNull;
    if (entry == null) return;
    await showFieldSheet<void>(
      context,
      AnimalDetail(
        animal: Animal(
          entry.jejuId,
          entry.nationalId,
          candidate.name,
          candidate.scientific,
          entry.taxon,
          entry.region,
        ),
      ),
    );
  }

  Future<void> _save() async {
    final shot = _shot;
    final result = _result;
    if (shot == null ||
        _busy ||
        (_saved && identical(shot.identification, result))) {
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final archive = await FieldArchive.open();
      final stored = _saved
          ? await archive.updateResult(shot, result!)
          : await archive.save(shot, identification: result);
      if (mounted) {
        setState(() {
          _shot = stored;
          _saved = true;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _error = '기기에 기록을 저장하지 못했어요. 다시 시도해 주세요.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _history() async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final records = await (await FieldArchive.open()).list();
      if (!mounted) return;
      final chosen = await showModalBottomSheet<FieldShot>(
        context: context,
        showDragHandle: true,
        builder: (context) => SafeArea(
          child: records.isEmpty
              ? const Padding(
                  padding: EdgeInsets.all(24),
                  child: Text('아직 저장한 현장 기록이 없어요.'),
                )
              : ListView.builder(
                  shrinkWrap: true,
                  itemCount: records.length,
                  itemBuilder: (context, i) {
                    final record = records[i];
                    return ListTile(
                      leading: const Icon(Icons.photo_camera_outlined),
                      title: Text(_localTime(record.stamp.capturedAt)),
                      subtitle: Text(_statusText(record.stamp)),
                      onTap: () => Navigator.pop(context, record),
                    );
                  },
                ),
        ),
      );
      if (chosen == null) return;
      final bytes = await chosen.photo.readAsBytes();
      if (mounted) {
        setState(() {
          _shot = chosen;
          _photo = bytes;
          _saved = true;
          _request++;
          _result = chosen.identification;
          _classifying = false;
          _identifyError = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _error = '저장한 현장 기록을 열지 못했어요.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final photo = _photo;
    final result = _result;
    final upToDate = _saved && identical(_shot?.identification, result);
    return Scaffold(
      appBar: AppBar(
        title: const Text(
          '관찰 카메라',
          style: TextStyle(fontWeight: FontWeight.w800),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 500),
            child: ListView(
              shrinkWrap: true,
              padding: const EdgeInsets.all(24),
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(24),
                  child: AspectRatio(
                    aspectRatio: 1.18,
                    child: ColoredBox(
                      color: const Color(0xFFE8ECDD),
                      child: photo == null
                          ? const Center(
                              child: Icon(
                                Icons.photo_camera_outlined,
                                size: 60,
                                color: leaf,
                              ),
                            )
                          : Image.memory(
                              photo,
                              fit: BoxFit.contain,
                              semanticLabel: '관찰 사진 미리보기',
                              errorBuilder: (context, error, stack) =>
                                  const Center(child: Text('표시할 수 없는 사진이에요.')),
                            ),
                    ),
                  ),
                ),
                const SizedBox(height: 24),
                Text(
                  photo == null ? '어떤 동물을 만났나요?' : '관찰 사진 미리보기',
                  style: const TextStyle(
                    fontSize: 25,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 8),
                if (photo == null)
                  const Text(
                    '노루·참새·칠성무당벌레·붉은귀거북·청개구리를 식별할 수 있어요. 동물 한 마리가 크게 보이게 촬영해 주세요.',
                    style: TextStyle(color: muted, height: 1.5),
                  ),
                if (photo != null) ...[
                  if (_classifying) ...[
                    const LinearProgressIndicator(),
                    const Text('기기에서 동물을 식별하고 있어요…'),
                  ] else ...[
                    if (_identifyError)
                      const Text('사진을 식별하지 못했어요. 다시 시도하거나 다른 사진을 골라 주세요.'),
                    if (result != null) ...[
                      Text(
                        result.confirmedId != null
                            ? '직접 확인한 동물'
                            : result.dismissed
                            ? '미확인으로 남긴 사진'
                            : switch (result.decision) {
                                'suggested' => 'AI가 제안한 동물',
                                'review' => '사진을 보고 후보를 확인해 주세요',
                                _ => '미확인 · 지원하는 종으로 식별하기 어려워요.',
                              },
                        style: const TextStyle(fontWeight: FontWeight.bold),
                      ),
                      for (final c in result.visibleCandidates)
                        ListTile(
                          contentPadding: EdgeInsets.zero,
                          title: Text('${c.groupName} · ${c.name}'),
                          subtitle: Text(c.scientific),
                          onTap: () => _details(c),
                          trailing: result.confirmedId == c.id
                              ? const Icon(
                                  Icons.check_circle,
                                  semanticLabel: '확인됨',
                                )
                              : TextButton(
                                  onPressed: _busy
                                      ? null
                                      : () => setState(
                                          () => _result = result.choose(c.id),
                                        ),
                                  child: const Text('이 종으로 확인'),
                                ),
                        ),
                      if (result.visibleCandidates.isNotEmpty)
                        TextButton(
                          onPressed: _busy
                              ? null
                              : () => setState(
                                  () => _result = result.choose(null),
                                ),
                          child: const Text('미확인으로 두기'),
                        ),
                    ],
                    TextButton.icon(
                      onPressed: _busy ? null : () => _identify(photo),
                      icon: const Icon(Icons.search),
                      label: Text(result == null ? '동물 식별' : '다시 식별'),
                    ),
                  ],
                ],
                if (_shot case final shot?) ...[
                  const SizedBox(height: 14),
                  Text('촬영 시각  ${_localTime(shot.stamp.capturedAt)}'),
                  Text(_statusText(shot.stamp)),
                  if (shot.stamp.fix case final fix?) ...[
                    Text('위치 측정 시각  ${_localTime(fix.at)}'),
                    Text(
                      '위도 ${fix.latitude.toStringAsFixed(6)} · 경도 ${fix.longitude.toStringAsFixed(6)}',
                    ),
                    Text('수평 정확도  ±${fix.accuracy.ceil()} m'),
                  ],
                  const SizedBox(height: 10),
                  FilledButton.tonalIcon(
                    onPressed: _busy || upToDate ? null : _save,
                    icon: Icon(upToDate ? Icons.check : Icons.save_outlined),
                    label: Text(
                      upToDate
                          ? '기기에 저장됨'
                          : _saved
                          ? '식별 결과 저장'
                          : '현장 기록 저장',
                    ),
                  ),
                ],
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 10),
                    child: Text(
                      _error!,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                  ),
                const SizedBox(height: 20),
                FilledButton.icon(
                  onPressed: _busy ? null : () => _pick(),
                  icon: Icon(
                    _fieldCamera || _hasCamera
                        ? Icons.photo_camera_outlined
                        : Icons.photo_outlined,
                  ),
                  label: Text(
                    _fieldCamera || _hasCamera
                        ? (photo == null ? '사진 촬영' : '다시 촬영')
                        : '사진 열기',
                  ),
                ),
                if (_fieldCamera)
                  TextButton.icon(
                    onPressed: _busy ? null : () => _pick(gallery: true),
                    icon: const Icon(Icons.photo_library_outlined),
                    label: const Text('갤러리 사진으로 식별'),
                  ),
                if (_fieldCamera)
                  TextButton(
                    onPressed: _busy ? null : _history,
                    child: const Text('저장한 현장 기록'),
                  ),
                if (photo != null)
                  TextButton(
                    onPressed: () => Navigator.pop(context, true),
                    child: const Text('도감에서 찾아보기'),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

String _localTime(DateTime time) => time.toLocal().toString().split('.').first;

String _statusText(FieldStamp stamp) => switch (stamp.status) {
  'current' => '촬영 시점 위치 측정됨',
  'stale' => '측정 시각 차이로 촬영 위치를 확인할 수 없어요.',
  _ => '촬영 시점에 확인된 위치가 없어요.',
};
