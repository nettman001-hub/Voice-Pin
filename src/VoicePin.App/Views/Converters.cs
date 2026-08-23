using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VoicePin.Core.Models;

namespace VoicePin.App.Views;

public class SalesStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is SalesStatus status ? status.ToKorean() : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class SalesStatusToBrushConverter : IValueConverter
{
    private static readonly Brush Pending = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A6100"));
    private static readonly Brush Confirmed = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9E63"));
    private static readonly Brush Edited = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED"));
    private static readonly Brush Auto = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D63D8"));

    static SalesStatusToBrushConverter()
    {
        Pending.Freeze();
        Confirmed.Freeze();
        Edited.Freeze();
        Auto.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            SalesStatus.Pending => Pending,
            SalesStatus.Confirmed => Confirmed,
            SalesStatus.ManualEdited => Edited,
            _ => Auto
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class PendingBackgroundConverter : IValueConverter
{
    private static readonly Brush WarnBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF7E0"));

    static PendingBackgroundConverter()
    {
        WarnBg.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is SalesStatus.Pending ? WarnBg : Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
