using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using VoicePin.Core.Models;
using VoicePin.Core.Rules;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Stt;

/// <summary>
/// 녹음 WAV를 Deepgram 사전녹음 API로 전사하고 학습 문장과의 유사도로 발음 점수를 산출한다.
/// 키 미설정/실패 시 로컬 추정 점수로 폴백한다.
/// </summary>
public class DeepgramPronunciationScorer : IPronunciationScorer
{
    private const string ListenUrl = "https://api.deepgram.com/v1/listen";

    private readonly ISettingsStore _settingsStore;
    private readonly ISecretProtector _protector;

    public DeepgramPronunciationScorer(ISettingsStore settingsStore, ISecretProtector protector)
    {
        _settingsStore = settingsStore;
        _protector = protector;
    }

    public async Task<PronunciationScore> ScoreAsync(string wavPath, string targetText)
    {
        var fallback = HeuristicScore(wavPath);

        try
        {
            var settings = _settingsStore.Load();
            if (!settings.HasDeepgramKey)
            {
                return fallback;
            }

            var apiKey = _protector.Unprotect(settings.DeepgramApiKeyProtected);
            var url = $"{ListenUrl}?model={Uri.EscapeDataString(settings.DeepgramModel)}" +
                      $"&language={Uri.EscapeDataString(settings.DeepgramLanguage)}&punctuate=false";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);

            using var content = new ByteArrayContent(await File.ReadAllBytesAsync(wavPath));
            content.Headers.TryAddWithoutValidation("Content-Type", "audio/wav");

            using var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                return fallback;
            }

            var json = await response.Content.ReadAsStringAsync();
            var transcript = ExtractTranscript(json);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return new PronunciationScore(0, string.Empty, "Deepgram 전사 실패(무발화)");
            }

            var percent = TextMatch.ScorePercent(targetText, transcript);
            return new PronunciationScore(percent, transcript, "Deepgram 발화 비교");
        }
        catch
        {
            return fallback;
        }
    }

    internal static string ExtractTranscript(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("results", out var results) ||
                !results.TryGetProperty("channels", out var channels) ||
                channels.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var channel = channels[0];
            if (!channel.TryGetProperty("alternatives", out var alts) || alts.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            return alts[0].TryGetProperty("transcript", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static PronunciationScore HeuristicScore(string wavPath)
    {
        double seconds = 0;
        try
        {
            using var reader = new NAudio.Wave.WaveFileReader(wavPath);
            seconds = reader.TotalTime.TotalSeconds;
        }
        catch
        {
            // 헤더 파싱 실패 시 기본값 사용
        }

        double baseScore = 60;
        if (seconds >= 2 && seconds <= 8)
        {
            baseScore += 10;
        }
        else if (seconds < 1.5 || seconds > 12)
        {
            baseScore -= 15;
        }

        var percent = Math.Clamp(baseScore, 30, 85);
        return new PronunciationScore(percent, string.Empty, "로컬 추정(길이 기반)");
    }
}
