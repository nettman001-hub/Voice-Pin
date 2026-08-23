using NAudio.CoreAudioApi;
using NAudio.Wave;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Audio;

/// <summary>
/// 시스템 출력 오디오(WASAPI 루프백)를 캡처하여 16kHz/16bit/mono PCM 청크로 방출한다.
/// </summary>
public class WasapiLoopbackSource : IAudioLoopbackSource
{
    private const int TargetRate = 16000;

    private WasapiLoopbackCapture? _capture;
    private BufferedWaveProvider? _buffer;
    private MediaFoundationResampler? _resampler;
    private Thread? _pumpThread;
    private volatile bool _running;
    private readonly byte[] _readBuffer = new byte[3200]; // 100ms @ 16kHz mono 16-bit

    public event EventHandler<byte[]>? ChunkAvailable;
    public bool IsRunning => _running;

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _capture = new WasapiLoopbackCapture();
        _buffer = new BufferedWaveProvider(_capture.WaveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(10),
            DiscardOnBufferOverflow = true
        };
        _capture.DataAvailable += (_, e) =>
        {
            try { _buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded); } catch { /* ignore */ }
        };
        _capture.RecordingStopped += (_, _) => { };

        _resampler = new MediaFoundationResampler(_buffer, new WaveFormat(TargetRate, 16, 1))
        {
            ResamplerQuality = 60
        };

        _capture.StartRecording();

        _running = true;
        _pumpThread = new Thread(PumpLoop) { IsBackground = true, Name = "VoicePin.LoopbackPump" };
        _pumpThread.Start();
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }
        _running = false;
        _pumpThread?.Join(1500);
        _pumpThread = null;

        try { _capture?.StopRecording(); } catch { /* ignore */ }
        try { _resampler?.Dispose(); } catch { /* ignore */ }
        try { _capture?.Dispose(); } catch { /* ignore */ }
        _resampler = null;
        _buffer = null;
        _capture = null;
    }

    private void PumpLoop()
    {
        while (_running)
        {
            var resampler = _resampler;
            if (resampler is null)
            {
                break;
            }
            var read = 0;
            try
            {
                read = resampler.Read(_readBuffer, 0, _readBuffer.Length);
            }
            catch
            {
                Thread.Sleep(20);
                continue;
            }
            if (read <= 0)
            {
                Thread.Sleep(15);
                continue;
            }

            var chunk = new byte[read];
            Array.Copy(_readBuffer, chunk, read);
            ChunkAvailable?.Invoke(this, chunk);
        }
    }
}
