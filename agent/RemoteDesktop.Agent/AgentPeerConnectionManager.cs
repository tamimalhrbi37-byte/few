using System.Text.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;

namespace RemoteDesktop.Agent;

/// <summary>
/// يدير اتصال WebRTC من جهة الكمبيوتر: يستقبل offer من الجوال، يبث الشاشة كفيديو،
/// ويستقبل أوامر التحكم (ماوس/كيبورد/طاقة) عبر Data Channel وينفذها فعلياً على الجهاز.
/// </summary>
public class AgentPeerConnectionManager
{
    private readonly SignalingClient _signaling;
    private RTCPeerConnection? _peerConnection;
    private readonly ScreenCapture _screenCapture = new(fps: 15);
    private readonly VpxVideoEncoder _encoder = new();
    private bool _encoderConfigured;

    public AgentPeerConnectionManager(SignalingClient signaling)
    {
        _signaling = signaling;
    }

    public async Task HandleSignalingMessageAsync(string rawMessage)
    {
        Console.WriteLine($"[Signaling] رسالة واردة: {rawMessage}");

        var msg = JsonSerializer.Deserialize<SignalMessage>(rawMessage, JsonOptions.Default);
        if (msg is null)
        {
            Console.WriteLine("[Signaling] فشل تحويل الرسالة لـ JSON صالح.");
            return;
        }

        try
        {
            await ProcessMessageAsync(msg);
        }
        catch (Exception ex)
        {
            // نطبع الخطأ كامل بدل ما نخليه يختفي بصمت - هذا أهم سطر للتشخيص
            Console.WriteLine($"[خطأ] فشل معالجة رسالة {msg.Type}: {ex}");
        }
    }

    private async Task ProcessMessageAsync(SignalMessage msg)
    {
        switch (msg.Type)
        {
            case "offer":
                Console.WriteLine("[Signaling] استلمت offer، جاري إنشاء الاتصال...");
                await HandleOfferAsync(msg.Sdp!);
                break;

            case "ice":
                if (_peerConnection != null && msg.Candidate != null)
                {
                    _peerConnection.addIceCandidate(new RTCIceCandidateInit
                    {
                        candidate = msg.Candidate,
                        sdpMid = msg.SdpMid,
                        sdpMLineIndex = (ushort)(msg.SdpMLineIndex ?? 0)
                    });
                }
                break;
        }
    }

    private async Task HandleOfferAsync(string offerSdp)
    {
        _peerConnection = CreatePeerConnection();

        var offer = new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = offerSdp };
        _peerConnection.setRemoteDescription(offer);

        var answer = _peerConnection.createAnswer();
        await _peerConnection.setLocalDescription(answer);

        Console.WriteLine("[Signaling] تم إنشاء answer، جاري إرساله...");

        await _signaling.SendAsync(JsonSerializer.Serialize(new SignalMessage
        {
            Type = "answer",
            Sdp = answer.sdp
        }, JsonOptions.Default));

