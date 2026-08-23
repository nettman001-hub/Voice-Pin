using VoicePin.Core.Export;
using VoicePin.Core.Models;
using Xunit;

namespace VoicePin.Core.Tests;

public class SettlementCalculatorTests
{
    [Fact]
    public void Compute_ExcludesPending_AndCountsUniqueBuyers()
    {
        var day = new DateTime(2026, 8, 20, 10, 0, 0);
        var records = new List<SalesRecord>
        {
            new() { Nickname = "홍길동", Amount = 10000, RecognizedAt = day.AddMinutes(1), Status = SalesStatus.Confirmed },
            new() { Nickname = "홍길동", Amount = 5000,  RecognizedAt = day.AddMinutes(2), Status = SalesStatus.Confirmed },
            new() { Nickname = "김영희", Amount = 7000,  RecognizedAt = day.AddMinutes(3), Status = SalesStatus.AutoSaved },
            new() { Nickname = "이철수", Amount = 99999, RecognizedAt = day.AddMinutes(4), Status = SalesStatus.Pending },
            new() { Nickname = "",      Amount = 3000,  RecognizedAt = day.AddMinutes(5), Status = SalesStatus.Pending }
        };

        var summary = SettlementCalculator.Compute(records, day.Date, day.Date.AddDays(1));

        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(22000, summary.TotalAmount);
        Assert.Equal(2, summary.UniqueBuyers);
        Assert.Equal(2, summary.PendingCount);
        Assert.Single(summary.DailyGroups);
        Assert.Equal(3, summary.DailyGroups[0].Records.Count);
    }

    [Fact]
    public void Compute_EmptyPeriod_ReturnsZeroes()
    {
        var summary = SettlementCalculator.Compute(new List<SalesRecord>(),
            DateTime.Today, DateTime.Today.AddDays(1));

        Assert.Equal(0, summary.TotalCount);
        Assert.Empty(summary.DailyGroups);
    }
}
