using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace VoicePin.App.Views;

public class AdminReportItem
{
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
}

public partial class AdminReportsPage : Page
{
    private readonly ObservableCollection<AdminReportItem> _reports = new();

    public AdminReportsPage()
    {
        InitializeComponent();
        _reports.Add(new AdminReportItem
        {
            Title = "신고 #1 — hong@example.com · 접수",
            Detail = "사유: 부적절한 음성 학습 파일 업로드 의심"
        });
        _reports.Add(new AdminReportItem
        {
            Title = "신고 #2 — kim@example.com · 처리 중",
            Detail = "사유: 스팸성 판매 멘트"
        });
        ReportList.ItemsSource = _reports;
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is AdminReportItem report)
        {
            report.Title = report.Title.Split("·")[0].Trim() + "· 완료";
            ReportList.Items.Refresh();
            MessageBox.Show("신고가 처리 완료로 변경되었습니다. (데모)", "다들려");
        }
    }
}
