import 'package:flutter/material.dart';

import 'navigation.dart';
import 'style.dart';

class ExplorerHome extends StatelessWidget {
  const ExplorerHome({
    super.key,
    required this.name,
    required this.openCredits,
    required this.openMap,
    required this.openCamera,
  });
  final String name;
  final VoidCallback openCredits;
  final VoidCallback openMap;
  final VoidCallback openCamera;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, size) {
      return SingleChildScrollView(
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: size.maxHeight),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _ExplorerHeader(title: '제주 탐험', openCredits: openCredits),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(24, 8, 24, 20),
                    child: Text(
                      name.isEmpty
                          ? '오늘은 어떤 동물을 만날까요?'
                          : '$name님, 어떤 동물을 만날까요?',
                      style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                child: Column(
                  children: [
                    FieldCameraButton(onPressed: openCamera),
                    const SizedBox(height: 10),
                    _PrimaryAction(
                      label: '지도',
                      icon: Icons.map_outlined,
                      onPressed: openMap,
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      );
    },
  );
}

class _PrimaryAction extends StatelessWidget {
  const _PrimaryAction({
    required this.label,
    required this.icon,
    required this.onPressed,
  });
  final String label;
  final IconData icon;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) => ConstrainedBox(
    constraints: const BoxConstraints(maxWidth: 260),
    child: SizedBox(
      width: double.infinity,
      child: FilledButton.icon(
        style: FilledButton.styleFrom(
          minimumSize: const Size(0, 60),
          backgroundColor: const Color(0xff205731),
          foregroundColor: Colors.white,
          elevation: 3,
          side: const BorderSide(color: sheet, width: 2),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(18),
          ),
        ),
        onPressed: onPressed,
        icon: Icon(icon, size: 26),
        label: Text(
          label,
          style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
        ),
      ),
    ),
  );
}

class ExplorerYard extends StatelessWidget {
  const ExplorerYard({
    super.key,
    required this.name,
    required this.total,
    required this.examples,
    required this.openCredits,
    required this.enterYard,
    required this.openGuide,
  });

  final String name;
  final int total;
  final bool examples;
  final VoidCallback openCredits;
  final VoidCallback enterYard;
  final VoidCallback openGuide;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, size) {
        final artHeight = (size.maxHeight * .43).clamp(110.0, 350.0);
        return SingleChildScrollView(
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: size.maxHeight),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _ExplorerHeader(title: '나의 마당', openCredits: openCredits),
                Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(18, 8, 18, 4),
                      child: CustomPaint(
                        painter: const _Sprigs(),
                        child: Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 11),
                          child: Text(
                            name.isEmpty
                                ? '오늘은 어떤 동물을 만날까요?'
                                : '$name님, 어떤 동물을 만날까요?',
                            textAlign: TextAlign.center,
                            style: const TextStyle(
                              fontSize: 21,
                              fontWeight: FontWeight.w600,
                              height: 1.35,
                              letterSpacing: -.7,
                            ),
                          ),
                        ),
                      ),
                    ),
                    SizedBox(
                      height: artHeight,
                      width: double.infinity,
                      child: ShaderMask(
                        blendMode: BlendMode.dstIn,
                        shaderCallback: (rect) => const LinearGradient(
                          begin: Alignment.topCenter,
                          end: Alignment.bottomCenter,
                          colors: [
                            Colors.transparent,
                            Colors.black,
                            Colors.black,
                            Colors.transparent,
                          ],
                          stops: [0, .024, .973, 1],
                        ).createShader(rect),
                        child: Image.asset(
                          'assets/art/explorer.jpg',
                          fit: BoxFit.contain,
                          semanticLabel: '한라산과 돌담 앞에서 쌍안경을 든 제주 탐험가',
                        ),
                      ),
                    ),
                  ],
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(18, 6, 18, 6),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Center(
                        child: _PrimaryAction(
                          label: '마당 입장',
                          icon: Icons.cottage_outlined,
                          onPressed: enterYard,
                        ),
                      ),
                      Align(
                        alignment: Alignment.centerLeft,
                        child: TextButton.icon(
                          onPressed: openGuide,
                          icon: const Icon(Icons.pets, size: 17),
                          label: Text(
                            "${examples ? '발견한 동물 · 예시' : '발견한 동물'} $total종",
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _ExplorerHeader extends StatelessWidget {
  const _ExplorerHeader({required this.title, required this.openCredits});
  final String title;
  final VoidCallback openCredits;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(24, 18, 16, 4),
    child: Row(
      children: [
        Expanded(
          child: Align(
            alignment: Alignment.centerLeft,
            child: CustomPaint(
              painter: const _BrushStroke(),
              child: Padding(
                padding: const EdgeInsets.only(bottom: 9),
                child: Text(
                  title,
                  style: const TextStyle(
                    fontSize: 32,
                    fontWeight: FontWeight.w800,
                    letterSpacing: -1.6,
                  ),
                ),
              ),
            ),
          ),
        ),
        IconButton.filledTonal(
          tooltip: '저작권 및 출처',
          onPressed: openCredits,
          icon: const Icon(Icons.menu_book_outlined),
          style: IconButton.styleFrom(
            backgroundColor: sheet,
            foregroundColor: ink,
          ),
        ),
      ],
    ),
  );
}

class _BrushStroke extends CustomPainter {
  const _BrushStroke();
  @override
  void paint(Canvas canvas, Size size) {
    final y = size.height - 4;
    final w = size.width * .84;
    final path = Path()
      ..moveTo(2, y)
      ..quadraticBezierTo(w * .28, y - 11, w * .54, y - 7)
      ..quadraticBezierTo(w * .51, y - 2, w, y - 7)
      ..quadraticBezierTo(w * .72, y + 1, w * .48, y - 1)
      ..quadraticBezierTo(w * .27, y - 3, 5, y + 3)
      ..quadraticBezierTo(0, y + 4, 2, y);
    canvas.drawPath(path, Paint()..color = const Color(0xFFCEDDB3));
  }

  @override
  bool shouldRepaint(_BrushStroke oldDelegate) => false;
}

class _Sprigs extends CustomPainter {
  const _Sprigs();
  @override
  void paint(Canvas canvas, Size size) {
    final pen = Paint()
      ..color = leaf
      ..strokeCap = StrokeCap.round
      ..strokeWidth = 2.6;
    final x = size.width - 7;
    canvas.drawLine(Offset(x, 0), Offset(x + 3, -6), pen);
    canvas.drawLine(Offset(x + 5, 5), Offset(x + 11, 0), pen);
    canvas.drawLine(Offset(x + 8, 10), Offset(x + 15, 8), pen);
  }

  @override
  bool shouldRepaint(_Sprigs oldDelegate) => false;
}
