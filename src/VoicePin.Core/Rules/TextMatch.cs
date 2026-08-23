using System.Text.RegularExpressions;

namespace VoicePin.Core.Rules;

public static partial class TextMatch
{
    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex("[가-힣a-zA-Z0-9]+")]
    private static partial Regex TokenRegex();

    public static string Normalize(string text)
    {
        var withoutPlaceholder = PlaceholderRegex().Replace(text ?? "", " ");
        return string.Concat(
            TokenRegex().Matches(withoutPlaceholder).Select(m => m.Value.ToLowerInvariant()));
    }

    /// <summary>문자 바이그램 Dice 유사도 (0.0 ~ 1.0)</summary>
    public static double BigramSimilarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }
        if (a.Length == 1 || b.Length == 1)
        {
            return string.Equals(a, b, StringComparison.Ordinal) ? 1 : 0;
        }

        var gramsA = CountBigrams(a);
        var gramsB = CountBigrams(b);

        var intersection = 0;
        foreach (var (gram, count) in gramsA)
        {
            if (gramsB.TryGetValue(gram, out var other))
            {
                intersection += Math.Min(count, other);
            }
        }

        return 2.0 * intersection / (Total(gramsA) + Total(gramsB));
    }

    public static double ScorePercent(string targetText, string recognizedText)
    {
        var target = Normalize(targetText);
        var recognized = Normalize(recognizedText);

        var bigram = BigramSimilarity(target, recognized);
        var unigram = UnigramSimilarity(target, recognized);
        return Math.Round(Math.Max(bigram, unigram) * 100);
    }

    /// <summary>문자 멀티셋 유사도(순서 무시, 삽입어에 둔감) 0.0 ~ 1.0</summary>
    public static double UnigramSimilarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        var countsA = CharCounts(a);
        var countsB = CharCounts(b);

        var intersection = 0;
        var totalA = 0;
        foreach (var (ch, count) in countsA)
        {
            totalA += count;
            if (countsB.TryGetValue(ch, out var other))
            {
                intersection += Math.Min(count, other);
            }
        }

        if (intersection == 0)
        {
            return 0;
        }

        return 2.0 * intersection / (totalA + countsB.Values.Sum());
    }

    private static Dictionary<string, int> CountBigrams(string value)
    {
        var counts = new Dictionary<string, int>(value.Length);
        for (var i = 0; i < value.Length - 1; i++)
        {
            var gram = value.Substring(i, 2);
            counts[gram] = counts.TryGetValue(gram, out var c) ? c + 1 : 1;
        }
        return counts;
    }

    private static Dictionary<char, int> CharCounts(string value)
    {
        var counts = new Dictionary<char, int>(value.Length);
        foreach (var ch in value)
        {
            counts[ch] = counts.TryGetValue(ch, out var c) ? c + 1 : 1;
        }
        return counts;
    }

    private static int Total(Dictionary<string, int> counts) => counts.Values.Sum();
}
