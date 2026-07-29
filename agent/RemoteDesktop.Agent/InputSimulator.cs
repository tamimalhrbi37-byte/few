using System.Runtime.InteropServices;

namespace RemoteDesktop.Agent;

/// <summary>
/// محاكاة الماوس والكيبورد على مستوى نظام التشغيل باستخدام SendInput.
/// يُستخدم لتنفيذ أوامر الجوال (تحريك الماوس، النقر، الكتابة، الاختصارات).
/// </summary>
public static class InputSimulator
{
    #region Win32 interop

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    private const uint KEYEVENTF_KEYUP = 0x0002;

    #endregion

    /// <summary>تحريك الماوس لموقع نسبي (0.0 - 1.0) من الشاشة - يفيد لأن دقة شاشة الجوال تختلف عن الكمبيوتر.</summary>
    public static void MoveTo(double relativeX, double relativeY)
    {
        var screenW = GetSystemMetrics(SM_CXSCREEN);
        var screenH = GetSystemMetrics(SM_CYSCREEN);

        var x = (int)(relativeX * screenW);
        var y = (int)(relativeY * screenH);

        SetCursorPos(x, y);
    }

    /// <summary>تحريك الماوس بشكل نسبي (delta) - يفيد لحركة تشبه اللمس/التتبع (touchpad-style).</summary>
    public static void MoveRelative(int dx, int dy)
    {
        GetCursorPos(out var current);
        SetCursorPos(current.X + dx, current.Y + dy);
    }

    public static void LeftClick()
    {
        SendMouseInput(MOUSEEVENTF_LEFTDOWN);
        SendMouseInput(MOUSEEVENTF_LEFTUP);
    }

    public static void RightClick()
    {
        SendMouseInput(MOUSEEVENTF_RIGHTDOWN);
        SendMouseInput(MOUSEEVENTF_RIGHTUP);
    }

    public static void LeftDown() => SendMouseInput(MOUSEEVENTF_LEFTDOWN);
    public static void LeftUp() => SendMouseInput(MOUSEEVENTF_LEFTUP);

    private static void SendMouseInput(uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags } }
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    /// <summary>كتابة نص كامل حرف حرف (يفيد لحقول الإدخال).</summary>
    public static void TypeText(string text)
    {
        foreach (var ch in text)
        {
            short vk = VkKeyScanUnicode(ch);
            SendKey((ushort)(vk & 0xff), keyUp: false);
            SendKey((ushort)(vk & 0xff), keyUp: true);
        }
    }

    /// <summary>ضغط اختصار (مثل Ctrl+Alt+Delete أو Alt+Tab) - يمرر أكواد المفاتيح الافتراضية (Virtual Key codes).</summary>
    public static void PressShortcut(ushort[] virtualKeyCodes)
    {
        foreach (var vk in virtualKeyCodes) SendKey(vk, keyUp: false);
        foreach (var vk in virtualKeyCodes.Reverse()) SendKey(vk, keyUp: true);
    }

    private static void SendKey(ushort vk, bool keyUp)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0u
                }
            }
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    private static short VkKeyScanUnicode(char ch) => VkKeyScan(ch);

    // أكواد شائعة للاختصارات (Virtual-Key Codes) - راجع قائمة مايكروسوفت الكاملة عند الحاجة لمزيد
    public static class VK
    {
        public const ushort CONTROL = 0x11;
        public const ushort ALT = 0x12;
        public const ushort SHIFT = 0x10;
        public const ushort WIN = 0x5B;
        public const ushort TAB = 0x09;
        public const ushort DELETE = 0x2E;
        public const ushort ESCAPE = 0x1B;
        public const ushort ENTER = 0x0D;
        public const ushort BACKSPACE = 0x08;
    }
}
