import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:image_picker/image_picker.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:url_launcher/url_launcher.dart';

import 'explore/camera.dart';
import 'explore/catalog.dart';
import 'explore/collection.dart';
import 'explore/encounter.dart';
import 'explore/guide.dart';
import 'explore/home.dart';
import 'explore/map.dart';
import 'explore/navigation.dart';
import 'explore/profile.dart';
import 'explore/style.dart';
import 'explore/yard.dart';
import 'explore/yard_file.dart';
import 'explore/yard_snapshot.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  if (kIsWeb) {
    await (FontLoader(
      'Jeju Guide Sans',
    )..addFont(rootBundle.load('assets/web_fonts/JejuGuideSans.woff2'))).load();
  }
  SharedPreferences? preferences;
  try {
    preferences = await SharedPreferences.getInstance();
  } catch (error) {
    debugPrint('Could not load explorer preferences: $error');
  }
  runApp(MyApp(preferences: preferences, publishYard: writeYardSnapshot));
}

class MyApp extends StatelessWidget {
  const MyApp({
    super.key,
    this.preferences,
    this.loadAnimals = loadGuideCatalog,
    this.loadRecords = readGuideRecords,
    this.publishYard,
    this.mapBuilder,
  });
  final SharedPreferences? preferences;
  final Future<List<Animal>> Function() loadAnimals;
  final Future<GuideRecords> Function(String?) loadRecords;
  final Future<void> Function(String)? publishYard;
  final WidgetBuilder? mapBuilder;

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: '제주 동물 도감',
    debugShowCheckedModeBanner: false,
    theme: explorerTheme(),
    locale: const Locale('ko'),
    supportedLocales: const [Locale('ko')],
    localizationsDelegates: GlobalMaterialLocalizations.delegates,
    routes: {mapRoute: ?mapBuilder},
    home: _FieldJournal(app: this),
  );
}

class _FieldJournal extends StatefulWidget {
  const _FieldJournal({required this.app});
  final MyApp app;
  @override
  State<_FieldJournal> createState() => _FieldJournalState();
}

