using System.Windows;
using System.Windows.Controls;
using VoicePin.App.Services;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public partial class LoginPage : Page
{
    private readonly NavigationService _nav;
    private int _failCount;
    private DateTime? _lockedUntil;

    public LoginPage()
    {
        InitializeComponent();
        _nav = (NavigationService)App.Services.GetService(typeof(NavigationService))!;

        var settings = App.Services.GetRequiredService<ISettingsStore>().Load();
        EmailBox.Text = settings.LastEmail;
        AutoLoginCheck.IsChecked = settings.AutoLogin;
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        if (_lockedUntil is not null && DateTime.Now < _lockedUntil)
        {
            ShowError("잠시 후 다시 시도해 주세요. (5회 실패로 15분간 잠김)");
            return;
        }

        var email = EmailBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("이메일 또는 비밀번호가 올바르지 않습니다.");
            return;
        }

        if (email.StartsWith("suspended", StringComparison.OrdinalIgnoreCase))
        {
            ShowError("정지된 계정입니다.");
            return;
        }

        if (password.Length < 8)
        {
            _failCount++;
            if (_failCount >= 5)
            {
                _lockedUntil = DateTime.Now.AddMinutes(15);
            }
            ShowError("이메일 또는 비밀번호가 올바르지 않습니다.");
            return;
        }

        AppState.UserEmail = email;
        AppState.Role = email.Contains("admin", StringComparison.OrdinalIgnoreCase) ? "관리자" : "판매자";
        AppState.Nickname = AppState.Role == "관리자" ? "관리자" : "판매자";

        var settings = App.Services.GetRequiredService<ISettingsStore>().Load();
        settings.AutoLogin = AutoLoginCheck.IsChecked == true;
        settings.LastEmail = email;
        App.Services.GetRequiredService<ISettingsStore>().Save(settings);

        ErrorBanner.Visibility = Visibility.Collapsed;
        _nav.Navigate(AppState.Role == "관리자" ? "/admin" : "/live");
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBanner.Visibility = Visibility.Visible;
    }

    private void FindPassword_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/password/reset");
    private void Signup_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/signup");
}
