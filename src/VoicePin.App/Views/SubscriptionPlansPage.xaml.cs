using System.Windows;
using System.Windows.Controls;
using VoicePin.App.Services;

namespace VoicePin.App.Views;

public partial class SubscriptionPlansPage : Page
{
    private readonly NavigationService _nav;

    public SubscriptionPlansPage()
    {
        InitializeComponent();
        _nav = (NavigationService)App.Services.GetService(typeof(NavigationService))!;
        CurrentText.Text = AppState.IsSubscribed
            ? $"현재 구독 중: {AppState.PlanName} · 만료일 {AppState.PlanExpiry:yyyy-MM-dd}"
            : "현재 구독 없음";
    }

    private void SelectPlan_Click(object sender, RoutedEventArgs e)
    {
        SubscriptionPaymentPage.SelectedPlanName = ((Button)sender).Tag.ToString()!;
        _nav.Navigate("/subscription/payment");
    }

    private void Manage_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/subscription/manage");
}
