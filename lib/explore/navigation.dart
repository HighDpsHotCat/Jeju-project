import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';

import 'style.dart';

class FieldNavigation extends StatelessWidget {
  const FieldNavigation({
    super.key,
    required this.guideSelected,
    required this.openGuide,
    required this.openCamera,
    required this.openHome,
  });
  final bool guideSelected;
  final VoidCallback openGuide;
  final VoidCallback openCamera;
  final VoidCallback openHome;

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        const Positioned.fill(
          top: 7,
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: sheet,
              borderRadius: BorderRadius.vertical(top: Radius.circular(32)),
              boxShadow: [
                BoxShadow(
                  color: Color(0x0F514731),
                  blurRadius: 18,
                  offset: Offset(0, -3),
                ),
              ],
            ),
          ),
        ),
        SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(14, 0, 14, 5),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                Expanded(
                  child: _SideTab(
                    label: '도감',
                    icon: CupertinoIcons.book,
                    selected: guideSelected,
                    onTap: openGuide,
                  ),
                ),
                Semantics(
                  button: true,
                  label: '카메라',
                  onTap: openCamera,
                  child: ExcludeSemantics(
                    child: Material(
                      shape: const CircleBorder(
                        side: BorderSide(color: sheet, width: 3),
                      ),
                      borderOnForeground: true,
                      color: forest,
                      clipBehavior: Clip.antiAlias,
                      child: Ink(
                        decoration: const BoxDecoration(
                          shape: BoxShape.circle,
                          gradient: LinearGradient(
                            begin: Alignment.topLeft,
                            end: Alignment.bottomRight,
                            colors: [Color(0xFF569B48), Color(0xFF1F602C)],
                          ),
                        ),
                        child: InkWell(
                          onTap: openCamera,
                          child: ConstrainedBox(
                            constraints: const BoxConstraints(
                              minWidth: 74,
                              minHeight: 74,
                            ),
                            child: const Padding(
                              padding: EdgeInsets.symmetric(
                                horizontal: 8,
                                vertical: 7,
                              ),
                              child: Column(
                                mainAxisSize: MainAxisSize.min,
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  Icon(
                                    CupertinoIcons.camera,
                                    color: Colors.white,
                                    size: 32,
                                  ),
                                  Text(
                                    '카메라',
                                    style: TextStyle(
                                      fontSize: 14,
                                      fontWeight: FontWeight.w700,
                                      color: Colors.white,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
                Expanded(
                  child: _SideTab(
                    label: '마당',
                    selected: !guideSelected,
                    onTap: openHome,
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _SideTab extends StatelessWidget {
  const _SideTab({
    required this.label,
    this.icon,
    required this.selected,
    required this.onTap,
  });
  final String label;
  final IconData? icon;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = selected ? forest : const Color(0xFF73766E);
    return Semantics(
      button: true,
      selected: selected,
      child: InkWell(
        borderRadius: BorderRadius.circular(22),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.only(top: 9),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (icon != null)
                Icon(icon, color: color, size: 29)
              else
                CustomPaint(
                  size: const Size.square(29),
                  painter: _Sprout(color, selected),
                ),
              const SizedBox(height: 1),
              Text(
                label,
                style: TextStyle(
                  color: color,
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 4),
              Container(
                height: 3,
                width: 49,
                decoration: BoxDecoration(
                  color: selected ? forest : Colors.transparent,
                  borderRadius: BorderRadius.circular(5),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Sprout extends CustomPainter {
  const _Sprout(this.color, this.filled);
  final Color color;
  final bool filled;

  @override
  void paint(Canvas canvas, Size size) {
    canvas.scale(size.width / 30, size.height / 30);
    final pen = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.1
      ..strokeJoin = StrokeJoin.round
      ..strokeCap = StrokeCap.round;
    canvas.drawPath(
      Path()
        ..moveTo(15, 27)
        ..quadraticBezierTo(16, 21, 15, 17),
      pen,
    );
    final leaves = Path()
      ..moveTo(15, 19)
      ..quadraticBezierTo(5, 21, 2, 10)
      ..quadraticBezierTo(11, 7, 15, 17)
      ..close()
      ..moveTo(15, 18)
      ..quadraticBezierTo(17, 5, 28, 5)
      ..quadraticBezierTo(27, 17, 15, 19)
      ..close();
    pen.style = filled ? PaintingStyle.fill : PaintingStyle.stroke;
    canvas.drawPath(leaves, pen);
  }

  @override
  bool shouldRepaint(_Sprout oldDelegate) =>
      color != oldDelegate.color || filled != oldDelegate.filled;
}
