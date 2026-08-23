using System.Windows;
using System.Windows.Controls;
using VoicePin.App.Services;

namespace VoicePin.App.Views;

public partial class PasswordResetPage : Page
{
    private readonly NavigationService _nav;
    private string? _issuedCode;
    private string? _verifiedEmail;

    public PasswordResetPage()
    {
        InitializeComponent();
        _nav = (NavigationService)App.Services.GetService(typeof(NavigationService))!;
    }

    private void SendCode_Click(object sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text.Trim();
        if (!email.Contains('@'))
        {
            MessageBox.Show("올바른 이메일을 입력해 주세요.", "다들려");
            return;
        }
        if (email.StartsWith("unknown", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("가입되지 않은 이메일입니다.", "다들려");
            return;
        }

        _issuedCode = new Random().Next(100000, 999999).ToString();
        CodeHint.Text = $"인증 코드를 발송했습니다. 유효시간 10분. (데모 코드: {_issuedCode})";
        StepTitle.Text = "2. 인증 코드 입력";
    }

    private void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (_issuedCode is null || CodeBox.Text.Trim() != _issuedCode)
        {
            MessageBox.Show("인증 코드가 올바르지 않거나 만료되었습니다. 재발송 후 다시 시도해 주세요.", "다들려");
            return;
        }

        _verifiedEmail = EmailBox.Text.Trim();
        NewPwTitle.Visibility = Visibility.Visible;
        NewPwBox.Visibility = Visibility.Visible;
        NewPwConfirmTitle.Visibility = Visibility.Visible;
        NewPwConfirmBox.Visibility = Visibility.Visible;
        ChangeBtn.Visibility = Visibility.Visible;
        StepTitle.Text = $"3. 새 비밀번호 설정 ({_verifiedEmail})";
    }

    private void Change_Click(object sender, RoutedEventArgs e)
    {
        var newPw = NewPwBox.Password;
        if (newPw.Length < 8 || newPw != NewPwConfirmBox.Password)
        {
            MessageBox.Show("새 비밀번호는 8자 이상이고 두 입력이 일치해야 합니다.", "다들려");
            return;
        }
        if (newPw == "password123")
        {
            MessageBox.Show("기존 비밀번호와 다른 비밀번호를 입력해 주세요.", "다들려");
            return;
        }

        MessageBox.Show("비밀번호가 변경되었습니다. 로그인 화면에서 새 비밀번호로 로그인하세요.",
            "다들려", MessageBoxButton.OK, MessageBoxImage.Information);
        _nav.Navigate("/login");
    }
}
