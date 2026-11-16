import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';

import 'dog_coat.dart';
import 'dog_photo_dialog.dart';
import 'style.dart';

class CameraScreen extends StatefulWidget {
  const CameraScreen({super.key, this.recovered, this.canRegisterDog = false});
  final bool canRegisterDog;
  final XFile? recovered;
  @override
  State<CameraScreen> createState() => _CameraScreenState();
}

class _CameraScreenState extends State<CameraScreen> {
  final _picker = ImagePicker();
  Uint8List? _photo;
  String? _error;
  bool _busy = false;

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

  Future<void> _pick({XFile? recovered, ImageSource? source}) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final file =
          recovered ??
          await _picker.pickImage(
            source:
                source ??
                (_hasCamera ? ImageSource.camera : ImageSource.gallery),
            maxWidth: 1800,
            maxHeight: 1800,
            requestFullMetadata: false,
          );
      if (file == null) return;
      final bytes = await file.readAsBytes();
      if (mounted) setState(() => _photo = bytes);
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

  @override
  Widget build(BuildContext context) {
    final photo = _photo;
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
                const Text(
                  '여기서 사진을 미리 보고 도감에서 닮은 동물을 찾아보세요.',
                  style: TextStyle(color: muted, height: 1.5),
                ),
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
                    _hasCamera
                        ? Icons.photo_camera_outlined
                        : Icons.photo_outlined,
                  ),
                  label: Text(
                    _hasCamera ? (photo == null ? '사진 촬영' : '다시 촬영') : '사진 열기',
                  ),
                ),
                if (_hasCamera)
                  TextButton.icon(
                    onPressed: _busy
                        ? null
                        : () => _pick(source: ImageSource.gallery),
                    icon: const Icon(Icons.photo_library_outlined),
                    label: const Text('앨범에서 선택'),
                  ),
                if (photo != null && widget.canRegisterDog)
                  FilledButton.tonalIcon(
                    icon: const Icon(Icons.pets),
                    label: const Text('강아지 털색 등록'),
                    onPressed: _busy
                        ? null
                        : () async {
                            final coat = await showDialog<DogCoat>(
                              context: context,
                              builder: (_) => DogPhotoDialog(photo),
                            );
                            if (context.mounted && coat != null)
                              Navigator.pop(context, coat);
                          },
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
