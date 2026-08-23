using VoicePin.Core.Models;

namespace VoicePin.Core.Services;

public class SalesSearchFilter
{
    public string? Query { get; set; }
    public SalesStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string SortBy { get; set; } = "latest";
}

public interface ISalesRepository
{
    Task<long> AddAsync(SalesRecord record);
    Task<SalesRecord?> GetAsync(long id);
    Task<List<SalesRecord>> GetAllAsync();
    Task<List<SalesRecord>> GetBySessionAsync(long sessionId);
    Task<List<SalesRecord>> SearchAsync(SalesSearchFilter filter);
    Task<bool> ExistsDuplicateAsync(string nickname, long amount, DateTime recognizedAt, int windowSeconds = 60);
    Task UpdateAsync(SalesRecord record);
    Task DeleteAsync(long id);
    SettlementSummary Summarize(DateTime from, DateTime to);
}

public interface IBroadcastSessionRepository
{
    Task<BroadcastSession> StartAsync(BroadcastSession session);
    Task EndAsync(long sessionId, DateTime endedAt);
    Task<BroadcastSession?> GetLatestAsync();
    Task<BroadcastSession?> GetAsync(long id);
}

public interface IRecognitionRuleRepository
{
    Task<List<RecognitionRule>> GetAllAsync();
    Task<long> AddAsync(RecognitionRule rule);
    Task UpdateAsync(RecognitionRule rule);
    Task DeleteAsync(long id);
}

public interface ICaptureRepository
{
    Task<long> AddAsync(CaptureImage capture);
    Task<List<CaptureImage>> GetBySaleIdAsync(long saleId);
    Task<CaptureImage?> GetLatestAsync();
    Task<int> CountSinceAsync(DateTime from);
    Task DeleteAsync(long id);
}

public interface ITrainingRepository
{
    Task<List<TrainingPhrase>> GetAllAsync();
    Task<long> AddAsync(TrainingPhrase phrase);
    Task IncrementRecordingAsync(long phraseId, DateTime trainedAt);
    Task DeleteAsync(long id);
}
