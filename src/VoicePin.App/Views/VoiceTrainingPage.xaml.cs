using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public class TrainingPhraseItem : INotifyPropertyChanged
{
    public long Id { get; init; }
    public string Text { get; init; } = "";
    private int _recordingCount;

    public int RecordingCount
    {
        get => _recordingCount;
        set { _recordingCount = value; OnPropertyChanged(nameof(RecordingCount)); OnPropertyChanged(nameof(BadgeVisibility)); }
    }

    public DateTime? LastTrainedAt { get; set; }

    public System.Windows.Visibility BadgeVisibility =>
        RecordingCount >= 3 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class VoiceTrainingPage : Page
{
    private readonly ITrainingRepository _repo;
    private readonly IMicrophoneRecorder _recorder;
    private readonly List<TrainingPhraseItem> _items = new();
    private TrainingPhraseItem? _selected;
    private DateTime _recordStartUtc;
    private bool _countdownRunning;

    public VoiceTrainingPage()
    {
        InitializeComponent();
        _repo = App.Services.GetRequiredService<ITrainingRepository>();
        _ = LoadAsync();
        _recorder = App.Services.GetRequiredService<IMicrophoneRecorder>();
        _recorder.RecordingTick += (_, elapsed) => Dispatcher.Invoke(() =>
            RecordStatus.Text = $"녹음 중… {elapsed.TotalSeconds:0.0}초 (중지는 자동)");
    }

    private async Task LoadAsync()
    {
        var phrases = await _repo.GetAllAsync();
        _items.Clear();
        foreach (var phrase in phrases)
        {
            _items.Add(new TrainingPhraseItem
            {
                Id = phrase.Id,
                Text = phrase.Text,
                RecordingCount = phrase.RecordingCount,
                LastTrainedAt = phrase.LastTrainedAt
            });
        }
        PhraseList.ItemsSource = null;
        PhraseList.ItemsSource = _items;
        UpdateProgress();

        if (_items.Count > 0)
        {
            PhraseList.SelectedIndex = 0;
        }
    }

    private void UpdateProgress()
    {
        ProgressText.Text = $"현재 진행 상황: {_items.Count(i => i.RecordingCount >= 3)}/{_items.Count} 완료";
    }

    private async void Train_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is not null)
        {
            await StartCountdownAndRecordAsync(_selected);
        }
    }

    private async void QuickTrain_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is long id)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item is not null)
            {
                PhraseList.SelectedItem = item;
                await StartCountdownAndRecordAsync(item);
            }
        }
    }

    private async Task StartCountdownAndRecordAsync(TrainingPhraseItem item)
    {
        if (_recorder.IsRecording || _countdownRunning)
        {
            return;
        }

        CurrentPhraseText.Text = item.Text;
        _countdownRunning = true;

        for (var remain = 3; remain >= 1; remain--)
        {
            if (_selected?.Id != item.Id)
            {
                _countdownRunning = false;
                return;
            }
            RecordStatus.Text = $"{remain}초 후 녹음 시작 — 준비하세요!";
            await Task.Delay(1000);
        }
        _countdownRunning = false;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoicePin", "training", item.Id.ToString());
        Directory.CreateDirectory(dir);
        var nextIndex = Directory.GetFiles(dir, "*.wav").Length + 1;
        var path = Path.Combine(dir, $"rec{nextIndex}.wav");

        try
        {
            _recordStartUtc = DateTime.UtcNow;
            _recorder.Start(path);
        }
        catch (Exception ex)
        {
            RecordStatus.Text = "마이크 권한이 필요합니다. (" + ex.Message + ")";
            return;
        }

        await Task.Delay(4000);

        if (!_recorder.IsRecording)
        {
            return;
        }

        var duration = _recorder.Stop();
        if (duration.TotalSeconds < 2)
        {
            RecordStatus.Text = "녹음이 너무 짧습니다. 2초 이상 또박또박 읽어 주세요.";
            try { File.Delete(path); } catch { /* ignore */ }
            return;
        }

        await _repo.IncrementRecordingAsync(item.Id, DateTime.Now);
        item.RecordingCount++;
        item.LastTrainedAt = DateTime.Now;
        PhraseList.Items.Refresh();
        UpdateProgress();
        RecordStatus.Text = $"녹음 완료 ({duration.TotalSeconds:0.0}초). 학습 데이터셋에 등록되었습니다.";
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            return;
        }
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoicePin", "training", _selected.Id.ToString());
        var lastFile = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.wav").OrderBy(f => f).LastOrDefault()
            : null;

        if (lastFile is null)
        {
            MessageBox.Show("재생할 녹음이 없습니다.", "다들려");
            return;
        }

        Task.Run(() =>
        {
            using var player = new WaveOutEvent();
            using var reader = new AudioFileReader(lastFile);
            player.Init(reader);
            player.Play();
            while (player.PlaybackState == PlaybackState.Playing)
            {
                Thread.Sleep(100);
            }
        });
    }

    private async void AddPhrase_Click(object sender, RoutedEventArgs e)
    {
        var text = NewPhraseBox.Text.Trim();
        if (text.Length == 0 || text.All(char.IsWhiteSpace))
        {
            return;
        }
        var created = new TrainingPhrase { Text = text };
        created.Id = await _repo.AddAsync(created);
        _items.Add(new TrainingPhraseItem
        {
            Id = created.Id,
            Text = created.Text,
            RecordingCount = 0
        });
        PhraseList.ItemsSource = null;
        PhraseList.ItemsSource = _items;
        NewPhraseBox.Clear();
        UpdateProgress();
    }

    private void PhraseList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = PhraseList.SelectedItem as TrainingPhraseItem;
        if (_selected is not null && !_countdownRunning && !_recorder.IsRecording)
        {
            CurrentPhraseText.Text = _selected.Text;
        }
    }
}
