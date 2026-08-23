namespace VoicePin.Core.Models;

public class SettlementSummary
{
    public int TotalCount { get; set; }
    public long TotalAmount { get; set; }
    public int UniqueBuyers { get; set; }
    public int PendingCount { get; set; }
    public List<DailyGroup> DailyGroups { get; set; } = new();
}

public class DailyGroup
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public long AmountSum { get; set; }
    public List<SalesRecord> Records { get; set; } = new();
}
