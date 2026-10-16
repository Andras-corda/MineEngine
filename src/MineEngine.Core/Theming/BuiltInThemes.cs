namespace MineEngine.Core.Theming;

/// <summary>
/// Thèmes livrés avec Mine Engine. Les thèmes Sombre et Clair définissent toutes les couleurs
/// (<see cref="ThemeColorKeys"/>) ; les autres ne donnent que ce qui change par rapport à leur base.
/// </summary>
public static class BuiltInThemes
{
    public const string DarkId = "dark";
    public const string LightId = "light";

    /// <summary>Gris neutres contrastés, accent bleu (thème par défaut en sombre).</summary>
    public static ThemeDefinition Dark { get; } = new(DarkId, "Sombre", ThemeBase.Dark, new Dictionary<string, string>
        {
            ["HubBackgroundBrush"] = "#2B2C30",
            ["SidebarBrush"] = "#1E1F22",
            ["CardBrush"] = "#34363B",
            ["CardHoverBrush"] = "#3B3D43",
            ["CardBorderBrush"] = "#3E4046",
            ["CardSelectedBrush"] = "#22344C",
            ["CardSelectedBorderBrush"] = "#3B8EEA",
            ["NavSelectedBrush"] = "#233C5C",
            ["AccentLineBrush"] = "#3B8EEA",
            ["BadgeBrush"] = "#3A3C42",
            ["BadgeTextBrush"] = "#C3C6CD",
            ["LinkBrush"] = "#6CB6FF",
            ["PreviewGridBrush"] = "#34363B",
            ["StrongTextBrush"] = "#F3F4F6",
            ["HintBrush"] = "#9C9FA6",
            ["TypeBadgeBrush"] = "#3A3C42",
            ["TypeBadgeTextBrush"] = "#C3C6CD",
            ["PreviewBackgroundBrush"] = "#1E1F22",
            ["PreviewBorderBrush"] = "#3E4046",
            ["SplitterBrush"] = "#2E3034",
            ["LogDebugBrush"] = "#8A8A94",
            ["LogSuccessBrush"] = "#4CC37A",
            ["LogWarningBrush"] = "#E0A93A",
            ["LogErrorBrush"] = "#FF6B6B",
            ["ListBoxItemUnselectedBackgroundPointerOverThemeBrush"] = "#34363B",
            ["ListBoxItemSelectedBackgroundThemeBrush"] = "#233C5C",
            ["ListBoxItemSelectedBackgroundPointerOverThemeBrush"] = "#29466B",
            ["ListBoxItemSelectedForegroundThemeBrush"] = "#F3F4F6",
            ["TreeViewItemBackgroundPointerOver"] = "#34363B",
            ["TreeViewItemBackgroundSelected"] = "#233C5C",
            ["TreeViewItemBackgroundSelectedPointerOver"] = "#29466B",
            ["TreeViewItemBackgroundSelectedPressed"] = "#2A5080",
            ["AccentBrush"] = "#2F7FD8",
            ["AccentHoverBrush"] = "#3B8EEA",
            ["AccentPressedBrush"] = "#2668B3",
            ["AccentForegroundBrush"] = "#FFFFFF",
            ["AccentButtonBackground"] = "#2F7FD8",
            ["AccentButtonBackgroundPointerOver"] = "#3B8EEA",
            ["AccentButtonBackgroundPressed"] = "#2668B3",
            ["AccentButtonForeground"] = "#FFFFFF",
            ["AccentButtonForegroundPointerOver"] = "#FFFFFF",
            ["AccentButtonForegroundPressed"] = "#E6EEF8",
            ["TextControlBackground"] = "#2E3035",
            ["TextControlBackgroundPointerOver"] = "#34363B",
            ["TextControlBackgroundFocused"] = "#26272B",
            ["TextControlBorderBrush"] = "#44464D",
            ["TextControlBorderBrushPointerOver"] = "#44464D",
            ["ButtonBackground"] = "#34363B",
            ["ButtonBackgroundPointerOver"] = "#3D3F45",
            ["ButtonBackgroundPressed"] = "#2E3035",
            ["ButtonBorderBrush"] = "#44464D",
            ["ButtonBorderBrushPointerOver"] = "#44464D",
            ["ComboBoxBackground"] = "#2E3035",
            ["ComboBoxBackgroundPointerOver"] = "#34363B",
            ["ComboBoxBackgroundFocused"] = "#26272B",
            ["ComboBoxBorderBrush"] = "#44464D",
            ["ComboBoxBorderBrushPointerOver"] = "#44464D",
            ["ComboBoxDropDownBackground"] = "#2B2C30",
            ["MenuFlyoutPresenterBackground"] = "#2B2C30",
            ["ToolTipBackground"] = "#2B2C30",
            ["CodeCurrentLineBrush"] = "#303237",
            ["CodeSelectionBrush"] = "#264F78",
            ["CodeErrorLineBrush"] = "#3A1F24",
        }, IsBuiltIn: true);

