using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Eq2Lfg.Core.Models;

namespace Eq2Lfg.App;

/// <summary>Role → accent brush (tank blue, healer green, dps red, support purple).</summary>
public sealed class RoleToBrushConverter : IValueConverter
{
    private static readonly Brush Tank = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
    private static readonly Brush Healer = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
    private static readonly Brush Dps = new SolidColorBrush(Color.FromRgb(0xE5, 0x73, 0x73));
    private static readonly Brush Support = new SolidColorBrush(Color.FromRgb(0xBA, 0x68, 0xC8));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            Role.Tank => Tank,
            Role.Healer => Healer,
            Role.Dps => Dps,
            Role.Support => Support,
            _ => Brushes.Gray,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Post kind → chip brush (group ads amber, player posts teal).</summary>
public sealed class KindToBrushConverter : IValueConverter
{
    private static readonly Brush GroupAd = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));
    private static readonly Brush PlayerLfg = new SolidColorBrush(Color.FromRgb(0x4D, 0xB6, 0xAC));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is PostKind.GroupAd ? GroupAd : PlayerLfg;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the bound int equals the converter parameter; used by the nav rail.</summary>
public sealed class IndexEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index && int.TryParse(parameter as string, out var expected) && index == expected;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && int.TryParse(parameter as string, out var index)
            ? index
            : Binding.DoNothing;
}

/// <summary>Visible when the bound int equals the parameter.</summary>
public sealed class IndexToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index && int.TryParse(parameter as string, out var expected) && index == expected
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Collapses the element when the bound string is null or empty.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
