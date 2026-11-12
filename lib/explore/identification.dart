import 'dart:convert';
import 'dart:math' as math;

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_onnxruntime/flutter_onnxruntime.dart';
import 'package:image/image.dart' as img;

class SpeciesCandidate {
  const SpeciesCandidate(
    this.id,
    this.name,
    this.scientific,
    this.group,
    this.score,
  );
  final String id, name, scientific, group;
  final double score;

  String get groupName =>
      const {
        'Mammalia': '포유류',
        'Aves': '조류',
        'Insecta': '곤충',
        'Reptilia': '파충류',
        'Amphibia': '양서류',
      }[group] ??
      group;

  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'scientific': scientific,
    'group': group,
    'score': score,
  };
  factory SpeciesCandidate.fromJson(Map<String, dynamic> data) =>
      SpeciesCandidate(
        data['id'],
        data['name'],
        data['scientific'],
        data['group'],
        (data['score'] as num).toDouble(),
      );
}

class PhotoIdentification {
  const PhotoIdentification(
    this.version,
    this.decision,
    this.candidates, {
    this.confirmedId,
    this.dismissed = false,
  });
  final String version, decision;
  final List<SpeciesCandidate> candidates;
  final String? confirmedId;
  final bool dismissed;
  List<SpeciesCandidate> get visibleCandidates =>
      dismissed ? const [] : candidates;

  PhotoIdentification choose(String? id) {
    if (id != null && !candidates.any((item) => item.id == id)) {
      throw ArgumentError.value(id);
    }
    return PhotoIdentification(
      version,
      decision,
      candidates,
      confirmedId: id,
      dismissed: id == null,
    );
  }

  Map<String, dynamic> toJson() => {
    'version': version,
    'decision': decision,
    'candidates': candidates.map((c) => c.toJson()).toList(),
    'confirmed_id': confirmedId,
    'dismissed': dismissed,
  };
  factory PhotoIdentification.fromJson(Map<String, dynamic> data) =>
      PhotoIdentification(
        data['version'],
        data['decision'],
        (data['candidates'] as List)
            .map((c) => SpeciesCandidate.fromJson(c))
            .toList(),
        confirmedId: data['confirmed_id'],
        dismissed: data['dismissed'] == true,
      );
}

List<double> _numbers(List<dynamic> values) =>
    values.map((v) => (v as num).toDouble()).toList(growable: false);

double _dot(List<double> a, List<double> b) {
  var sum = 0.0;
  for (var i = 0; i < a.length; i++) {
    sum += a[i] * b[i];
  }
  return sum;
}

class SpeciesHead {
  SpeciesHead(Map<String, dynamic> data)
    : version = data['version'],
      margin = data['margin'],
      species = List<Map<String, dynamic>>.from(data['species']),
      weights = (data['weights'] as List).map((r) => _numbers(r)).toList(),
      bias = _numbers(data['bias']);
  final String version;
  final double margin;
  final List<Map<String, dynamic>> species;
  final List<List<double>> weights;
  final List<double> bias;

  PhotoIdentification classify(List<double> logits) {
    final mean = logits.reduce((a, b) => a + b) / logits.length;
    final centered = logits.map((v) => v - mean).toList();
    final norm = math.sqrt(_dot(centered, centered));
    if (!norm.isFinite || norm < 1e-8) {
      return PhotoIdentification(version, 'unknown', []);
    }
    final vector = centered.map((v) => v / norm).toList();
    final scores = [
      for (var i = 0; i < weights.length; i++)
        _dot(weights[i], vector) + bias[i],
    ];
    final maxScore = scores.reduce(math.max);
    final exp = scores.map((v) => math.exp(v - maxScore)).toList();
    final sum = exp.reduce((a, b) => a + b);
    final probs = exp.map((v) => v / sum).toList();
    final other = probs.skip(species.length).fold(0.0, (a, b) => a + b);
    final order = List.generate(species.length, (i) => i)
      ..sort((a, b) => probs[b].compareTo(probs[a]));
    final best = order.first;
    final label = species[best];
    final similarity = _dot(vector, _numbers(label['centroid']));
    final delta = probs[best] - math.max(other, probs[order[1]]);
    final suggested =
        probs[best] >= label['minScore'] &&
        delta >= margin &&
        similarity >= label['minCos'];
    if (!suggested &&
        (probs[best] < .28 ||
            other >= probs[best] ||
            similarity < label['minCos'] - .06)) {
      return PhotoIdentification(version, 'unknown', []);
    }
    final candidates = <SpeciesCandidate>[];
    for (final i in order.take(suggested ? 1 : 3)) {
      if (probs[i] < .15) continue;
      final s = species[i];
      candidates.add(
        SpeciesCandidate(
          s['id'],
          s['name'],
          s['scientific'],
          s['group'],
          probs[i],
        ),
      );
    }
    return PhotoIdentification(
      version,
      suggested ? 'suggested' : 'review',
      candidates,
    );
  }
}

Float32List prepareSpeciesPixels(Uint8List bytes) {
  final decoded = img.decodeImage(bytes);
  if (decoded == null) throw const FormatException('지원하지 않는 사진 형식입니다.');
  final square = img.copyResize(
    decoded,
    width: 224,
    height: 224,
    interpolation: img.Interpolation.average,
  );
  final rgb = square
      .convert(format: img.Format.uint8, numChannels: 3)
      .getBytes(order: img.ChannelOrder.rgb);
  const plane = 224 * 224;
  const mean = [.485, .456, .406], std = [.229, .224, .225];
  final tensor = Float32List(3 * plane);
  for (var c = 0; c < 3; c++) {
    for (var i = 0; i < plane; i++) {
      tensor[c * plane + i] = (rgb[3 * i + c] / 255 - mean[c]) / std[c];
    }
  }
  return tensor;
}

class SpeciesIdentifier {
  SpeciesIdentifier._();
  static final instance = SpeciesIdentifier._();
  static const model = 'assets/identification/dinov2-small-3afdc8bc.onnx';
  Future<(OrtSession, SpeciesHead)>? _loading;

  Future<(OrtSession, SpeciesHead)> _load() async {
    try {
      final head = SpeciesHead(
        jsonDecode(
          await rootBundle.loadString('assets/identification/classifier.json'),
        ),
      );
      final runtime = OnnxRuntime();
      final session = kIsWeb
          ? await runtime.createSession(
              Uri.base.resolve('assets/$model').toString(),
            )
          : await runtime.createSessionFromAsset(
              model,
              options: OrtSessionOptions(intraOpNumThreads: 2),
            );
      return (session, head);
    } catch (_) {
      _loading = null;
      rethrow;
    }
  }

  Future<PhotoIdentification> identify(Uint8List photo) async {
    final (session, head) = await (_loading ??= _load());
    final pixels = await compute(prepareSpeciesPixels, photo);
    final input = await OrtValue.fromList(pixels, [1, 3, 224, 224]);
    Map<String, OrtValue> outputs = {};
    try {
      outputs = await session.run({'pixel_values': input});
      final tokens = await outputs['last_hidden_state']!.asFlattenedList();
      // DINOv2-small places its 384-dimensional image (CLS) token first.
      return head.classify(_numbers(tokens.sublist(0, 384)));
    } finally {
      await input.dispose();
      for (final value in outputs.values) {
        await value.dispose();
      }
    }
  }
}