        Console.WriteLine("[Signaling] تم إرسال answer.");
    }

    private RTCPeerConnection CreatePeerConnection()
    {
        var config = new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>
            {
                new() { urls = "stun:stun.l.google.com:19302" }
                // TODO: أضف TURN server خاص بك هنا لو كان أحد الطرفين خلف NAT صارم (شبكات جوال/شركات)
            }
        };

        var pc = new RTCPeerConnection(config);

        var videoFormat = new List<VideoFormat> { new(VideoCodecsEnum.VP8, 96) };
        var videoTrack = new MediaStreamTrack(videoFormat, MediaStreamStatusEnum.SendOnly);
        pc.addTrack(videoTrack);

        pc.onicecandidate += candidate =>
        {
            if (candidate == null) return;
            _ = _signaling.SendAsync(JsonSerializer.Serialize(new SignalMessage
            {
                Type = "ice",
                Candidate = candidate.candidate,
                SdpMid = candidate.sdpMid,
                SdpMLineIndex = candidate.sdpMLineIndex
            }, JsonOptions.Default));
        };

        pc.onconnectionstatechange += state =>
        {
            Console.WriteLine($"حالة اتصال WebRTC: {state}");
            if (state == RTCPeerConnectionState.connected)
            {
                _screenCapture.OnFrameCaptured += OnScreenFrame;
                _screenCapture.Start();
            }
            else if (state is RTCPeerConnectionState.closed or RTCPeerConnectionState.failed or RTCPeerConnectionState.disconnected)
            {
                _screenCapture.Stop();
                _screenCapture.OnFrameCaptured -= OnScreenFrame;
            }
        };

        pc.ondatachannel += channel =>
        {
            channel.onmessage += (_, _, data) =>
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                HandleControlCommand(json);
            };
        };

        return pc;
    }

    private void OnScreenFrame(byte[] bgraData, int width, int height)
    {
        if (_peerConnection is not { connectionState: RTCPeerConnectionState.connected }) return;

        if (!_encoderConfigured)
        {
            // يهيّئ المُرمِّز بأبعاد الشاشة الفعلية أول مرة فقط
            _encoderConfigured = true;
        }

        var encoded = _encoder.EncodeVideo(width, height, bgraData, VideoPixelFormatsEnum.Bgra, VideoCodecsEnum.VP8);
        if (encoded != null)
        {
            _peerConnection.SendVideo((uint)(1000 / 15), encoded);
        }
    }

    private void HandleControlCommand(string json)
    {
        ControlCommand? cmd;
        try
        {
            cmd = JsonSerializer.Deserialize<ControlCommand>(json, JsonOptions.Default);
        }
        catch
        {
            return;
        }

        if (cmd is null) return;

        try
        {
            switch (cmd.Type)
            {
                case "mouseMove":
                    if (cmd.X.HasValue && cmd.Y.HasValue)
                        InputSimulator.MoveTo(cmd.X.Value, cmd.Y.Value);
                    else if (cmd.Dx.HasValue && cmd.Dy.HasValue)
                        InputSimulator.MoveRelative(cmd.Dx.Value, cmd.Dy.Value);
                    break;

                case "mouseClick":
                    if (cmd.Button == "right") InputSimulator.RightClick();
                    else InputSimulator.LeftClick();
                    break;

                case "mouseDown":
                    InputSimulator.LeftDown();
                    break;

                case "mouseUp":
                    InputSimulator.LeftUp();
                    break;

                case "keyText":
                    if (cmd.Text != null) InputSimulator.TypeText(cmd.Text);
                    break;

                case "shortcut":
                    if (cmd.Keys != null)
                    {
                        var vkCodes = cmd.Keys.Select(MapKeyNameToVk).ToArray();
                        InputSimulator.PressShortcut(vkCodes);
                    }
                    break;

                case "power":
                    switch (cmd.Action)
                    {
                        case "shutdown": PowerControl.Shutdown(); break;
                        case "restart": PowerControl.Restart(); break;
                        case "cancel": PowerControl.CancelPendingShutdown(); break;
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"خطأ أثناء تنفيذ الأمر: {ex.Message}");
        }
    }

    private static ushort MapKeyNameToVk(string name) => name.ToUpperInvariant() switch
    {
        "CONTROL" or "CTRL" => InputSimulator.VK.CONTROL,
        "ALT" => InputSimulator.VK.ALT,
        "SHIFT" => InputSimulator.VK.SHIFT,
        "WIN" => InputSimulator.VK.WIN,
        "TAB" => InputSimulator.VK.TAB,
        "DELETE" or "DEL" => InputSimulator.VK.DELETE,
        "ESCAPE" or "ESC" => InputSimulator.VK.ESCAPE,
        "ENTER" => InputSimulator.VK.ENTER,
        "BACKSPACE" => InputSimulator.VK.BACKSPACE,
        _ => (ushort)0
    };
}
