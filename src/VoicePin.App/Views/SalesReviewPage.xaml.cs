using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public class ReviewItem : INotifyPropertyChanged
{
    public long Id { get; init; }
    public string Nickname { get; set; } = "";
    public long Amount { get; set; }
    public DateTime RecognizedAt { get; init; }
    public string Transcript { get; init; } = "";
    public SalesStatus Status { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public System.Windows.Visibility PendingVisible =>
        Status == SalesStatus.Pending ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class SalesReviewPage : Page
{
    private readonly ISalesRepository _sales;
    private readonly IBroadcastSessionRepository _sessions;

    public ObservableCollection<ReviewItem> Items { get; } = new();
    private long? _latestSessionId;
    private ReviewItem? _editingItem;

    public SalesReviewPage()
    {
        InitializeComponent();
        _sales = App.Services.GetRequiredService<ISalesRepository>();
        _sessions = App.Services.GetRequiredService<IBroadcastSessionRepository>();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var session = await _sessions.GetLatestAsync();
        if (session is null)
        {
            SessionInfoText.Text = "저장된 방송 세션이 없습니다. 라이브 청취를 시작해 보세요.";
            return;
        }

        _latestSessionId = session.Id;
        SessionInfoText.Text =
            $"방송 회차: {session.SessionNo}　|　라이브 시작: {session.StartedAt:yyyy-MM-dd HH:mm}　|　" +
            $"라이브 종료: {(session.EndedAt is null ? "진행 중" : $"{session.EndedAt:yyyy-MM-dd HH:mm}")}";

        var records = session.Id > 0 ? await _sales.GetBySessionAsync(session.Id) : new List<SalesRecord>();
        Items.Clear();
        foreach (var record in records)
        {
            Items.Add(new ReviewItem
            {
                Id = record.Id,
                Nickname = record.Nickname,
                Amount = record.Amount,
                RecognizedAt = record.RecognizedAt,
                Transcript = record.Transcript,
                Status = record.Status,
                IsSelected = record.Status == SalesStatus.Confirmed
            });
        }
        ReviewList.ItemsSource = Items;
    }

    private async void SaveOne_Click(object sender, RoutedEventArgs e)
    {
        if (_editingItem is null)
        {
            return;
        }
        if (!long.TryParse(EditAmountBox.Text.Replace(",", ""), out var amount))
        {
            MessageBox.Show("금액은 숫자로 입력해 주세요.", "다들려");
            return;
        }

        _editingItem.Nickname = EditNicknameBox.Text.Trim();
        _editingItem.Amount = amount;
        _editingItem.Status = SalesStatus.ManualEdited;

        await PersistAsync(_editingItem);
        CloseEditPanel();
        ReviewList.Items.Refresh();
    }

    private async Task PersistAsync(ReviewItem item)
    {
        await _sales.UpdateAsync(new SalesRecord
        {
            Id = item.Id,
            Nickname = item.Nickname,
            Amount = item.Amount,
            RecognizedAt = item.RecognizedAt,
            Transcript = item.Transcript,
            Status = item.Status
        });
    }

    private void EditOne_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not long id)
        {
            return;
        }
        _editingItem = Items.FirstOrDefault(i => i.Id == id);
        if (_editingItem is null)
        {
            return;
        }
        EditNicknameBox.Text = _editingItem.Nickname;
        EditAmountBox.Text = $"{_editingItem.Amount:N0}";
        EditPanel.Visibility = Visibility.Visible;
        ConfirmBtn.Visibility = Visibility.Collapsed;
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e) => CloseEditPanel();

    private void CloseEditPanel()
    {
        EditPanel.Visibility = Visibility.Collapsed;
        ConfirmBtn.Visibility = Visibility.Visible;
        _editingItem = null;
    }

    private async void ConfirmAll_Click(object sender, RoutedEventArgs e)
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("확정할 항목을 선택해 주세요.", "다들려");
            return;
        }
        if (selected.Any(i => i.Status == SalesStatus.Pending && (string.IsNullOrEmpty(i.Nickname) || i.Amount <= 0)))
        {
            MessageBox.Show("'보류' 상태에서 닉네임이나 금액이 비어 있는 내역이 있습니다.\n먼저 수정 후 확정해 주세요.", "다들려");
            return;
        }

        foreach (var item in selected.Where(i => i.Status != SalesStatus.Confirmed))
        {
            item.Status = SalesStatus.Confirmed;
            await PersistAsync(item);
        }

        ReviewList.Items.Refresh();
        MessageBox.Show($"{selected.Count}건이 확정되었습니다. 정산 화면의 집계에 반영됩니다.", "다들려",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
