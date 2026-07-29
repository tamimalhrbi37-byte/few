namespace RemoteDesktop.Agent;

/// <summary>
/// نقطة تشغيل برنامج الـ Agent - يشتغل على جهاز الكمبيوتر ويستقبل الأوامر من الجوال.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Remote Desktop Agent ===");

        // اقرأ الإعدادات من ملف config.json (يُنشأ تلقائياً أول مرة إذا ما كان موجود)
        var config = AgentConfig.LoadOrCreate();

        Console.WriteLine($"عنوان السيرفر : {config.ServerUrl}");
        Console.WriteLine($"كود الاقتران   : {config.PairCode}");
        Console.WriteLine("شغّل تطبيق الجوال وأدخل نفس كود الاقتران للاتصال.\n");

        var signaling = new SignalingClient(config.ServerUrl, config.PairCode, role: "agent");
        var peerConnectionManager = new AgentPeerConnectionManager(signaling);

        signaling.OnMessageReceived += peerConnectionManager.HandleSignalingMessageAsync;

        await signaling.ConnectAsync();

        Console.WriteLine("متصل بالسيرفر. بانتظار اتصال الجوال...");
        Console.WriteLine("اضغط Ctrl+C لإيقاف البرنامج.");

        // خليه يشتغل باستمرار (لاحقاً: حوّله لـ Windows Service أو نفّذه من Tray icon)
        await Task.Delay(Timeout.Infinite);
    }
}
