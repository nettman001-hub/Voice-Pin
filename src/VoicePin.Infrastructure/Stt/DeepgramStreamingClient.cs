using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Stt;

/// <summary>
/// Deepgram 실시간 스트리밍 STT 클라이언트 (Nova-3).
/// linear16 16kHz mono PCM을 WebSocket으로 전송하고 전사 결과를 방출한다.
/// </summary>
public sealed class DeepgramStreamingClient : ISttStreamer
{
    public const string DefaultUrl = "wss://api.deepgram.com/v1/listen";

    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _language;
    private ClientWebSocket? _socket;

    public event EventHandler<SttResult>? ResultReceived;
    public event EventHandler<string>? ErrorOccurred;
    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public DeepgramStreamingClient(string apiKey, string model = "nova-3", string language = "ko")
    {
        _apiKey = apiKey;
        _model = model;
        _language = language;
    }

    public async Task ConnectAsync(IEnumerable<string> keyterms, CancellationToken cancellationToken)
    {
        var url = BuildUrl(keyterms);
        _socket = new ClientWebSocket();
        _socket.Options.SetRequestHeader("Authorization", "Token " + _apiKey);
        await _socket.ConnectAsync(new Uri(url), cancellationToken);
        _ = Task.Run(() => ReceiveLoopAsync(_socket));
    }

    internal string BuildUrl(IEnumerable<string> keyterms)
    {
        var sb = new StringBuilder(DefaultUrl);
        sb.Append("?model=").Append(Uri.EscapeDataString(_model));
        sb.Append("&language=").Append(Uri.EscapeDataString(_language));
        sb.Append("&encoding=linear16&sample_rate=16000&channels=1");
        sb.Append("&interim_results=true&endpointing=300");
        foreach (var term in keyterms.Where(t => !string.IsNullOrWhiteSpace(t)).Take(50))
        {
            sb.Append("&keyterm=").Append(Uri.EscapeDataString(term.Trim()));
        }
        return sb.ToString();
    }

    public async Task SendAudioAsync(ReadOnlyMemory<byte> pcm16kMono, CancellationToken cancellationToken)
    {
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            return;
        }
        await socket.SendAsync(pcm16kMono, WebSocketMessageType.Binary, true, cancellationToken);
    }

    public async Task FinishAsync()
    {
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes("{\"type\":\"CloseStream\"}");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token);
        }
        catch { /* ignore */ }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
                if (result.Count == 0)
                {
                    continue;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var transcript = ParseTranscript(json, out var isFinal);
                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    ResultReceived?.Invoke(this, new SttResult(transcript, isFinal));
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }

    internal static string ParseTranscript(string json, out bool isFinal)
    {
        isFinal = false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("channel", out var channel) ||
                !channel.TryGetProperty("alternatives", out var alts) ||
                alts.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var alt = alts[0];
            var text = alt.TryGetProperty("transcript", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            isFinal = root.TryGetProperty("is_final", out var f) && f.GetBoolean();
            return text;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            var socket = _socket;
            if (socket is not null && socket.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client-close", cts.Token);
            }
            socket?.Dispose();
        }
        catch { /* ignore */ }
        _socket = null;
    }
}
