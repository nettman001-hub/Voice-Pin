using VoicePin.Core.Models;

namespace VoicePin.Core.Rules;

public sealed record DetectionOutcome(
    IReadOnlyList<RecognitionRule> MatchedRules,
    SaleCandidate Candidate,
    bool IsSaleConfirmation,
    RuleAction Actions)
{
    public bool ShouldSaveSale => (Actions & RuleAction.SaveSale) != 0;
    public bool ShouldCapture => (Actions & RuleAction.Capture) != 0;
}

public class TranscriptAnalyzer
{
    private readonly IReadOnlyList<RecognitionRule> _rules;

    public TranscriptAnalyzer(IReadOnlyList<RecognitionRule> rules)
    {
        _rules = rules;
    }

    public DetectionOutcome Analyze(string transcript)
    {
        var matched = new List<RecognitionRule>();
        var actions = RuleAction.None;

        foreach (var rule in _rules.Where(r => r.Enabled).OrderByDescending(r => r.Priority))
        {
            if (!transcript.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            matched.Add(rule);
            actions |= rule.Actions;
        }

        var candidate = SaleExtractor.Extract(transcript);
        var isConfirmation = SaleExtractor.IsSaleConfirmation(transcript);

        // 판매 확정 멘트인데 저장 규칙이 매칭되지 않아도 기본 추출 동작 수행
        if (isConfirmation && matched.Count == 0)
        {
            actions |= RuleAction.SaveSale;
        }

        return new DetectionOutcome(matched, candidate, isConfirmation, actions);
    }
}
