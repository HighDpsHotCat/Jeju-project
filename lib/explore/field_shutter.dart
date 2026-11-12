import 'dart:async';

import 'package:camera/camera.dart';
import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';

import 'field_record.dart';

class FieldShutter extends StatefulWidget {
  const FieldShutter({super.key});
  @override
  State<FieldShutter> createState() => _FieldShutterState();
}

class _FieldShutterState extends State<FieldShutter>
    with WidgetsBindingObserver {
  CameraController? _camera;
  StreamSubscription<Position>? _positions;
  GpsFix? _fix;
  String? _cameraError;
  String _location = '촬영 위치를 확인하고 있어요.';
  bool _shooting = false;
  bool _active = true;
  int _generation = 0;
  Future<void> _queue = Future.value();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _schedule(true);
  }

  bool _current(int generation) =>
      mounted && _active && generation == _generation;

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    // Mobile permission dialogs can be inactive while still in the foreground.
    if (state == AppLifecycleState.paused && _active) _schedule(false);
    if (state == AppLifecycleState.resumed && !_active) _schedule(true);
  }

  void _schedule(bool active) {
    _active = active;
    final generation = ++_generation;
    _queue = _queue.then((_) async {
      await _release();
      if (!_current(generation)) return;
      setState(() {
        _cameraError = null;
        _location = '촬영 위치를 확인하고 있어요.';
      });
      try {
        final cameras = await availableCameras();
        if (!_current(generation)) return;
        if (cameras.isEmpty) {
          throw CameraException('NoCamera', 'No camera available');
        }
        final back = cameras
            .where((camera) => camera.lensDirection == CameraLensDirection.back)
            .firstOrNull;
        final controller = CameraController(
          back ?? cameras.first,
          ResolutionPreset.high,
          enableAudio: false,
        );
        _camera = controller;
        await controller.initialize();
        if (!_current(generation)) return;
        setState(() {});
        unawaited(_locate(generation));
      } on Exception catch (error) {
        if (_current(generation)) {
          setState(
            () => _cameraError =
                error is CameraException && error.code.contains('Denied')
                ? '기기 설정에서 카메라 권한을 허용해 주세요.'
                : '카메라를 열 수 없어요. 다시 시도해 주세요.',
          );
        }
      }
    });
  }

  Future<void> _release() async {
    final sub = _positions;
    final camera = _camera;
    _positions = null;
    _camera = null;
    _fix = null;
    try {
      await sub?.cancel();
    } on Exception {
      debugPrint('Field location stream was already unavailable at teardown.');
    }
    try {
      await camera?.dispose();
    } on Exception {
      // A denied or interrupted initialization may have no native camera to close.
      debugPrint('Field camera was already unavailable at teardown.');
    }
  }

  Future<void> _locate(int generation) async {
    void notice(String text) {
      if (_current(generation)) {
        setState(() {
          _fix = null;
          _location = text;
        });
      }
    }

    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        notice('기기 위치 서비스가 꺼져 있어요. 위치 없이 촬영할 수 있어요.');
        return;
      }
      if (!_current(generation)) return;
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied && _current(generation)) {
        permission = await Geolocator.requestPermission();
      }
      if (!_current(generation)) return;
      if (permission != LocationPermission.whileInUse &&
          permission != LocationPermission.always) {
        notice('위치 권한이 없어 사진과 촬영 시각만 기록합니다.');
        return;
      }
      _positions =
          Geolocator.getPositionStream(
            locationSettings: const LocationSettings(
              accuracy: LocationAccuracy.high,
            ),
          ).listen((p) {
            if (!_current(generation)) return;
            final fix = GpsFix(
              p.timestamp,
              p.latitude,
              p.longitude,
              p.accuracy,
            );
            if (!p.hasAccuracy || !fix.valid) {
              notice('위치 측정값을 확인할 수 없어요.');
              return;
            }
            setState(() {
              _fix = fix;
              _location = '위치 측정 정확도 ±${fix.accuracy.ceil()} m';
            });
          }, onError: (_) => notice('위치를 측정하지 못했어요. 위치 없이 촬영할 수 있어요.'));
    } on Exception {
      notice('위치를 사용할 수 없어요. 위치 없이 촬영할 수 있어요.');
    }
  }

  Future<void> _shoot() async {
    final camera = _camera;
    if (!_active ||
        camera == null ||
        !camera.value.isInitialized ||
        _shooting) {
      return;
    }
    final generation = _generation;
    setState(() => _shooting = true);
    try {
      // Freeze the latest sensor fix at the shutter, before the asynchronous photo write.
      final stamp = FieldStamp(DateTime.now().toUtc(), _fix);
      final image = await camera.takePicture();
      if (mounted &&
          _current(generation) &&
          ModalRoute.of(context)?.isCurrent == true) {
        Navigator.pop(context, FieldShot(image, stamp));
      }
    } on CameraException {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(const SnackBar(content: Text('촬영하지 못했어요. 다시 시도해 주세요.')));
      }
    } finally {
      if (mounted) setState(() => _shooting = false);
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _schedule(false);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final camera = _camera;
    final ready =
        _cameraError == null &&
        _active &&
        camera != null &&
        camera.value.isInitialized;
    return Scaffold(
      appBar: AppBar(title: const Text('현장 촬영')),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: Center(
                child: _cameraError != null
                    ? Padding(
                        padding: const EdgeInsets.all(20),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(_cameraError!),
                            TextButton(
                              onPressed: () => _schedule(true),
                              child: const Text('다시 시도'),
                            ),
                          ],
                        ),
                      )
                    : ready
                    ? CameraPreview(camera)
                    : const CircularProgressIndicator(),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(12),
              child: Text(_location, textAlign: TextAlign.center),
            ),
            Padding(
              padding: const EdgeInsets.only(bottom: 14),
              child: FilledButton.icon(
                onPressed: ready && !_shooting ? _shoot : null,
                icon: const Icon(Icons.camera_alt),
                label: const Text('셔터 누르기'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
