using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VoicePin.App.Services;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.App.Views;

public partial class SalesListPage : Page
{
    private readonly NavigationService _nav;
    private readonly ISalesRepository _sales;

    public ObservableCollection<SalesRecord> Records { get; } = new();

    public SalesListPage()
    {
        InitializeComponent();
        _nav = App.Services.GetRequiredService<NavigationService>();
        _sales = App.Services.GetRequiredService<ISalesRepository>();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var filter = new SalesSearchFilter { Query = SearchBox.Text, SortBy = SortBy() };
        if (StatusCombo.SelectedIndex > 0)
        {
            filter.Status = (SalesStatus)(StatusCombo.SelectedIndex - 1);
        }
        var days = PeriodCombo.SelectedIndex switch
        {
            0 => 1,
            1 => 7,
            2 => 30,
            _ => 0
        };
        if (days > 0)
        {
            filter.From = DateTime.Today.AddDays(-(days - 1));
        }

        var records = await _sales.SearchAsync(filter);
        Records.Clear();
        foreach (var record in records)
        {
            Records.Add(record);
        }
        SalesList.ItemsSource = Records;
    }

    private string SortBy() => SortCombo.SelectedIndex switch
    {
        1 => "oldest",
        2 => "amount",
        _ => "latest"
    };

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => _ = LoadAsync();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => _ = LoadAsync();

    private void Sale_Clicked(object sender, MouseButtonEventArgs e)
    {
        if (SalesList.SelectedItem is SalesRecord record)
        {
            _nav.Navigate($"/sales/{record.Id}");
        }
    }

    private void Sale_DoubleClick(object sender, MouseButtonEventArgs e) { }

    private void OpenReview_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/sales/review");
    private void OpenSettlement_Click(object sender, RoutedEventArgs e) => _nav.Navigate("/settlement");
}
