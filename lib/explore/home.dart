import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'style.dart';

class ExplorerHome extends StatelessWidget {
  const ExplorerHome({
    super.key,
    required this.name,
    required this.total,
    required this.examples,
    required this.openSettings,
    required this.openCharacter,
    required this.openGuide,
    required this.openMap,
  });

  final String name;
  final int total;
  final bool examples;
  final VoidCallback openSettings;
  final VoidCallback openCharacter;
  final VoidCallback openGuide;
  final VoidCallback openMap;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, size) {
        final artHeight = math.min(
          size.maxWidth * 504 / 515,
          math.max(238.0, size.maxHeight - 249),
        );
        return SingleChildScrollView(
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: size.maxHeight),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(24, 18, 16, 4),
                  child: Row(
                    children: [
                      Expanded(
                        child: Align(
                          alignment: Alignment.centerLeft,
                          child: CustomPaint(
                            painter: const _BrushStroke(),
                            child: const Padding(
                              padding: EdgeInsets.only(bottom: 9),
                              child: Text(
                                '나의 탐험',
                                style: TextStyle(
                                  fontSize: 32,
                                  fontWeight: FontWeight.w800,
                                  letterSpacing: -1.6,
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                      TextButton.icon(
                        onPressed: openSettings,
                        icon: const Icon(Icons.settings_outlined, size: 25),
                        label: const Text(
                          '설정',
                          style: TextStyle(
                            fontWeight: FontWeight.w700,
                            fontSize: 17,
                          ),
                        ),
                        style: TextButton.styleFrom(foregroundColor: ink),
                      ),
                    ],
                  ),
                ),
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
                    Wrap(
                      alignment: WrapAlignment.center,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      spacing: 8,
                      children: [
                        FilledButton.tonal(
                          onPressed: openCharacter,
                          style: FilledButton.styleFrom(
                            backgroundColor: const Color(0xFFE3ECCB),
                            foregroundColor: ink,
                            minimumSize: const Size(136, 38),
                          ),
                          child: const Text(
                            '나의 캐릭터',
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                        TextButton.icon(
                          onPressed: openMap,
                          icon: const Icon(Icons.map_outlined),
                          label: const Text('관찰 지도'),
                        ),
                      ],
                    ),
                  ],
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(24, 12, 24, 17),
                  child: Material(
                    color: sheet,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(23),
                      side: const BorderSide(color: line),
                    ),
                    clipBehavior: Clip.antiAlias,
                    child: InkWell(
                      onTap: openGuide,
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 18,
                          vertical: 13,
                        ),
                        child: Row(
                          children: [
                            const PawMark(),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Text(
                                examples ? '발견한 동물 · 예시' : '발견한 동물',
                                style: const TextStyle(
                                  fontSize: 17,
                                  fontWeight: FontWeight.w700,
                                  letterSpacing: -.7,
                                ),
                              ),
                            ),
                            Container(
                              width: 1,
                              height: 28,
                              color: line,
                              margin: const EdgeInsets.symmetric(
                                horizontal: 12,
                              ),
                            ),
                            Text(
                              '$total종',
                              style: const TextStyle(
                                color: forest,
                                fontSize: 28,
                                fontWeight: FontWeight.w800,
                                letterSpacing: -.8,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
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