class _FieldJournalState extends State<_FieldJournal>
    with WidgetsBindingObserver {
  late String _name = widget.app.preferences?.getString('explorer.name') ?? '';
  late bool _examples =
      widget.app.preferences?.getBool('explorer.examples') ?? false;
  Map<String, FirstEncounter> _registered = {};
  final _breedIds = ValueNotifier<Set<String>>({});
  String _profile = 'local';
  bool _recordsReady = false;
  final _snapshot = ValueNotifier<String?>(null);
  int _exportGeneration = 0;
  Future<void> _writes = Future.value();
  Future<List<Animal>>? _catalog;
  String _guideSearch = '';
  Key? _guideSearchKey;
  FieldTab _tab = FieldTab.main;
  FieldTab _guideOrigin = FieldTab.main;
  bool _cameraOpen = false;
  int _readGeneration = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _refreshRecords();
      if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
        _recoverCamera();
      }
    });
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _snapshot.dispose();
    _breedIds.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _refreshRecords();
  }

  Future<List<Animal>> _animals() => _catalog ??= widget.app.loadAnimals();

  Future<void> _refreshRecords() async {
    final generation = ++_readGeneration;
    _exportGeneration++;
    if (_recordsReady) {
      setState(() {
        _recordsReady = false;
        _snapshot.value = null;
      });
    }
    try {
      final records = await widget.app.loadRecords(
        widget.app.preferences?.getString('device_profile.user_id'),
      );
      final prefs = widget.app.preferences;
      final manual = readManualEncounters(
        prefs?.getString('guide.encounters.${records.profile}'),
      );
      final legacy =
          prefs?.getStringList('guide.manual.${records.profile}') ??
          const <String>[];
      final entries = <String, FirstEncounter>{
        for (final id in records.species)
          id: records.firstMet[id] ?? FirstEncounter.unknown,
      };
      void add(String id, FirstEncounter entry) {
        if (id.isEmpty) return;
        entries.update(
          id,
          (previous) => previous.earliest(entry),
          ifAbsent: () => entry,
        );
      }

      for (final id in legacy) {
        add(id, FirstEncounter.unknown);
      }
      manual.forEach(add);
      final breedIds = entries.keys
          .where((id) => breedSpeciesId(id) != null)
          .toSet();
      for (final id in breedIds) {
        add(breedSpeciesId(id)!, entries.remove(id)!);
      }
      if (entries.isNotEmpty) {
        for (final a in await _animals()) {
          if (a.nationalId.isEmpty || a.jejuId.isEmpty) continue;
          final alias = entries.remove(a.nationalId);
          if (alias != null) add(a.jejuId, alias);
        }
      }
      if (!mounted || generation != _readGeneration) return;
      setState(() {
        _profile = records.profile;
        _registered = entries;
        _breedIds.value = breedIds;
        _recordsReady = true;
      });
      await _publishYard();
    } catch (_) {
      if (mounted && generation == _readGeneration) {
        _message('기기에서 발견 기록을 읽지 못했어요.');
      }
    }
  }

  Future<String> _yardSnapshot() async {
    final ids = _examples ? exampleSpeciesIds : _registered.keys.toSet();
    final profile = _profile;
    final examples = _examples;
    return encodeYardSnapshot(
      profile: profile,
      examples: examples,
      discoveries: ids,
      encounters: _examples ? const {} : _registered,
      catalog: ids.isEmpty ? const [] : await _animals(),
    );
  }

  Future<void> _publishYard() async {
    final write = widget.app.publishYard;
    if (!_recordsReady) return;
    final generation = ++_exportGeneration;
    try {
      final json = await _yardSnapshot();
      if (!mounted || generation != _exportGeneration || !_recordsReady) return;
      setState(() => _snapshot.value = json);
      if (write == null) return;
      _writes = _writes
          .then((_) async {
            if (mounted && generation == _exportGeneration) await write(json);
          })
          .catchError((Object error) {
            debugPrint('Could not update local yard collection: $error');
            if (mounted && generation == _exportGeneration) {
              _message('마당 자동 연결에 실패했어요. 도감에서 마당으로 보내기를 이용하세요.');
            }
          });
      await _writes;
    } catch (error) {
      if (mounted && generation == _exportGeneration) {
        _message('마당 목록을 준비하지 못했어요.');
      }
    }
  }

  Future<void> _copyToYard() async {
    final data = _snapshot.value;
    if (data == null) return;
    try {
      // Safari requires the clipboard call to run within this button gesture.
      await Clipboard.setData(ClipboardData(text: data));
      if (mounted) _message('목록을 복사했어요. 마당 발바닥 메뉴에서 도감 불러오기를 누르세요.');
    } catch (_) {
      if (mounted) _message('등록 목록을 복사하지 못했어요.');
    }
  }

  Future<String> _register(
    Animal animal,
    FirstEncounter encounter, {
    PetBreed? breed,
  }) async {
    if (_examples) return '예시 도감에서는 관찰 기록을 저장하지 않아요.';
    if (!_recordsReady) return '도감 기록을 읽는 중이에요. 잠시 후 다시 시도해 주세요.';
    if (breed != null && _breedIds.value.contains(breed.id)) {
      return '${breed.name} · 이미 수집한 품종이에요.';
    }
    if (breed == null && _registered.containsKey(animal.id)) {
      return '${animal.name} · 이미 도감에 등록되어 있어요.';
    }
    final prefs = widget.app.preferences;
    final key = 'guide.encounters.$_profile';
    try {
      if (prefs == null) throw StateError('Missing local preferences');
      final entries = readManualEncounters(prefs.getString(key));
      if (!_registered.containsKey(animal.id)) {
        entries.putIfAbsent(animal.id, () => encounter);
      }
      if (breed != null) entries.putIfAbsent(breed.id, () => encounter);
      if (!await prefs.setString(key, writeManualEncounters(entries))) {
        throw StateError('Could not save registration');
      }
    } catch (_) {
      return '이 기기에 등록 기록을 저장하지 못했어요.';
    }
    if (mounted) await _refreshRecords();
    return breed == null ? '${animal.name} 등록 완료' : '${breed.name} 수집 완료';
  }

  Future<void> _registerManually(
    Animal animal,
    FirstEncounter encounter,
  ) async {
    final message = await _register(animal, encounter);
    if (mounted) _message(message);
  }

  Future<String> _registerPhoto(String id, FirstEncounter encounter) async {
    final animals = await _animals();
    final animal = animals.where((animal) => animal.jejuId == id).firstOrNull;
    if (!mounted || animal == null || id.isEmpty) return '도감에서 해당 종을 찾지 못했어요.';
    return _register(animal, encounter);
  }

  void _message(String message) =>
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(message)));

  void _selectTab(FieldTab tab) {
    if (_tab == tab) return;
    FocusManager.instance.primaryFocus?.unfocus();
    setState(() {
      if (tab == FieldTab.guide) {
        _catalog ??= widget.app.loadAnimals();
        _guideOrigin = _tab;
      }
      _tab = tab;
    });
  }

  void _openGuide() => _selectTab(FieldTab.guide);

  void _goBack() =>
      _selectTab(_tab == FieldTab.guide ? _guideOrigin : FieldTab.main);

  void _enterYard() {
    if (ModalRoute.of(context)?.isCurrent != true) return;
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        settings: const RouteSettings(name: '/yard'),
        builder: (_) => YardScreen(snapshot: _snapshot),
      ),
    );
  }

  Future<void> _openMap() async {
    if (widget.app.mapBuilder != null) {
      await Navigator.pushNamed(context, mapRoute);
      return;
    }
    final url = observationMapUrl;
    if (!kIsWeb &&
        (defaultTargetPlatform == TargetPlatform.android ||
            defaultTargetPlatform == TargetPlatform.iOS)) {
      await Navigator.of(context).push(
        MaterialPageRoute<void>(builder: (_) => ObservationMapPage(url: url)),
      );
      return;
    }
    try {
      if (await launchUrl(url, mode: LaunchMode.externalApplication)) return;
    } on PlatformException {
      // Some devices have no browser configured.
    }
    if (mounted) _message('관찰 지도를 열 수 없어요. 브라우저 설정을 확인해 주세요.');
  }

  Future<void> _openCamera([List<XFile> recovered = const []]) async {
    if (_cameraOpen) return;
    _cameraOpen = true;
    FocusManager.instance.primaryFocus?.unfocus();
    try {
      final search = await Navigator.push<String>(
        context,
        MaterialPageRoute(
          builder: (_) => CameraScreen(
            recovered: recovered,
            registerPhoto: _registerPhoto,
            preferences: widget.app.preferences,
          ),
        ),
      );
      if (mounted && search != null) {
        if (search.isNotEmpty) {
          setState(() {
            _guideSearch = search;
            _guideSearchKey = UniqueKey();
          });
        }
        _openGuide();
      }
    } finally {
      _cameraOpen = false;
    }
  }

  Future<void> _recoverCamera() async {
    try {
      final result = await ImagePicker().retrieveLostData();
      if (!mounted) return;
      final prefs = widget.app.preferences;
      final paths =
          prefs?.getStringList(pendingObservationPaths) ?? const <String>[];
      final files = [for (final path in paths) XFile(path), ?result.file];
      if (paths.isNotEmpty) await prefs?.remove(pendingObservationPaths);
      if (!mounted) return;
      if (files.isNotEmpty) {
        await _openCamera(files);
      } else if (result.exception != null) {
        _message('마지막 촬영 사진을 복원하지 못했어요.');
      }
    } on MissingPluginException {
      // There is no native camera host in a Flutter widget test.
      return;
    } on PlatformException {
      if (mounted) _message('마지막 촬영 사진을 복원하지 못했어요.');
    }
  }

  Future<void> _settings() async {
    final value = await showFieldSheet<bool>(
      context,
      SettingsSheet(examples: _examples, openCharacter: _character),
    );
    if (value == null || !mounted || value == _examples) return;
    _exportGeneration++;
    setState(() {
      _examples = value;
      _snapshot.value = null;
    });
    await _store(widget.app.preferences?.setBool('explorer.examples', value));
    if (mounted) await _publishYard();
  }

  Future<void> _character() async {
    final value = await showFieldSheet<String>(
      context,
      CharacterSheet(name: _name),
    );
    if (value == null || !mounted || value == _name) return;
    setState(() => _name = value);
    await _store(widget.app.preferences?.setString('explorer.name', value));
  }

  Future<void> _store(Future<bool>? write) async {
    try {
      if (await write == true) return;
    } catch (_) {
      // The current session can still use the chosen setting.
    }
    if (mounted) _message('이 기기에 설정을 저장하지 못했어요.');
  }

  @override
  Widget build(BuildContext context) {
    final guideProfile = _profile;
    final ids = _examples ? exampleSpeciesIds : _registered.keys.toSet();
    final keyboardOpen = MediaQuery.viewInsetsOf(context).bottom > 0;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark.copyWith(
        statusBarColor: Colors.transparent,
        systemNavigationBarColor: sheet,
      ),
      child: ColoredBox(
        color: const Color(0xFFEFEAE1),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 540),
            child: PopScope(
              canPop: _tab == FieldTab.main,
              onPopInvokedWithResult: (didPop, result) {
                if (!didPop) _goBack();
              },
              child: Scaffold(
                body: Stack(
                  fit: StackFit.expand,
                  children: [
                    if (_tab == FieldTab.main)
                      Image.asset(
                        'assets/art/main_trail.jpg',
                        fit: BoxFit.cover,
                        excludeFromSemantics: true,
                      ),
                    SafeArea(
                      bottom: false,
                      child: IndexedStack(
                        sizing: StackFit.expand,
                        index: _tab.index,
                        children: [
                          if (_catalog == null)
                            const SizedBox.shrink()
                          else
                            AnimalGuide(
                              key: _guideSearchKey,
                              initialSearch: _guideSearch,
                              catalog: _catalog!,
                              discovered: ids,
                              breedIds: _breedIds,
                              onCollectBreed: _examples || !_recordsReady
                                  ? null
                                  : (animal, breed) async {
                                      if (guideProfile != _profile) {
                                        return '프로필이 바뀌었어요. 도감을 다시 열어 주세요.';
                                      }
                                      return _register(
                                        animal,
                                        FirstEncounter(
                                          calendarDay(DateTime.now()),
                                          '',
                                        ),
                                        breed: breed,
                                      );
                                    },
                              examples: _examples,
                              onBack: _goBack,
                              backLabel: _guideOrigin == FieldTab.yard
                                  ? '마당으로 돌아가기'
                                  : '메인으로 돌아가기',
                              onRegister: _recordsReady && !_examples
                                  ? _registerManually
                                  : null,
                              onSendToYard: _snapshot.value == null
                                  ? null
                                  : _copyToYard,
                              onRetry: () {
                                setState(
                                  () => _catalog = widget.app.loadAnimals(),
                                );
                                _refreshRecords();
                              },
                            ),
                          ExplorerHome(
                            name: _name,
                            total: ids.length,
                            examples: _examples,
                            openSettings: _settings,
                            openGuide: _openGuide,
                            openCamera: _openCamera,
                            openMap: _openMap,
                          ),
                          ExplorerYard(
                            name: _name,
                            total: ids.length,
                            examples: _examples,
                            openSettings: _settings,
                            openGuide: _openGuide,
                            enterYard: _enterYard,
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                bottomNavigationBar: keyboardOpen
                    ? null
                    : FieldNavigation(selected: _tab, onSelect: _selectTab),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
