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

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  SharedPreferences? preferences;
  try {
    preferences = await SharedPreferences.getInstance();
  } catch (error) {
    debugPrint('Could not load explorer preferences: $error');
  }
  runApp(MyApp(preferences: preferences));
}

class MyApp extends StatelessWidget {
  const MyApp({
    super.key,
    this.preferences,
    this.loadAnimals = loadGuideCatalog,
    this.loadDiscoveries = readDiscoveredSpecies,
  });
  final SharedPreferences? preferences;
  final Future<List<Animal>> Function() loadAnimals;
  final Future<Set<String>> Function(String?) loadDiscoveries;

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

  Future<void> _refreshRecords() async {
    final generation = ++_readGeneration;
    try {
      final ids = await widget.app.loadDiscoveries(
        widget.app.preferences?.getString('device_profile.user_id'),
      );
      if (mounted &&
          generation == _readGeneration &&
          !setEquals(ids, _registered)) {
        setState(() => _registered = ids);
      }
    } catch (_) {
      if (mounted && generation == _readGeneration) {
        _message('기기에서 발견 기록을 읽지 못했어요.');
      }
    }
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
    setState(() => _examples = value);
    await _store(widget.app.preferences?.setBool('explorer.examples', value));
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
                          onRetry: () => setState(
                            () => _catalog = widget.app.loadAnimals(),
                          ),
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
