using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MineEngine.Assets.Graphs;

namespace MineEngine.Editor.Views.Graphs;

/// <summary>
/// Couleurs de l'éditeur nodal, les mêmes en clair et en sombre (comme les Blueprints) :
/// fils et broches par type de valeur, en-têtes de nœuds par famille. Le fil d'exécution
/// prend la couleur du texte du thème (blanc en sombre, presque noir en clair).
/// </summary>
public static class GraphBrushes
{
    private static readonly Dictionary<PinType, Brush> Pins = new()
    {
        [PinType.Exec] = Create("#F2F2F2"),
        [PinType.Boolean] = Create("#D04A3C"),
        [PinType.Number] = Create("#3DBA6E"),
        [PinType.Text] = Create("#D980C6"),
        [PinType.Player] = Create("#4C9BE8"),
        [PinType.Position] = Create("#E0A53C"),
        [PinType.Item] = Create("#3EC1C9"),
        [PinType.Block] = Create("#B07A4F"),
        [PinType.Sound] = Create("#A88BE6"),
        [PinType.Object] = Create("#7FA7FF"),
        [PinType.Enum] = Create("#C9B458"),
    };

    private static readonly Dictionary<NodeCategory, Brush> Categories = new()
    {
        [NodeCategory.Event] = Create("#A23B3B"),
        [NodeCategory.Condition] = Create("#3F7F4A"),
        [NodeCategory.Action] = Create("#3B6EA5"),
        [NodeCategory.Variable] = Create("#A8742A"),
        [NodeCategory.Function] = Create("#6A4AAE"),
        [NodeCategory.Object] = Create("#2D7D8C"),
    };

    /// <summary>Couleur d'un type de valeur. L'exécution utilise la couleur du texte du thème (voir la vue).</summary>
    public static Brush ForPin(DataType type) => Pins.GetValueOrDefault(type.Kind, Brushes.Gray);

    public static Brush ForCategory(NodeCategory category) => Categories.GetValueOrDefault(category, Brushes.Gray);

    private static SolidColorBrush Create(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Couleur d'une broche ou d'un fil selon son type (DataType) : objets du projet en bleu clair, énumérations en ocre.</summary>
public sealed class PinTypeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DataType type ? GraphBrushes.ForPin(type) : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Couleur de l'en-tête d'un nœud selon sa famille (NodeCategory).</summary>
public sealed class NodeCategoryBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is NodeCategory category ? GraphBrushes.ForCategory(category) : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Libellé français d'une accessibilité ("Public", "Protégé", "Privé").</summary>
public sealed class AccessLevelLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is AccessLevel access ? access.Label() : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
