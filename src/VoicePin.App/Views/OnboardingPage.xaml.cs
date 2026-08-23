using System.Windows;
using System.Windows.Controls;
using VoicePin.App.Services;

namespace VoicePin.App.Views;

public partial class OnboardingPage : Page
{
    private readonly NavigationService _nav;

    public OnboardingPage()
    {
        InitializeComponent();
        _nav = (NavigationService)App.Services.GetService(typeof(NavigationService))!;
    }

    private void Login_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/login");
    private void Signup_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/signup");
    private void Pricing_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/pricing");
    private void FindPassword_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/password/reset");
}
