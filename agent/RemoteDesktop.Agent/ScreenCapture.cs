using System.Drawing;
using System.Drawing.Imaging;

namespace RemoteDesktop.Agent;

/// <summary>
/// يلتقط الشاشة بشكل دوري (بمعدل إطارات محدد) ويرسل كل إطار كبيانات BGRA خام
/// جاهزة لتمريرها لمُرمِّز الفيديو (VP8) قبل بثها عبر WebRTC.
/// </summary>
public class ScreenCapture : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private readonly int _width;
    private readonly int _height;

    public event Action<byte[], int, int>? OnFrameCaptured;

    public ScreenCapture(int fps = 15)
    {
        _width = SystemInformation_ScreenWidth();
        _height = SystemInformation_ScreenHeight();

        _timer = new System.Timers.Timer(1000.0 / fps);
        _timer.Elapsed += (_, _) => CaptureFrame();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void CaptureFrame()
    {
        try
        {
            using var bitmap = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, new Size(_width, _height));
                // TODO: أضف رسم مؤشر الماوس يدوياً لو احتجت لإظهاره (CopyFromScreen ما يلتقطه دائماً)
            }

            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, _width, _height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            var byteCount = bmpData.Stride * _height;
            var buffer = new byte[byteCount];
            System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, buffer, 0, byteCount);
            bitmap.UnlockBits(bmpData);

            OnFrameCaptured?.Invoke(buffer, _width, _height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"خطأ أثناء التقاط الشاشة: {ex.Message}");
        }
    }

    private static int SystemInformation_ScreenWidth() =>
        System.Windows.Forms.SystemInformation.VirtualScreen.Width;

    private static int SystemInformation_ScreenHeight() =>
        System.Windows.Forms.SystemInformation.VirtualScreen.Height;

    public void Dispose() => _timer.Dispose();
}
