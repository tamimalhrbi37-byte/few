import 'package:flutter/material.dart';
import 'package:flutter_webrtc/flutter_webrtc.dart';
import '../services/signaling_service.dart';
import '../services/webrtc_service.dart';

class ControlScreen extends StatefulWidget {
  final String serverUrl;
  final String pairCode;

  const ControlScreen({super.key, required this.serverUrl, required this.pairCode});

  @override
  State<ControlScreen> createState() => _ControlScreenState();
}

class _ControlScreenState extends State<ControlScreen> {
  late final SignalingService _signaling;
  late final WebRtcService _webrtc;
  String _status = 'جارِ الاتصال...';

  @override
  void initState() {
    super.initState();
    _signaling = SignalingService();
    _webrtc = WebRtcService(_signaling, onConnectionStateChange: _onStateChange);
    _start();
  }

  void _onStateChange(RTCPeerConnectionState state) {
    setState(() {
      switch (state) {
        case RTCPeerConnectionState.RTCPeerConnectionStateConnected:
          _status = 'متصل ✅';
          break;
        case RTCPeerConnectionState.RTCPeerConnectionStateConnecting:
          _status = 'جارِ إنشاء الاتصال...';
          break;
        case RTCPeerConnectionState.RTCPeerConnectionStateFailed:
        case RTCPeerConnectionState.RTCPeerConnectionStateDisconnected:
          _status = 'انقطع الاتصال ⚠️';
          break;
        default:
          _status = state.toString();
      }
    });
  }

  Future<void> _start() async {
    try {
      debugPrint('[App] تهيئة WebRTC...');
      await _webrtc.init();

      debugPrint('[App] الاتصال بالسيرفر: ${widget.serverUrl} كود=${widget.pairCode}');
      await _signaling.connect(serverUrl: widget.serverUrl, pairCode: widget.pairCode);

      debugPrint('[App] إنشاء عرض WebRTC (offer)...');
      await _webrtc.connect();

      debugPrint('[App] تم إرسال الـ offer، بانتظار رد الكمبيوتر...');
    } catch (e, st) {
      debugPrint('[App] خطأ فادح أثناء الاتصال: $e\n$st');
      if (mounted) setState(() => _status = 'خطأ: $e');
    }
  }

  @override
  void dispose() {
    _webrtc.dispose();
    _signaling.dispose();
    super.dispose();
  }

  void _showKeyboardDialog() {
    final controller = TextEditingController();
    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('كتابة نص'),
        content: TextField(
          controller: controller,
          autofocus: true,
          decoration: const InputDecoration(hintText: 'اكتب هنا...'),
          onSubmitted: (text) {
            _webrtc.typeText(text);
            Navigator.pop(context);
          },
        ),
        actions: [
          TextButton(
            onPressed: () {
              _webrtc.typeText(controller.text);
              Navigator.pop(context);
            },
            child: const Text('إرسال'),
          ),
        ],
      ),
    );
  }

  void _showShortcutsSheet() {
    showModalBottomSheet(
      context: context,
      builder: (_) => SafeArea(
        child: Wrap(
          children: [
            ListTile(
              leading: const Icon(Icons.tab),
              title: const Text('Alt + Tab (تبديل النوافذ)'),
              onTap: () {
                _webrtc.sendShortcut(['ALT', 'TAB']);
                Navigator.pop(context);
              },
            ),
            ListTile(
              leading: const Icon(Icons.window),
              title: const Text('مفتاح Windows'),
              onTap: () {
                _webrtc.sendShortcut(['WIN']);
                Navigator.pop(context);
              },
            ),
            ListTile(
              leading: const Icon(Icons.arrow_back),
              title: const Text('Escape'),
              onTap: () {
                _webrtc.sendShortcut(['ESCAPE']);
                Navigator.pop(context);
              },
            ),
            ListTile(
              leading: const Icon(Icons.backspace_outlined),
              title: const Text('Backspace'),
              onTap: () {
                _webrtc.sendShortcut(['BACKSPACE']);
                Navigator.pop(context);
              },
            ),
            ListTile(
              leading: const Icon(Icons.keyboard_return),
              title: const Text('Enter'),
              onTap: () {
                _webrtc.sendShortcut(['ENTER']);
                Navigator.pop(context);
              },
            ),
          ],
        ),
      ),
    );
  }

  void _showPowerDialog() {
    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('التحكم بالطاقة'),
        content: const Text('اختر الإجراء المطلوب على الكمبيوتر:'),
        actions: [
          TextButton(
            onPressed: () {
              _webrtc.restartPc();
              Navigator.pop(context);
            },
            child: const Text('إعادة تشغيل'),
          ),
          TextButton(
            onPressed: () {
              _webrtc.shutdownPc();
              Navigator.pop(context);
            },
            child: const Text('إطفاء', style: TextStyle(color: Colors.red)),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('إلغاء'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_status, style: const TextStyle(fontSize: 16)),
      ),
      body: Column(
        children: [
          Expanded(
            child: GestureDetector(
              // سحب بالإصبع = تحريك الماوس نسبياً (مثل التاتش باد)
              onPanUpdate: (details) {
                _webrtc.moveMouseRelative(
                  details.delta.dx.round(),
                  details.delta.dy.round(),
                );
              },
              // نقرة سريعة = ضغط زر الماوس الأيسر
              onTap: () => _webrtc.click(),
              // ضغط مطوّل = زر الماوس الأيمن
              onLongPress: () => _webrtc.click(button: 'right'),
              child: Container(
                color: Colors.black,
                width: double.infinity,
                child: RTCVideoView(
                  _webrtc.remoteRenderer,
                  objectFit: RTCVideoViewObjectFit.RTCVideoViewObjectFitContain,
                ),
              ),
            ),
          ),
          SafeArea(
            top: false,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceEvenly,
              children: [
                IconButton(
                  icon: const Icon(Icons.keyboard),
                  tooltip: 'لوحة المفاتيح',
                  onPressed: _showKeyboardDialog,
                ),
                IconButton(
                  icon: const Icon(Icons.shortcut),
                  tooltip: 'اختصارات',
                  onPressed: _showShortcutsSheet,
                ),
                IconButton(
                  icon: const Icon(Icons.mouse),
                  tooltip: 'نقرة يمين',
                  onPressed: () => _webrtc.click(button: 'right'),
                ),
                IconButton(
                  icon: const Icon(Icons.power_settings_new, color: Colors.red),
                  tooltip: 'الطاقة',
                  onPressed: _showPowerDialog,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
