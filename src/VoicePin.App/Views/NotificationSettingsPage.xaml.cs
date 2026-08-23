using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace VoicePin.App.Views;

public partial class NotificationSettingsPage : Page
{
    private static readonly (string Label, string Key)[] Events =
    {
        ("판매 내역 저장 실패", "SaveFailure"),
        ("보류 건 발생", "PendingCreated"),
        ("구독 만료 예정", "SubscriptionExpiry"),
        ("인식 오류", "RecognitionError")
    };

    private readonly Dictionary<string, (ToggleButton Push, ToggleButton Email)> _toggles = new();

    public NotificationSettingsPage()
    {
        InitializeComponent();
        foreach (var (label, key) in Events)
        {
            ToggleHost.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 10)
            });

            var push = new ToggleButton { Style = (Style)Application.Current.Resources["ToggleSwitch"], IsChecked = true, Margin = new Thickness(0, 0, 0, 12) };
            var email = new ToggleButton { Style = (Style)Application.Current.Resources["ToggleSwitch"], IsChecked = false, Margin = new Thickness(0, 0, 0, 12) };
            ToggleHost.Children.Add(push);
            ToggleHost.Children.Add(email);
            _toggles[key] = (push, email);
        }
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        var pushOn = _toggles.Values.Count(t => t.Push.IsChecked == true);
        var mailOn = _toggles.Values.Count(t => t.Email.IsChecked == true);
        MessageBox.Show(
            $"테스트 알림을 발송했습니다. (데모)\n푸시 켜짐 이벤트: {pushOn}개 · 이메일 켜짐 이벤트: {mailOn}개",
            "다들려", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
