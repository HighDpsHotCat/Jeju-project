import 'dart:convert';

import 'catalog.dart';
import 'encounter.dart';

String encodeYardSnapshot({
  required String profile,
  required bool examples,
  required Set<String> discoveries,
  required List<Animal> catalog,
  Set<String> startInYard = const {},
  Map<String, FirstEncounter> encounters = const {},
}) {
  final remaining = {...discoveries}..remove('');
  final animals = <Map<String, Object>>[];
  for (final animal in catalog) {
    final local = remaining.remove(animal.jejuId);
    final national = remaining.remove(animal.nationalId);
    if (!local && !national) continue;
    animals.add({
      'id': animal.id,
      'name': animal.yardName,
      'modelKey': animal.modelKey,
      if (startInYard.contains(animal.id)) 'startInYard': true,
      ...?encounters[animal.id]?.fields,
    });
    if (animal.id == 'JJ-000821') {
      for (final coat in const {
        'dog_black': '검은색 강아지',
        'dog_brown': '갈색 강아지',
        'dog_white': '흰색 강아지',
      }.entries) {
        animals.add({
          'id': '${animal.id}/${coat.key}',
          'name': coat.value,
          'modelKey': coat.key,
        });
      }
    }
    if (remaining.isEmpty) break;
  }
  // A newer field recorder can contain species missing from the bundled catalog.
  for (final id in remaining.toList()..sort()) {
    animals.add({
      'id': id,
      'name': id,
      'modelKey': '',
      ...?encounters[id]?.fields,
    });
  }
  return jsonEncode({
    'version': 1,
    'profile': profile,
    'examples': examples,
    'animals': animals,
  });
}
