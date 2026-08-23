namespace VoicePin.Core.Models;

public class CaptureImage
{
    public long Id { get; set; }
    public long? SaleId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
}
