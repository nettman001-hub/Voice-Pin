namespace VoicePin.Core.Models;

public enum SalesStatus
{
    AutoSaved,
    Pending,
    ManualEdited,
    Confirmed
}

public static class SalesStatusText
{
    public static string ToKorean(this SalesStatus status) => status switch
    {
        SalesStatus.AutoSaved => "자동저장",
        SalesStatus.Pending => "보류",
        SalesStatus.ManualEdited => "수동수정",
        SalesStatus.Confirmed => "확정",
        _ => status.ToString()
    };
}
