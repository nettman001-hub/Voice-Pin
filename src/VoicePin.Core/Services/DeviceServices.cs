using VoicePin.Core.Models;

namespace VoicePin.Core.Services;

public interface IAudioLoopbackSource
{
    event EventHandler<byte[]>? ChunkAvailable;
    bool IsRunning { get; }
    void Start();
    void Stop();
}

public sealed record SttResult(string Transcript, bool IsFinal);

public interface ISttStreamer : IAsyncDisposable
{
    event EventHandler<SttResult>? ResultReceived;
    event EventHandler<string>? ErrorOccurred;
    bool IsConnected { get; }
    Task ConnectAsync(IEnumerable<string> keyterms, CancellationToken cancellationToken);
    Task SendAudioAsync(ReadOnlyMemory<byte> pcm16kMono, CancellationToken cancellationToken);
    Task FinishAsync();
}

public interface IScreenCaptureService
{
    string CaptureNormalizedRegion(NormalizedRect region, string outputDirectory);
    (int X, int Y, int W, int H) ToPixelRect(NormalizedRect region);
}

public interface ISecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public interface IMicrophoneRecorder : IDisposable
{
    event EventHandler<TimeSpan>? RecordingTick;
    bool IsRecording { get; }
    void Start(string outputWavPath);
    TimeSpan Stop();
}

public interface INotificationService
{
    void Notify(string title, string message);
}
