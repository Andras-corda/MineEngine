using System.Windows;
using System.Windows.Controls;

namespace MineEngine.Editor.Views.Sparkle;

/// <summary>Keeps its child square (the preview): the largest square that fits, centered.</summary>
public sealed class SquarePanel : Decorator
{
    protected override Size MeasureOverride(Size constraint)
    {
        double side = Side(constraint);
        Child?.Measure(new Size(side, side));
        return new Size(side, side);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        double side = Side(arrangeSize);
        Child?.Arrange(new Rect((arrangeSize.Width - side) / 2, (arrangeSize.Height - side) / 2, side, side));
        return arrangeSize;
    }

    private static double Side(Size size)
    {
        double side = Math.Min(size.Width, size.Height);
        if (double.IsInfinity(side))
        {
            side = double.IsInfinity(size.Width) ? size.Height : size.Width;
        }

        return double.IsInfinity(side) || double.IsNaN(side) ? 300 : Math.Max(0, side);
    }
}
