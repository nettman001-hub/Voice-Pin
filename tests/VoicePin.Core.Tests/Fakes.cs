using VoicePin.Core.Listening;
using VoicePin.Core.Models;
using VoicePin.Core.Services;
using Xunit;

namespace VoicePin.Core.Tests;

#pragma warning disable CS0067
public class FakeAudioSource : IAudioLoopbackSource
{
    public event EventHandler<byte[]>? ChunkAvailable;
    public bool IsRunning { get; private set; }
    public void Start() => IsRunning = true;
    public void Stop() => IsRunning = false;
}

public class FakeStt : ISttStreamer
{
    public event EventHandler<SttResult>? ResultReceived;
    public event EventHandler<string>? ErrorOccurred;
    public bool IsConnected { get; private set; }
    public List<string> Keyterms { get; } = new();

    public Task ConnectAsync(IEnumerable<string> keyterms, CancellationToken cancellationToken)
    {
        Keyterms.AddRange(keyterms);
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task SendAudioAsync(ReadOnlyMemory<byte> pcm16kMono, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task FinishAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class InMemoryRuleRepository : IRecognitionRuleRepository
{
    public List<RecognitionRule> Rules { get; } = new()
    {
        new() { Id = 1, Keyword = "구매확정", Actions = RuleAction.SaveSale, Priority = 10, Enabled = true },
        new() { Id = 2, Keyword = "캡처", Actions = RuleAction.Capture, Priority = 8, Enabled = true }
    };

    public Task<List<RecognitionRule>> GetAllAsync() => Task.FromResult(Rules.ToList());
    public Task<long> AddAsync(RecognitionRule rule)
    {
        rule.Id = Rules.Max(r => r.Id) + 1;
        Rules.Add(rule);
        return Task.FromResult(rule.Id);
    }
    public Task UpdateAsync(RecognitionRule rule)
    {
        var index = Rules.FindIndex(r => r.Id == rule.Id);
        if (index >= 0)
        {
            Rules[index] = rule;
        }
        return Task.CompletedTask;
    }
    public Task DeleteAsync(long id) => Task.FromResult(Rules.RemoveAll(r => r.Id == id));
}

public class InMemorySalesRepository : ISalesRepository
{
    private long _nextId = 1;
    public List<SalesRecord> Records { get; } = new();

    public Task<long> AddAsync(SalesRecord record)
    {
        record.Id = _nextId++;
        Records.Add(record);
        return Task.FromResult(record.Id);
    }

    public Task<SalesRecord?> GetAsync(long id) =>
        Task.FromResult<SalesRecord?>(Records.FirstOrDefault(r => r.Id == id));

    public Task<List<SalesRecord>> GetAllAsync() => Task.FromResult(Records.OrderByDescending(r => r.RecognizedAt).ToList());

    public Task<List<SalesRecord>> GetBySessionAsync(long sessionId) =>
        Task.FromResult(Records.Where(r => r.SessionId == sessionId).OrderBy(r => r.RecognizedAt).ToList());

    public Task<List<SalesRecord>> SearchAsync(SalesSearchFilter filter) => GetAllAsync();

    public Task<bool> ExistsDuplicateAsync(string nickname, long amount, DateTime recognizedAt, int windowSeconds = 60) =>
        Task.FromResult(Records.Any(r => r.Nickname == nickname && r.Amount == amount));

    public Task UpdateAsync(SalesRecord record)
    {
        var index = Records.FindIndex(r => r.Id == record.Id);
        if (index >= 0)
        {
            Records[index] = record;
        }
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(long id)
    {
        var record = await GetAsync(id);
        if (record is not null)
        {
            Records.Remove(record);
        }
    }

    public SettlementSummary Summarize(DateTime from, DateTime to) => SettlementCalculatorCompute(from, to);

    private SettlementSummary SettlementCalculatorCompute(DateTime from, DateTime to)
        => Core.Export.SettlementCalculator.Compute(Records, from, to);
}

public class InMemorySessionRepository : IBroadcastSessionRepository
{
    private long _nextId = 1;
    public List<BroadcastSession> Sessions { get; } = new();

    public Task<BroadcastSession> StartAsync(BroadcastSession session)
    {
        session.Id = _nextId++;
        Sessions.Add(session);
        return Task.FromResult(session);
    }

    public Task EndAsync(long sessionId, DateTime endedAt)
    {
        var session = Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session is not null)
        {
            session.EndedAt = endedAt;
        }
        return Task.CompletedTask;
    }

    public Task<BroadcastSession?> GetLatestAsync() =>
        Task.FromResult(Sessions.OrderByDescending(s => s.StartedAt).FirstOrDefault());

    public Task<BroadcastSession?> GetAsync(long id) =>
        Task.FromResult<BroadcastSession?>(Sessions.FirstOrDefault(s => s.Id == id));
}

public class InMemoryCaptureRepository : ICaptureRepository
{
    private long _nextId = 1;
    public List<CaptureImage> Captures { get; } = new();

    public Task<long> AddAsync(CaptureImage capture)
    {
        capture.Id = _nextId++;
        Captures.Add(capture);
        return Task.FromResult(capture.Id);
    }

    public Task<List<CaptureImage>> GetBySaleIdAsync(long saleId) =>
        Task.FromResult(Captures.Where(c => c.SaleId == saleId).ToList());

    public Task<CaptureImage?> GetLatestAsync() =>
        Task.FromResult(Captures.OrderByDescending(c => c.CapturedAt).FirstOrDefault());

    public Task<int> CountSinceAsync(DateTime from) =>
        Task.FromResult(Captures.Count(c => c.CapturedAt >= from));

    public async Task DeleteAsync(long id)
    {
        var capture = Captures.FirstOrDefault(c => c.Id == id);
        if (capture is not null)
        {
            Captures.Remove(capture);
        }
        await Task.CompletedTask;
    }
}

public class FakeScreenCapture : IScreenCaptureService
{
    public int CallCount { get; private set; }

    public string CaptureNormalizedRegion(NormalizedRect region, string outputDirectory)
    {
        CallCount++;
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, $"fake_{CallCount}.png");
        File.WriteAllText(path, "png");
        return path;
    }

    public (int X, int Y, int W, int H) ToPixelRect(NormalizedRect region) => (0, 0, 100, 100);
}

public class FakeSettingsStore : ISettingsStore
{
    public AppSettings Settings { get; private set; } = new();

    public AppSettings Load() => Settings;
    public void Save(AppSettings settings) => Settings = settings;
}
