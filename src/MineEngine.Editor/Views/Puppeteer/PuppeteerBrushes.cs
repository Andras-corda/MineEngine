using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MineEngine.Assets.Puppeteer;

namespace MineEngine.Editor.Views.Puppeteer;

/// <summary>
/// Puppeteer colors, the same in light and dark, close to Unreal's: root and composites
/// gray, tasks purple, decorators blue, services green.
/// </summary>
public static class PuppeteerBrushes
{
    private static readonly Dictionary<BtCategory, Brush> Categories = new()
    {
        [BtCategory.Root] = Create("#3E4248"),
        [BtCategory.Composite] = Create("#5A6270"),
        [BtCategory.Task] = Create("#7442A6"),
        [BtCategory.Decorator] = Create("#2F6FB5"),
        [BtCategory.Service] = Create("#2E8A5A"),
    };

    public static Brush ForCategory(BtCategory category) => Categories.GetValueOrDefault(category, Brushes.Gray);

    private static SolidColorBrush Create(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Color of a node, decorator or service according to its category (BtCategory).</summary>
public sealed class BtCategoryBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is BtCategory category ? PuppeteerBrushes.ForCategory(category) : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
