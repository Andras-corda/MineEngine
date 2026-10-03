using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MineEngine.Editor.Converters;

/// <summary>Nombre nul devient Visible, sinon Collapsed (message "liste vide").</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
