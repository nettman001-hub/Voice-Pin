using VoicePin.Core.Models;
using VoicePin.Core.Rules;
using Xunit;

namespace VoicePin.Core.Tests;

public class TranscriptAnalyzerTests
{
    private static readonly IReadOnlyList<RecognitionRule> Rules = new List<RecognitionRule>
    {
        new() { Keyword = "구매확정", Actions = RuleAction.SaveSale, Priority = 10, Enabled = true },
        new() { Keyword = "캡처", Actions = RuleAction.Capture, Priority = 8, Enabled = true },
        new() { Keyword = "결제 완료", Actions = RuleAction.SaveSale | RuleAction.Capture, Priority = 6, Enabled = true },
        new() { Keyword = "비활성단어", Actions = RuleAction.SaveSale, Priority = 1, Enabled = false }
    };

    [Fact]
    public void Analyze_SaleConfirmation_Saves()
    {
        var analyzer = new TranscriptAnalyzer(Rules);
        var outcome = analyzer.Analyze("구매확정 됐습니다 구매하신 분은 홍길동님 금액은 30000원");

        Assert.True(outcome.ShouldSaveSale);
        Assert.False(outcome.ShouldCapture);
        Assert.Equal("홍길동", outcome.Candidate.Nickname);
        Assert.Equal(30000, outcome.Candidate.Amount);
    }

    [Fact]
    public void Analyze_CaptureKeyword_CapturesOnly()
    {
        var analyzer = new TranscriptAnalyzer(Rules);
        var outcome = analyzer.Analyze("댓글 확인되면 캡처 부탁드립니다");

        Assert.False(outcome.ShouldSaveSale);
        Assert.True(outcome.ShouldCapture);
    }

    [Fact]
    public void Analyze_BothActions()
    {
        var analyzer = new TranscriptAnalyzer(Rules);
        var outcome = analyzer.Analyze("결제 완료 되셨으면 캡처 남겨주세요");

        Assert.True(outcome.ShouldSaveSale);
        Assert.True(outcome.ShouldCapture);
    }

    [Fact]
    public void Analyze_DisabledRule_Ignored()
    {
        var analyzer = new TranscriptAnalyzer(Rules);
        var outcome = analyzer.Analyze("비활성단어 테스트입니다");

        Assert.Empty(outcome.MatchedRules);
    }

    [Fact]
    public void Analyze_ConfirmationWithoutRules_StillSaves()
    {
        var analyzer = new TranscriptAnalyzer(new List<RecognitionRule>());
        var outcome = analyzer.Analyze("구매확정 됐습니다 닉네임은 철수 금액은 5000원");

        Assert.True(outcome.ShouldSaveSale);
    }
}
