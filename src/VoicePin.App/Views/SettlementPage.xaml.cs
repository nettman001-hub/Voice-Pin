using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VoicePin.Core.Export;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public partial class SettlementPage : Page
{
    private readonly ISalesRepository _sales;
    private SettlementSummary? _summary;
    private (DateTime From, DateTime To) _range;

    public SettlementPage()
    {
        InitializeComponent();
        _sales = App.Services.GetRequiredService<ISalesRepository>();
        PeriodToday.IsChecked = true;
        _ = LoadAsync();
    }

    private async void Period_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded && _summary is null)
        {
            // 초기 로드 중
        }
        await LoadAsync();
    }

    private Task LoadAsync()
    {
        try
        {
            var today = DateTime.Today;
            if (PeriodWeek.IsChecked == true)
            {
                var monday = today.AddDays(-(int)today.DayOfWeek + 1);
                if ((int)today.DayOfWeek == 0)
                {
                    monday = today.AddDays(-6);
                }
                _range = (monday, today.AddDays(1));
            }
            else if (PeriodMonth.IsChecked == true)
            {
                _range = (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1));
            }
            else if (PeriodCustom.IsChecked == true)
            {
                if (!DateTime.TryParse(FromBox.Text, out var from) ||
                    !DateTime.TryParse(ToBox.Text, out var to))
                {
                    MessageBox.Show("시작일과 종료일을 yyyy-MM-dd 형식으로 입력해 주세요.", "다들려");
                    return Task.CompletedTask;
                }
                if (from > to)
                {
                    MessageBox.Show("시작일이 종료일보다 늦습니다. 조회하지 않습니다.", "다들려");
                    return Task.CompletedTask;
                }
                _range = (from, to.AddDays(1));
            }
            else
            {
                _range = (today, today.AddDays(1));
            }

            _summary = _sales.Summarize(_range.From, _range.To);
            RenderSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show("잠시 후 다시 시도해 주세요. (" + ex.Message + ")", "다들려");
        }
        return Task.CompletedTask;
    }

    private void RenderSummary()
    {
        if (_summary is null)
        {
            return;
        }

        CountText.Text = $"{_summary.TotalCount:N0}건";
        AmountSumText.Text = $"{_summary.TotalAmount:N0}원";
        BuyerText.Text = $"{_summary.UniqueBuyers:N0}명";
        PendingText.Text = $"{_summary.PendingCount:N0}건";

        DailyPanel.Children.Clear();
        EmptyHint.Visibility = _summary.DailyGroups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var group in _summary.DailyGroups)
        {
            var card = new Border
            {
                Style = (Style)Application.Current.Resources["Card"],
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(16, 10, 16, 10),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var stack = new StackPanel();

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock
            {
                Text = group.Date.ToString("yyyy-MM-dd (ddd)", System.Globalization.CultureInfo.GetCultureInfo("ko-KR")),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });
            header.Children.Add(new TextBlock
            {
                Text = $"{group.Count}건 · 합계 {group.AmountSum:N0}원",
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["Color.Primary"],
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(header);

            var detail = new TextBlock
            {
                Text = string.Join("\n", group.Records.Select(r =>
                    $"　{r.Nickname} · {r.Amount:N0}원 · {r.RecognizedAt:HH:mm:ss} · {r.Status.ToKorean()}")),
                FontSize = 12.5,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["Color.Muted"],
                Margin = new Thickness(4, 6, 0, 0),
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(detail);

            card.Child = stack;
            var toggle = false;
            card.MouseLeftButtonUp += (_, _) =>
            {
                toggle = !toggle;
                detail.Visibility = toggle ? Visibility.Visible : Visibility.Collapsed;
            };

            DailyPanel.Children.Add(card);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_summary is null || _range.From == default)
        {
            return;
        }

        var records = _sales.GetAllAsync().GetAwaiter().GetResult();
        var csv = CsvExporter.BuildCsv(records, _range.From, _range.To);
        if (csv is null)
        {
            MessageBox.Show("내보낼 확정 데이터가 없습니다.", "다들려",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV 파일|*.csv",
            FileName = $"voicepin_sales_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            CsvExporter.WriteFile(csv, dialog.FileName);
            MessageBox.Show($"내보내기가 완료되었습니다.\n{dialog.FileName}", "다들려",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("파일 생성 실패: " + ex.Message, "다들려");
        }
    }
}
