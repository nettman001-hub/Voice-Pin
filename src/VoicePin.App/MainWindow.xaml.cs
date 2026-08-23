using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VoicePin.App.Services;

namespace VoicePin.App;

public partial class MainWindow : Window
{
    private readonly NavigationService _nav;
    private readonly DispatcherTimer _toastTimer;

    public MainWindow()
    {
        InitializeComponent();

        _nav = App.Services.GetRequiredService<NavigationService>();
        _nav.Navigated += OnNavigated;

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) =>
        {
            ToastBar.Visibility = Visibility.Collapsed;
            _toastTimer.Stop();
        };

        Loaded += (_, _) => _nav.Navigate("/onboarding");
    }

    public void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBar.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void OnNavigated(object? sender, (string Path, Page Page) args)
    {
        PageHost.Content = args.Page;

        var title = args.Page switch
        {
            Views.OnboardingPage => "시작",
            Views.LoginPage => "로그인",
            Views.SignupPage => "회원가입",
            Views.PasswordResetPage => "비밀번호 찾기·재설정",
            Views.PricingPage => "요금제 안내",
            Views.LiveHomePage => "라이브 청취 홈",
            Views.VoiceTrainingPage => "음성 학습",
            Views.RecognitionRulesPage => "인식 단어 및 동작 규칙 설정",
            Views.SalesListPage => "판매 내역 목록",
            Views.SalesDetailPage => "판매 내역 상세",
            Views.SalesReviewPage => "방송 후 일괄 확인·수정",
            Views.SettlementPage => "판매 정산·내보내기",
            Views.SubscriptionPlansPage => "요금제 선택",
            Views.SubscriptionPaymentPage => "결제 수단 등록·결제",
            Views.SubscriptionManagePage => "구독 관리·결제 내역",
            Views.NotificationSettingsPage => "알림 설정",
            Views.MyPage => "마이페이지",
            Views.AdminDashboardPage => "관리자 대시보드",
            Views.AdminMembersPage => "회원 관리",
            Views.AdminReportsPage => "신고 처리",
            Views.AdminStatsPage => "이용 통계 및 시스템 오류 로그",
            _ => AppState.AppName
        };
        HeaderTitle.Text = title;

        UserBadge.Text = AppState.IsLoggedIn
            ? $"{AppState.Nickname} ({AppState.UserEmail}) · {AppState.Role} · 구독: {AppState.PlanName}"
            : "비회원";

        BackBtn.IsEnabled = _nav.CanBack;

        HighlightNav(args.Path);
    }

    private void HighlightNav(string path)
    {
        foreach (var radio in new[] { NavLive })
        {
            radio.IsChecked = path == "/live";
        }
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string path } && IsLoaded)
        {
            _nav.Navigate(path);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_nav.CanBack)
        {
            _nav.Back();
        }
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        _nav.Navigate("/live");
    }
}