    /// <summary>Gris clairs, accent bleu.</summary>
    public static ThemeDefinition Light { get; } = new(LightId, "Clair", ThemeBase.Light, new Dictionary<string, string>
        {
            ["HubBackgroundBrush"] = "#F3F4F6",
            ["SidebarBrush"] = "#E6E7EA",
            ["CardBrush"] = "#FFFFFF",
            ["CardHoverBrush"] = "#F0F1F4",
            ["CardBorderBrush"] = "#D9DBE0",
            ["CardSelectedBrush"] = "#E8F1FC",
            ["CardSelectedBorderBrush"] = "#2F7FD8",
            ["NavSelectedBrush"] = "#DCE9FA",
            ["AccentLineBrush"] = "#2F7FD8",
            ["BadgeBrush"] = "#E6E7EA",
            ["BadgeTextBrush"] = "#4A4D55",
            ["LinkBrush"] = "#1F66C1",
            ["PreviewGridBrush"] = "#E2E4E8",
            ["StrongTextBrush"] = "#1B1C1F",
            ["HintBrush"] = "#5F626A",
            ["TypeBadgeBrush"] = "#E6E7EA",
            ["TypeBadgeTextBrush"] = "#4A4D55",
            ["PreviewBackgroundBrush"] = "#E6E7EA",
            ["PreviewBorderBrush"] = "#D9DBE0",
            ["SplitterBrush"] = "#D9DBE0",
            ["LogDebugBrush"] = "#7A7A86",
            ["LogSuccessBrush"] = "#1E8449",
            ["LogWarningBrush"] = "#9A6200",
            ["LogErrorBrush"] = "#C0392B",
            ["ListBoxItemUnselectedBackgroundPointerOverThemeBrush"] = "#F0F1F4",
            ["ListBoxItemSelectedBackgroundThemeBrush"] = "#DCE9FA",
            ["ListBoxItemSelectedBackgroundPointerOverThemeBrush"] = "#CFE1F8",
            ["ListBoxItemSelectedForegroundThemeBrush"] = "#1B1C1F",
            ["TreeViewItemBackgroundPointerOver"] = "#F0F1F4",
            ["TreeViewItemBackgroundSelected"] = "#DCE9FA",
            ["TreeViewItemBackgroundSelectedPointerOver"] = "#CFE1F8",
            ["TreeViewItemBackgroundSelectedPressed"] = "#BFD7F5",
            ["AccentBrush"] = "#2F7FD8",
            ["AccentHoverBrush"] = "#3B8EEA",
            ["AccentPressedBrush"] = "#2668B3",
            ["AccentForegroundBrush"] = "#FFFFFF",
            ["AccentButtonBackground"] = "#2F7FD8",
            ["AccentButtonBackgroundPointerOver"] = "#3B8EEA",
            ["AccentButtonBackgroundPressed"] = "#2668B3",
            ["AccentButtonForeground"] = "#FFFFFF",
            ["AccentButtonForegroundPointerOver"] = "#FFFFFF",
            ["AccentButtonForegroundPressed"] = "#E6EEF8",
            ["TextControlBackground"] = "#FFFFFF",
            ["TextControlBackgroundPointerOver"] = "#F7F8FA",
            ["TextControlBackgroundFocused"] = "#FFFFFF",
            ["TextControlBorderBrush"] = "#CFD2D8",
            ["TextControlBorderBrushPointerOver"] = "#CFD2D8",
            ["ButtonBackground"] = "#FFFFFF",
            ["ButtonBackgroundPointerOver"] = "#F0F1F4",
            ["ButtonBackgroundPressed"] = "#E6E7EA",
            ["ButtonBorderBrush"] = "#CFD2D8",
            ["ButtonBorderBrushPointerOver"] = "#CFD2D8",
            ["ComboBoxBackground"] = "#FFFFFF",
            ["ComboBoxBackgroundPointerOver"] = "#F7F8FA",
            ["ComboBoxBackgroundFocused"] = "#FFFFFF",
            ["ComboBoxBorderBrush"] = "#CFD2D8",
            ["ComboBoxBorderBrushPointerOver"] = "#CFD2D8",
            ["ComboBoxDropDownBackground"] = "#FFFFFF",
            ["MenuFlyoutPresenterBackground"] = "#FFFFFF",
            ["ToolTipBackground"] = "#FFFFFF",
            ["CodeCurrentLineBrush"] = "#F0F1F4",
            ["CodeSelectionBrush"] = "#ADD6FF",
            ["CodeErrorLineBrush"] = "#FBE3E4",
        }, IsBuiltIn: true);

    // Palettes de couleurs (quatre teintes chacune) : fond le plus sombre, cartes, accent.

    /// <summary>Bleus nuit : #0D1433, #171F55, #274272, #6C90C3.</summary>
    public static ThemeDefinition Blue { get; } = FromPalette("blue", "Bleu nuit",
        ThemePalette.Parse("#0D1433", "#0A0F28", "#171F55", "#E8EDF8", "#49699A"));

