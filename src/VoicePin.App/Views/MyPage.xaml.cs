using System.Windows;
using System.Windows.Controls;
using VoicePin.App.Services;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public partial class MyPage : Page
{
    private readonly ISettingsStore _settingsStore;
    private readonly ISecretProtector _protector;

    public MyPage()
    {
        InitializeComponent();
        _settingsStore = App.Services.GetRequiredService<ISettingsStore>();
        _protector = App.Services.GetRequiredService<ISecretProtector>();

        var settings = _settingsStore.Load();
        EmailBox.Text = AppState.UserEmail ?? settings.LastEmail;
        NicknameBox.Text = AppState.Nickname;
        ModelBox.Text = string.IsNullOrEmpty(settings.DeepgramModel) ? "nova-3" : settings.DeepgramModel;
        LangBox.Text = string.IsNullOrEmpty(settings.DeepgramLanguage) ? "ko" : settings.DeepgramLanguage;

        KeyStatus.Text = settings.HasDeepgramKey
            ? "● API 키가 저장되어 있습니다 (암호화됨)."
            : "○ API 키 미설정 — 라이브 청취를 시작하려면 키를 입력하세요.";
    }

    private void ShowKey_Changed(object sender, RoutedEventArgs e)
    {
        if (ShowKeyCheck.IsChecked == true)
        {
            ApiKeyPlainBox.Text = ApiKeyBox.Password;
            ApiKeyBox.Visibility = Visibility.Collapsed;
            ApiKeyPlainBox.Visibility = Visibility.Visible;
        }
        else
        {
            ApiKeyBox.Password = ApiKeyPlainBox.Text;
            ApiKeyPlainBox.Visibility = Visibility.Collapsed;
            ApiKeyBox.Visibility = Visibility.Visible;
        }
    }

    private void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        var key = (ShowKeyCheck.IsChecked == true ? ApiKeyPlainBox.Text : ApiKeyBox.Password).Trim();
        if (key.Length == 0)
        {
            MessageBox.Show("API 키를 입력해 주세요.", "다들려");
            return;
        }

        var settings = _settingsStore.Load();
        settings.DeepgramApiKeyProtected = _protector.Protect(key);
        settings.DeepgramModel = string.IsNullOrWhiteSpace(ModelBox.Text) ? "nova-3" : ModelBox.Text.Trim();
        settings.DeepgramLanguage = string.IsNullOrWhiteSpace(LangBox.Text) ? "ko" : LangBox.Text.Trim();
        _settingsStore.Save(settings);

        KeyStatus.Text = "● API 키가 저장되어 있습니다 (암호화됨).";
        MessageBox.Show("STT 설정이 저장되었습니다.", "다들려", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        AppState.Nickname = string.IsNullOrWhiteSpace(NicknameBox.Text)
            ? AppState.Nickname
            : NicknameBox.Text.Trim();
        MessageBox.Show("계정 정보가 저장되었습니다. (데모: 서버 연동 전)", "다들려",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteAccount_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "정말 계정 삭제를 요청할까요? 삭제 대기 상태로 전환되며 일정 기간 후 영구 삭제됩니다.",
            "계정 삭제 요청", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm == MessageBoxResult.Yes)
        {
            MessageBox.Show("계정 삭제 요청이 접수되었습니다. (데모)", "다들려");
        }
    }
}
