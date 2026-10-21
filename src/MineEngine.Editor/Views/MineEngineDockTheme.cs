using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AvalonDock.Controls;
using AvalonDock.Themes;
using Keys = AvalonDock.Themes.VS2013.Themes.ResourceKeys;

namespace MineEngine.Editor.Views;

/// <summary>
/// Habillage des panneaux AvalonDock dans le style de l'accueil : la mise en forme du thème
/// VS2013 est conservée, mais toutes ses couleurs (bleu de Visual Studio, poignées pointillées)
/// sont remplacées par celles de la palette Mine Engine. Le thème est reconstruit à chaque
/// changement clair / sombre, une fois la nouvelle palette appliquée.
/// </summary>
public sealed class MineEngineDockTheme : DictionaryTheme
{
    static MineEngineDockTheme()
    {
        // Le bouton menu en haut à droite des onglets (liste des documents) n'est pas gardé :
        // les onglets restent accessibles par leur en-tête et par Ctrl+Tab.
        EventManager.RegisterClassHandler(typeof(DropDownButton), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnDropDownLoaded));

        // The Dashboard, Resources and Languages are opened from the toolbar: their tabs are not shown.
        EventManager.RegisterClassHandler(typeof(TabItem), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnTabLoaded));
        EventManager.RegisterClassHandler(typeof(TabItem), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler((sender, e) => OnTabLoaded(sender, e)));
    }

    /// <summary>Documents opened from the toolbar, whose tab is hidden in the work area.</summary>
    public static IReadOnlySet<string> ToolbarDocuments { get; } = new HashSet<string>(StringComparer.Ordinal) { "home", "resources", "languages" };

    private static void OnTabLoaded(object sender, RoutedEventArgs e)
    {
        // The tab panel of AvalonDock sets the visibility of its tabs itself (overflow): the tab is given no width instead.
        if (sender is TabItem tab && tab.MaxWidth > 0
            && (tab.Content as AvalonDock.Layout.LayoutContent ?? tab.DataContext as AvalonDock.Layout.LayoutContent) is { ContentId: { } id }
            && ToolbarDocuments.Contains(id))
        {
            tab.MaxWidth = 0;
            tab.Padding = new Thickness(0);
            tab.Margin = new Thickness(0);
            tab.IsTabStop = false;
            tab.IsHitTestVisible = false;
        }
    }

    public MineEngineDockTheme(bool isDark, Func<string, Brush> palette)
        : base(Build(isDark, palette ?? throw new ArgumentNullException(nameof(palette))))
    {
    }

    private static void OnDropDownLoaded(object sender, RoutedEventArgs e)
    {
        for (DependencyObject? parent = sender as DependencyObject; parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is LayoutDocumentPaneControl)
            {
                ((UIElement)sender).Visibility = Visibility.Collapsed;
                return;
            }
        }
    }

    private static ResourceDictionary Build(bool isDark, Func<string, Brush> palette)
    {
        DictionaryTheme baseTheme = isDark ? new Vs2013DarkTheme() : new Vs2013LightTheme();
        var dictionary = new ResourceDictionary();
        dictionary.MergedDictionaries.Add(baseTheme.ThemeResourceDictionary);

        // Les clés posées directement ici l'emportent sur celles du thème fusionné.
        foreach ((ComponentResourceKey key, string brush) in ColorMap)
        {
            dictionary[key] = palette(brush);
        }

        // Guides de dépôt (comme Unreal) : boutons aux couleurs des cartes, flèches claires, zone visée
        // remplie de l'accent en transparence et soulignée d'un trait d'accent.
        dictionary[Keys.DockingButtonBackgroundBrushKey] = palette("CardBrush");
        dictionary[Keys.DockingButtonForegroundArrowBrushKey] = palette("StrongTextBrush");
        dictionary[Keys.DockingButtonStarBorderBrushKey] = palette("CardBorderBrush");
        dictionary[Keys.DockingButtonStarBackgroundBrushKey] = Translucent(palette("SidebarBrush"), 0xE6);
        dictionary[Keys.PreviewBoxBackgroundBrushKey] = Translucent(palette("AccentLineBrush"), 0x50);
        return dictionary;
    }

    private static Brush Translucent(Brush brush, byte alpha)
    {
        Color color = brush is SolidColorBrush solid ? solid.Color : Colors.Gray;
        var result = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        result.Freeze();
        return result;
    }

    /// <summary>Clé du thème VS2013 et pinceau de la palette qui la remplace.</summary>
    private static readonly (ComponentResourceKey Key, string Brush)[] ColorMap =
    [
        // Fond général, bordures, aperçu de placement, fenêtres flottantes
        (Keys.Background, "SidebarBrush"),
        (Keys.PanelBorderBrush, "CardBorderBrush"),
        (Keys.TabBackground, "SidebarBrush"),
        (Keys.ControlAccentBrushKey, "AccentLineBrush"),
        (Keys.PreviewBoxBorderBrushKey, "AccentLineBrush"),
        (Keys.DockingButtonForegroundBrushKey, "AccentLineBrush"),
        (Keys.FloatingDocumentWindowBackground, "SidebarBrush"),
        (Keys.FloatingDocumentWindowBorder, "CardBorderBrush"),
        (Keys.FloatingToolWindowBackground, "SidebarBrush"),
        (Keys.FloatingToolWindowBorder, "CardBorderBrush"),
        (Keys.NavigatorWindowBackground, "CardBrush"),
        (Keys.NavigatorWindowForeground, "HintBrush"),
        (Keys.NavigatorWindowSelectedBackground, "NavSelectedBrush"),
        (Keys.NavigatorWindowSelectedText, "StrongTextBrush"),

        // Onglets de documents (Dashboard, fichiers) : l'onglet actif est bleu plein, les autres discrets
        (Keys.DocumentWellTabSelectedActiveBackground, "AccentBrush"),
        (Keys.DocumentWellTabSelectedActiveText, "AccentForegroundBrush"),
        (Keys.DocumentWellTabSelectedInactiveBackground, "CardBrush"),
        (Keys.DocumentWellTabSelectedInactiveText, "StrongTextBrush"),
        (Keys.DocumentWellTabUnselectedBackground, "SidebarBrush"),
        (Keys.DocumentWellTabUnselectedText, "HintBrush"),
        (Keys.DocumentWellTabUnselectedHoveredBackground, "CardHoverBrush"),
        (Keys.DocumentWellTabUnselectedHoveredText, "StrongTextBrush"),

        // Bouton de fermeture des onglets de documents
        (Keys.DocumentWellTabButtonSelectedActiveGlyph, "AccentForegroundBrush"),
        (Keys.DocumentWellTabButtonSelectedActiveHoveredBackground, "AccentHoverBrush"),
        (Keys.DocumentWellTabButtonSelectedActiveHoveredBorder, "AccentHoverBrush"),
        (Keys.DocumentWellTabButtonSelectedActiveHoveredGlyph, "AccentForegroundBrush"),
        (Keys.DocumentWellTabButtonSelectedActivePressedBackground, "AccentPressedBrush"),
        (Keys.DocumentWellTabButtonSelectedActivePressedBorder, "AccentPressedBrush"),
        (Keys.DocumentWellTabButtonSelectedActivePressedGlyph, "AccentForegroundBrush"),
        (Keys.DocumentWellTabButtonSelectedInactiveGlyph, "HintBrush"),
        (Keys.DocumentWellTabButtonSelectedInactiveHoveredBackground, "CardHoverBrush"),
        (Keys.DocumentWellTabButtonSelectedInactiveHoveredBorder, "CardHoverBrush"),
        (Keys.DocumentWellTabButtonSelectedInactiveHoveredGlyph, "StrongTextBrush"),
        (Keys.DocumentWellTabButtonSelectedInactivePressedBackground, "BadgeBrush"),
        (Keys.DocumentWellTabButtonSelectedInactivePressedBorder, "BadgeBrush"),
        (Keys.DocumentWellTabButtonSelectedInactivePressedGlyph, "StrongTextBrush"),
        (Keys.DocumentWellTabButtonUnselectedTabHoveredGlyph, "HintBrush"),
        (Keys.DocumentWellTabButtonUnselectedTabHoveredButtonHoveredBackground, "BadgeBrush"),
        (Keys.DocumentWellTabButtonUnselectedTabHoveredButtonHoveredBorder, "BadgeBrush"),
        (Keys.DocumentWellTabButtonUnselectedTabHoveredButtonHoveredGlyph, "StrongTextBrush"),
        (Keys.DocumentWellTabButtonUnselectedTabHoveredButtonPressedBackground, "BadgeBrush"),
        (Keys.DocumentWellTabButtonUnselectedTabHoveredButtonPressedBorder, "BadgeBrush"),
        (Keys.DocumentWellTabButtonUnselectedTabHoveredButtonPressedGlyph, "StrongTextBrush"),

        // Liste déroulante des onglets
        (Keys.DocumentWellOverflowButtonDefaultGlyph, "HintBrush"),
        (Keys.DocumentWellOverflowButtonHoveredBackground, "CardHoverBrush"),
        (Keys.DocumentWellOverflowButtonHoveredBorder, "CardHoverBrush"),
        (Keys.DocumentWellOverflowButtonHoveredGlyph, "StrongTextBrush"),
        (Keys.DocumentWellOverflowButtonPressedBackground, "NavSelectedBrush"),
        (Keys.DocumentWellOverflowButtonPressedBorder, "NavSelectedBrush"),
        (Keys.DocumentWellOverflowButtonPressedGlyph, "StrongTextBrush"),

        // Barres de titre des panneaux : plates, sans poignée pointillée (grip = fond)
        (Keys.ToolWindowCaptionActiveBackground, "NavSelectedBrush"),
        (Keys.ToolWindowCaptionActiveText, "StrongTextBrush"),
        (Keys.ToolWindowCaptionActiveGrip, "NavSelectedBrush"),
        (Keys.ToolWindowCaptionInactiveBackground, "SidebarBrush"),
        (Keys.ToolWindowCaptionInactiveText, "HintBrush"),
        (Keys.ToolWindowCaptionInactiveGrip, "SidebarBrush"),
        (Keys.ToolWindowCaptionButtonActiveGlyph, "StrongTextBrush"),
        (Keys.ToolWindowCaptionButtonActiveHoveredBackground, "CardHoverBrush"),
        (Keys.ToolWindowCaptionButtonActiveHoveredBorder, "CardHoverBrush"),
        (Keys.ToolWindowCaptionButtonActiveHoveredGlyph, "StrongTextBrush"),
        (Keys.ToolWindowCaptionButtonActivePressedBackground, "BadgeBrush"),
        (Keys.ToolWindowCaptionButtonActivePressedBorder, "BadgeBrush"),
        (Keys.ToolWindowCaptionButtonActivePressedGlyph, "StrongTextBrush"),
        (Keys.ToolWindowCaptionButtonInactiveGlyph, "HintBrush"),
        (Keys.ToolWindowCaptionButtonInactiveHoveredBackground, "CardHoverBrush"),
        (Keys.ToolWindowCaptionButtonInactiveHoveredBorder, "CardHoverBrush"),
        (Keys.ToolWindowCaptionButtonInactiveHoveredGlyph, "StrongTextBrush"),
        (Keys.ToolWindowCaptionButtonInactivePressedBackground, "BadgeBrush"),
        (Keys.ToolWindowCaptionButtonInactivePressedBorder, "BadgeBrush"),
        (Keys.ToolWindowCaptionButtonInactivePressedGlyph, "StrongTextBrush"),

        // Onglets des panneaux (Sortie / Diagnostics) : texte bleu pour l'onglet actif
        (Keys.ToolWindowTabSelectedActiveBackground, "SidebarBrush"),
        (Keys.ToolWindowTabSelectedActiveText, "AccentLineBrush"),
        (Keys.ToolWindowTabSelectedInactiveBackground, "SidebarBrush"),
        (Keys.ToolWindowTabSelectedInactiveText, "StrongTextBrush"),
        (Keys.ToolWindowTabUnselectedBackground, "SidebarBrush"),
        (Keys.ToolWindowTabUnselectedText, "HintBrush"),
        (Keys.ToolWindowTabUnselectedHoveredBackground, "CardHoverBrush"),
        (Keys.ToolWindowTabUnselectedHoveredText, "StrongTextBrush"),

        // Panneaux masqués automatiquement (onglets sur le bord)
        (Keys.AutoHideTabDefaultBackground, "SidebarBrush"),
        (Keys.AutoHideTabDefaultBorder, "CardBorderBrush"),
        (Keys.AutoHideTabDefaultText, "HintBrush"),
        (Keys.AutoHideTabHoveredBackground, "CardHoverBrush"),
        (Keys.AutoHideTabHoveredBorder, "AccentLineBrush"),
        (Keys.AutoHideTabHoveredText, "StrongTextBrush"),
    ];
}
