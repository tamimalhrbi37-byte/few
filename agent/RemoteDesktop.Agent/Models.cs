namespace RemoteDesktop.Agent;

/// <summary>رسالة إشارات WebRTC (offer / answer / ice-candidate) تُمرَّر عبر سيرفر الـ Relay.</summary>
public class SignalMessage
{
    public string Type { get; set; } = ""; // "offer" | "answer" | "ice"
    public string? Sdp { get; set; }
    public string? Candidate { get; set; }
    public string? SdpMid { get; set; }
    public int? SdpMLineIndex { get; set; }
}

/// <summary>أمر تحكم قادم من الجوال عبر WebRTC Data Channel.</summary>
public class ControlCommand
{
    public string Type { get; set; } = "";
    // "mouseMove" | "mouseClick" | "mouseDown" | "mouseUp" | "mouseScroll"
    // "keyText" | "shortcut" | "power"

    public double? X { get; set; }          // موقع نسبي 0.0-1.0 لتحريك الماوس
    public double? Y { get; set; }
    public int? Dx { get; set; }            // حركة نسبية (touchpad style)
    public int? Dy { get; set; }
    public string? Button { get; set; }     // "left" | "right"
    public string? Text { get; set; }       // نص للكتابة
    public string[]? Keys { get; set; }     // أسماء مفاتيح الاختصار مثل ["CONTROL","ALT","DELETE"]
    public string? Action { get; set; }     // "shutdown" | "restart" | "cancel"
}
