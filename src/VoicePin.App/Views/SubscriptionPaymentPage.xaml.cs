using System.Windows;
using System.Windows.Controls;
using VoicePin.App.Services;

namespace VoicePin.App.Views;

public partial class SubscriptionPaymentPage : Page
{
    public static string SelectedPlanName { get; set; } = "프로";

    private static readonly Dictionary<string, long> PlanPrices = new()
    {
        ["베이직"] = 9900,
        ["프로"] = 19900,
        ["프리미엄"] = 29900
    };

    private readonly NavigationService _nav;

    public SubscriptionPaymentPage()
    {
        InitializeComponent();
        _nav = (NavigationService)App.Services.GetService(typeof(NavigationService))!;

        var price = PlanPrices.GetValueOrDefault(SelectedPlanName, 19900);
        PlanSummary.Text = $"{SelectedPlanName} 플랜 — 월 {price:N0}원";
        NextRenewal.Text = $"다음 갱신일: {DateTime.Today.AddMonths(1):yyyy-MM-dd}";
    }

    private void Pay_Click(object sender, RoutedEventArgs e)
    {
        if (CardNumberBox.Text.Replace("-", "").Length < 12 ||
            !ExpiryBox.Text.Contains('/') || CvcBox.Password.Length < 3 || BirthBox.Text.Length < 6)
        {
            ErrorBanner.Visibility = Visibility.Visible;
            return;
        }

        AppState.IsSubscribed = true;
        AppState.PlanName = SelectedPlanName;
        AppState.PlanExpiry = DateTime.Today.AddMonths(1);
        ErrorBanner.Visibility = Visibility.Collapsed;

        MessageBox.Show($"결제가 완료되었습니다. ({SelectedPlanName}, 다음 갱신일 {AppState.PlanExpiry:yyyy-MM-dd})\n" +
                        "데모 빌드로 실제 결제는 이루어지지 않았습니다.", "다들려",
            MessageBoxButton.OK, MessageBoxImage.Information);

        _nav.Navigate("/subscription/manage");
    }
}
