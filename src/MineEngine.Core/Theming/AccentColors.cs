namespace MineEngine.Core.Theming;

/// <summary>Couleur d'accent proposée dans les paramètres.</summary>
public sealed record AccentPreset(string Name, RgbColor Color)
{
    public string DisplayName => Tr.T(Name);

    public override string ToString() => DisplayName;
}

/// <summary>
/// Couleur d'accent : celle des boutons principaux, de l'onglet actif et des sélections. Choisir
/// un accent recalcule toutes les couleurs qui en dérivent, à partir du fond du thème.
/// </summary>
public static class AccentColors
{
    /// <summary>Bleu de Mine Engine, l'accent des thèmes livrés.</summary>
    public static RgbColor Default { get; } = new(0x2F, 0x7F, 0xD8);

    public static IReadOnlyList<AccentPreset> Presets { get; } =
    [
        new("Blue", Default),
        new("Purple", new RgbColor(0x8B, 0x5C, 0xF6)),
        new("Green", new RgbColor(0x2E, 0xA0, 0x5A)),
        new("Orange", new RgbColor(0xE0, 0x7B, 0x1F)),
        new("Red", new RgbColor(0xD9, 0x45, 0x45)),
        new("Pink", new RgbColor(0xD9, 0x46, 0x8F)),
        new("Cyan", new RgbColor(0x1C, 0x9C, 0xB0)),
    ];

    /// <summary>Copie des couleurs d'un thème où toutes les couleurs d'accent viennent de <paramref name="accent"/>.</summary>
    public static IReadOnlyDictionary<string, string> Apply(IReadOnlyDictionary<string, string> colors, RgbColor accent, ThemeBase themeBase)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var result = new Dictionary<string, string>(colors, StringComparer.Ordinal);
        bool dark = themeBase == ThemeBase.Dark;

        // Les sélections sont un mélange de l'accent et du fond, plus marqué au survol puis au clic.
        string backgroundKey = dark ? "SidebarBrush" : "CardBrush";
        RgbColor background = colors.TryGetValue(backgroundKey, out string? text) && RgbColor.TryParse(text, out RgbColor parsed)
            ? parsed
            : dark ? new RgbColor(0x1E, 0x1F, 0x22) : new RgbColor(0xFF, 0xFF, 0xFF);
        (double card, double nav, double hover, double pressed) = dark ? (0.22, 0.30, 0.40, 0.50) : (0.10, 0.17, 0.23, 0.31);

        RgbColor light = accent.Lighten(0.12);
        RgbColor line = dark ? light : accent;
        RgbColor darker = accent.Darken(0.15);

        Set(result, accent, "AccentBrush", "AccentButtonBackground");
        Set(result, light, "AccentHoverBrush", "AccentButtonBackgroundPointerOver");
        Set(result, line, "AccentLineBrush", "CardSelectedBorderBrush");
        Set(result, darker, "AccentPressedBrush", "AccentButtonBackgroundPressed");
        Set(result, background.Mix(accent, card), "CardSelectedBrush");
        Set(result, background.Mix(accent, nav), "NavSelectedBrush", "ListBoxItemSelectedBackgroundThemeBrush", "TreeViewItemBackgroundSelected");
        Set(result, background.Mix(accent, hover), "ListBoxItemSelectedBackgroundPointerOverThemeBrush", "TreeViewItemBackgroundSelectedPointerOver");
        Set(result, background.Mix(accent, pressed), "TreeViewItemBackgroundSelectedPressed");
        return result;
    }

    /// <summary>Accent d'un thème : sa couleur AccentBrush, ou le bleu de Mine Engine.</summary>
    public static RgbColor Of(IReadOnlyDictionary<string, string> colors) =>
        colors.TryGetValue("AccentBrush", out string? text) && RgbColor.TryParse(text, out RgbColor color) ? color : Default;

    private static void Set(Dictionary<string, string> colors, RgbColor color, params string[] keys)
    {
        foreach (string key in keys)
        {
            colors[key] = color.ToString();
        }
    }
}
