import 'dart:math' as math;

enum DogCoat {
  brown('갈색'),
  white('흰색'),
  black('검은색');

  const DogCoat(this.label);
  final String label;
  String get modelKey => 'dog_$name';
}

DogCoat? furColor(int r, int g, int b) {
  final light = .30 * r + .59 * g + .11 * b;
  final chroma = math.max(r, math.max(g, b)) - math.min(r, math.min(g, b));
  if (r >= g && g >= b - 3 && r - b > 24 && light > 34) return DogCoat.brown;
  if (light >= 150 && chroma < 55) return DogCoat.white;
  if (light < 88) return DogCoat.black;
  return null;
}
