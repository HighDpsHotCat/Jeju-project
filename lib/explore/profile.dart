import 'package:flutter/material.dart';

class CharacterSheet extends StatefulWidget {
  const CharacterSheet({super.key, required this.name});
  final String name;
  @override
  State<CharacterSheet> createState() => _CharacterSheetState();
}

class _CharacterSheetState extends State<CharacterSheet> {
  late String _name = widget.name;
  @override
  Widget build(BuildContext context) => Column(
    mainAxisSize: MainAxisSize.min,
    children: [
      const CircleAvatar(
        radius: 48,
        backgroundImage: AssetImage('assets/art/explorer_portrait.jpg'),
      ),
      const SizedBox(height: 12),
      const Text(
        '나의 캐릭터',
        style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800),
      ),
      const SizedBox(height: 18),
      TextFormField(
        initialValue: _name,
        maxLength: 12,
        textInputAction: TextInputAction.done,
        decoration: const InputDecoration(
          labelText: '탐험가 이름',
          hintText: '제주 탐험가',
        ),
        onChanged: (name) => _name = name,
        onFieldSubmitted: (name) => Navigator.pop(context, name.trim()),
      ),
      const SizedBox(height: 6),
      SizedBox(
        width: double.infinity,
        child: FilledButton(
          onPressed: () => Navigator.pop(context, _name.trim()),
          child: const Text('저장'),
        ),
      ),
    ],
  );
}

class SettingsSheet extends StatefulWidget {
  const SettingsSheet({super.key, required this.examples});
  final bool examples;
  @override
  State<SettingsSheet> createState() => _SettingsSheetState();
}

class _SettingsSheetState extends State<SettingsSheet> {
  late bool _enabled = widget.examples;
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    mainAxisSize: MainAxisSize.min,
    children: [
      const Text(
        '설정',
        style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800),
      ),
      const SizedBox(height: 8),
      SwitchListTile.adaptive(
        contentPadding: EdgeInsets.zero,
        value: _enabled,
        title: const Text(
          '예시 기록 보기',
          style: TextStyle(fontWeight: FontWeight.w700),
        ),
        subtitle: const Text('12종이 등록된 예시 도감을 둘러봐요.'),
        onChanged: (value) => setState(() => _enabled = value),
      ),
      const SizedBox(height: 14),
      FilledButton(
        onPressed: () => Navigator.pop(context, _enabled),
        child: const Text('적용'),
      ),
    ],
  );
}
