using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MineEngine.Editor.Converters;

/// <summary>
/// Texte vide devient Visible, sinon Collapsed (pour afficher un texte indicatif).
/// Avec le paramètre "Invert", c'est l'inverse : visible seulement s'il y a un texte.
/// </summary>
public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) != (parameter as string == "Invert") ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
