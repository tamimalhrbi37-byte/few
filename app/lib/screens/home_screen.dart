import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'control_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  final _serverController = TextEditingController();
  final _pairCodeController = TextEditingController();
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _loadSavedValues();
  }

  Future<void> _loadSavedValues() async {
    final prefs = await SharedPreferences.getInstance();
    _serverController.text = prefs.getString('serverUrl') ?? 'ws://192.168.1.10:5080/ws';
    _pairCodeController.text = prefs.getString('pairCode') ?? '';
    setState(() => _loading = false);
  }

  Future<void> _connect() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('serverUrl', _serverController.text.trim());
    await prefs.setString('pairCode', _pairCodeController.text.trim());

    if (!mounted) return;
    Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => ControlScreen(
        serverUrl: _serverController.text.trim(),
        pairCode: _pairCodeController.text.trim(),
      ),
    ));
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    return Scaffold(
      appBar: AppBar(title: const Text('التحكم عن بعد بالكمبيوتر')),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const SizedBox(height: 24),
            const Icon(Icons.desktop_windows, size: 96, color: Colors.indigo),
            const SizedBox(height: 32),
            TextField(
              controller: _serverController,
              decoration: const InputDecoration(
                labelText: 'عنوان السيرفر',
                hintText: 'ws://192.168.1.10:5080/ws',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: _pairCodeController,
              decoration: const InputDecoration(
                labelText: 'كود الاقتران',
                hintText: 'مثال: A1B2C3D4',
                border: OutlineInputBorder(),
              ),
              textAlign: TextAlign.center,
              style: const TextStyle(letterSpacing: 4, fontSize: 18),
            ),
            const SizedBox(height: 32),
            FilledButton.icon(
              onPressed: _connect,
              icon: const Icon(Icons.link),
              label: const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text('اتصال', style: TextStyle(fontSize: 16)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
