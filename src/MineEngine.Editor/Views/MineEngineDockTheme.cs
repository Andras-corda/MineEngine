using System.Windows;
using System.Windows.Media;
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
    public MineEngineDockTheme(bool isDark, Func<string, Brush> palette)
        : base(Build(isDark, palette ?? throw new ArgumentNullException(nameof(palette))))
    {
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

        dictionary[Keys.PreviewBoxBackgroundBrushKey] = Translucent(palette("AccentLineBrush"), 0x40);
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

        // Onglets de documents (Dashboard, fichiers) : sélection lilas comme la barre latérale de l'accueil
        (Keys.DocumentWellTabSelectedActiveBackground, "NavSelectedBrush"),
        (Keys.DocumentWellTabSelectedActiveText, "StrongTextBrush"),
        (Keys.DocumentWellTabSelectedInactiveBackground, "CardBrush"),
        (Keys.DocumentWellTabSelectedInactiveText, "StrongTextBrush"),
        (Keys.DocumentWellTabUnselectedBackground, "SidebarBrush"),
        (Keys.DocumentWellTabUnselectedText, "HintBrush"),
        (Keys.DocumentWellTabUnselectedHoveredBackground, "CardHoverBrush"),
        (Keys.DocumentWellTabUnselectedHoveredText, "StrongTextBrush"),

        // Bouton de fermeture des onglets de documents
        (Keys.DocumentWellTabButtonSelectedActiveGlyph, "BadgeTextBrush"),
        (Keys.DocumentWellTabButtonSelectedActiveHoveredBackground, "CardHoverBrush"),
        (Keys.DocumentWellTabButtonSelectedActiveHoveredBorder, "CardHoverBrush"),
        (Keys.DocumentWellTabButtonSelectedActiveHoveredGlyph, "StrongTextBrush"),
        (Keys.DocumentWellTabButtonSelectedActivePressedBackground, "BadgeBrush"),
        (Keys.DocumentWellTabButtonSelectedActivePressedBorder, "BadgeBrush"),
        (Keys.DocumentWellTabButtonSelectedActivePressedGlyph, "StrongTextBrush"),
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

        // Onglets des panneaux (Sortie / Diagnostics) : texte lilas pour l'onglet actif
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
