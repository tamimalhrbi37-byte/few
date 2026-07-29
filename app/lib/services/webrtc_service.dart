import 'dart:convert';
import 'package:flutter_webrtc/flutter_webrtc.dart';
import 'signaling_service.dart';

/// يدير اتصال WebRTC من جهة الجوال: ينشئ offer، يستقبل بث الشاشة كفيديو،
/// ويرسل أوامر التحكم (ماوس/كيبورد/طاقة) عبر Data Channel للكمبيوتر.
class WebRtcService {
  final SignalingService signaling;
  RTCPeerConnection? _peerConnection;
  RTCDataChannel? _controlChannel;

  final RTCVideoRenderer remoteRenderer = RTCVideoRenderer();

  bool _connected = false;
  bool get isConnected => _connected;

  final void Function(RTCPeerConnectionState state)? onConnectionStateChange;

  WebRtcService(this.signaling, {this.onConnectionStateChange});

  Future<void> init() async {
    await remoteRenderer.initialize();

    signaling.messages.listen((msg) async {
      switch (msg['type']) {
        case 'answer':
          await _peerConnection?.setRemoteDescription(
            RTCSessionDescription(msg['sdp'] as String, 'answer'),
          );
          break;
        case 'ice':
          if (msg['candidate'] != null) {
            await _peerConnection?.addCandidate(RTCIceCandidate(
              msg['candidate'] as String,
              msg['sdpMid'] as String?,
              msg['sdpMLineIndex'] as int?,
            ));
          }
          break;
      }
    });
  }

  Future<void> connect() async {
    final config = {
      'iceServers': [
        {'urls': 'stun:stun.l.google.com:19302'}
        // TODO: أضف TURN server خاص بك هنا لضمان الاتصال خلف شبكات NAT الصارمة
      ]
    };

    _peerConnection = await createPeerConnection(config);

    _peerConnection!.onTrack = (event) {
      if (event.track.kind == 'video' && event.streams.isNotEmpty) {
        remoteRenderer.srcObject = event.streams.first;
      }
    };

    _peerConnection!.onIceCandidate = (candidate) {
      signaling.send({
        'type': 'ice',
        'candidate': candidate.candidate,
        'sdpMid': candidate.sdpMid,
        'sdpMLineIndex': candidate.sdpMLineIndex,
      });
    };

    _peerConnection!.onConnectionState = (state) {
      _connected = state == RTCPeerConnectionState.RTCPeerConnectionStateConnected;
      onConnectionStateChange?.call(state);
    };

    // قناة بيانات لإرسال أوامر التحكم (ماوس/كيبورد/طاقة) للكمبيوتر
    _controlChannel = await _peerConnection!.createDataChannel(
      'input',
      RTCDataChannelInit()..ordered = true,
    );

    // نطلب استقبال فيديو فقط (recvonly) لأن الجوال لا يرسل فيديو
    await _peerConnection!.addTransceiver(
      kind: RTCRtpMediaType.RTCRtpMediaTypeVideo,
      init: RTCRtpTransceiverInit(direction: TransceiverDirection.RecvOnly),
    );

    final offer = await _peerConnection!.createOffer();
    await _peerConnection!.setLocalDescription(offer);

    signaling.send({'type': 'offer', 'sdp': offer.sdp});
  }

  // ===================== أوامر التحكم =====================

  void _sendCommand(Map<String, dynamic> command) {
    if (_controlChannel?.state == RTCDataChannelState.RTCDataChannelOpen) {
      _controlChannel!.send(RTCDataChannelMessage(jsonEncode(command)));
    }
  }

  /// تحريك الماوس لموقع نسبي (0.0-1.0) من الشاشة - يفيد عند الضغط المباشر على مكان في الفيديو
  void moveMouseAbsolute(double relativeX, double relativeY) {
    _sendCommand({'type': 'mouseMove', 'x': relativeX, 'y': relativeY});
  }

  /// تحريك نسبي (touchpad-style) - يفيد للسحب بالإصبع مثل التاتش باد
  void moveMouseRelative(int dx, int dy) {
    _sendCommand({'type': 'mouseMove', 'dx': dx, 'dy': dy});
  }

  void click({String button = 'left'}) {
    _sendCommand({'type': 'mouseClick', 'button': button});
  }

  void mouseDown() => _sendCommand({'type': 'mouseDown'});
  void mouseUp() => _sendCommand({'type': 'mouseUp'});

  void typeText(String text) {
    _sendCommand({'type': 'keyText', 'text': text});
  }

  void sendShortcut(List<String> keys) {
    _sendCommand({'type': 'shortcut', 'keys': keys});
  }

  void shutdownPc() => _sendCommand({'type': 'power', 'action': 'shutdown'});
  void restartPc() => _sendCommand({'type': 'power', 'action': 'restart'});
  void cancelPowerAction() => _sendCommand({'type': 'power', 'action': 'cancel'});

  Future<void> dispose() async {
    await remoteRenderer.dispose();
    await _controlChannel?.close();
    await _peerConnection?.close();
  }
}
