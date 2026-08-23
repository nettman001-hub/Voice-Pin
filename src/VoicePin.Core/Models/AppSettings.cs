namespace VoicePin.Core.Models;

public class NormalizedRect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; } = 0.3;
    public double H { get; set; } = 0.3;
}

public class AppSettings
{
    public string DeepgramApiKeyProtected { get; set; } = string.Empty;
    public string DeepgramModel { get; set; } = "nova-3";
    public string DeepgramLanguage { get; set; } = "ko";
    public string CaptureAreaName { get; set; } = "댓글 목록";
    public NormalizedRect CaptureRegion { get; set; } = new();
    public bool AutoLogin { get; set; }
    public string LastEmail { get; set; } = string.Empty;

    public bool HasDeepgramKey => !string.IsNullOrEmpty(DeepgramApiKeyProtected);
}
