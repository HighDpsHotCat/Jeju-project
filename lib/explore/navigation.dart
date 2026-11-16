import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';

import 'style.dart';

enum FieldTab { guide, main, yard }

class FieldNavigation extends StatelessWidget {
  const FieldNavigation({
    super.key,
    required this.selected,
    required this.onSelect,
  });
  final FieldTab selected;
  final ValueChanged<FieldTab> onSelect;

  @override
  Widget build(BuildContext context) => Material(
    color: sheet,
    elevation: 3,
    shadowColor: const Color(0x24514731),
    child: SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 0, 12, 5),
        child: Row(
          children: [
            for (final tab in FieldTab.values)
              Expanded(
                child: _SideTab(
                  label: switch (tab) {
                    FieldTab.guide => '도감',
                    FieldTab.main => '메인',
                    FieldTab.yard => '마당',
                  },
                  icon: switch (tab) {
                    FieldTab.guide => CupertinoIcons.book,
                    FieldTab.main =>
                      selected == tab
                          ? CupertinoIcons.house_fill
                          : CupertinoIcons.house,
                    FieldTab.yard => null,
                  },
                  selected: selected == tab,
                  onTap: () => onSelect(tab),
                ),
              ),
          ],
        ),
      ),
    ),
  );
}

class FieldCameraButton extends StatelessWidget {
  const FieldCameraButton({super.key, required this.onPressed});
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) => Material(
    color: const Color(0xff86350e),
    elevation: 6,
    shape: const CircleBorder(side: BorderSide(color: sheet, width: 3)),
    clipBehavior: Clip.antiAlias,
    child: InkWell(
      onTap: onPressed,
      child: ConstrainedBox(
        constraints: const BoxConstraints(minWidth: 74, minHeight: 74),
        child: const Padding(
          padding: EdgeInsets.all(9),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(CupertinoIcons.camera, color: Colors.white, size: 30),
              Text(
                '카메라',
                style: TextStyle(
                  fontSize: 14,
                  color: Colors.white,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ),
      ),
    ),
  );
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
