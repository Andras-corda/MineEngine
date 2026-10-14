using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MineEngine.Editor.Converters;

/// <summary>Largeur en pixels vers une largeur de colonne (réserve la place des boutons de la fenêtre).</summary>
public sealed class WidthToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double width && width > 0 ? new GridLength(width) : new GridLength(138);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
