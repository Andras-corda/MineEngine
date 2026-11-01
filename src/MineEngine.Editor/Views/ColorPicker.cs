using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MineEngine.Core.Theming;

namespace MineEngine.Editor.Views;

/// <summary>
/// Color picker (V0.7.14): a swatch with its code; a click opens a square of saturation and brightness, a
/// bar of hue and the code "#RRGGBB" to type. <see cref="Color"/> is the code, bound both ways.
/// </summary>
public sealed class ColorPicker : ContentControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(string), typeof(ColorPicker),
        new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColorPicker)d).ShowColor()));

    private const double SquareWidth = 200;
    private const double SquareHeight = 140;

    private readonly Border _swatch = new() { Width = 26, Height = 18, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1) };
    private readonly TextBlock _code = new() { FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Popup _popup = new() { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true };
    private readonly Rectangle _hueLayer = new() { Width = SquareWidth, Height = SquareHeight };
    private readonly Ellipse _squareMark = new() { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
    private readonly Rectangle _hueMark = new() { Width = 4, Height = 18, Stroke = Brushes.White, StrokeThickness = 1.5, IsHitTestVisible = false };
    private readonly TextBox _input = new() { FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 8, 0, 0) };
    private double _hue;
    private double _saturation;
    private double _value = 1;
    private bool _updating;

    public ColorPicker()
    {
        Focusable = false;
        _swatch.SetResourceReference(Border.BorderBrushProperty, "HintBrush");
        var button = new Button
        {
            Padding = new Thickness(6, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { _swatch, _code } },
        };
        button.Click += (_, _) => _popup.IsOpen = !_popup.IsOpen;
        _popup.PlacementTarget = button;
        _popup.Child = BuildPopup();
        Content = new Grid { Children = { button, _popup } };
        ShowColor();
    }

    /// <summary>The color, "#RRGGBB".</summary>
    public string Color
    {
        get => (string)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private Border BuildPopup()
    {
        // The square: the hue, whitened to the left, darkened to the bottom.
        var white = new Rectangle
        {
            Width = SquareWidth, Height = SquareHeight,
            Fill = new LinearGradientBrush(Colors.White, System.Windows.Media.Color.FromArgb(0, 255, 255, 255), 0),
        };
        var black = new Rectangle
        {
            Width = SquareWidth, Height = SquareHeight,
            Fill = new LinearGradientBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), Colors.Black, 90),
        };
        var square = new Canvas { Width = SquareWidth, Height = SquareHeight, Background = Brushes.Transparent, ClipToBounds = true, Cursor = Cursors.Cross };
        square.Children.Add(_hueLayer);
        square.Children.Add(white);
        square.Children.Add(black);
        square.Children.Add(_squareMark);
        Track(square, p =>
        {
            _saturation = Math.Clamp(p.X / SquareWidth, 0, 1);
            _value = 1 - Math.Clamp(p.Y / SquareHeight, 0, 1);
            Commit();
        });

        var stops = new GradientStopCollection();
        for (int i = 0; i <= 6; i++)
        {
            stops.Add(new GradientStop(FromHsv(Math.Min(i * 60, 359.9), 1, 1), i / 6.0));
        }

        var hue = new Canvas { Width = SquareWidth, Height = 14, Margin = new Thickness(0, 8, 0, 0), Cursor = Cursors.Hand };
        hue.Children.Add(new Rectangle { Width = SquareWidth, Height = 14, Fill = new LinearGradientBrush(stops, 0) });
        hue.Children.Add(_hueMark);
        Canvas.SetTop(_hueMark, -2);
        Track(hue, p =>
        {
            _hue = Math.Clamp(p.X / SquareWidth, 0, 0.9999) * 360;
            Commit();
        });

        _input.TextChanged += (_, _) =>
        {
            if (!_updating && RgbColor.TryParse(_input.Text.Trim(), out RgbColor color))
            {
                Color = color.ToString();
            }
        };

        var border = new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { square, hue, _input } },
        };
        border.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
        return border;
    }

    /// <summary>Follows the pointer on a part of the picker while the button is held.</summary>
    private static void Track(FrameworkElement element, Action<Point> move)
    {
        element.MouseLeftButtonDown += (_, e) =>
        {
            element.CaptureMouse();
            move(e.GetPosition(element));
            e.Handled = true;
        };
        element.MouseMove += (_, e) =>
        {
            if (element.IsMouseCaptured)
            {
                move(e.GetPosition(element));
            }
        };
        element.MouseLeftButtonUp += (_, _) => element.ReleaseMouseCapture();
    }

    private void Commit()
    {
        System.Windows.Media.Color color = FromHsv(_hue, _saturation, _value);
        _updating = true;
        Color = new RgbColor(color.R, color.G, color.B).ToString();
        _updating = false;
        ShowMarks();
    }

    /// <summary>Shows the bound color; the hue is kept for the greys (they have none).</summary>
    private void ShowColor()
    {
        RgbColor color = RgbColor.TryParse(Color, out RgbColor parsed) ? parsed : new RgbColor(255, 255, 255);
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        _swatch.Background = brush;
        _code.Text = color.ToString();
        if (!_updating)
        {
            (double hue, double saturation, double value) = ToHsv(color);
            if (saturation > 0.001 && value > 0.001)
            {
                _hue = hue;
            }

            _saturation = saturation;
            _value = value;
            _input.Text = color.ToString();
        }

        ShowMarks();
    }

    private void ShowMarks()
    {
        _hueLayer.Fill = new SolidColorBrush(FromHsv(_hue, 1, 1));
        Canvas.SetLeft(_squareMark, _saturation * SquareWidth - 6);
        Canvas.SetTop(_squareMark, (1 - _value) * SquareHeight - 6);
        Canvas.SetLeft(_hueMark, _hue / 360 * SquareWidth - 2);
    }

    private static System.Windows.Media.Color FromHsv(double hue, double saturation, double value)
    {
        double c = value * saturation;
        double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = value - c;
        (double r, double g, double b) = (hue % 360) switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return System.Windows.Media.Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    private static (double Hue, double Saturation, double Value) ToHsv(RgbColor color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        double hue = delta == 0 ? 0
            : max == r ? 60 * ((g - b) / delta % 6)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);
        return (hue < 0 ? hue + 360 : hue, max == 0 ? 0 : delta / max, max);
    }
}
