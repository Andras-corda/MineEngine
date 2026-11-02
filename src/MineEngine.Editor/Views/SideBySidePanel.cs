using System.Windows;
using System.Windows.Controls;

namespace MineEngine.Editor.Views;

/// <summary>
/// Two children side by side when the width allows it (the first one takes the remaining width,
/// at least <see cref="FirstMinWidth"/>), otherwise one under the other: an editor tab shows the
/// list and its preview together, the narrow Inspector shows the preview below.
/// </summary>
public sealed class SideBySidePanel : Panel
{
    public static readonly DependencyProperty FirstMinWidthProperty = DependencyProperty.Register(
        nameof(FirstMinWidth), typeof(double), typeof(SideBySidePanel), new FrameworkPropertyMetadata(420.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(SideBySidePanel), new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double FirstMinWidth
    {
        get => (double)GetValue(FirstMinWidthProperty);
        set => SetValue(FirstMinWidthProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>Side by side, the second child takes the remaining width (and the first one its own), like a graph beside its settings.</summary>
    public static readonly DependencyProperty FillSecondProperty = DependencyProperty.Register(
        nameof(FillSecond), typeof(bool), typeof(SideBySidePanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public bool FillSecond
    {
        get => (bool)GetValue(FillSecondProperty);
        set => SetValue(FillSecondProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count < 2)
        {
            return MeasureStacked(availableSize);
        }

        UIElement first = InternalChildren[0];
        UIElement second = InternalChildren[1];
        if (FillSecond)
        {
            // The second child (a graph) would be as wide as it can: its minimum width decides.
            first.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            double rest = availableSize.Width - first.DesiredSize.Width - Gap;
            if (!double.IsInfinity(availableSize.Width) && rest >= MinWidthOf(second))
            {
                second.Measure(new Size(rest, availableSize.Height));
                return new Size(availableSize.Width, Math.Max(first.DesiredSize.Height, second.DesiredSize.Height));
            }

            return MeasureStacked(availableSize);
        }

        second.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        double side = second.DesiredSize.Width;
        if (SideBySide(availableSize.Width, side))
        {
            first.Measure(new Size(availableSize.Width - side - Gap, availableSize.Height));
            return new Size(availableSize.Width, Math.Max(first.DesiredSize.Height, second.DesiredSize.Height));
        }

        return MeasureStacked(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (FillSecond && InternalChildren.Count >= 2 && finalSize.Width - InternalChildren[0].DesiredSize.Width - Gap >= MinWidthOf(InternalChildren[1]))
        {
            double first = InternalChildren[0].DesiredSize.Width;
            InternalChildren[0].Arrange(new Rect(0, 0, first, finalSize.Height));
            InternalChildren[1].Arrange(new Rect(first + Gap, 0, finalSize.Width - first - Gap, finalSize.Height));
            return finalSize;
        }

        if (!FillSecond && InternalChildren.Count >= 2 && SideBySide(finalSize.Width, InternalChildren[1].DesiredSize.Width))
        {
            double side = InternalChildren[1].DesiredSize.Width;
            InternalChildren[0].Arrange(new Rect(0, 0, finalSize.Width - side - Gap, finalSize.Height));
            InternalChildren[1].Arrange(new Rect(finalSize.Width - side, 0, side, finalSize.Height));
            return finalSize;
        }

        double top = 0;
        foreach (UIElement child in InternalChildren)
        {
            double width = Math.Min(finalSize.Width, Math.Max(child.DesiredSize.Width, ReferenceEquals(child, InternalChildren[0]) ? finalSize.Width : 0));
            child.Arrange(new Rect(0, top, width, child.DesiredSize.Height));
            top += child.DesiredSize.Height + Gap;
        }

        return finalSize;
    }

    private static double MinWidthOf(UIElement element) => element is FrameworkElement { MinWidth: > 0 } framework ? framework.MinWidth : 0;

    private bool SideBySide(double width, double side) => !double.IsInfinity(width) && width >= FirstMinWidth + Gap + side;

    private Size MeasureStacked(Size availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }

        height += Math.Max(0, InternalChildren.Count - 1) * Gap;
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }
}
