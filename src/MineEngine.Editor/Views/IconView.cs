using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;
using Shape = System.Windows.Shapes.Shape;

namespace MineEngine.Editor.Views;

/// <summary>
/// Affiche une icône de Themes/Icons.xaml désignée par sa clé : un glyphe de police
/// ("Glyph.Save") ou une géométrie vectorielle ("BlockGeometry"). Les view models donnent
/// ainsi un nom d'icône, jamais un code de caractère. La couleur est celle de Foreground
/// (héritée du parent par défaut), la taille celle de IconSize.
/// </summary>
public sealed class IconView : ContentControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(IconView), new PropertyMetadata(null, OnIconChanged));

    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize), typeof(double), typeof(IconView), new PropertyMetadata(16.0, OnIconChanged));

    public IconView()
    {
        Focusable = false;
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>Clé de l'icône dans Icons.xaml.</summary>
    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Côté de l'icône, en pixels.</summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((IconView)d).Rebuild();

    private void Rebuild()
    {
        object? resource = string.IsNullOrEmpty(Icon) ? null : TryFindResource(Icon);
        Content = resource switch
        {
            Geometry geometry => CreatePath(geometry),
            string glyph => CreateGlyph(glyph),
            _ => null,
        };
    }

    private Path CreatePath(Geometry geometry)
    {
        var path = new Path
        {
            Data = geometry,
            Width = IconSize,
            Height = IconSize,
            Stretch = Stretch.Uniform,
            // Le trait ne grossit pas avec l'icône : un peu plus épais pour les grandes tailles.
            StrokeThickness = IconSize >= 20 ? 1.5 : 1.2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            SnapsToDevicePixels = true,
        };
        path.SetBinding(Shape.StrokeProperty, new Binding(nameof(Foreground)) { Source = this });
        return path;
    }

    private TextBlock CreateGlyph(string glyph)
    {
        var text = new TextBlock
        {
            Text = glyph,
            FontSize = IconSize,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        if (TryFindResource("IconFont") is FontFamily font)
        {
            text.FontFamily = font;
        }

        TextOptions.SetTextRenderingMode(text, TextRenderingMode.Grayscale);
        return text;
    }
}
