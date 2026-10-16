namespace MineEngine.Core.Theming;

/// <summary>
/// Construit toutes les couleurs d'un thème sombre à partir de quelques teintes : fond, barre
/// latérale, cartes, texte et accent. Les survols, bordures, champs et sélections en sont dérivés
/// par mélange, ce qui garde les thèmes livrés cohérents entre eux.
/// </summary>
public sealed record ThemePalette(RgbColor Background, RgbColor Sidebar, RgbColor Card, RgbColor Text, RgbColor Accent)
{
    public static ThemePalette Parse(string background, string sidebar, string card, string text, string accent) =>
        new(RgbColor.Parse(background), RgbColor.Parse(sidebar), RgbColor.Parse(card), RgbColor.Parse(text), RgbColor.Parse(accent));

    /// <summary>Toutes les couleurs du thème (base sombre).</summary>
    public IReadOnlyDictionary<string, string> ToColors()
    {
        // Couleurs de la console, de l'éditeur de code et du texte sur accent : celles du thème sombre.
        var colors = new Dictionary<string, string>(BuiltInThemes.Dark.Colors, StringComparer.Ordinal);

        RgbColor field = Background.Mix(Card, 0.5);
        RgbColor fieldBorder = Card.Mix(Text, 0.16);
        Set(colors, Background, "HubBackgroundBrush", "ComboBoxDropDownBackground", "MenuFlyoutPresenterBackground", "ToolTipBackground");
        Set(colors, Sidebar, "SidebarBrush", "PreviewBackgroundBrush");
        Set(colors, Card, "CardBrush", "PreviewGridBrush", "ListBoxItemUnselectedBackgroundPointerOverThemeBrush",
            "TreeViewItemBackgroundPointerOver", "TextControlBackgroundPointerOver", "ButtonBackground", "ComboBoxBackgroundPointerOver");
        Set(colors, Card.Mix(Text, 0.06), "CardHoverBrush", "ButtonBackgroundPointerOver");
        Set(colors, Card.Mix(Text, 0.10), "CardBorderBrush", "PreviewBorderBrush");
        Set(colors, Sidebar.Mix(Text, 0.08), "SplitterBrush");
        Set(colors, Card.Mix(Text, 0.08), "BadgeBrush", "TypeBadgeBrush");
        Set(colors, Text.Mix(Background, 0.2), "BadgeTextBrush", "TypeBadgeTextBrush");
        Set(colors, Text, "StrongTextBrush", "ListBoxItemSelectedForegroundThemeBrush");
        Set(colors, Text.Mix(Background, 0.4), "HintBrush");
        Set(colors, Accent.Lighten(0.35), "LinkBrush");
        Set(colors, field, "TextControlBackground", "ComboBoxBackground", "ButtonBackgroundPressed");
        Set(colors, Sidebar.Mix(Background, 0.5), "TextControlBackgroundFocused", "ComboBoxBackgroundFocused");
        Set(colors, fieldBorder, "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "ButtonBorderBrush",
            "ButtonBorderBrushPointerOver", "ComboBoxBorderBrush", "ComboBoxBorderBrushPointerOver");
        Set(colors, Background.Mix(Text, 0.04), "CodeCurrentLineBrush");
        Set(colors, Background.Mix(Accent, 0.45), "CodeSelectionBrush");
        Set(colors, Background.Mix(new RgbColor(0xFF, 0x40, 0x40), 0.15), "CodeErrorLineBrush");
        return AccentColors.Apply(colors, Accent, ThemeBase.Dark);
    }

    private static void Set(Dictionary<string, string> colors, RgbColor color, params string[] keys)
    {
        foreach (string key in keys)
        {
            colors[key] = color.ToString();
        }
    }
}
