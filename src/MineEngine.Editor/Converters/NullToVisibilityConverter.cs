using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MineEngine.Editor.Converters;

/// <summary>Masque l'élément quand la valeur liée est null (section affichée seulement si quelque chose est sélectionné).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
