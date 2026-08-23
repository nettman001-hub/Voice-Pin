using System.IO;

namespace VoicePin.App.Services;

public static class AppLogger
{
    private static readonly object Gate = new();
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VoicePin", "logs");

    public static void Write(string source, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}\n\n";
            lock (Gate)
            {
                File.AppendAllText(Path.Combine(LogDir, "app.log"), line);
            }
        }
        catch
        {
            // 로깅 실패는 무시
        }
    }

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n";
            lock (Gate)
            {
                File.AppendAllText(Path.Combine(LogDir, "app.log"), line);
            }
        }
        catch
        {
            // 로깅 실패는 무시
        }
    }
}
