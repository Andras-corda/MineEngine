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
        [PinType.ItemStack] = Create("#5FD18A"),
        [PinType.Object] = Create("#7FA7FF"),
        [PinType.Enum] = Create("#C9B458"),
        [PinType.Decimal] = Create("#A3D94A"),
        [PinType.Direction] = Create("#E07A4A"),
        [PinType.BlockState] = Create("#8C5A3A"),
        [PinType.Entity] = Create("#2E7BC4"),
        [PinType.Vector2i] = Create("#E8C547"),
        [PinType.Vector3i] = Create("#F0B020"),
        [PinType.Vector2f] = Create("#E8D98A"),
        [PinType.Vector3f] = Create("#F2CC4E"),
        [PinType.Char] = Create("#E9A3D4"),
        [PinType.Long] = Create("#2E9E5A"),
        [PinType.Float] = Create("#B8E36A"),
        [PinType.Byte] = Create("#7CC68E"),
        [PinType.Uuid] = Create("#9AA7C7"),
        [PinType.LivingEntity] = Create("#4A90D9"),
        [PinType.TextComponent] = Create("#F06EAA"),
        [PinType.Identifier] = Create("#C7A6F0"),
        [PinType.EquipmentSlot] = Create("#8FB3A0"),
        [PinType.Hand] = Create("#D6B48C"),
        [PinType.Rarity] = Create("#B07CE0"),
        [PinType.Fluid] = Create("#3A8FE0"),
        [PinType.BoundingBox] = Create("#E0C060"),
        [PinType.DamageSource] = Create("#C0504A"),
        [PinType.EffectInstance] = Create("#8E6FD8"),
        [PinType.NbtTag] = Create("#A0A0A0"),
        [PinType.World] = Create("#5FA35F"),
    };

    private static readonly Dictionary<NodeCategory, Brush> Categories = new()
    {
        [NodeCategory.Event] = Create("#A23B3B"),
        [NodeCategory.Condition] = Create("#3F7F4A"),
        [NodeCategory.Action] = Create("#3B6EA5"),
        [NodeCategory.Variable] = Create("#A8742A"),
        [NodeCategory.Function] = Create("#6A4AAE"),
        [NodeCategory.Object] = Create("#2D7D8C"),
        [NodeCategory.Library] = Create("#6B7280"),
        [NodeCategory.Global] = Create("#2F8F83"),
    };

    /// <summary>Gray of the function libraries (nodes and domains of the Nodes window).</summary>
    public static Brush Library => Categories[NodeCategory.Library];

    // Domains of the Nodes window, one color per family of blocks as in MCreator.
    private static readonly Dictionary<NodeGroup, Brush> Groups = new()
    {
        [NodeGroup.Events] = Create("#B5463F"),
        [NodeGroup.Logic] = Create("#4E7FC0"),
        [NodeGroup.Math] = Create("#4E9A5B"),
        [NodeGroup.Text] = Create("#B0569E"),
        [NodeGroup.Player] = Create("#3E8FB0"),
        [NodeGroup.Item] = Create("#3AA58C"),
        [NodeGroup.Block] = Create("#9A6B3F"),
        [NodeGroup.World] = Create("#7A8B2E"),
        [NodeGroup.Program] = Create("#6A55B0"),
        [NodeGroup.Collections] = Create("#8A6FC4"),
        [NodeGroup.Entities] = Create("#3B7FBF"),
        [NodeGroup.Globals] = Create("#2F8F83"),
        [NodeGroup.Conversions] = Create("#8A8F98"),
    };

    private static readonly Brush AllGroups = Create("#6B7280");

    /// <summary>Couleur d'un type de valeur. L'exécution utilise la couleur du texte du thème (voir la vue).</summary>
    /// <remarks>A collection takes the color of its values, as in Unreal.</remarks>
    public static Brush ForPin(DataType type) => type.IsCollection && type.Element is { } element
        ? ForPin(element)
        : Pins.GetValueOrDefault(type.Kind, Brushes.Gray);

    public static Brush ForCategory(NodeCategory category) => Categories.GetValueOrDefault(category, Brushes.Gray);

    /// <summary>Color of a domain of the Nodes window; null (every node) is gray.</summary>
    public static Brush ForGroup(NodeGroup? group) => group is { } value ? Groups.GetValueOrDefault(value, AllGroups) : AllGroups;

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

/// <summary>Color of a comment ("#RRGGBB"): its title ("header"), or the light fill of its box ("fill").</summary>
public sealed class CommentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Color color;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(value as string ?? "#5A6B7D");
        }
        catch (FormatException)
        {
            color = Color.FromRgb(0x5A, 0x6B, 0x7D);
        }

        color.A = parameter as string == "fill" ? (byte)0x30 : (byte)0xE0;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Couleur de l'en-tête d'un nœud selon sa famille (NodeCategory).</summary>
public sealed class NodeCategoryBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is NodeCategory category ? GraphBrushes.ForCategory(category) : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// Color of a domain of the Nodes window from its key: a <see cref="NodeGroup"/> name for a graph,
/// "bt:" and a Puppeteer category for a tree, null for every node.
/// </summary>
public sealed class NodeGroupBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        "bt:Goal" => Puppeteer.PuppeteerBrushes.ForCategory(MineEngine.Assets.Puppeteer.BtCategory.Task),
        string key when key.StartsWith("bt:", StringComparison.Ordinal)
                        && Enum.TryParse(key[3..], out MineEngine.Assets.Puppeteer.BtCategory category) => Puppeteer.PuppeteerBrushes.ForCategory(category),
        string key when key.StartsWith(ViewModels.Graphs.NodeLibraryItem.LibraryKeyPrefix, StringComparison.Ordinal) => GraphBrushes.Library,
        string key when Enum.TryParse(key, out NodeGroup group) => GraphBrushes.ForGroup(group),
        _ => GraphBrushes.ForGroup(null),
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Libellé français d'une accessibilité ("Public", "Protégé", "Privé").</summary>
public sealed class AccessLevelLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is AccessLevel access ? access.Label() : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
