using VoicePin.Core.Rules;
using Xunit;

namespace VoicePin.Core.Tests;

public class TextMatchTests
{
    [Fact]
    public void ScorePercent_PlaceholdersIgnored()
    {
        var score = TextMatch.ScorePercent("구매하신 분은 [닉네임]님입니다", "구매 하신 분은 홍길동 님입니다");

        Assert.True(score >= 85, $"expected >= 85 but got {score}");
    }

    [Fact]
    public void ScorePercent_Identical_Is100()
    {
        Assert.Equal(100, TextMatch.ScorePercent("구매확정 됐습니다", "구매확정 됐습니다"));
    }

    [Fact]
    public void ScorePercent_Unrelated_IsLow()
    {
        var score = TextMatch.ScorePercent("구매확정 됐습니다", "오늘 날씨 좋다");

        Assert.True(score <= 20, $"expected <= 20 but got {score}");
    }

    [Fact]
    public void ScorePercent_Empty_Returns0()
    {
        Assert.Equal(0, TextMatch.ScorePercent("구매확정 됐습니다", ""));
    }
}
