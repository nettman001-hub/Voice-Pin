using VoicePin.Core.Rules;
using Xunit;

namespace VoicePin.Core.Tests;

public class SaleExtractorTests
{
    [Theory]
    [InlineData("구매확정 됐습니다. 구매하신 분은 홍길동님 금액은 30000원입니다", "홍길동", 30000)]
    [InlineData("구매하실 분은 러블리샵님이시고요, 가격은 3만원입니다", "러블리샵", 30000)]
    [InlineData("구매자는 김철수 입니다. 30,000원 결제됐습니다", "김철수", 30000)]
    [InlineData("닉네임은 민지님, 금액은 2만 원 입니다", "민지", 20000)]
    public void Extract_NicknameAndAmount(string transcript, string nickname, long amount)
    {
        var candidate = SaleExtractor.Extract(transcript);

        Assert.Equal(nickname, candidate.Nickname);
        Assert.Equal(amount, candidate.Amount);
    }

    [Fact]
    public void Extract_MissingAmount_ReturnsNull()
    {
        var candidate = SaleExtractor.Extract("구매확정 됐습니다. 구매하신 분은 홍길동님입니다");

        Assert.Equal("홍길동", candidate.Nickname);
        Assert.Null(candidate.Amount);
    }

    [Theory]
    [InlineData("구매확정 됐습니다")]
    [InlineData("결제 완료되셨습니다")]
    public void IsSaleConfirmation_True(string transcript)
    {
        Assert.True(SaleExtractor.IsSaleConfirmation(transcript));
    }

    [Fact]
    public void IsSaleConfirmation_False()
    {
        Assert.False(SaleExtractor.IsSaleConfirmation("오늘 날씨가 좋네요"));
    }
}
