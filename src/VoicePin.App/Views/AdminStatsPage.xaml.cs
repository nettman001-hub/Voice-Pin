using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace VoicePin.App.Views;

public class AdminLogItem
{
    public string Time { get; set; } = "";
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";

    public override string ToString() => $"{Time}　{Level}　{Message}";
}

public partial class AdminStatsPage : Page
{
    private readonly ObservableCollection<AdminLogItem> _logs = new();

    public AdminStatsPage()
    {
        InitializeComponent();
        _logs.Add(new AdminLogItem { Time = "2026-08-23 14:23", Level = "ERROR", Message = "STT 스트리밍 연결 실패 (timeout)" });
        _logs.Add(new AdminLogItem { Time = "2026-08-23 13:10", Level = "WARN", Message = "캡처 영역 범위 초과 — 캡처 생략" });
        _logs.Add(new AdminLogItem { Time = "2026-08-23 11:02", Level = "ERROR", Message = "결제 승인 후 구독 활성화 저장 실패 → 자동 결제 취소" });
        _logs.Add(new AdminLogItem { Time = "2026-08-22 20:41", Level = "WARN", Message = "동시 청취 세션 한도 근접 (프로 플랜)" });

        ApplyFilter();
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (LevelFilter is null)
        {
            return;
        }
        var level = LevelFilter.SelectedIndex switch
        {
            1 => "ERROR",
            2 => "WARN",
            _ => ""
        };
        LogList.ItemsSource = level.Length == 0
            ? _logs
            : new ObservableCollection<AdminLogItem>(_logs.Where(l => l.Level == level));
    }
}
