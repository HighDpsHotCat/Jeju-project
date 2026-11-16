import 'dart:io';

import 'package:flutter/services.dart';
import 'package:sqflite/sqflite.dart';

const _channel = MethodChannel('jeju/yard');

Future<void> writeYardSnapshot(String json) async {
  if (!Platform.isAndroid && !Platform.isIOS && !Platform.isMacOS) return;
  // sqflite's iOS database directory is the app's Documents directory.
  final path = Platform.isIOS
      ? await getDatabasesPath()
      : await _channel.invokeMethod<String>('collectionDirectory');
  if (path == null || path.isEmpty) {
    throw const FileSystemException('No yard directory');
  }
  final directory = await Directory(path).create(recursive: true);
  final temp = File('${directory.path}/.yard-collection-$pid.tmp');
  try {
    await temp.writeAsString(json, flush: true);
    await temp.rename('${directory.path}/yard-collection.json');
  } finally {
    if (await temp.exists()) await temp.delete();
  }
}
