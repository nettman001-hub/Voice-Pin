using System.Text.RegularExpressions;

namespace VoicePin.Core.Rules;

public enum VoiceCommandKind
{
    None,
    StartEdit,
    SetNickname,
    SetAmount,
    FinishEdit
}

public sealed record VoiceCommand(VoiceCommandKind Kind, string? Value = null);

public static partial class VoiceCommandParser
{
    [GeneratedRegex(@"(?:닉네임|구매자)\s*(?:은|는|이)?\s*([가-힣a-zA-Z0-9_]+)")]
    private static partial Regex NicknameRegex();

    public static VoiceCommand Parse(string transcript)
    {
        var text = transcript.Trim();

        if (text.Contains("수정 완료") || text.Contains("수정완료"))
        {
            return new VoiceCommand(VoiceCommandKind.FinishEdit);
        }
        if (text.Contains("수정 시작") || text.Contains("수정시작"))
        {
            return new VoiceCommand(VoiceCommandKind.StartEdit);
        }

        var nicknameMatch = NicknameRegex().Match(text);
        if (nicknameMatch.Success)
        {
            return new VoiceCommand(VoiceCommandKind.SetNickname, SaleExtractor.CleanNickname(nicknameMatch.Groups[1].Value));
        }

        if (text.Contains("금액") || text.Contains("가격"))
        {
            var amount = SaleExtractor.ExtractAmount(text);
            if (amount.HasValue)
            {
                return new VoiceCommand(VoiceCommandKind.SetAmount, amount.Value.ToString());
            }
        }

        if (text.Contains("삭제") )
        {
            return new VoiceCommand(VoiceCommandKind.StartEdit); // 삭제 명령도 수정 모드로 진입 후 처리
        }

        return new VoiceCommand(VoiceCommandKind.None);
    }
}
