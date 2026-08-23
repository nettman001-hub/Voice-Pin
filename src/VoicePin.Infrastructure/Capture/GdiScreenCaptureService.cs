using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Capture;

/// <summary>정규화 좌표(0~1)를 현재 가상 화면 해상도로 환산해 지정 영역을 PNG로 캡처한다.</summary>
public class GdiScreenCaptureService : IScreenCaptureService
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    public (int X, int Y, int W, int H) ToPixelRect(NormalizedRect region)
    {
        var vx = GetSystemMetrics(SmXVirtualScreen);
        var vy = GetSystemMetrics(SmYVirtualScreen);
        var vw = GetSystemMetrics(SmCxVirtualScreen);
        var vh = GetSystemMetrics(SmCyVirtualScreen);

        if (vw <= 0) vw = 1920;
        if (vh <= 0) vh = 1080;

        var x = vx + (int)Math.Round(region.X * vw);
        var y = vy + (int)Math.Round(region.Y * vh);
        var w = (int)Math.Round(Math.Clamp(region.W, 0.01, 1.0) * vw);
        var h = (int)Math.Round(Math.Clamp(region.H, 0.01, 1.0) * vh);

        // 화면 범위를 벗어나면 오류(SP-005 예외)
        if (x < vx || y < vy || x + w > vx + vw || y + h > vy + vh)
        {
            throw new InvalidOperationException("캡처 영역이 화면 범위를 벗어났습니다.");
        }

        return (x, y, Math.Max(w, 16), Math.Max(h, 16));
    }

    public string CaptureNormalizedRegion(NormalizedRect region, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var (x, y, w, h) = ToPixelRect(region);

        using var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
        }

        var path = Path.Combine(outputDirectory, $"capture_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }
}
