namespace VoicePin.Core.Models;

public class BroadcastSession
{
    public long Id { get; set; }
    public string SessionNo { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public static string BuildSessionNo(DateTime startedAt) => startedAt.ToString("yyyyMMdd_HH");
}
