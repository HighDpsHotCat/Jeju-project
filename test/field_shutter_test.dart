import 'dart:async';

import 'package:camera_platform_interface/camera_platform_interface.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:jeju_project/explore/field_record.dart';
import 'package:jeju_project/explore/field_shutter.dart';

class _Camera extends CameraPlatform {
  final image = Completer<XFile>();
  final errors = StreamController<CameraErrorEvent>.broadcast();
  @override
  Future<List<CameraDescription>> availableCameras() async => [
    const CameraDescription(
      name: 'back',
      lensDirection: CameraLensDirection.back,
      sensorOrientation: 0,
    ),
  ];
  @override
  Future<int> createCameraWithSettings(
    CameraDescription cameraDescription,
    MediaSettings mediaSettings,
  ) async => 1;
  @override
  Future<void> initializeCamera(
    int cameraId, {
    ImageFormatGroup imageFormatGroup = ImageFormatGroup.unknown,
  }) async {}
  @override
  Stream<CameraInitializedEvent> onCameraInitialized(int cameraId) =>
      Stream.value(
        const CameraInitializedEvent(
          1,
          640,
          480,
          ExposureMode.auto,
          false,
          FocusMode.auto,
          false,
        ),
      );
  @override
  Stream<DeviceOrientationChangedEvent> onDeviceOrientationChanged() =>
      const Stream.empty();
  @override
  Stream<CameraErrorEvent> onCameraError(int cameraId) => errors.stream;
  @override
  Widget buildPreview(int cameraId) => const SizedBox();
  @override
  Future<XFile> takePicture(int cameraId) => image.future;
  @override
  Future<void> dispose(int cameraId) async {}
}

class _Location extends GeolocatorPlatform {
  _Location(this.permission);
  final LocationPermission permission;
  final fixes = StreamController<Position>.broadcast();
  @override
  Future<bool> isLocationServiceEnabled() async => true;
  @override
  Future<LocationPermission> checkPermission() async => permission;
  @override
  Stream<Position> getPositionStream({LocationSettings? locationSettings}) =>
      fixes.stream;
  void emit(double latitude) => fixes.add(
    Position(
      latitude: latitude,
      longitude: 126.5,
      timestamp: DateTime.now().toUtc(),
      accuracy: 4.3,
      hasAccuracy: true,
      altitude: 0,
      altitudeAccuracy: 0,
      heading: 0,
      headingAccuracy: 0,
      speed: 0,
      speedAccuracy: 0,
    ),
  );
}

void main() {
  for (final allowed in [true, false]) {
    testWidgets(
      allowed ? 'photo uses the pre-shutter fix while the camera is writing' : 'location denial still permits a photo without invented coordinates',
      (tester) async {
        final previousCamera = CameraPlatform.instance,
            previousLocation = GeolocatorPlatform.instance;
        final camera = _Camera();
        final location = _Location(
          allowed
              ? LocationPermission.whileInUse
              : LocationPermission.deniedForever,
        );
        CameraPlatform.instance = camera;
        GeolocatorPlatform.instance = location;
        addTearDown(() {
          CameraPlatform.instance = previousCamera;
          GeolocatorPlatform.instance = previousLocation;
        });
        FieldShot? result;
        await tester.pumpWidget(
          MaterialApp(
            home: Builder(
              builder: (context) => Scaffold(
                body: TextButton(
                  onPressed: () async {
                    result = await Navigator.push<FieldShot>(
                      context,
                      MaterialPageRoute(builder: (_) => const FieldShutter()),
                    );
                  },
                  child: const Text('열기'),
                ),
              ),
            ),
          ),
        );
        await tester.tap(find.text('열기'));
        await tester.pumpAndSettle();
        if (allowed) {
          location.emit(33.4);
          await tester.pump();
        }
        await tester.tap(find.text('셔터 누르기'));
        if (allowed) {
          location.emit(37.5);
          await tester.pump();
        }
        camera.image.complete(XFile('photo.jpg'));
        await tester.pumpAndSettle();
        expect(result!.photo.path, 'photo.jpg');
        expect(result!.stamp.fix?.latitude, allowed ? 33.4 : null);
        expect(location.fixes.hasListener, isFalse);
      },
    );
  }
}
