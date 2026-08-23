using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public partial class CaptureViewerWindow : Window
{
    private readonly SalesRecord _sale;
    private readonly List<CaptureImage> _captures;
    private readonly SalesDetailPage _ownerPage;
    private int _index;

    public CaptureViewerWindow(SalesRecord sale, CaptureImage initial, SalesDetailPage ownerPage)
    {
        InitializeComponent();

        _sale = sale;
        _ownerPage = ownerPage;
        _captures = App.Services.GetRequiredService<ICaptureRepository>()
            .GetBySaleIdAsync(sale.Id).GetAwaiter().GetResult();

        if (_captures.All(c => c.Id != initial.Id))
        {
            _captures.Add(initial);
        }

        _index = Math.Max(0, _captures.FindIndex(c => c.Id == initial.Id));
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (_captures.Count == 0)
        {
            ImageHost.Source = null;
            IndexText.Text = "저장된 캡처가 없습니다";
            PrevBtn.IsEnabled = false;
            NextBtn.IsEnabled = false;
            return;
        }

        var capture = _captures[_index];
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(capture.FilePath);
            bitmap.EndInit();
            ImageHost.Source = bitmap;
        }
        catch
        {
            ImageHost.Source = null;
        }

        IndexText.Text = $"{_index + 1} / {_captures.Count}";
        NicknameText.Text = _sale.Nickname;
        AmountText.Text = $"{_sale.Amount:N0}원";
        CapturedAtText.Text = capture.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss");
        AreaText.Text = string.IsNullOrEmpty(capture.AreaName) ? "설정 영역" : capture.AreaName;

        PrevBtn.IsEnabled = _index > 0;
        NextBtn.IsEnabled = _index < _captures.Count - 1;
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_index > 0)
        {
            _index--;
            ShowCurrent();
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_index < _captures.Count - 1)
        {
            _index++;
            ShowCurrent();
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_captures.Count == 0)
        {
            return;
        }
        var current = _captures[_index];
        var confirm = MessageBox.Show("이 캡처 이미지를 삭제할까요?", "다들려",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await App.Services.GetRequiredService<ICaptureRepository>().DeleteAsync(current.Id);
        try { File.Delete(current.FilePath); } catch { /* ignore */ }
        _captures.RemoveAt(_index);
        _index = Math.Clamp(_index, 0, Math.Max(_captures.Count - 1, 0));
        ShowCurrent();
        await _ownerPage.ReloadCapturesAsync();
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_captures.Count == 0 || !File.Exists(_captures[_index].FilePath))
        {
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 이미지|*.png",
            FileName = Path.GetFileName(_captures[_index].FilePath)
        };
        if (dialog.ShowDialog() == true)
        {
            File.Copy(_captures[_index].FilePath, dialog.FileName, overwrite: true);
            MessageBox.Show("다운로드가 완료되었습니다.", "다들려", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Share_Click(object sender, RoutedEventArgs e)
    {
        if (_captures.Count == 0)
        {
            return;
        }
        Clipboard.SetText(_captures[_index].FilePath);
        MessageBox.Show("이미지 경로를 클립보드로 공유했습니다.", "다들려",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
