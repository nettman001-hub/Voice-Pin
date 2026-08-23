using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NAudio.Wave;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public enum TrainState
{
    Idle,
    Countdown,
    Recording
}

public class TrainingPhraseItem : INotifyPropertyChanged
{
    public long Id { get; init; }
    public string Text { get; init; } = "";

    private int _recordingCount;
    public int RecordingCount
    {
        get => _recordingCount;
        set { _recordingCount = value; Refresh(); }
    }

    public DateTime? LastTrainedAt { get; set; }

    private double? _lastScore;
    public double? LastScore
    {
        get => _lastScore;
        set { _lastScore = value; Refresh(); }
    }

    public string ScoreText => LastScore is null ? "" : $"점수 {LastScore:0}%";

    public Brush ScoreBrush => LastScore switch
    {
        null => Brushes.Transparent,
        >= 80 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9E63")),
        >= 60 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C77700")),
        _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C03636"))
    };

    public Visibility BadgeVisibility =>
        RecordingCount >= 3 ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

public partial class VoiceTrainingPage : Page
{
    private readonly ITrainingRepository _repo;
    private readonly IPronunciationScorer _scorer;
    private readonly IMicrophoneRecorder _recorder;
    private readonly List<TrainingPhraseItem> _items = new();

    private TrainingPhraseItem? _selected;
    private TrainState _trainState = TrainState.Idle;
    private CancellationTokenSource? _countdownCts;
    private string? _currentWavPath;

    public VoiceTrainingPage()
    {
        InitializeComponent();
        _repo = App.Services.GetRequiredService<ITrainingRepository>();
        _scorer = App.Services.GetRequiredService<IPronunciationScorer>();
        _recorder = App.Services.GetRequiredService<IMicrophoneRecorder>();
        _recorder.RecordingTick += (_, elapsed) => Dispatcher.Invoke(() =>
            RecordStatus.Text = $"● 녹음 중… {elapsed.TotalSeconds:0.0}초 (멈춤 버튼으로 종료)");
        _ = LoadAsync();
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
                LastTrainedAt = phrase.LastTrainedAt,
                LastScore = phrase.LastScore
            });
        }
        PhraseList.ItemsSource = null;
        PhraseList.ItemsSource = _items;
        UpdateProgress();

        if (_items.Count > 0 && PhraseList.SelectedIndex < 0)
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
        if (_trainState == TrainState.Idle)
        {
            if (_selected is null)
            {
                RecordStatus.Text = "먼저 학습할 문장을 목록에서 선택해 주세요.";
                return;
            }
            await StartTrainingAsync(_selected);
        }
        else
        {
            await StopTrainingAsync();
        }
    }

    private async void QuickTrain_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not long id)
        {
            return;
        }
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }

        if (_trainState != TrainState.Idle)
        {
            return;
        }

        PhraseList.SelectedItem = item;
        CurrentPhraseText.Text = item.Text;
        await StartTrainingAsync(item);
    }

    private async Task StartTrainingAsync(TrainingPhraseItem item)
    {
        if (_trainState != TrainState.Idle)
        {
            return;
        }

        CurrentPhraseText.Text = item.Text;
        _trainState = TrainState.Countdown;
        SetTrainButton();

        _countdownCts = new CancellationTokenSource();
        var token = _countdownCts.Token;

        try
        {
            for (var remain = 3; remain >= 1; remain--)
            {
                RecordStatus.Text = $"{remain}초 후 녹음 시작 — 준비하세요! (취소하려면 다시 클릭)";
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException)
        {
            ResetToIdle();
            RecordStatus.Text = "훈련을 취소했습니다.";
            return;
        }

        if (_selected?.Id != item.Id)
        {
            ResetToIdle();
            return;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoicePin", "training", item.Id.ToString());
        Directory.CreateDirectory(dir);
        var nextIndex = Directory.GetFiles(dir, "*.wav").Length + 1;
        _currentWavPath = Path.Combine(dir, $"rec{nextIndex}.wav");

        try
        {
            _recorder.Start(_currentWavPath);
        }
        catch (Exception ex)
        {
            ResetToIdle();
            RecordStatus.Text = "마이크 권한이 필요합니다. (" + ex.Message + ")";
            return;
        }

        _trainState = TrainState.Recording;
        SetTrainButton();
    }

    private async Task StopTrainingAsync()
    {
        if (_trainState != TrainState.Recording)
        {
            if (_trainState == TrainState.Countdown)
            {
                _countdownCts?.Cancel();
            }
            return;
        }

        var item = _selected;
        var wavPath = _currentWavPath;
        ResetToIdle();

        if (item is null || wavPath is null || !_recorder.IsRecording)
        {
            return;
        }

        var duration = _recorder.Stop();
        if (duration.TotalSeconds < 2)
        {
            try { File.Delete(wavPath); } catch { /* ignore */ }
            RecordStatus.Text = $"녹음이 너무 짧습니다({duration.TotalSeconds:0.0}초). 2초 이상 또박또박 읽어 주세요.";
            return;
        }

        RecordStatus.Text = "점수 계산 중…";
        var score = await _scorer.ScoreAsync(wavPath, item.Text);

        await _repo.IncrementRecordingAsync(item.Id, DateTime.Now, score.Percent);
        item.RecordingCount++;
        item.LastTrainedAt = DateTime.Now;
        item.LastScore = score.Percent;
        PhraseList.Items.Refresh();
        UpdateProgress();

        RecordStatus.Text = $"녹음 완료 ({duration.TotalSeconds:0.0}초)\n" +
                            $"인식된 문장: '{(string.IsNullOrEmpty(score.RecognizedText) ? "(없음)" : score.RecognizedText)}'\n" +
                            $"학습 점수: {score.Percent:0}% ({score.Method})";
    }

    private void ResetToIdle()
    {
        _trainState = TrainState.Idle;
        _countdownCts?.Cancel();
        _countdownCts = null;
        _currentWavPath = null;
        SetTrainButton();
    }

    private void SetTrainButton()
    {
        TrainBtn.Content = _trainState switch
        {
            TrainState.Countdown => "⏳ 준비 중… (클릭하면 취소)",
            TrainState.Recording => "■ 멈춤",
            _ => "🎤 반복 훈련 시작 (3초 후 녹음)"
        };
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
            RecordingCount = 0,
            LastScore = null
        });
        PhraseList.ItemsSource = null;
        PhraseList.ItemsSource = _items;
        NewPhraseBox.Clear();
        UpdateProgress();
    }

    private async void DeletePhrase_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not long id)
        {
            return;
        }
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }

        var confirm = MessageBox.Show($"'{item.Text}' 문장과 녹음 파일을 삭제할까요?", "다들려",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await _repo.DeleteAsync(id);

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoicePin", "training", id.ToString());
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // 파일 삭제 실패는 무시
        }

        _items.Remove(item);
        PhraseList.ItemsSource = null;
        PhraseList.ItemsSource = _items;
        if (ReferenceEquals(_selected, item))
        {
            _selected = null;
            CurrentPhraseText.Text = "";
        }
        UpdateProgress();
    }

    private void PhraseList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = PhraseList.SelectedItem as TrainingPhraseItem;
        if (_selected is not null && _trainState == TrainState.Idle)
        {
            CurrentPhraseText.Text = _selected.Text;
        }
    }
}
