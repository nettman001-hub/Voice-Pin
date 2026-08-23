using VoicePin.Core.Rules;
using Xunit;

namespace VoicePin.Core.Tests;

public class VoiceCommandParserTests
{
    [Theory]
    [InlineData("수정 시작", VoiceCommandKind.StartEdit)]
    [InlineData("수정 완료", VoiceCommandKind.FinishEdit)]
    [InlineData("닉네임은 홍길동", VoiceCommandKind.SetNickname)]
    [InlineData("금액은 3만원", VoiceCommandKind.SetAmount)]
    public void Parse_Commands(string transcript, VoiceCommandKind kind)
    {
        var command = VoiceCommandParser.Parse(transcript);

        Assert.Equal(kind, command.Kind);
    }

    [Fact]
    public void Parse_NicknameValue_Cleaned()
    {
        var command = VoiceCommandParser.Parse("닉네임은 홍길동");

        Assert.Equal("홍길동", command.Value);
    }

    [Fact]
    public void Parse_AmountValue_ConvertedToWon()
    {
        var command = VoiceCommandParser.Parse("금액은 3만원");

        Assert.Equal("30000", command.Value);
    }

    [Fact]
    public void Parse_Unknown_ReturnsNone()
    {
        var command = VoiceCommandParser.Parse("오늘 날씨 좋네요");

        Assert.Equal(VoiceCommandKind.None, command.Kind);
    }
}
