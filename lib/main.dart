import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:image_picker/image_picker.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'explore/camera.dart';
import 'explore/catalog.dart';
import 'explore/collection.dart';
import 'explore/encounter.dart';
import 'explore/guide.dart';
import 'explore/home.dart';

import 'package:url_launcher/url_launcher.dart';

import 'explore/navigation.dart';
import 'explore/credits.dart';
import 'explore/observation_map.dart';
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
    routes: {if (mapBuilder != null) '/map': mapBuilder!},
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
  late final String _name =
      widget.app.preferences?.getString('explorer.name') ?? '';
  late final bool _examples =
      widget.app.preferences?.getBool('explorer.examples') ?? false;
  Map<String, FirstEncounter> _registered = {};
  String _profile = 'local';
  bool _recordsReady = false;
  final _snapshot = ValueNotifier<String?>(null);
  int _exportGeneration = 0;
  Future<void> _writes = Future.value();
  Future<List<Animal>>? _catalog;
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
      if (entries.isNotEmpty) {
        for (final a in await _animals()) {
          if (a.nationalId.isEmpty) continue;
          final tagged = entries.remove('KTSN-${a.nationalId}');
          if (tagged != null) add(a.id, tagged);
          if (a.jejuId.isNotEmpty) {
            final alias = entries.remove(a.nationalId);
            if (alias != null) add(a.jejuId, alias);
          }
        }
      }
      if (!mounted || generation != _readGeneration) return;
      setState(() {
        _profile = records.profile;
        _registered = entries;
        _recordsReady = true;
      });
      await _publishYard();
    } catch (_) {
      if (mounted && generation == _readGeneration) {
        _message('기기에서 발견 기록을 읽지 못했어요.');
      }
    }
  }

  Set<String> get _discovered => {
    ...contestSpeciesIds,
    ...(_examples ? exampleSpeciesIds : _registered.keys),
  };

  Future<String> _yardSnapshot() async {
    final ids = _discovered;
    final profile = _profile;
    final examples = _examples;
    return encodeYardSnapshot(
      profile: profile,
      examples: examples,
      discoveries: ids,
      startInYard: contestSpeciesIds,
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

  Future<void> _register(Animal animal, FirstEncounter encounter) async {
    if (_examples || !_recordsReady || _discovered.contains(animal.id)) {
      return;
    }
    final prefs = widget.app.preferences;
    final key = 'guide.encounters.$_profile';
    try {
      if (prefs == null) throw StateError('Missing local preferences');
      final entries = readManualEncounters(prefs.getString(key));
      entries.putIfAbsent(animal.id, () => encounter);
      if (!await prefs.setString(key, writeManualEncounters(entries))) {
        throw StateError('Could not save registration');
      }
    } catch (_) {
      if (mounted) _message('이 기기에 등록 기록을 저장하지 못했어요.');
      return;
    }
    if (!mounted) return;
    await _refreshRecords();
    if (mounted && _recordsReady) _message('${animal.name} 등록 완료');
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
      await Navigator.pushNamed(context, '/map');
      return;
    }
    final url = Uri.https(
      'jeju-wildlife-field-guide.gimhoyeon.chatgpt.site',
      '/map',
    );
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
    if (mounted) _message('관찰 지도를 열 수 없어요. 브라우저 설정을 확인해주세요.');
  }

  Future<void> _openCamera([XFile? recovered]) async {
    if (_cameraOpen) return;
    _cameraOpen = true;
    FocusManager.instance.primaryFocus?.unfocus();
    try {
      final browse = await Navigator.push<bool>(
        context,
        MaterialPageRoute(builder: (_) => CameraScreen(recovered: recovered)),
      );
      if (mounted && browse == true) _openGuide();
    } finally {
      _cameraOpen = false;
    }
  }

  Future<void> _recoverCamera() async {
    try {
      final result = await ImagePicker().retrieveLostData();
      if (!mounted || result.isEmpty) return;
      if (result.file case final file?) {
        await _openCamera(file);
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

  void _openCredits() {
    Navigator.of(
      context,
    ).push(MaterialPageRoute<void>(builder: (_) => const PhotoCreditsPage()));
  }

  @override
  Widget build(BuildContext context) {
    final ids = _discovered;
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
                              catalog: _catalog!,
                              discovered: ids,
                              examples: _examples,
                              onBack: _goBack,
                              backLabel: _guideOrigin == FieldTab.yard
                                  ? '마당으로 돌아가기'
                                  : '메인으로 돌아가기',
                              onRegister: _recordsReady && !_examples
                                  ? _register
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
                            openCredits: _openCredits,
                            openCamera: _openCamera,
                            openMap: _openMap,
                          ),
                          ExplorerYard(
                            name: _name,
                            total: ids.length,
                            examples: _examples,
                            openCredits: _openCredits,
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
