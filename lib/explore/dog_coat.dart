import 'dart:math' as math;
import 'dart:typed_data';

enum DogCoat {
  brown('갈색'),
  white('흰색'),
  black('검은색');

  const DogCoat(this.label);
  final String label;
  String get modelKey => 'dog_$name';
}

// Vote over a small patch so a single highlight or dark hair cannot decide the coat.
DogCoat? sampleDogCoat(
  ByteData rgba,
  int width,
  int height,
  double x,
  double y,
) {
  final cx = (x * (width - 1)).round();
  final cy = (y * (height - 1)).round();
  final radius = math.max(1, (math.min(width, height) * .035).round());
  final votes = List.filled(DogCoat.values.length, 0);
  var total = 0;
  for (
    var row = math.max(0, cy - radius);
    row <= math.min(height - 1, cy + radius);
    row++
  ) {
    for (
      var col = math.max(0, cx - radius);
      col <= math.min(width - 1, cx + radius);
      col++
    ) {
      final i = (row * width + col) * 4;
      if (rgba.getUint8(i + 3) < 128) continue;
      final r = rgba.getUint8(i),
          g = rgba.getUint8(i + 1),
          b = rgba.getUint8(i + 2);
      final light = .30 * r + .59 * g + .11 * b;
      DogCoat? coat;
      if (r >= g && g >= b - 3 && r - b > 24 && light > 34) {
        coat = DogCoat.brown;
      } else if (light >= 150 &&
          math.max(r, math.max(g, b)) - math.min(r, math.min(g, b)) < 55) {
        coat = DogCoat.white;
      } else if (light < 88) {
        coat = DogCoat.black;
      }
      if (coat != null) votes[coat.index]++;
      total++;
    }
  }
  for (final coat in DogCoat.values) {
    if (votes[coat.index] > total / 2) return coat;
  }
  return null;
}
