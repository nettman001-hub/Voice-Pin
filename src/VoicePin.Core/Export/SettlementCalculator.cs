using VoicePin.Core.Models;

namespace VoicePin.Core.Export;

public static class SettlementCalculator
{
    public static SettlementSummary Compute(IEnumerable<SalesRecord> records, DateTime from, DateTime to)
    {
        var inRange = records.Where(r => r.RecognizedAt >= from && r.RecognizedAt < to).ToList();
        var counted = inRange.Where(r => r.Status != SalesStatus.Pending).ToList();
        var pendingCount = inRange.Count(r => r.Status == SalesStatus.Pending);

        var summary = new SettlementSummary
        {
            TotalCount = counted.Count,
            TotalAmount = counted.Sum(r => r.Amount),
            UniqueBuyers = counted.Where(r => !string.IsNullOrEmpty(r.Nickname))
                .Select(r => r.Nickname)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            PendingCount = pendingCount
        };

        summary.DailyGroups = counted
            .GroupBy(r => r.RecognizedAt.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new DailyGroup
            {
                Date = g.Key,
                Count = g.Count(),
                AmountSum = g.Sum(r => r.Amount),
                Records = g.OrderByDescending(r => r.RecognizedAt).ToList()
            })
            .ToList();

        return summary;
    }
}
