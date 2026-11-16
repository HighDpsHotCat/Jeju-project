import 'package:flutter/material.dart';

import 'catalog.dart';
import 'encounter.dart';
import 'style.dart';

class AnimalGuide extends StatefulWidget {
  const AnimalGuide({
    super.key,
    required this.catalog,
    required this.discovered,
    required this.examples,
    required this.onBack,
    required this.backLabel,
    required this.onRetry,
    required this.onRegister,
    required this.onSendToYard,
  });
  final Future<List<Animal>> catalog;
  final Set<String> discovered;
  final bool examples;
  final VoidCallback onBack;
  final String backLabel;
  final VoidCallback onRetry;
  final void Function(Animal, FirstEncounter)? onRegister;
  final VoidCallback? onSendToYard;

  @override
  State<AnimalGuide> createState() => _AnimalGuideState();
}

class _AnimalGuideState extends State<AnimalGuide> {
  final _search = TextEditingController();
  String _query = '';
  GuideRegion _region = GuideRegion.combined;
  AnimalGroup _group = AnimalGroup.all;

  List<Animal>? _nationalSource;
  List<Animal> _national = const [];

  List<Animal> _nationalAnimals(List<Animal> animals) {
    if (!identical(animals, _nationalSource)) {
      _national = animals.where((a) => a.nationalId.isNotEmpty).toList()
        ..sort(compareNationalAnimals);
      _nationalSource = animals;
    }
    return _national;
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  bool _isDiscovered(Animal animal) =>
      widget.discovered.contains(animal.jejuId) ||
      widget.discovered.contains(animal.nationalId);

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, size) {
        final photoWidth = size.maxWidth < 360 ? 62.0 : 84.0;
        final badgeWidth = size.maxWidth < 360 ? 80.0 : 96.0;
        return FutureBuilder<List<Animal>>(
          future: widget.catalog,
          builder: (context, snapshot) {
            final animals = snapshot.data ?? const <Animal>[];
            final regional = _region == GuideRegion.national
                ? _nationalAnimals(animals)
                : animals;
            final filtered =
                _region == GuideRegion.combined &&
                    _group == AnimalGroup.all &&
                    _query.isEmpty
                ? animals
                : regional
                      .where((a) => a.matches(_region, _group, _query))
                      .toList(growable: false);
            return CustomScrollView(
              keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
              slivers: [
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(14, 14, 14, 0),
                  sliver: SliverList.list(
                    children: [
                      Row(
                        children: [
                          IconButton(
                            onPressed: widget.onBack,
                            tooltip: widget.backLabel,
                            icon: const Icon(
                              Icons.arrow_back_rounded,
                              size: 26,
                            ),
                          ),
                          const Expanded(
                            child: Text(
                              '동물 도감',
                              style: TextStyle(
                                fontWeight: FontWeight.w800,
                                fontSize: 27,
                                letterSpacing: -1.2,
                              ),
                            ),
                          ),
                          PopupMenuButton<GuideRegion>(
                            tooltip: '도감 지역 선택',
                            initialValue: _region,
                            onSelected: (value) =>
                                setState(() => _region = value),
                            itemBuilder: (context) => GuideRegion.values
                                .map(
                                  (region) => CheckedPopupMenuItem(
                                    value: region,
                                    checked: region == _region,
                                    child: Text(region.label),
                                  ),
                                )
                                .toList(),
                            child: Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 12,
                                vertical: 7,
                              ),
                              decoration: BoxDecoration(
                                color: const Color(0xFFEEF2E0),
                                borderRadius: BorderRadius.circular(25),
                                border: Border.all(
                                  color: const Color(0xFFD5DDBE),
                                ),
                              ),
                              child: Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Text(
                                    _region.label,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                  const SizedBox(width: 4),
                                  const Icon(Icons.expand_more, size: 18),
                                ],
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _search,
                        textInputAction: TextInputAction.search,
                        onChanged: (value) =>
                            setState(() => _query = value.trim().toLowerCase()),
                        decoration: InputDecoration(
                          hintText: '동물 이름 검색',
                          filled: true,
                          fillColor: const Color(0xFFECEEE4),
                          prefixIcon: const Icon(
                            Icons.search_rounded,
                            color: ink,
                          ),
                          suffixIcon: _search.text.isEmpty
                              ? null
                              : IconButton(
                                  tooltip: '검색어 지우기',
                                  icon: const Icon(Icons.close, size: 19),
                                  onPressed: () => setState(() {
                                    _search.clear();
                                    _query = '';
                                  }),
                                ),
                          isDense: true,
                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 12,
                            vertical: 11,
                          ),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(32),
                            borderSide: BorderSide.none,
                          ),
                        ),
                      ),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          for (final group in AnimalGroup.values) ...[
                            if (group != AnimalGroup.all)
                              const SizedBox(width: 6),
                            Expanded(
                              child: Semantics(
                                selected: _group == group,
                                child: OutlinedButton(
                                  style: OutlinedButton.styleFrom(
                                    foregroundColor: _group == group
                                        ? Colors.white
                                        : ink,
                                    backgroundColor: _group == group
                                        ? forest
                                        : sheet,
                                    side: BorderSide(
                                      color: _group == group ? forest : line,
                                    ),
                                    minimumSize: const Size(0, 37),
                                    padding: const EdgeInsets.symmetric(
                                      horizontal: 1,
                                    ),
                                    textStyle: Theme.of(context)
                                        .textTheme
                                        .labelLarge
                                        ?.copyWith(
                                          fontWeight: FontWeight.w700,
                                          fontSize: 15,
                                        ),
                                  ),
                                  onPressed: () =>
                                      setState(() => _group = group),
                                  child: Text(group.label),
                                ),
                              ),
                            ),
                          ],
                        ],
                      ),
                      const SizedBox(height: 14),
                      _DiscoveryBanner(
                        total: animals.where(_isDiscovered).length,
                        examples: widget.examples,
                      ),
                      Align(
                        alignment: Alignment.centerRight,
                        child: TextButton.icon(
                          icon: const Icon(Icons.ios_share_rounded, size: 17),
                          onPressed: widget.onSendToYard,
                          label: const Text('마당으로 보내기'),
                        ),
                      ),
                      Container(
                        decoration: BoxDecoration(
                          color: const Color(0xFFF1EBE0),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        padding: const EdgeInsets.symmetric(
                          horizontal: 4,
                          vertical: 7,
                        ),
                        child: DefaultTextStyle.merge(
                          style: const TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w700,
                            color: ink,
                          ),
                          child: Row(
                            children: [
                              SizedBox(
                                width: photoWidth,
                                child: const Center(child: Text('사진')),
                              ),
                              const SizedBox(width: 12),
                              const Expanded(child: Text('이름 · 설명')),
                              SizedBox(
                                width: badgeWidth,
                                child: const Center(child: Text('등록 여부')),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                if (snapshot.connectionState != ConnectionState.done)
                  const SliverToBoxAdapter(
                    child: Padding(
                      padding: EdgeInsets.all(34),
                      child: Center(
                        child: CircularProgressIndicator(
                          semanticsLabel: '도감 불러오는 중',
                        ),
                      ),
                    ),
                  )
                else if (snapshot.hasError)
                  SliverToBoxAdapter(
                    child: Column(
                      children: [
                        const SizedBox(height: 28),
                        const Text('종 목록을 불러오지 못했어요.'),
                        TextButton(
                          onPressed: widget.onRetry,
                          child: const Text('다시 불러오기'),
                        ),
                      ],
                    ),
                  )
                else if (filtered.isEmpty)
                  SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(18, 34, 18, 24),
                      child: Column(
                        children: [
                          const Icon(
                            Icons.search_off_rounded,
                            size: 30,
                            color: muted,
                          ),
                          const SizedBox(height: 8),
                          const Text('조건에 맞는 동물이 없어요.'),
                          TextButton(
                            onPressed: () => setState(() {
                              _search.clear();
                              _query = '';
                              _region = GuideRegion.combined;
                              _group = AnimalGroup.all;
                            }),
                            child: const Text('전체 도감 보기'),
                          ),
                        ],
                      ),
                    ),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(14, 1, 14, 16),
                    sliver: SliverList.builder(
                      itemCount: filtered.length,
                      itemBuilder: (context, index) {
                        final animal = filtered[index];
                        final found = _isDiscovered(animal);
                        return _AnimalRow(
                          animal: animal,
                          found: found,
                          examples: widget.examples,
                          photoWidth: photoWidth,
                          badgeWidth: badgeWidth,
                          onRegister: widget.onRegister,
                        );
                      },
                    ),
                  ),
              ],
            );
          },
        );
      },
    );
  }
}

