using System.Windows;
using System.Windows.Controls;
using VoicePin.App.Services;

namespace VoicePin.App.Views;

public partial class SubscriptionManagePage : Page
{
    private readonly NavigationService _nav;

    public SubscriptionManagePage()
    {
        InitializeComponent();
        _nav = (NavigationService)App.Services.GetService(typeof(NavigationService))!;

        PlanText.Text = AppState.IsSubscribed ? AppState.PlanName : "-";
        StatusText.Text = AppState.IsSubscribed ? "활성" : "해지";
        ExpiryText.Text = AppState.IsSubscribed ? $"{AppState.PlanExpiry:yyyy-MM-dd}" : "-";
    }

    private void ChangePlan_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/subscription/plans");

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("구독을 해지하면 현재 만료일까지 이용 후 자동 갱신되지 않습니다.\n정말 해지하시겠습니까?",
            "구독 해지", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            AppState.IsSubscribed = false;
            PlanText.Text = "-";
            StatusText.Text = "해지";
            ExpiryText.Text = "-";
            MessageBox.Show("구독이 해지되었습니다.", "다들려", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
