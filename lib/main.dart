import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:image_picker/image_picker.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'explore/camera.dart';
import 'explore/catalog.dart';
import 'explore/collection.dart';
import 'explore/guide.dart';
import 'explore/home.dart';
import 'explore/navigation.dart';
import 'explore/profile.dart';
import 'explore/style.dart';
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
  });
  final SharedPreferences? preferences;
  final Future<List<Animal>> Function() loadAnimals;
  final Future<GuideRecords> Function(String?) loadRecords;
  final Future<void> Function(String)? publishYard;

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: '제주 동물 도감',
    debugShowCheckedModeBanner: false,
    theme: explorerTheme(),
    locale: const Locale('ko'),
    supportedLocales: const [Locale('ko')],
    localizationsDelegates: GlobalMaterialLocalizations.delegates,
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
  Set<String> _registered = {};
  String _profile = 'local';
  bool _recordsReady = false;
  String? _snapshotForCopy;
  int _exportGeneration = 0;
  Future<void> _writes = Future.value();
  Future<List<Animal>>? _catalog;
  bool _inGuide = false;
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
        _snapshotForCopy = null;
      });
    }
    try {
      final records = await widget.app.loadRecords(
        widget.app.preferences?.getString('device_profile.user_id'),
      );
      final ids = {
        ...records.species,
        ...?widget.app.preferences?.getStringList(
          'guide.manual.${records.profile}',
        ),
      }..remove('');
      if (ids.isNotEmpty) {
        final catalog = await _animals();
        // A field card can use a national ID while a hand-entered card uses a Jeju ID.
        for (final a in catalog) {
          if (a.jejuId.isNotEmpty &&
              a.nationalId.isNotEmpty &&
              ids.remove(a.nationalId)) {
            ids.add(a.jejuId);
          }
        }
      }
      if (!mounted || generation != _readGeneration) return;
      setState(() {
        _profile = records.profile;
        _registered = ids;
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
    final ids = _examples ? exampleSpeciesIds : _registered;
    final profile = _profile;
    final examples = _examples;
    return encodeYardSnapshot(
      profile: profile,
      examples: examples,
      discoveries: ids,
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
      setState(() => _snapshotForCopy = json);
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
    final data = _snapshotForCopy;
    if (data == null) return;
    try {
      // Safari requires the clipboard call to run within this button gesture.
      await Clipboard.setData(ClipboardData(text: data));
      if (mounted) _message('목록을 복사했어요. 마당 발바닥 메뉴에서 도감 불러오기를 누르세요.');
    } catch (_) {
      if (mounted) _message('등록 목록을 복사하지 못했어요.');
    }
  }

  Future<void> _register(Animal animal) async {
    if (_examples || !_recordsReady || _registered.contains(animal.id)) return;
    final prefs = widget.app.preferences;
    final key = 'guide.manual.$_profile';
    try {
      if (prefs == null) throw StateError('Missing local preferences');
      final ids = {...?prefs.getStringList(key), animal.id}.toList()..sort();
      if (!await prefs.setStringList(key, ids)) {
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

  void _openGuide() {
    FocusManager.instance.primaryFocus?.unfocus();
    setState(() {
      _catalog ??= widget.app.loadAnimals();
      _inGuide = true;
    });
  }

  void _openHome() {
    FocusManager.instance.primaryFocus?.unfocus();
    setState(() => _inGuide = false);
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

  Future<void> _settings() async {
    final value = await showFieldSheet<bool>(
      context,
      SettingsSheet(examples: _examples),
    );
    if (value == null || !mounted || value == _examples) return;
    _exportGeneration++;
    setState(() {
      _examples = value;
      _snapshotForCopy = null;
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
    final ids = _examples ? exampleSpeciesIds : _registered;
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
              canPop: !_inGuide,
              onPopInvokedWithResult: (didPop, result) {
                if (!didPop) _openHome();
              },
              child: Scaffold(
                body: SafeArea(
                  bottom: false,
                  child: IndexedStack(
                    index: _inGuide ? 1 : 0,
                    children: [
                      ExplorerHome(
                        name: _name,
                        total: ids.length,
                        examples: _examples,
                        openSettings: _settings,
                        openCharacter: _character,
                        openGuide: _openGuide,
                      ),
                      if (_catalog != null)
                        AnimalGuide(
                          catalog: _catalog!,
                          discovered: ids,
                          examples: _examples,
                          onBack: _openHome,
                          onRegister: _recordsReady && !_examples
                              ? _register
                              : null,
                          onSendToYard: _snapshotForCopy == null
                              ? null
                              : _copyToYard,
                          onRetry: () {
                            setState(() => _catalog = widget.app.loadAnimals());
                            _refreshRecords();
                          },
                        ),
                    ],
                  ),
                ),
                bottomNavigationBar: MediaQuery.viewInsetsOf(context).bottom > 0
                    ? null
                    : FieldNavigation(
                        guideSelected: _inGuide,
                        openGuide: _openGuide,
                        openCamera: _openCamera,
                        openHome: _openHome,
                      ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
