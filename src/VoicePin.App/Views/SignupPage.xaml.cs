using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VoicePin.App.Services;

namespace VoicePin.App.Views;

public partial class SignupPage : Page
{
    private readonly NavigationService _nav;
    private string? _issuedCode;
    private int _verifyFailCount;

    public SignupPage()
    {
        InitializeComponent();
        _nav = (NavigationService)App.Services.GetService(typeof(NavigationService))!;
        TermsCheck.Checked += (_, _) => Validate();
        TermsCheck.Unchecked += (_, _) => Validate();
        PrivacyCheck.Checked += (_, _) => Validate();
        PrivacyCheck.Unchecked += (_, _) => Validate();
    }

    private void Validate()
    {
        var ok = TermsCheck.IsChecked == true && PrivacyCheck.IsChecked == true;
        RequestCodeBtn.IsEnabled = ok;
    }

    private void RequestCode_Click(object sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text.Trim();
        if (!email.Contains('@'))
        {
            ShowBanner("올바른 이메일 주소를 입력해 주세요.", true);
            return;
        }
        if (email.StartsWith("taken", StringComparison.OrdinalIgnoreCase))
        {
            ShowBanner("이미 가입된 이메일입니다. 로그인 화면에서 로그인해 주세요.", true);
            return;
        }
        if (!IsValidPassword(PasswordBox.Password))
        {
            ShowBanner("비밀번호는 8자 이상, 영문·숫자·특수문자 조합이어야 합니다.", true);
            return;
        }
        if (PasswordBox.Password != PasswordConfirmBox.Password)
        {
            ShowBanner("비밀번호가 일치하지 않습니다.", true);
            return;
        }

        _issuedCode = new Random().Next(100000, 999999).ToString();
        CodeHint.Text = $"입력된 이메일({email})로 6자리 인증 코드를 발송했습니다. 10분간 유효합니다. (데모 코드: {_issuedCode})";
        CodeCard.Visibility = Visibility.Visible;
        ShowBanner($"인증 코드가 발송되었습니다. 데모 빌드이므로 화면의 코드({_issuedCode})를 입력하세요.", false);
    }

    private void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (_verifyFailCount >= 5)
        {
            ShowBanner("인증 코드를 5회 연속 잘못 입력하여 15분간 재시도가 제한됩니다.", true);
            return;
        }
        if (_issuedCode is null || CodeBox.Text.Trim() != _issuedCode)
        {
            _verifyFailCount++;
            ShowBanner("인증 코드가 올바르지 않습니다. 다시 입력해 주세요.", true);
            return;
        }

        AppState.UserEmail = EmailBox.Text.Trim();
        AppState.Role = RoleSeller.IsChecked == true ? "판매자" : "관리자";
        AppState.Nickname = "판매자";

        MessageBox.Show("가입이 완료되었습니다! 홈 화면으로 이동합니다.", "다들려",
            MessageBoxButton.OK, MessageBoxImage.Information);
        _nav.Navigate("/live");
    }

    private static bool IsValidPassword(string password)
    {
        return password.Length >= 8
               && password.Any(char.IsLetter)
               && password.Any(char.IsDigit)
               && password.Any(c => !char.IsLetterOrDigit(c));
    }

    private void ShowBanner(string message, bool isError)
    {
        InfoBanner.Background = new SolidColorBrush(
            isError ? (Color)ColorConverter.ConvertFromString("#FDECEC")
                    : (Color)ColorConverter.ConvertFromString("#E8F1FF"));
        InfoText.Foreground = new SolidColorBrush(isError
            ? (Color)ColorConverter.ConvertFromString("#C03636")
            : (Color)ColorConverter.ConvertFromString("#1D63D8"));
        InfoText.Text = message;
        InfoBanner.Visibility = Visibility.Visible;
    }

    private void LoginLink_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/login");
}
