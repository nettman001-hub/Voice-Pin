using VoicePin.Core.Listening;
using VoicePin.Core.Models;
using Xunit;

namespace VoicePin.Core.Tests;

public class ListeningEngineTests
{
    private static (ListeningEngine Engine, FakeScreenCapture Screen, InMemorySalesRepository Sales, InMemorySessionRepository Sessions) CreateEngine()
    {
        var audio = new FakeAudioSource();
        var rules = new InMemoryRuleRepository();
        var sales = new InMemorySalesRepository();
        var sessions = new InMemorySessionRepository();
        var captures = new InMemoryCaptureRepository();
        var screen = new FakeScreenCapture();

        var engine = new ListeningEngine(
            audio,
            () => new FakeStt(),
            rules,
            sales,
            sessions,
            captures,
            screen,
            new FakeSettingsStore());

        return (engine, screen, sales, sessions);
    }

    [Fact]
    public async Task Start_CreatesSession_AndStopEndsIt()
    {
        var (engine, _, _, sessions) = CreateEngine();

        await engine.StartAsync(Path.GetTempPath());

        Assert.Equal(ListeningState.Listening, engine.State);

        await engine.StopAsync();

        Assert.Equal(ListeningState.Idle, engine.State);
        Assert.Single(sessions.Sessions);
        Assert.NotNull(sessions.Sessions[0].EndedAt);
    }

    [Fact]
    public async Task ProcessTranscript_SaleMention_AutoSavedWithExtractedFields()
    {
        var (engine, _, sales, _) = CreateEngine();
        await engine.StartAsync(Path.GetTempPath());

        await engine.ProcessTranscriptAsync("구매확정 됐습니다. 구매하신 분은 홍길동님 금액은 30000원입니다");

        var record = Assert.Single(sales.Records);
        Assert.Equal(SalesStatus.AutoSaved, record.Status);
        Assert.Equal("홍길동", record.Nickname);
        Assert.Equal(30000, record.Amount);
        Assert.Contains("구매확정", record.Transcript);

        await engine.StopAsync();
    }

    [Fact]
    public async Task ProcessTranscript_DuplicateMention_NotSavedTwice()
    {
        var (engine, _, sales, _) = CreateEngine();
        await engine.StartAsync(Path.GetTempPath());

        await engine.ProcessTranscriptAsync("구매확정 됐습니다 구매하신 분은 홍길동님 금액은 30000원");
        await engine.ProcessTranscriptAsync("구매확정 됐습니다 구매하신 분은 홍길동님 금액은 30000원");

        Assert.Single(sales.Records);
        await engine.StopAsync();
    }

    [Fact]
    public async Task ProcessTranscript_MissingAmount_SavedAsPending()
    {
        var (engine, _, sales, _) = CreateEngine();
        await engine.StartAsync(Path.GetTempPath());

        await engine.ProcessTranscriptAsync("구매확정 됐습니다 구매하신 분은 홍길동님입니다");

        var record = Assert.Single(sales.Records);
        Assert.Equal(SalesStatus.Pending, record.Status);

        await engine.StopAsync();
    }

    [Fact]
    public async Task ProcessTranscript_EditFlow_UpdatesLatestRecordAndMarksManualEdited()
    {
        var (engine, _, sales, _) = CreateEngine();
        await engine.StartAsync(Path.GetTempPath());

        await engine.ProcessTranscriptAsync("구매확정 됐습니다 구매하신 분은 김민수님 금액은 10000원");
        await engine.ProcessTranscriptAsync("수정 시작");
        Assert.Equal(ListeningState.EditMode, engine.State);

        await engine.ProcessTranscriptAsync("닉네임은 홍길동");
        await engine.ProcessTranscriptAsync("금액은 3만원");
        await engine.ProcessTranscriptAsync("수정 완료");

        Assert.Equal(ListeningState.Listening, engine.State);
        var record = Assert.Single(sales.Records);
        Assert.Equal("홍길동", record.Nickname);
        Assert.Equal(30000, record.Amount);
        Assert.Equal(SalesStatus.ManualEdited, record.Status);

        await engine.StopAsync();
    }
}
