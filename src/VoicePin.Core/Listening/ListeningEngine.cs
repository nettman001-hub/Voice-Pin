using System.Diagnostics;
using VoicePin.Core.Models;
using VoicePin.Core.Rules;
using VoicePin.Core.Services;

namespace VoicePin.Core.Listening;

public enum ListeningState
{
    Idle,
    Connecting,
    Listening,
    EditMode
}

public sealed class ListeningEngine : IAsyncDisposable
{
    public const int EditTimeoutSeconds = 10;
    public const int CaptureDedupeSeconds = 5;

    private readonly IAudioLoopbackSource _audio;
    private readonly Func<ISttStreamer> _sttFactory;
    private readonly IRecognitionRuleRepository _rules;
    private readonly ISalesRepository _sales;
    private readonly IBroadcastSessionRepository _sessions;
    private readonly ICaptureRepository _captures;
    private readonly IScreenCaptureService _screen;
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _clock;

    private ISttStreamer? _stt;
    private BroadcastSession? _session;
    private TranscriptAnalyzer? _analyzer;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _editTimeoutCts;
    private DateTime _lastCaptureAt = DateTime.MinValue;
    private bool _disposed;

    public ListeningState State { get; private set; } = ListeningState.Idle;

    public event EventHandler<ListeningState>? StateChanged;
    public event EventHandler<SttResult>? TranscriptReceived;
    public event EventHandler<SalesRecord>? SaleSaved;
    public event EventHandler<CaptureImage>? CaptureSaved;
    public event EventHandler<string>? Notice;

