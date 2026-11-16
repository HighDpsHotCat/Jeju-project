import 'dart:ui' as ui;

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import 'dog_coat.dart';

class DogPhotoDialog extends StatefulWidget {
  const DogPhotoDialog(this.photo, {super.key});
  final Uint8List photo;

  @override
  State<DogPhotoDialog> createState() => _DogPhotoDialogState();
}

class _DogPhotoDialogState extends State<DogPhotoDialog> {
  ByteData? pixels;
  int width = 0, height = 0;
  DogCoat? selected;
  String hint = '사진에서 강아지의 몸통 털을 눌러 주세요.';

  @override
  void initState() {
    super.initState();
    _readPhoto();
  }

  Future<void> _readPhoto() async {
    try {
      final codec = await ui.instantiateImageCodec(
        widget.photo,
        targetWidth: 160,
      );
      try {
        final frame = await codec.getNextFrame();
        final image = frame.image;
        try {
          final rgba = await image.toByteData(
            format: ui.ImageByteFormat.rawRgba,
          );
          if (rgba == null) throw StateError('No pixels');
          if (!mounted) return;
          setState(() {
            width = image.width;
            height = image.height;
            pixels = rgba;
            selected = sampleDogCoat(rgba, width, height, .5, .5);
            if (selected != null) {
              hint = '사진 중앙은 ${selected!.label}으로 보여요. 몸통을 눌러 다시 확인할 수 있어요.';
            }
          });
        } finally {
          image.dispose();
        }
      } finally {
        codec.dispose();
      }
    } catch (_) {
      if (mounted) setState(() => hint = '색을 읽지 못했어요. 사진을 보고 직접 골라 주세요.');
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('강아지 털색 등록'),
    content: SingleChildScrollView(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            width: 280,
            height: 200,
            child: Center(
              child: pixels == null
                  ? Image.memory(
                      widget.photo,
                      fit: BoxFit.contain,
                      errorBuilder: (_, _, _) =>
                          const Icon(Icons.broken_image_outlined),
                    )
                  : AspectRatio(
                      aspectRatio: width / height,
                      child: LayoutBuilder(
                        builder: (_, box) => GestureDetector(
                          onTapDown: (tap) {
                            final color = sampleDogCoat(
                              pixels!,
                              width,
                              height,
                              tap.localPosition.dx / box.maxWidth,
                              tap.localPosition.dy / box.maxHeight,
                            );
                            setState(() {
                              selected = color;
                              hint = color == null
                                  ? '색이 모호해요. 다른 부분을 누르거나 직접 골라 주세요.'
                                  : '${color.label}으로 보여요. 맞으면 등록해 주세요.';
                            });
                          },
                          child: Image.memory(widget.photo, fit: BoxFit.fill),
                        ),
                      ),
                    ),
            ),
          ),
          const SizedBox(height: 12),
          Text(hint),
          Wrap(
            spacing: 6,
            children: [
              for (final coat in DogCoat.values)
                ChoiceChip(
                  label: Text(coat.label),
                  selected: coat == selected,
                  onSelected: (_) => setState(() {
                    selected = coat;
                    hint = '${coat.label} 강아지를 등록할까요?';
                  }),
                ),
            ],
          ),
        ],
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('취소'),
      ),
      FilledButton(
        onPressed: selected == null
            ? null
            : () => Navigator.pop(context, selected),
        child: const Text('등록'),
      ),
    ],
  );
}
