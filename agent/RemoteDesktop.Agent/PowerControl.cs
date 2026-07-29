using System.Diagnostics;

namespace RemoteDesktop.Agent;

/// <summary>إطفاء أو إعادة تشغيل الجهاز عن بعد.</summary>
public static class PowerControl
{
    public static void Shutdown()
    {
        Process.Start(new ProcessStartInfo("shutdown", "/s /t 5 /c \"إطفاء عن بعد من تطبيق التحكم\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    public static void Restart()
    {
        Process.Start(new ProcessStartInfo("shutdown", "/r /t 5 /c \"إعادة تشغيل عن بعد من تطبيق التحكم\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    /// <summary>إلغاء أمر الإطفاء/إعادة التشغيل المجدول (خلال مهلة الـ 5 ثواني).</summary>
    public static void CancelPendingShutdown()
    {
        Process.Start(new ProcessStartInfo("shutdown", "/a")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }
}