    /// <summary>Rouges carmin : #19171B, #75020F, #51080D, #2B0307.</summary>
    public static ThemeDefinition Red { get; } = FromPalette("red", "Rouge carmin",
        ThemePalette.Parse("#19171B", "#121014", "#271318", "#F2E8EA", "#75020F"));

    /// <summary>Verts forêt : #022B22, #275D46, #569578, #101C13.</summary>
    public static ThemeDefinition Green { get; } = FromPalette("green", "Vert forêt",
        ThemePalette.Parse("#09231A", "#101C13", "#0B372B", "#E6F2EC", "#3E795F"));

    /// <summary>Or et bruns : #19171B, #9E8123, #2F2921, #563A17.</summary>
    public static ThemeDefinition Gold { get; } = FromPalette("gold", "Or",
        ThemePalette.Parse("#19171B", "#121014", "#2F2921", "#F2EEE4", "#9E8123"));

    /// <summary>Violets et ardoise : #1E202C, #60519B, #31323E, #BFC0D1.</summary>
    public static ThemeDefinition Purple { get; } = FromPalette("purple", "Violet",
        ThemePalette.Parse("#1E202C", "#17181F", "#292A37", "#ECECF4", "#60519B"));

    /// <summary>Fonds noirs, textes blancs et bordures marquées.</summary>
    public static ThemeDefinition HighContrast { get; } = new("contrast", "Contraste élevé", ThemeBase.Dark, new Dictionary<string, string>
        {
            ["HubBackgroundBrush"] = "#000000",
            ["SidebarBrush"] = "#0A0A0A",
            ["CardBrush"] = "#141414",
            ["CardHoverBrush"] = "#262626",
            ["CardBorderBrush"] = "#8C8C8C",
            ["CardSelectedBrush"] = "#0F2A4D",
            ["NavSelectedBrush"] = "#0F2A4D",
            ["BadgeBrush"] = "#262626",
            ["BadgeTextBrush"] = "#FFFFFF",
            ["PreviewGridBrush"] = "#3A3A3A",
            ["StrongTextBrush"] = "#FFFFFF",
            ["HintBrush"] = "#D6D6D6",
            ["TypeBadgeBrush"] = "#262626",
            ["TypeBadgeTextBrush"] = "#FFFFFF",
            ["PreviewBackgroundBrush"] = "#0A0A0A",
            ["PreviewBorderBrush"] = "#8C8C8C",
            ["SplitterBrush"] = "#8C8C8C",
            ["LinkBrush"] = "#8FC8FF",
            ["ListBoxItemUnselectedBackgroundPointerOverThemeBrush"] = "#262626",
            ["ListBoxItemSelectedBackgroundThemeBrush"] = "#0F2A4D",
            ["ListBoxItemSelectedBackgroundPointerOverThemeBrush"] = "#16396A",
            ["ListBoxItemSelectedForegroundThemeBrush"] = "#FFFFFF",
            ["TreeViewItemBackgroundPointerOver"] = "#262626",
            ["TreeViewItemBackgroundSelected"] = "#0F2A4D",
            ["TreeViewItemBackgroundSelectedPointerOver"] = "#16396A",
            ["TextControlBackground"] = "#000000",
            ["TextControlBackgroundPointerOver"] = "#141414",
            ["TextControlBackgroundFocused"] = "#000000",
            ["TextControlBorderBrush"] = "#B3B3B3",
            ["TextControlBorderBrushPointerOver"] = "#FFFFFF",
            ["ButtonBackground"] = "#141414",
            ["ButtonBackgroundPointerOver"] = "#262626",
            ["ButtonBackgroundPressed"] = "#000000",
            ["ButtonBorderBrush"] = "#B3B3B3",
            ["ButtonBorderBrushPointerOver"] = "#FFFFFF",
            ["ComboBoxBackground"] = "#000000",
            ["ComboBoxBackgroundPointerOver"] = "#141414",
            ["ComboBoxBackgroundFocused"] = "#000000",
            ["ComboBoxBorderBrush"] = "#B3B3B3",
            ["ComboBoxBorderBrushPointerOver"] = "#FFFFFF",
            ["ComboBoxDropDownBackground"] = "#0A0A0A",
            ["MenuFlyoutPresenterBackground"] = "#0A0A0A",
            ["ToolTipBackground"] = "#0A0A0A",
            ["CodeCurrentLineBrush"] = "#1A1A1A",
            ["LogDebugBrush"] = "#BDBDBD",
            ["LogWarningBrush"] = "#FFD166",
            ["LogErrorBrush"] = "#FF8080",
        }, IsBuiltIn: true);

    public static IReadOnlyList<ThemeDefinition> All { get; } = [Dark, Light, Blue, Red, Green, Gold, Purple, HighContrast];

    private static ThemeDefinition FromPalette(string id, string name, ThemePalette palette) =>
        new(id, name, ThemeBase.Dark, palette.ToColors(), IsBuiltIn: true);

    /// <summary>Thème complet qui sert de base : Sombre ou Clair.</summary>
    public static ThemeDefinition BaseOf(ThemeBase themeBase) => themeBase == ThemeBase.Light ? Light : Dark;
}
