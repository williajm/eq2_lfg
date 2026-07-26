using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Eq2Lfg.Core.Models;

namespace Eq2Lfg.App;

/// <summary>
/// Role → accent brush (tank blue, healer green, dps red, support purple).
/// With ConverterParameter=Tint, a translucent version for badge backgrounds.
/// </summary>
public sealed class RoleToBrushConverter : IValueConverter
{
    private static readonly Color TankColor = Color.FromRgb(0x4F, 0xC3, 0xF7);
    private static readonly Color HealerColor = Color.FromRgb(0x81, 0xC7, 0x84);
    private static readonly Color DpsColor = Color.FromRgb(0xE5, 0x73, 0x73);
    private static readonly Color SupportColor = Color.FromRgb(0xBA, 0x68, 0xC8);

    private static readonly Brush Tank = Freeze(TankColor);
    private static readonly Brush Healer = Freeze(HealerColor);
    private static readonly Brush Dps = Freeze(DpsColor);
    private static readonly Brush Support = Freeze(SupportColor);

    private static readonly Brush TankTint = Freeze(TankColor, 0x26);
    private static readonly Brush HealerTint = Freeze(HealerColor, 0x26);
    private static readonly Brush DpsTint = Freeze(DpsColor, 0x26);
    private static readonly Brush SupportTint = Freeze(SupportColor, 0x26);
    private static readonly Brush GrayTint = Freeze(Colors.Gray, 0x26);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var tint = "Tint".Equals(parameter as string, StringComparison.OrdinalIgnoreCase);
        return value switch
        {
            Role.Tank => tint ? TankTint : Tank,
            Role.Healer => tint ? HealerTint : Healer,
            Role.Dps => tint ? DpsTint : Dps,
            Role.Support => tint ? SupportTint : Support,
            _ => tint ? GrayTint : Brushes.Gray,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush Freeze(Color color, byte alpha = 0xFF)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Role → archetype icon (shield / ankh / crossed swords / lute).</summary>
public sealed class RoleToImageConverter : IValueConverter
{
    private static readonly BitmapImage Tank = Load("tank");
    private static readonly BitmapImage Healer = Load("healer");
    private static readonly BitmapImage Dps = Load("dps");
    private static readonly BitmapImage Support = Load("support");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            Role.Tank => Tank,
            Role.Healer => Healer,
            Role.Dps => Dps,
            Role.Support => Support,
            _ => null,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static BitmapImage Load(string name)
    {
        var image = new BitmapImage(
            new Uri($"pack://application:,,,/Assets/Roles/{name}.png"));
        image.Freeze();
        return image;
    }
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
