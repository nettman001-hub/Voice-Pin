namespace VoicePin.Core.Models;

public class TrainingPhrase
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public int RecordingCount { get; set; }
    public DateTime? LastTrainedAt { get; set; }

    public bool IsTrained => RecordingCount >= 3;
}