class _DiscoveryBanner extends StatelessWidget {
  const _DiscoveryBanner({required this.total, required this.examples});
  final int total;
  final bool examples;
  @override
  Widget build(BuildContext context) {
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: const Color(0xFFEEF3DC),
        borderRadius: BorderRadius.circular(15),
      ),
      child: Stack(
        alignment: Alignment.centerLeft,
        children: [
          Positioned(
            right: 0,
            bottom: 0,
            child: ExcludeSemantics(
              child: Image.asset(
                'assets/art/hallasan.jpg',
                width: 154,
                fit: BoxFit.fitWidth,
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 17, vertical: 13),
            child: Wrap(
              spacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                const Icon(Icons.pets_rounded, color: leaf, size: 25),
                Text(
                  examples ? '발견한 동물 · 예시' : '발견한 동물',
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                Text(
                  '$total종',
                  style: const TextStyle(
                    fontSize: 21,
                    fontWeight: FontWeight.w800,
                    color: forest,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _AnimalPhoto extends StatelessWidget {
  const _AnimalPhoto(this.animal, {required this.width, required this.height});
  final Animal animal;
  final double width;
  final double height;
  @override
  Widget build(BuildContext context) {
    final photo = animal.photo;
    return ClipRRect(
      borderRadius: BorderRadius.circular(13),
      child: SizedBox(
        width: width,
        height: height,
        child: photo == null
            ? const ColoredBox(
                color: Color(0xFFEDF0E4),
                child: Icon(Icons.pets_outlined, color: Color(0xFF95A18B)),
              )
            : Image.asset(
                photo,
                fit: animal.guidePhoto?['unmodified'] == true
                    ? BoxFit.contain
                    : BoxFit.cover,
                excludeFromSemantics: true,
              ),
      ),
    );
  }
}

class _Registration extends StatelessWidget {
  const _Registration(this.found);
  final bool found;
  @override
  Widget build(BuildContext context) => DecoratedBox(
    decoration: BoxDecoration(
      color: found ? sage : const Color(0xFFEEEDE9),
      borderRadius: BorderRadius.circular(12),
    ),
    child: Padding(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 5),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            found ? Icons.check_circle_rounded : Icons.cancel_rounded,
            color: found ? leaf : const Color(0xFF8B8C88),
            size: 15,
          ),
          const SizedBox(width: 3),
          Flexible(
            child: Text(
              found ? '등록 완료' : '미등록',
              textAlign: TextAlign.center,
              style: TextStyle(
                color: found ? forest : const Color(0xFF595B56),
                fontSize: 13,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    ),
  );
}

class _AnimalRow extends StatelessWidget {
  const _AnimalRow({
    required this.animal,
    required this.found,
    required this.examples,
    required this.photoWidth,
    required this.badgeWidth,
    required this.onRegister,
  });
  final Animal animal;
  final bool found;
  final bool examples;
  final double photoWidth;
  final double badgeWidth;
  final void Function(Animal, FirstEncounter)? onRegister;

  @override
  Widget build(BuildContext context) => Column(
    children: [
      Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: () async {
            final encounter = await showFieldSheet<FirstEncounter>(
              context,
              _AnimalDetail(
                animal: animal,
                found: found,
                examples: examples,
                canRegister: !found && onRegister != null,
              ),
            );
            if (encounter != null) onRegister?.call(animal, encounter);
          },
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 7),
            child: Row(
              children: [
                _AnimalPhoto(
                  animal,
                  width: photoWidth,
                  height: photoWidth * .80,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        animal.name,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                          height: 1.25,
                          letterSpacing: -.6,
                        ),
                      ),
                      const SizedBox(height: 3),
                      Text(
                        animal.description,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 13,
                          color: Color(0xFF51564C),
                          height: 1.28,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 3),
                SizedBox(
                  width: badgeWidth,
                  child: Align(
                    alignment: Alignment.centerRight,
                    child: _Registration(found),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
      const Divider(height: 1, thickness: .8),
    ],
  );
}

class _AnimalDetail extends StatelessWidget {
  const _AnimalDetail({
    required this.animal,
    required this.found,
    required this.examples,
    required this.canRegister,
  });
  final Animal animal;
  final bool found;
  final bool examples;
  final bool canRegister;
  @override
  Widget build(BuildContext context) => Column(
    mainAxisSize: MainAxisSize.min,
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Row(
        children: [
          _AnimalPhoto(animal, width: 88, height: 80),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  animal.name,
                  style: const TextStyle(
                    fontSize: 23,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                if (animal.korean.isNotEmpty)
                  Text(
                    animal.scientific,
                    style: const TextStyle(
                      fontStyle: FontStyle.italic,
                      color: muted,
                    ),
                  ),
                const SizedBox(height: 5),
                Text(animal.groupName),
              ],
            ),
          ),
        ],
      ),
      if (animal.guidePhoto case final credit?)
        Padding(
          padding: const EdgeInsets.only(top: 12),
          child: SelectableText(
            [
              if (credit['note'] != null) credit['note'],
              if (credit['title'] != null) credit['title'],
              "사진 · ${credit['displayCredit'] ?? credit['author']} · ${credit['license']}",
              "원본  ${credit['source']}",
              "이용 조건  ${credit['licenseUrl']}",
              credit['unmodified'] == true
                  ? "원본 그대로 제공"
                  : "도감용 크기 조정 · 원본과 같은 이용 조건",
            ].join('\n'),
          ),
        )
      else if (animal.photo != null)
        Padding(
          padding: const EdgeInsets.only(top: 16),
          child: Text(animal.description),
        ),
      const Divider(height: 28),
      if (animal.jejuId.isNotEmpty) Text('제주 관찰 기록  ${animal.jejuId}'),
      if (animal.nationalId.isNotEmpty) Text('국가생물종목록  ${animal.nationalId}'),
      const SizedBox(height: 14),
      Row(
        children: [
          Text(examples ? '예시 도감   ' : '나의 도감   '),
          _Registration(found),
        ],
      ),
      if (canRegister) ...[const SizedBox(height: 12), const _EncounterForm()],
    ],
  );
}

class _EncounterForm extends StatefulWidget {
  const _EncounterForm();

  @override
  State<_EncounterForm> createState() => _EncounterFormState();
}

class _EncounterFormState extends State<_EncounterForm> {
  DateTime? day = DateTime.now();
  String? city, district;

  Future<void> _pickDate() async {
    final selected = await showDatePicker(
      context: context,
      initialDate: day ?? DateTime.now(),
      firstDate: DateTime(1),
      lastDate: DateTime.now(),
      helpText: '처음 만난 날짜',
    );
    if (selected != null && mounted) setState(() => day = selected);
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Row(
        children: [
          const Text('처음 만난 날짜'),
          Expanded(
            child: TextButton(
              onPressed: _pickDate,
              child: Text(day == null ? '날짜 선택' : calendarDay(day!)),
            ),
          ),
          if (day != null)
            IconButton(
              tooltip: '날짜 모름',
              onPressed: () => setState(() => day = null),
              icon: const Icon(Icons.clear, size: 18),
            ),
        ],
      ),
      DropdownButtonFormField<String>(
        decoration: const InputDecoration(labelText: '처음 만난 장소 (선택)'),
        hint: const Text('시 선택'),
        items: [
          const DropdownMenuItem(value: '', child: Text('장소 모름')),
          for (final area in jejuDistricts.keys)
            DropdownMenuItem(value: area, child: Text(area)),
        ],
        onChanged: (value) => setState(() {
          city = value;
          district = null;
        }),
      ),
      if (jejuDistricts.containsKey(city)) ...[
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          key: ValueKey(city),
          decoration: const InputDecoration(labelText: '읍·면·동 (선택)'),
          hint: const Text('읍·면·동 선택'),
          items: [
            const DropdownMenuItem(value: '', child: Text('시까지만 기록')),
            for (final name in jejuDistricts[city]!)
              DropdownMenuItem(value: name, child: Text(name)),
          ],
          onChanged: (value) => setState(() => district = value),
        ),
      ],
      const SizedBox(height: 12),
      FilledButton.icon(
        onPressed: () => Navigator.pop(
          context,
          FirstEncounter(
            day == null ? '' : calendarDay(day!),
            [city, district].whereType<String>().join(' ').trim(),
          ),
        ),
        icon: const Icon(Icons.add_rounded),
        label: const Text('직접 관찰한 동물로 등록'),
      ),
    ],
  );
}
