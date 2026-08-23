using System.Windows;
using System.Windows.Controls;
using VoicePin.App.Views;

namespace VoicePin.App.Services;

public class NavigationService
{
    private readonly Stack<string> _history = new();

    public string? CurrentPath { get; private set; }

    public event EventHandler<(string Path, Page Page)>? Navigated;

    public void Navigate(string path)
    {
        if (!string.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            _history.Push(CurrentPath ?? path);
        }
        Show(path);
    }

    public bool CanBack => _history.Count > 0;

    public void Back()
    {
        if (_history.Count == 0)
        {
            return;
        }
        var previous = _history.Pop();
        CurrentPath = null;
        Show(previous);
    }

    private void Show(string path)
    {
        try
        {
            var page = RouteTable.Resolve(path);
            CurrentPath = path;
            Navigated?.Invoke(this, (path, page));
        }
        catch (Exception ex)
        {
            MessageBox.Show("화면을 열 수 없습니다: " + ex.Message, "다들려",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public static class RouteTable
{
    public static Page Resolve(string path)
    {
        var clean = path.TrimEnd('/');
        if (clean.Length == 0)
        {
            clean = "/onboarding";
        }

        if (clean == "/sales/review")
        {
            return new SalesReviewPage();
        }
        if (clean.StartsWith("/sales/"))
        {
            var rest = clean["/sales/".Length..];
            var slashIndex = rest.IndexOf('/');
            var idPart = slashIndex > 0 ? rest[..slashIndex] : rest;
            if (long.TryParse(idPart, out var saleId))
            {
                return new SalesDetailPage(saleId);
            }
        }

        return clean switch
        {
            "/onboarding" => new OnboardingPage(),
            "/login" => new LoginPage(),
            "/signup" => new SignupPage(),
            "/password/reset" => new PasswordResetPage(),
            "/pricing" => new PricingPage(),
            "/live" => new LiveHomePage(),
            "/voice-training" => new VoiceTrainingPage(),
            "/recognition-rules" => new RecognitionRulesPage(),
            "/sales" => new SalesListPage(),
            "/settlement" => new SettlementPage(),
            "/subscription/plans" => new SubscriptionPlansPage(),
            "/subscription/payment" => new SubscriptionPaymentPage(),
            "/subscription/manage" => new SubscriptionManagePage(),
            "/notifications/settings" => new NotificationSettingsPage(),
            "/my" => new MyPage(),
            "/admin" => new AdminDashboardPage(),
            "/admin/members" => new AdminMembersPage(),
            "/admin/reports" => new AdminReportsPage(),
            "/admin/stats" => new AdminStatsPage(),
            _ => throw new InvalidOperationException("알 수 없는 경로: " + path)
        };
    }
}

public static class AppState
{
    public const string AppName = "다들려";

    public static string? UserEmail { get; set; }
    public static string Nickname { get; set; } = "판매자";
    public static string Role { get; set; } = "판매자";
    public static bool IsLoggedIn => !string.IsNullOrEmpty(UserEmail);

    public static bool IsSubscribed { get; set; } = true;
    public static string PlanName { get; set; } = "프로";
    public static DateTime PlanExpiry { get; set; } = DateTime.Today.AddMonths(1);
}
