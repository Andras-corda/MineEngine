namespace MineEngine.Core.Theming;

/// <summary>
/// Couleurs qu'un thème peut définir : ce sont les clés des pinceaux utilisés par les vues
/// (DynamicResource). Un fichier de thème les reprend telles quelles.
/// </summary>
public static class ThemeColorKeys
{
    public static IReadOnlyList<string> All { get; } =
    [
        "HubBackgroundBrush",
        "SidebarBrush",
        "CardBrush",
        "CardHoverBrush",
        "CardBorderBrush",
        "CardSelectedBrush",
        "CardSelectedBorderBrush",
        "NavSelectedBrush",
        "AccentLineBrush",
        "BadgeBrush",
        "BadgeTextBrush",
        "LinkBrush",
        "PreviewGridBrush",
        "StrongTextBrush",
        "HintBrush",
        "TypeBadgeBrush",
        "TypeBadgeTextBrush",
        "PreviewBackgroundBrush",
        "PreviewBorderBrush",
        "SplitterBrush",
        "LogDebugBrush",
        "LogSuccessBrush",
        "LogWarningBrush",
        "LogErrorBrush",
        "ListBoxItemUnselectedBackgroundPointerOverThemeBrush",
        "ListBoxItemSelectedBackgroundThemeBrush",
        "ListBoxItemSelectedBackgroundPointerOverThemeBrush",
        "ListBoxItemSelectedForegroundThemeBrush",
        "TreeViewItemBackgroundPointerOver",
        "TreeViewItemBackgroundSelected",
        "TreeViewItemBackgroundSelectedPointerOver",
        "TreeViewItemBackgroundSelectedPressed",
        "AccentBrush",
        "AccentHoverBrush",
        "AccentPressedBrush",
        "AccentForegroundBrush",
        "AccentButtonBackground",
        "AccentButtonBackgroundPointerOver",
        "AccentButtonBackgroundPressed",
        "AccentButtonForeground",
        "AccentButtonForegroundPointerOver",
        "AccentButtonForegroundPressed",
        "TextControlBackground",
        "TextControlBackgroundPointerOver",
        "TextControlBackgroundFocused",
        "TextControlBorderBrush",
        "TextControlBorderBrushPointerOver",
        "ButtonBackground",
        "ButtonBackgroundPointerOver",
        "ButtonBackgroundPressed",
        "ButtonBorderBrush",
        "ButtonBorderBrushPointerOver",
        "ComboBoxBackground",
        "ComboBoxBackgroundPointerOver",
        "ComboBoxBackgroundFocused",
        "ComboBoxBorderBrush",
        "ComboBoxBorderBrushPointerOver",
        "ComboBoxDropDownBackground",
        "MenuFlyoutPresenterBackground",
        "ToolTipBackground",
        "CodeCurrentLineBrush",
        "CodeSelectionBrush",
        "CodeErrorLineBrush",
    ];

    /// <summary>Couleurs qui viennent de la couleur d'accent (voir <see cref="AccentColors"/>).</summary>
    public static IReadOnlyList<string> Accent { get; } =
    [
        "AccentBrush", "AccentHoverBrush", "AccentPressedBrush", "AccentLineBrush", "CardSelectedBorderBrush",
        "AccentButtonBackground", "AccentButtonBackgroundPointerOver", "AccentButtonBackgroundPressed",
        "CardSelectedBrush", "NavSelectedBrush", "ListBoxItemSelectedBackgroundThemeBrush",
        "ListBoxItemSelectedBackgroundPointerOverThemeBrush", "TreeViewItemBackgroundSelected",
        "TreeViewItemBackgroundSelectedPointerOver", "TreeViewItemBackgroundSelectedPressed",
    ];

    public static bool IsKnown(string key) => All.Contains(key, StringComparer.Ordinal);
}
