using System.Globalization;
using System.Text.RegularExpressions;

namespace VoicePin.Core.Rules;

public sealed record SaleCandidate(string? Nickname, long? Amount);

public static partial class SaleExtractor
{
    // "구매하신 분은 홍길동님", "구매자는 김철수", "닉네임은 러블리샵" 등
    [GeneratedRegex(@"(?:(?:구매하신|구매하실)\s*분|구매자|닉네임)\s*(?:은|는|이)\s*([가-힣a-zA-Z0-9_]{1,20}?)(?:님|께서|입니다|이시고|이시구요|\s|$|[,.])")]
    private static partial Regex NicknamePattern();

    // "3만원", "3만 원", "30,000원", "30000원"
    [GeneratedRegex(@"(\d{1,3}(?:,\d{3})+|\d+)\s*만?\s*원")]
    private static partial Regex AmountPattern();

    public static SaleCandidate Extract(string transcript)
    {
        var nickname = ExtractNickname(transcript);
        var amount = ExtractAmount(transcript);
        return new SaleCandidate(nickname, amount);
    }

    public static string? ExtractNickname(string transcript)
    {
        var match = NicknamePattern().Match(transcript);
        return match.Success ? CleanNickname(match.Groups[1].Value) : null;
    }

    public static long? ExtractAmount(string transcript)
    {
        foreach (Match match in AmountPattern().Matches(transcript))
        {
            var digits = match.Groups[1].Value.Replace(",", "");
            if (!long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }
            var isMan = match.Value.Contains("만");
            if (isMan && value < 10_000)
            {
                value *= 10_000;
            }
            if (value > 0)
            {
                return value;
            }
        }
        return null;
    }

    public static bool IsSaleConfirmation(string transcript)
    {
        return transcript.Contains("구매확정")
            || transcript.Contains("구매 확정")
            || transcript.Contains("결제 완료")
            || transcript.Contains("결제완료")
            || (transcript.Contains("구매") && transcript.Contains("확정"));
    }

    public static string CleanNickname(string raw)
    {
        var cleaned = raw.Trim(' ', ',', '.', '!');
        foreach (var suffix in new[] { "님입니다", "님이시고", "이시구요", "입니다", "님이세요", "님" })
        {
            if (cleaned.EndsWith(suffix))
            {
                cleaned = cleaned[..^suffix.Length];
                break;
            }
        }
        return cleaned.Trim();
    }
}
