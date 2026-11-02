using System.Windows;
using System.Windows.Media;

namespace MineEngine.Editor.Views.Gui;

/// <summary>Points of the snapping grid of the GUI editor, drawn over the window of the GUI (in pixels of the GUI).</summary>
public sealed class GuiGrid : FrameworkElement
{
    public static readonly DependencyProperty StepXProperty = Register(nameof(StepX), 2);
    public static readonly DependencyProperty StepYProperty = Register(nameof(StepY), 2);
    public static readonly DependencyProperty OffsetXProperty = Register(nameof(OffsetX), 0);
    public static readonly DependencyProperty OffsetYProperty = Register(nameof(OffsetY), 0);

    private static readonly Brush Dot = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));

    public GuiGrid()
    {
        IsHitTestVisible = false;
    }

    public int StepX
    {
        get => (int)GetValue(StepXProperty);
        set => SetValue(StepXProperty, value);
    }

    public int StepY
    {
        get => (int)GetValue(StepYProperty);
        set => SetValue(StepYProperty, value);
    }

    public int OffsetX
    {
        get => (int)GetValue(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    public int OffsetY
    {
        get => (int)GetValue(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        // A grid finer than 4 pixels would only grey the window: one point in two then.
        int stepX = Math.Max(1, StepX) * (StepX < 4 ? 2 : 1);
        int stepY = Math.Max(1, StepY) * (StepY < 4 ? 2 : 1);
        double size = 0.5;
        for (double x = OffsetX % stepX; x <= ActualWidth; x += stepX)
        {
            for (double y = OffsetY % stepY; y <= ActualHeight; y += stepY)
            {
                drawingContext.DrawRectangle(Dot, null, new Rect(x - size / 2, y - size / 2, size, size));
            }
        }
    }

    private static DependencyProperty Register(string name, int value) =>
        DependencyProperty.Register(name, typeof(int), typeof(GuiGrid), new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender));
}
