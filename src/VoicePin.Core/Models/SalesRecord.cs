namespace VoicePin.Core.Models;

public class SalesRecord
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public long Amount { get; set; }
    public DateTime RecognizedAt { get; set; }
    public string Transcript { get; set; } = string.Empty;
    public SalesStatus Status { get; set; } = SalesStatus.AutoSaved;
    public bool DuplicateSuspect { get; set; }
}
