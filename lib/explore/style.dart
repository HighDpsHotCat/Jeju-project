import 'package:flutter/material.dart';

const paper = Color(0xFFFBF6EF);
const sheet = Color(0xFFFFFCF8);
const ink = Color(0xFF1D321D);
const forest = Color(0xFF22652F);
const leaf = Color(0xFF54883C);
const sage = Color(0xFFEBF1DB);
const muted = Color(0xFF666A60);
const line = Color(0xFFE2DACE);

ThemeData explorerTheme() {
  final colors = ColorScheme.fromSeed(seedColor: forest).copyWith(
    primary: forest,
    onPrimary: Colors.white,
    surface: sheet,
    onSurface: ink,
    outline: line,
    secondaryContainer: sage,
  );
  final theme = ThemeData(useMaterial3: true, colorScheme: colors);
  return theme.copyWith(
    scaffoldBackgroundColor: paper,
    textTheme: theme.textTheme.apply(bodyColor: ink, displayColor: ink),
    iconTheme: const IconThemeData(color: ink),
    dividerColor: line,
    inputDecorationTheme: const InputDecorationTheme(
      border: OutlineInputBorder(
        borderRadius: BorderRadius.all(Radius.circular(16)),
      ),
    ),
  );
}

Future<T?> showFieldSheet<T>(BuildContext context, Widget child) {
  return showModalBottomSheet<T>(
    context: context,
    useSafeArea: true,
    isScrollControlled: true,
    showDragHandle: true,
    backgroundColor: sheet,
    constraints: const BoxConstraints(maxWidth: 520),
    builder: (context) => SingleChildScrollView(
      padding: EdgeInsets.fromLTRB(
        24,
        0,
        24,
        24 +
            MediaQuery.viewInsetsOf(context).bottom +
            MediaQuery.paddingOf(context).bottom,
      ),
      child: child,
    ),
  );
}

class PawMark extends StatelessWidget {
  const PawMark({super.key, this.size = 48});
  final double size;

  @override
  Widget build(BuildContext context) => Container(
    height: size,
    width: size,
    decoration: const BoxDecoration(color: sage, shape: BoxShape.circle),
    child: Icon(Icons.pets_rounded, color: leaf, size: size * .57),
  );
}
