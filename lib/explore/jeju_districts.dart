import 'dart:convert';
import 'dart:math' as math;

import 'package:archive/archive.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

typedef JejuAreaLookup = String Function(double latitude, double longitude);

final _cached = _load();

/// Resolves coordinates locally to a city and administrative neighborhood.
/// Points in the sea, outside Jeju, or on an ambiguous boundary return ''.
Future<JejuAreaLookup> loadJejuAreaLookup() => _cached;

Future<JejuAreaLookup> _load() async {
  final bytes = await rootBundle.load('assets/catalog/jeju_districts.json.gz');
  return decodeJejuAreas(
    bytes.buffer.asUint8List(bytes.offsetInBytes, bytes.lengthInBytes),
  );
}

@visibleForTesting
JejuAreaLookup decodeJejuAreas(Uint8List compressed) {
  final json = utf8.decode(GZipDecoder().decodeBytes(compressed));
  final polygons = <_AreaPolygon>[];
  for (final district in jsonDecode(json) as List) {
    for (final polygon in district['polygons'] as List) {
      polygons.add(_AreaPolygon(district['name'] as String, polygon as List));
    }
  }
  return (latitude, longitude) {
    if (!latitude.isFinite || !longitude.isFinite) return '';
    final x = longitude * 1000000;
    final y = latitude * 1000000;
    for (final polygon in polygons) {
      if (x < polygon.west ||
          x > polygon.east ||
          y < polygon.south ||
          y > polygon.north) {
        continue;
      }
      final outer = _contains(polygon.rings.first, x, y);
      if (outer == _Position.edge) return '';
      if (outer == _Position.outside) continue;
      var inHole = false;
      for (var i = 1; i < polygon.rings.length; i++) {
        final hole = _contains(polygon.rings[i], x, y);
        if (hole == _Position.edge) return '';
        if (hole == _Position.inside) {
          inHole = true;
          break;
        }
      }
      if (!inHole) return polygon.name;
    }
    return '';
  };
}

class _AreaPolygon {
  _AreaPolygon(this.name, List rawRings)
    : rings = [
        for (final ring in rawRings)
          Int32List.fromList((ring as List).cast<int>()),
      ] {
    final outer = rings.first;
    west = east = outer[0];
    south = north = outer[1];
    for (var i = 2; i < outer.length; i += 2) {
      west = math.min(west, outer[i]);
      east = math.max(east, outer[i]);
      south = math.min(south, outer[i + 1]);
      north = math.max(north, outer[i + 1]);
    }
  }
  final String name;
  final List<Int32List> rings;
  late int west, east, south, north;
}

enum _Position { outside, inside, edge }

// Half-open crossings count the vertex shared by two successive edges only once.
_Position _contains(Int32List ring, double x, double y) {
  var inside = false;
  var ax = ring[ring.length - 2], ay = ring.last;
  for (var i = 0; i < ring.length; i += 2) {
    final bx = ring[i], by = ring[i + 1];
    if (x >= math.min(ax, bx) &&
        x <= math.max(ax, bx) &&
        y >= math.min(ay, by) &&
        y <= math.max(ay, by) &&
        (x - ax) * (by - ay) == (y - ay) * (bx - ax)) {
      return _Position.edge;
    }
    if ((ay > y) != (by > y) && x < ax + (bx - ax) * ((y - ay) / (by - ay))) {
      inside = !inside;
    }
    ax = bx;
    ay = by;
  }
  return inside ? _Position.inside : _Position.outside;
}
