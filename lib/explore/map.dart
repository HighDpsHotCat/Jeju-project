import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

const mapRoute = '/map';
const _host = 'jeju-wildlife-field-guide.gimhoyeon.chatgpt.site';

Future<void> openSharedGuide(BuildContext context, Map<String, String> query) =>
    _open(context, Uri.https(_host, '/', query));

Future<void> _open(BuildContext context, Uri url) async {
  try {
    if (await launchUrl(url, mode: LaunchMode.externalApplication)) return;
  } catch (_) {}
  if (context.mounted) {
    ScaffoldMessenger.of(context)
        .showSnackBar(const SnackBar(content: Text('브라우저에서 열 수 없어요.')));
  }
}

class ObservationMapScreen extends StatefulWidget {
  const ObservationMapScreen({super.key});
  @override
  State<ObservationMapScreen> createState() => _ObservationMapScreenState();
}

class _ObservationMapScreenState extends State<ObservationMapScreen> {
  void open() => _open(context, Uri.https(_host, '/map'));
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) open();
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('관찰 지도')),
    body: Center(
      child: FilledButton.icon(
        onPressed: open,
        icon: const Icon(Icons.map_outlined),
        label: const Text('제주 관찰 지도 열기'),
      ),
    ),
  );
}
