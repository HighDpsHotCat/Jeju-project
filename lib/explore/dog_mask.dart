import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'dog_coat.dart';

const dogInputSize = 224;
const _dogClass = 12;

class _Map3D {
  _Map3D(this.channels, this.height, this.width)
    : pixels = Float32List(channels * height * width);
  final int channels, height, width;
  final Float32List pixels;
  int get area => height * width;
}

_Map3D _convolve(_Map3D x, Map layer, ByteData packed, int base) {
  final int k = layer['k'], stride = layer['stride'], pad = layer['pad'];
  final int dilation = layer['dilation'], channels = layer['ch'];
  final bool depth = layer['depth'];
  final extent = dilation * (k - 1) + 1;
  final y = _Map3D(
    channels,
    (x.height + 2 * pad - extent) ~/ stride + 1,
    (x.width + 2 * pad - extent) ~/ stride + 1,
  );
  final src = x.pixels, dst = y.pixels;
  final start = base + (layer['offset'] as int);
  final taps = (depth ? 1 : x.channels) * k * k;
  for (int o = 0; o < channels; o++) {
    final scale = packed.getFloat32(start + 4 * o, Endian.little);
    final bias = packed.getFloat32(start + 4 * (channels + o), Endian.little);
    final weights = start + 8 * channels + o * taps;
    final dest = o * y.area;
    dst.fillRange(dest, dest + y.area, bias);
    if (k == 1 && stride == 1) {
      for (int c = 0; c < x.channels; c++) {
        final w = packed.getInt8(weights + c) * scale;
        if (w == 0) continue;
        final input = c * x.area;
        for (int i = 0; i < x.area; i++) {
          dst[dest + i] += src[input + i] * w;
        }
      }
    } else {
      for (int c = 0; c < (depth ? 1 : x.channels); c++) {
        final input = (depth ? o : c) * x.area;
        for (int ky = 0; ky < k; ky++) {
          for (int kx = 0; kx < k; kx++) {
            final w = packed.getInt8(weights + (c * k + ky) * k + kx) * scale;
            for (int row = 0; row < y.height; row++) {
              final sy = row * stride - pad + ky * dilation;
              if (sy < 0 || sy >= x.height) continue;
              final sx0 = kx * dilation - pad;
              final left = math.max(0, (-sx0 / stride).ceil());
              final right = math.min(
                y.width,
                ((x.width - sx0) / stride).ceil(),
              );
              for (int col = left; col < right; col++) {
                dst[dest + row * y.width + col] +=
                    src[input + sy * x.width + col * stride + sx0] * w;
              }
            }
          }
        }
      }
    }
  }
  return y;
}

_Map3D _resize(_Map3D x, _Map3D shape) {
  final out = _Map3D(x.channels, shape.height, shape.width);
  for (int row = 0; row < out.height; row++) {
    final fy = ((row + .5) * x.height / out.height - .5).clamp(
      0.0,
      x.height - 1.0,
    );
    final y0 = fy.floor(), y1 = fy.ceil(), dy = fy - y0;
    for (int col = 0; col < out.width; col++) {
      final fx = ((col + .5) * x.width / out.width - .5).clamp(
        0.0,
        x.width - 1.0,
      );
      final x0 = fx.floor(), x1 = fx.ceil(), dx = fx - x0;
      for (int c = 0; c < x.channels; c++) {
        final i = c * x.area;
        final top =
            x.pixels[i + y0 * x.width + x0] * (1 - dx) +
            x.pixels[i + y0 * x.width + x1] * dx;
        final bottom =
            x.pixels[i + y1 * x.width + x0] * (1 - dx) +
            x.pixels[i + y1 * x.width + x1] * dx;
        out.pixels[c * out.area + row * out.width + col] =
            top * (1 - dy) + bottom * dy;
      }
    }
  }
  return out;
}

_Map3D _apply(String op, _Map3D x, _Map3D? other) {
  if (op == 'mul' && x.area < other!.area) return _apply(op, other, x);
  if (op == 'resize') return _resize(x, other!);
  if (op == 'AdaptiveAvgPool2d') {
    final y = _Map3D(x.channels, 1, 1);
    for (int c = 0; c < x.channels; c++) {
      double sum = 0;
      for (int i = 0; i < x.area; i++) {
        sum += x.pixels[c * x.area + i];
      }
      y.pixels[c] = sum / x.area;
    }
    return y;
  }
  final y = _Map3D(x.channels, x.height, x.width);
  final dst = y.pixels, src = x.pixels;
  for (int i = 0; i < src.length; i++) {
    final v = src[i];
    dst[i] = switch (op) {
      'ReLU' => math.max(v, 0.0),
      'Hardsigmoid' => (v + 3).clamp(0.0, 6.0) / 6,
      'Hardswish' => v * (v + 3).clamp(0.0, 6.0) / 6,
      'Sigmoid' => 1 / (1 + math.exp(-v)),
      'add' => v + other!.pixels[i],
      'mul' => v * other!.pixels[other.area == 1 ? i ~/ x.area : i],
      _ => throw FormatException('Unsupported mask layer $op'),
    };
  }
  return y;
}

// Runs on the camera worker isolate. Only confident dog-mask pixels vote.
DogCoat? recognizeDogCoat(Uint8List rgba, ByteData model) {
  final input = _Map3D(3, dogInputSize, dogInputSize);
  if (rgba.length != input.area * 4) {
    throw ArgumentError('Unexpected image size');
  }
  const mean = [.485, .456, .406], std = [.229, .224, .225];
  for (int c = 0; c < 3; c++) {
    for (int i = 0; i < input.area; i++) {
      input.pixels[c * input.area + i] =
          (rgba[i * 4 + c] / 255 - mean[c]) / std[c];
    }
  }
  final length = model.getUint32(0, Endian.little);
  final layers = (jsonDecode(
    utf8.decode(model.buffer.asUint8List(model.offsetInBytes + 4, length)),
  ) as List).cast<Map>();
  final refs = List.filled(layers.length + 1, 0);
  for (final l in layers) {
    refs[l['a'] as int]++;
    if (l.containsKey('b')) refs[l['b'] as int]++;
  }
  final maps = <_Map3D?>[input];
  for (final l in layers) {
    final int a = l['a'];
    final int? b = l['b'];
    final x = maps[a]!;
    maps.add(
      l['op'] == 'Conv2d'
          ? _convolve(x, l, model, 4 + length)
          : _apply(l['op'], x, b == null ? null : maps[b]),
    );
    if (--refs[a] == 0) maps[a] = null;
    if (b != null && --refs[b] == 0) maps[b] = null;
  }
  final scores = maps.last!;
  final votes = List.filled(DogCoat.values.length, 0);
  int accepted = 0;
  for (int i = 0; i < scores.area; i++) {
    final dog = scores.pixels[_dogClass * scores.area + i];
    double rival = -double.infinity;
    for (int c = 0; c < scores.channels; c++) {
      if (c != _dogClass) {
        rival = math.max(rival, scores.pixels[c * scores.area + i]);
      }
    }
    if (dog - rival < 1.6 || rgba[i * 4 + 3] < 200) continue;
    accepted++;
    final coat = furColor(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]);
    if (coat != null) votes[coat.index]++;
  }
  if (accepted < scores.area * .025) return null;
  for (final coat in DogCoat.values) {
    if (votes[coat.index] > accepted * .55) return coat;
  }
  return null;
}
