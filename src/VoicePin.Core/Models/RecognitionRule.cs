namespace VoicePin.Core.Models;

[Flags]
public enum RuleAction
{
    None = 0,
    SaveSale = 1,
    Capture = 2
}

public class RecognitionRule
{
    public long Id { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public RuleAction Actions { get; set; } = RuleAction.SaveSale;
    public int Priority { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsBuiltIn { get; set; }

    public string ActionsText => Actions switch
    {
        RuleAction.SaveSale => "DB 저장",
        RuleAction.Capture => "화면 캡처",
        RuleAction.SaveSale | RuleAction.Capture => "DB 저장 + 화면 캡처",
        _ => "-"
    };
}
