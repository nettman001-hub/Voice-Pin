using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public class RuleItem : INotifyPropertyChanged
{
    public long Id { get; init; }
    public string Keyword { get; set; } = "";
    public RuleAction Actions { get; set; }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; OnPropertyChanged(nameof(Enabled)); }
    }

    public bool IsBuiltIn { get; init; }
    public int Priority { get; init; }

    public bool CanDelete => !IsBuiltIn;

    public string ActionsText => Actions switch
    {
        RuleAction.SaveSale => "동작: DB 저장",
        RuleAction.Capture => "동작: 화면 캡처",
        RuleAction.SaveSale | RuleAction.Capture => "동작: DB 저장 + 화면 캡처",
        _ => "동작: 없음"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class RecognitionRulesPage : Page
{
    private readonly IRecognitionRuleRepository _rules;
    private readonly ISettingsStore _settingsStore;
    private readonly List<RuleItem> _items = new();

    public RecognitionRulesPage()
    {
        InitializeComponent();
        _rules = App.Services.GetRequiredService<IRecognitionRuleRepository>();
        _settingsStore = App.Services.GetRequiredService<ISettingsStore>();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var rules = await _rules.GetAllAsync();
        _items.Clear();
        foreach (var rule in rules)
        {
            _items.Add(new RuleItem
            {
                Id = rule.Id,
                Keyword = rule.Keyword,
                Actions = rule.Actions,
                Enabled = rule.Enabled,
                IsBuiltIn = rule.IsBuiltIn,
                Priority = rule.Priority
            });
        }
        RuleList.ItemsSource = null;
        RuleList.ItemsSource = _items;

        var settings = _settingsStore.Load();
        switch (settings.CaptureAreaName)
        {
            case "화면 전체":
                AreaFull.IsChecked = true;
                break;
            case "주문 내역 (하단 30%)":
                AreaOrders.IsChecked = true;
                break;
            default:
                AreaComments.IsChecked = true;
                break;
        }
        AreaSavedHint.Text = $"현재 영역: {settings.CaptureAreaName}";
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var keyword = NewKeywordBox.Text.Trim();
        if (keyword.Length == 0 || keyword.All(char.IsWhiteSpace))
        {
            MessageBox.Show("단어를 입력해 주세요.", "다들려");
            return;
        }
        if (_items.Any(i => string.Equals(i.Keyword, keyword, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("중복된 단어입니다.", "다들려");
            return;
        }

        var actions = ActionBoth.IsChecked == true ? RuleAction.SaveSale | RuleAction.Capture
            : ActionCapture.IsChecked == true ? RuleAction.Capture
            : RuleAction.SaveSale;

        var created = new RecognitionRule { Keyword = keyword, Actions = actions, Priority = 3 };
        created.Id = await _rules.AddAsync(created);

        _items.Insert(0, new RuleItem
        {
            Id = created.Id,
            Keyword = created.Keyword,
            Actions = created.Actions,
            Enabled = true,
            IsBuiltIn = false,
            Priority = created.Priority
        });
        RuleList.ItemsSource = null;
        RuleList.ItemsSource = _items;
        NewKeywordBox.Clear();
    }

    private async void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if ((sender as ToggleButton)?.DataContext is not RuleItem item)
        {
            return;
        }
        var rule = new RecognitionRule { Id = item.Id, Keyword = item.Keyword, Actions = item.Actions, Enabled = item.Enabled };
        await _rules.UpdateAsync(rule);
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not long id) return;
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null) return;

        var actions = PromptActions(item);
        if (actions is null) return;

        item.Actions = actions.Value;
        var rule = new RecognitionRule
        {
            Id = item.Id,
            Keyword = item.Keyword,
            Actions = item.Actions,
            Enabled = item.Enabled,
            Priority = item.Priority
        };
        await _rules.UpdateAsync(rule);
        RuleList.Items.Refresh();
    }

    private static RuleAction? PromptActions(RuleItem item)
    {
        var dialog = new Window
        {
            Title = "동작 편집 — " + item.Keyword,
            Width = 340,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Owner = Application.Current.MainWindow
        };
        var stack = new StackPanel { Margin = new Thickness(16) };
        var save = new RadioButton { Content = "DB 저장", GroupName = "a", IsChecked = item.Actions == RuleAction.SaveSale, FontSize = 13 };
        var capture = new RadioButton { Content = "화면 캡처", GroupName = "a", IsChecked = item.Actions == RuleAction.Capture, FontSize = 13, Margin = new Thickness(0, 8, 0, 0) };
        var both = new RadioButton { Content = "DB 저장 + 화면 캡처", GroupName = "a", IsChecked = item.Actions == (RuleAction.SaveSale | RuleAction.Capture), FontSize = 13, Margin = new Thickness(0, 8, 0, 12) };
        var ok = new Button { Content = "저장", Width = 90, Height = 30 };
        ok.Click += (_, _) => dialog.DialogResult = true;
        stack.Children.Add(save);
        stack.Children.Add(capture);
        stack.Children.Add(both);
        stack.Children.Add(ok);
        dialog.Content = stack;

        return dialog.ShowDialog() == true
            ? both.IsChecked == true ? RuleAction.SaveSale | RuleAction.Capture
                : capture.IsChecked == true ? RuleAction.Capture
                : RuleAction.SaveSale
            : null;
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not long id) return;
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null || item.IsBuiltIn)
        {
            return;
        }

        var confirm = MessageBox.Show($"'{item.Keyword}' 규칙을 삭제할까요?", "다들려",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        await _rules.DeleteAsync(id);
        _items.Remove(item);
        RuleList.ItemsSource = null;
        RuleList.ItemsSource = _items;
    }

    private void SaveArea_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsStore.Load();
        if (AreaFull.IsChecked == true)
        {
            settings.CaptureAreaName = "화면 전체";
            settings.CaptureRegion = new NormalizedRect { X = 0, Y = 0, W = 1, H = 1 };
        }
        else if (AreaOrders.IsChecked == true)
        {
            settings.CaptureAreaName = "주문 내역 (하단 30%)";
            settings.CaptureRegion = new NormalizedRect { X = 0.05, Y = 0.68, W = 0.9, H = 0.28 };
        }
        else
        {
            settings.CaptureAreaName = "댓글 목록 (우측 40%)";
            settings.CaptureRegion = new NormalizedRect { X = 0.55, Y = 0.1, W = 0.42, H = 0.6 };
        }
        _settingsStore.Save(settings);
        AreaSavedHint.Text = $"저장 완료: {settings.CaptureAreaName}";
    }
}
