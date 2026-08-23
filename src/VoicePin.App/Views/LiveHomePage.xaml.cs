using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VoicePin.App.Services;
using VoicePin.Core.Listening;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public class TranscriptItem
{
    public string Time { get; init; } = "";
    public string Text { get; init; } = "";
    public bool IsFinal { get; init; }
}

public partial class LiveHomePage : Page
{
    private readonly NavigationService _nav;
    private readonly ListeningEngine _engine;
    private readonly ISalesRepository _sales;
    private readonly ICaptureRepository _captures;

    public ObservableCollection<TranscriptItem> Transcripts { get; } = new();
    public ObservableCollection<SalesRecord> RecentSales { get; } = new();

    public LiveHomePage()
    {
        InitializeComponent();

        _nav = App.Services.GetRequiredService<NavigationService>();
        _engine = App.Services.GetRequiredService<ListeningEngine>();
        _sales = App.Services.GetRequiredService<ISalesRepository>();
        _captures = App.Services.GetRequiredService<ICaptureRepository>();

        TranscriptList.ItemsSource = Transcripts;
        RecentSalesList.ItemsSource = RecentSales;

        _engine.StateChanged += Engine_StateChanged;
        _engine.TranscriptReceived += Engine_TranscriptReceived;
        _engine.SaleSaved += Engine_SaleSaved;
        _engine.CaptureSaved += Engine_CaptureSaved;

        Unloaded += (_, _) =>
        {
            _engine.StateChanged -= Engine_StateChanged;
            _engine.TranscriptReceived -= Engine_TranscriptReceived;
            _engine.SaleSaved -= Engine_SaleSaved;
            _engine.CaptureSaved -= Engine_CaptureSaved;
        };

        UpdateStatus(_engine.State);
        RefreshTodaySummary();
    }

    private async void StartStop_Click(object sender, RoutedEventArgs e)
    {
        var settingsStore = App.Services.GetRequiredService<ISettingsStore>();
        if (_engine.State == ListeningState.Idle)
        {
            var settings = settingsStore.Load();
            if (!settings.HasDeepgramKey)
            {
                MessageBox.Show(
                    "Deepgram API 키가 설정되지 않았습니다.\n마이페이지에서 API 키를 먼저 입력해 주세요.",
                    "다들려", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _engine.StartAsync(AppContext.BaseDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show("청취 시작 실패: " + ex.Message, "다들려");
            }
        }
        else
        {
            await _engine.StopAsync();
        }
    }

    private void Engine_StateChanged(object? sender, ListeningState state) => Dispatcher.Invoke(() => UpdateStatus(state));

    private void UpdateStatus(ListeningState state)
    {
        StatusText.Text = state switch
        {
            ListeningState.Connecting => "연결 중…",
            ListeningState.Listening => "청취 중",
            ListeningState.EditMode => "수정 대기 중",
            _ => "대기 중"
        };
        StatusDot.Fill = state == ListeningState.Listening || state == ListeningState.EditMode
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5484D"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD2DC"));
        EditBadge.Visibility = state == ListeningState.EditMode ? Visibility.Visible : Visibility.Collapsed;
        StartStopBtn.Content = state == ListeningState.Idle ? "● 청취 시작" : "■ 청취 중지";
        SessionInfo.Text = state == ListeningState.Idle
            ? "틱톡 라이브를 켠 뒤 [청취 시작]을 누르세요."
            : $"방송 회차 {DateTime.Now:yyyyMMdd_HH} · 시스템 오디오를 실시간 텍스트로 변환 중입니다.";
    }

    private void Engine_TranscriptReceived(object? sender, SttResult result) => Dispatcher.Invoke(() =>
    {
        if (Transcripts.Count > 0 && !result.IsFinal && !Transcripts[0].IsFinal)
        {
            Transcripts[0] = new TranscriptItem
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Text = result.Transcript,
                IsFinal = false
            };
        }
        else
        {
            Transcripts.Insert(0, new TranscriptItem
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Text = result.Transcript,
                IsFinal = result.IsFinal
            });
        }
        while (Transcripts.Count > 50)
        {
            Transcripts.RemoveAt(Transcripts.Count - 1);
        }
        TranscriptList.Items.Refresh();
    });

    private void Engine_SaleSaved(object? sender, SalesRecord record) => Dispatcher.Invoke(() =>
    {
        RecentSales.Insert(0, record);
        while (RecentSales.Count > 20)
        {
            RecentSales.RemoveAt(RecentSales.Count - 1);
        }
        RefreshTodaySummary();
    });

    private void Engine_CaptureSaved(object? sender, CaptureImage capture) => Dispatcher.Invoke(() =>
        RefreshTodaySummary());

    private void RefreshTodaySummary()
    {
        _ = RefreshTodaySummaryAsync();
    }

    private async Task RefreshTodaySummaryAsync()
    {
        var todayStart = DateTime.Today;
        var records = _sales.SearchAsync(new SalesSearchFilter { From = todayStart }).GetAwaiter().GetResult();
        TodayCount.Text = records.Count(r => r.Status != SalesStatus.Pending).ToString();
        var captures = await _captures.CountSinceAsync(todayStart);
        TodayCaptures.Text = captures.ToString();

        RecentSales.Clear();
        foreach (var record in records.Take(20))
        {
            RecentSales.Add(record);
        }
    }

    private void OpenRules_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/recognition-rules");
    private void OpenSales_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/sales");
}
