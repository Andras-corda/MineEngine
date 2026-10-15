using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MineEngine.Editor.Themes;

/// <summary>Police de l'interface (Roboto, intégrée à l'application), appliquée à toutes les fenêtres.</summary>
public static class EditorFonts
{
    /// <summary>Roboto, chargée depuis les ressources de MineEngine.Editor.</summary>
    public static FontFamily Interface { get; } =
        new(new Uri("pack://application:,,,/MineEngine.Editor;component/"), "./Fonts/#Roboto");

    /// <summary>
    /// Fait de Roboto la police par défaut : celle des fenêtres (héritée par tout leur contenu)
    /// et celle des contrôles de ModernWpf (ContentControlThemeFontFamily).
    /// </summary>
    public static void Apply(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Resources["ContentControlThemeFontFamily"] = Interface;
        TextElement.FontFamilyProperty.OverrideMetadata(typeof(Window), new FrameworkPropertyMetadata(Interface));
    }
}
