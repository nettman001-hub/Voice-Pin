using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VoicePin.App.Services;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public partial class SalesDetailPage : Page
{
    private readonly NavigationService _nav;
    private readonly ISalesRepository _sales;
    private readonly ICaptureRepository _captures;
    private readonly IBroadcastSessionRepository _sessions;

    private readonly long _saleId;
    private SalesRecord? _record;

    public SalesDetailPage(long saleId)
    {
        _saleId = saleId;
        InitializeComponent();
        _nav = App.Services.GetRequiredService<NavigationService>();
        _sales = App.Services.GetRequiredService<ISalesRepository>();
        _captures = App.Services.GetRequiredService<ICaptureRepository>();
        _sessions = App.Services.GetRequiredService<IBroadcastSessionRepository>();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _record = await _sales.GetAsync(_saleId);
        if (_record is null)
        {
            MessageBox.Show("판매 내역을 찾을 수 없습니다.", "다들려");
            _nav.Navigate("/sales");
            return;
        }

        NicknameBox.Text = _record.Nickname;
        AmountBox.Text = $"{_record.Amount:N0}";
        RecognizedAtText.Text = _record.RecognizedAt.ToString("yyyy-MM-dd HH:mm:ss");
        TranscriptText.Text = string.IsNullOrEmpty(_record.Transcript) ? "(전사 문장 없음)" : _record.Transcript;

        StatusText2.Text = _record.Status.ToKorean() + (_record.DuplicateSuspect ? " · 중복 의심" : "");
        StatusText2.Foreground = (System.Windows.Media.Brush)new SalesStatusToBrushConverter()
            .Convert(_record.Status, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture)!;

        var session = await _sessions.GetAsync(_record.SessionId);
        SessionNoText.Text = session?.SessionNo ?? "-";

        await LoadCapturesAsync();
    }

    private async Task LoadCapturesAsync()
    {
        CapturePanel.Children.Clear();
        var captures = await _captures.GetBySaleIdAsync(_saleId);
        NoCaptureHint.Visibility = captures.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var capture in captures)
        {
            if (!File.Exists(capture.FilePath))
            {
                continue;
            }
            var button = new Button
            {
                Tag = capture,
                Cursor = System.Windows.Input.Cursors.Hand,
                Width = 150,
                Height = 100,
                Margin = new Thickness(0, 0, 8, 8),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = App.Current.Resources["Color.Border"] as System.Windows.Media.Brush
            };
            try
            {
                var image = new Image { Stretch = System.Windows.Media.Stretch.UniformToFill };
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(capture.FilePath);
                bitmap.EndInit();
                image.Source = bitmap;
                button.Content = image;
            }
            catch
            {
                button.Content = "이미지 로드 실패";
            }
            button.Click += CaptureThumb_Click;
            CapturePanel.Children.Add(button);
        }
    }

    public async Task ReloadCapturesAsync() => await LoadCapturesAsync();

    private void CaptureThumb_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is CaptureImage capture)
        {
            new CaptureViewerWindow(_record!, capture, this).ShowDialog();
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        NicknameBox.IsReadOnly = false;
        AmountBox.IsReadOnly = false;
        EditBtn.Visibility = Visibility.Collapsed;
        EditActions.Visibility = Visibility.Visible;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_record is null)
        {
            return;
        }
        if (!long.TryParse(AmountBox.Text.Replace(",", ""), out var amount))
        {
            MessageBox.Show("금액은 숫자로 입력해 주세요.", "다들려");
            return;
        }

        _record.Nickname = NicknameBox.Text.Trim();
        _record.Amount = amount;
        _record.Status = _record.Status == SalesStatus.Confirmed ? SalesStatus.ManualEdited : SalesStatus.ManualEdited;
        await _sales.UpdateAsync(_record);

        NicknameBox.IsReadOnly = true;
        AmountBox.IsReadOnly = true;
        EditBtn.Visibility = Visibility.Visible;
        EditActions.Visibility = Visibility.Collapsed;
        StatusText2.Text = _record.Status.ToKorean();

        MessageBox.Show("수정되었습니다. (상태: 수동수정)", "다들려");
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        NicknameBox.IsReadOnly = true;
        AmountBox.IsReadOnly = true;
        EditBtn.Visibility = Visibility.Visible;
        EditActions.Visibility = Visibility.Collapsed;
        _ = LoadAsync();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show("이 판매 내역을 삭제할까요?", "다들려",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes || _record is null)
        {
            return;
        }
        await _sales.DeleteAsync(_record.Id);
        _nav.Navigate("/sales");
    }
}
