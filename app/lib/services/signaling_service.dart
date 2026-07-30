import 'dart:async';
import 'dart:convert';
import 'package:web_socket_channel/web_socket_channel.dart';

/// خدمة الاتصال بسيرفر الـ Relay عبر WebSocket، مسؤولة عن إرسال واستقبال
/// رسائل إشارات WebRTC (offer/answer/ice) بين الجوال والكمبيوتر.
class SignalingService {
  WebSocketChannel? _channel;
  final _messageController = StreamController<Map<String, dynamic>>.broadcast();

  Stream<Map<String, dynamic>> get messages => _messageController.stream;

  Future<void> connect({
    required String serverUrl, // مثال: ws://192.168.1.10:5080/ws
    required String pairCode,
  }) async {
    final uri = Uri.parse(serverUrl).replace(queryParameters: {
      'role': 'app',
      'pairCode': pairCode,
    });

    _channel = WebSocketChannel.connect(uri);

    // ننتظر تأكيد نجاح الاتصال فعلياً (handshake) بدل ما نفترض نجاحه فوراً -
    // لو فشل (IP غلط، فايروول، السيرفر مو شغّال...) بيرمي استثناء هنا مباشرة
    await _channel!.ready;
    print('[Signaling] تم الاتصال بالسيرفر بنجاح ($serverUrl)');

    _channel!.stream.listen(
      (raw) {
        print('[Signaling] رسالة واردة: $raw');
        final data = jsonDecode(raw as String) as Map<String, dynamic>;
        _messageController.add(data);
      },
      onError: (error) => print('[Signaling] خطأ في اتصال الإشارات: $error'),
      onDone: () => print('[Signaling] انقطع الاتصال بسيرفر الإشارات'),
    );
  }

  void send(Map<String, dynamic> message) {
    print('[Signaling] إرسال: ${jsonEncode(message)}');
    _channel?.sink.add(jsonEncode(message));
  }

  void dispose() {
    _channel?.sink.close();
    _messageController.close();
  }
}
