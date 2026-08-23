using System.Text;
using VoicePin.Core.Export;
using VoicePin.Core.Models;
using Xunit;

namespace VoicePin.Core.Tests;

public class CsvExporterTests
{
    private static SalesRecord Record(long id, string nickname, long amount, DateTime at, SalesStatus status) =>
        new()
        {
            Id = id,
            Nickname = nickname,
            Amount = amount,
            RecognizedAt = at,
            Transcript = $"{nickname} {amount}원",
            Status = status
        };

    [Fact]
    public void BuildCsv_ConfirmedOnly_WithBomAndColumnOrder()
    {
        var baseTime = new DateTime(2026, 8, 20, 14, 0, 0);
        var records = new[]
        {
            Record(1, "홍길동", 19900, baseTime.AddMinutes(1), SalesStatus.Confirmed),
            Record(2, "김영희", 35000, baseTime.AddMinutes(2), SalesStatus.AutoSaved),
            Record(3, "이철수", 25500, baseTime.AddMinutes(3), SalesStatus.Pending)
        };

        var csv = CsvExporter.BuildCsv(records, baseTime, baseTime.AddDays(1));

        Assert.NotNull(csv);

        var header = csv!.Split('\n')[0].TrimEnd('\r');
        Assert.Equal("구매자 닉네임,금액,인식 시각,원본 문장,상태", header);
        Assert.Contains("홍길동", csv);
        Assert.DoesNotContain("이철수", csv);
    }

    [Fact]
    public void WriteFile_EmitsUtf8Bom()
    {
        var baseTime = new DateTime(2026, 8, 20, 14, 0, 0);
        var records = new[]
        {
            Record(1, "홍길동", 19900, baseTime.AddMinutes(1), SalesStatus.Confirmed)
        };
        var csv = CsvExporter.BuildCsv(records, baseTime, baseTime.AddDays(1))!;
        var path = Path.Combine(Path.GetTempPath(), $"voicepin_test_{Guid.NewGuid():N}.csv");

        try
        {
            CsvExporter.WriteFile(csv, path);
            var head = File.ReadAllBytes(path)[..3];
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, head);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BuildCsv_ZeroConfirmed_ReturnsNull()
    {
        var baseTime = new DateTime(2026, 8, 20, 14, 0, 0);
        var records = new[]
        {
            Record(1, "홍길동", 19900, baseTime.AddMinutes(1), SalesStatus.AutoSaved)
        };

        var csv = CsvExporter.BuildCsv(records, baseTime, baseTime.AddDays(1));

        Assert.Null(csv);
    }
}