    public ListeningEngine(
        IAudioLoopbackSource audio,
        Func<ISttStreamer> sttFactory,
        IRecognitionRuleRepository rules,
        ISalesRepository sales,
        IBroadcastSessionRepository sessions,
        ICaptureRepository captures,
        IScreenCaptureService screen,
        ISettingsStore settings,
        TimeProvider? clock = null)
    {
        _audio = audio;
        _sttFactory = sttFactory;
        _rules = rules;
        _sales = sales;
        _sessions = sessions;
        _captures = captures;
        _screen = screen;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task StartAsync(string outputDirectory)
    {
        if (State is not ListeningState.Idle)
        {
            return;
        }

        var ruleList = await _rules.GetAllAsync();
        var keyterms = ruleList.Where(r => r.Enabled).Select(r => r.Keyword).ToList();
        _analyzer = new TranscriptAnalyzer(ruleList);

        SetState(ListeningState.Connecting);
        _cts = new CancellationTokenSource();

        _session = new BroadcastSession
        {
            SessionNo = BroadcastSession.BuildSessionNo(_clock.GetLocalNow().DateTime),
            StartedAt = _clock.GetLocalNow().DateTime
        };
        _session = await _sessions.StartAsync(_session);

        _stt = _sttFactory();
        _stt.ResultReceived += OnTranscript;
        _stt.ErrorOccurred += OnSttError;

        _audio.ChunkAvailable += OnAudioChunk;

        try
        {
            await _stt.ConnectAsync(keyterms, _cts.Token);
        }
        catch (Exception ex)
        {
            Notice?.Invoke(this, "STT 연결 실패: " + ex.Message);
            await StopInternalAsync();
            return;
        }

        _audio.Start();
        SetState(ListeningState.Listening);
    }

    public async Task StopAsync()
    {
        if (State == ListeningState.Idle)
        {
            return;
        }
        await StopInternalAsync();
    }

    private async Task StopInternalAsync()
    {
        _audio.ChunkAvailable -= OnAudioChunk;
        _audio.Stop();

        if (_stt is not null)
        {
            try { await _stt.FinishAsync(); } catch { /* ignore */ }
            _stt.ResultReceived -= OnTranscript;
            _stt.ErrorOccurred -= OnSttError;
            await _stt.DisposeAsync();
            _stt = null;
        }

        if (_session is not null && State != ListeningState.Idle)
        {
            _session.EndedAt = _clock.GetLocalNow().DateTime;
            await _sessions.EndAsync(_session.Id, _session.EndedAt.Value);
        }

        ExitEditMode();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        SetState(ListeningState.Idle);
    }

    private void OnAudioChunk(object? sender, byte[] chunk)
    {
        if (_stt is { IsConnected: true })
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _stt.SendAudioAsync(chunk, _cts?.Token ?? CancellationToken.None);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    OnSttError(this, ex.Message);
                }
            });
        }
    }

    private void OnSttError(object? sender, string message)
    {
        Debug.WriteLine("STT error: " + message);
        Notice?.Invoke(this, "네트워크 오류: " + message);
        _ = StopInternalAsync();
    }

    private void OnTranscript(object? sender, SttResult result)
    {
        TranscriptReceived?.Invoke(this, result);
        if (!result.IsFinal || string.IsNullOrWhiteSpace(result.Transcript))
        {
            return;
        }

        _ = ProcessTranscriptAsync(result.Transcript);
    }

    public async Task ProcessTranscriptAsync(string transcript)
    {
        if (State == ListeningState.EditMode)
        {
            await HandleEditCommandAsync(transcript);
            return;
        }

        if (_analyzer is null || _session is null)
        {
            return;
        }

        var command = VoiceCommandParser.Parse(transcript);
        if (command.Kind == VoiceCommandKind.StartEdit)
        {
            EnterEditMode(transcript);
            return;
        }

        var outcome = _analyzer.Analyze(transcript);

        if (outcome.ShouldSaveSale)
        {
            await SaveSaleAsync(outcome, transcript);
        }

        if (outcome.ShouldCapture)
        {
            await CaptureScreenAsync(transcript);
        }
    }

    private async Task SaveSaleAsync(DetectionOutcome outcome, string transcript)
    {
        if (_session is null)
        {
            return;
        }

        var now = _clock.GetLocalNow().DateTime;
        var nickname = outcome.Candidate.Nickname ?? string.Empty;
        var amount = outcome.Candidate.Amount ?? 0;

        var complete = !string.IsNullOrEmpty(nickname) && amount > 0;
        if (complete && await _sales.ExistsDuplicateAsync(nickname, amount, now))
        {
            Notice?.Invoke(this, $"중복 의심 건이 감지되어 저장하지 않았습니다: {nickname} / {amount:N0}원");
            return;
        }

        var record = new SalesRecord
        {
            SessionId = _session.Id,
            Nickname = nickname,
            Amount = amount,
            RecognizedAt = now,
            Transcript = transcript,
            Status = complete ? SalesStatus.AutoSaved : SalesStatus.Pending
        };
        record.Id = await _sales.AddAsync(record);
        SaleSaved?.Invoke(this, record);

        if (!complete)
        {
            Notice?.Invoke(this, "닉네임 또는 금액을 추출하지 못해 '보류'로 저장했습니다.");
        }
    }

    private async Task CaptureScreenAsync(string transcript)
    {
        var now = _clock.GetLocalNow().DateTime;
        if ((now - _lastCaptureAt).TotalSeconds < CaptureDedupeSeconds)
        {
            return;
        }

        try
        {
            var settings = _settings.Load();
            var capturesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoicePin", "captures");
            Directory.CreateDirectory(capturesDir);

            var filePath = _screen.CaptureNormalizedRegion(settings.CaptureRegion, capturesDir);

            var latestSale = (await _sales.GetAllAsync()).FirstOrDefault();
            var capture = new CaptureImage
            {
                SaleId = latestSale?.Id,
                FilePath = filePath,
                AreaName = settings.CaptureAreaName,
                CapturedAt = now
            };
            capture.Id = await _captures.AddAsync(capture);
            _lastCaptureAt = now;
            CaptureSaved?.Invoke(this, capture);
        }
        catch (InvalidOperationException ex)
        {
            Notice?.Invoke(this, ex.Message);
        }
        catch (Exception ex)
        {
            Notice?.Invoke(this, "화면 캡처 실패: " + ex.Message);
        }
    }

    private void EnterEditMode(string triggerTranscript)
    {
        SetState(ListeningState.EditMode);
        Notice?.Invoke(this, "수정 대기: 필드와 값을 말씀하세요. (예: '닉네임은 홍길동')");

        _editTimeoutCts?.Cancel();
        _editTimeoutCts = new CancellationTokenSource();
        var token = _editTimeoutCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(EditTimeoutSeconds), token);
                if (!token.IsCancellationRequested)
                {
                    ExitEditMode();
                    Notice?.Invoke(this, $"{EditTimeoutSeconds}초간 발화가 없어 일반 청취 상태로 복귀했습니다.");
                }
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private void ExitEditMode()
    {
        if (State != ListeningState.EditMode)
        {
            return;
        }
        _editTimeoutCts?.Cancel();
        _editTimeoutCts = null;
        if (_session is not null)
        {
            SetState(ListeningState.Listening);
        }
    }

    private async Task HandleEditCommandAsync(string transcript)
    {
        var command = VoiceCommandParser.Parse(transcript);

        switch (command.Kind)
        {
            case VoiceCommandKind.FinishEdit:
                var latest = (await _sales.GetAllAsync()).FirstOrDefault();
                if (latest is not null)
                {
                    latest.Status = SalesStatus.ManualEdited;
                    await _sales.UpdateAsync(latest);
                }
                ExitEditMode();
                Notice?.Invoke(this, "수정을 확정했습니다. (상태: 수동수정)");
                break;

            case VoiceCommandKind.SetNickname:
            case VoiceCommandKind.SetAmount:
                var target = (await _sales.GetAllAsync()).FirstOrDefault();
                if (target is null)
                {
                    Notice?.Invoke(this, "수정할 내역이 없습니다.");
                    break;
                }
                if (command.Kind == VoiceCommandKind.SetNickname && command.Value is not null)
                {
                    target.Nickname = command.Value;
                }
                if (command.Kind == VoiceCommandKind.SetAmount && long.TryParse(command.Value, out var amt))
                {
                    target.Amount = amt;
                }
                await _sales.UpdateAsync(target);
                Notice?.Invoke(this, $"최근 내역이 갱신되었습니다: {target.Nickname} / {target.Amount:N0}원");
                break;

            default:
                Notice?.Invoke(this, "다시 말씀해 주세요.");
                break;
        }
    }

    private void SetState(ListeningState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        await StopAsync();
    }
}
