using NAudio.Wave;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Audio;

/// <summary>마이크 녹음(음성 학습용). WAV 파일로 저장하며 최소 2초 검사는 호출자가 수행.</summary>
public class MicrophoneRecorder : IMicrophoneRecorder
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private readonly System.Timers.Timer _tickTimer = new(200);
    private DateTime _startedAt;

    public event EventHandler<TimeSpan>? RecordingTick;
    public bool IsRecording => _waveIn is not null;

    public MicrophoneRecorder()
    {
        _tickTimer.Elapsed += (_, _) =>
        {
            if (IsRecording)
            {
                RecordingTick?.Invoke(this, DateTime.UtcNow - _startedAt);
            }
        };
    }

    public void Start(string outputWavPath)
    {
        if (IsRecording)
        {
            return;
        }

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(44100, 16, 1),
            DeviceNumber = 0
        };
        _waveIn.DataAvailable += (_, e) =>
        {
            try { _writer?.Write(e.Buffer, 0, e.BytesRecorded); } catch { /* ignore */ }
        };

        _writer = new WaveFileWriter(outputWavPath, _waveIn.WaveFormat);
        _startedAt = DateTime.UtcNow;
        _tickTimer.Start();
        _waveIn.StartRecording();
    }

    public TimeSpan Stop()
    {
        if (!IsRecording)
        {
            return TimeSpan.Zero;
        }

        _tickTimer.Stop();
        var duration = DateTime.UtcNow - _startedAt;

        var waveIn = _waveIn;
        if (waveIn is not null)
        {
            try { waveIn.StopRecording(); } catch { /* ignore */ }
            try { waveIn.Dispose(); } catch { /* ignore */ }
        }
        _writer = null;
        _waveIn = null;

        return duration;
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            Stop();
        }
        _tickTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
