part of 'guide.dart';

typedef CollectBreed = Future<String> Function(Animal animal, PetBreed breed);

class _PetDetail extends StatefulWidget {
  const _PetDetail({
    required this.animal,
    required this.breedIds,
    required this.examples,
    required this.onCollect,
    required this.info,
  });

  final Animal animal;
  final ValueListenable<Set<String>> breedIds;
  final bool examples;
  final CollectBreed? onCollect;
  final Widget Function(bool hasBreed) info;

  @override
  State<_PetDetail> createState() => _PetDetailState();
}

class _PetDetailState extends State<_PetDetail> {
  bool showBreeds = true;
  bool saving = false;

  Future<void> openBreed(PetBreed breed, bool collected) async {
    final collect = widget.onCollect;
    final confirmed = await showFieldSheet<bool>(
      context,
      Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(breed.name, style: Theme.of(context).textTheme.headlineSmall),
          Text(breed.english),
          const SizedBox(height: 12),
          _AnimalPhoto(
            breed.photo,
            width: double.infinity,
            height: 200,
            fit: BoxFit.contain,
          ),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: collected || collect == null
                ? null
                : () => Navigator.pop(context, true),
            icon: Icon(collected ? Icons.check_circle : Icons.add),
            label: Text(collected ? '수집 완료' : '직접 관찰한 품종으로 수집'),
          ),
          const SizedBox(height: 12),
          _PhotoCredit(breed.photo.credit!),
        ],
      ),
    );
    if (confirmed != true || !mounted || collect == null || saving) return;
    setState(() => saving = true);
    try {
      final result = await collect(widget.animal, breed);
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(result)));
      }
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => ValueListenableBuilder<Set<String>>(
    valueListenable: widget.breedIds,
    builder: (context, saved, _) {
      final breeds = widget.animal.breeds;
      final ids = widget.examples ? const <String>{} : saved;
      final count = breeds.where((breed) => ids.contains(breed.id)).length;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SegmentedButton<bool>(
            segments: const [
              ButtonSegment(value: false, label: Text('기본 정보')),
              ButtonSegment(value: true, label: Text('품종 수집')),
            ],
            selected: {showBreeds},
            showSelectedIcon: false,
            onSelectionChanged: (value) =>
                setState(() => showBreeds = value.single),
          ),
          const SizedBox(height: 16),
          if (!showBreeds)
            widget.info(count > 0)
          else ...[
            Text(
              '${widget.animal.name} · 품종 수집 $count/${breeds.length}',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            if (widget.examples) const Text('예시 도감에서는 수집 기록을 저장하지 않아요.'),
            const SizedBox(height: 8),
            for (final breed in breeds)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: _AnimalPhoto(breed.photo, width: 68, height: 54),
                title: Text(breed.name),
                subtitle: Text(breed.english),
                trailing: Text(
                  ids.contains(breed.id) ? '수집 완료' : '미수집',
                  style: TextStyle(
                    color: ids.contains(breed.id) ? forest : muted,
                  ),
                ),
                onTap: saving
                    ? null
                    : () => openBreed(breed, ids.contains(breed.id)),
              ),
          ],
        ],
      );
    },
  );
}
