using System.Windows;
using System.Windows.Controls;
using VoicePin.App.Services;

namespace VoicePin.App.Views;

public partial class PricingPage : Page
{
    private readonly NavigationService _nav;

    public PricingPage()
    {
        InitializeComponent();
        _nav = (NavigationService)App.Services.GetService(typeof(NavigationService))!;
        CurrentBadge.Text = AppState.IsSubscribed
            ? $"현재 구독 중: {AppState.PlanName} · 만료일 {AppState.PlanExpiry:yyyy-MM-dd}"
            : "현재 구독 없음 · 7일 무료 체험 가능";
    }

    private void SelectPlan_Click(object sender, RoutedEventArgs e)
    {
        var planName = ((Button)sender).Tag.ToString()!;
        if (!AppState.IsLoggedIn)
        {
            MessageBox.Show("로그인 후 요금제를 선택할 수 있습니다.", "다들려");
            _nav.Navigate("/login");
            return;
        }
        SubscriptionPaymentPage.SelectedPlanName = planName;
        _nav.Navigate("/subscription/payment");
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_nav.CanBack)
        {
            _nav.Back();
        }
    }
}
