using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MineEngine.Editor.Settings;

namespace MineEngine.Editor.Services;

/// <summary>Réglages de l'interface qui ne dépendent pas du thème : taille et options des éditeurs.</summary>
public interface IInterfaceService
{
    /// <summary>Taille de l'interface appliquée (1 = 100 %).</summary>
    double UiScale { get; }

    void Apply(EditorSettings settings);
}

/// <summary>
/// Publie les réglages des éditeurs comme ressources de l'application, lues par les vues en
/// DynamicResource (<see cref="CodeFontSizeKey"/>...), et agrandit le contenu de chaque fenêtre,
/// menu contextuel et info-bulle d'un même facteur.
/// </summary>
public sealed class WpfInterfaceService : IInterfaceService
{
    public const string CodeFontSizeKey = "CodeFontSize";
    public const string CodeWordWrapKey = "CodeWordWrap";
    public const string GraphGridVisibilityKey = "GraphGridVisibility";
    public const string GraphSnapToGridKey = "GraphSnapToGrid";

    private static ScaleTransform s_scale = CreateScale(1);

    private readonly Application _application;

    static WpfInterfaceService()
    {
        // Les nouvelles fenêtres et les fenêtres surgissantes reçoivent la taille choisie à leur ouverture.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) => ScaleWindow((Window)sender)));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((sender, _) => ScaleElement((FrameworkElement)sender)));
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler((sender, _) => ScaleElement((FrameworkElement)sender)));
    }

    public WpfInterfaceService(Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
    }

    public double UiScale => s_scale.ScaleX;

    public void Apply(EditorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _application.Resources[CodeFontSizeKey] = (double)settings.CodeFontSize;
        _application.Resources[CodeWordWrapKey] = settings.CodeWordWrap;
        _application.Resources[GraphGridVisibilityKey] = settings.GraphShowGrid ? Visibility.Visible : Visibility.Collapsed;
        _application.Resources[GraphSnapToGridKey] = settings.GraphSnapToGrid;

        if (Math.Abs(settings.UiScale - s_scale.ScaleX) > 0.001)
        {
            s_scale = CreateScale(settings.UiScale);
            foreach (Window window in _application.Windows)
            {
                ScaleWindow(window);
            }
        }
    }

    private static ScaleTransform CreateScale(double factor)
    {
        var scale = new ScaleTransform(factor, factor);
        scale.Freeze();
        return scale;
    }

    private static void ScaleWindow(Window window)
    {
        if (window.Content is FrameworkElement content)
        {
            ScaleElement(content);
        }
    }

    private static void ScaleElement(FrameworkElement element)
    {
        element.LayoutTransform = s_scale.ScaleX == 1 ? Transform.Identity : s_scale;
    }
}
