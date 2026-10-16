namespace MineEngine.Core.Theming;

/// <summary>Famille d'un thème : elle choisit l'habillage clair ou sombre des contrôles standard.</summary>
public enum ThemeBase
{
    Dark,
    Light,
}

/// <summary>
/// Thème de l'interface : un nom, une base (clair ou sombre) et des couleurs. Les couleurs qu'il ne
/// définit pas sont reprises du thème complet de sa base (<see cref="BuiltInThemes.BaseOf"/>).
/// </summary>
public sealed record ThemeDefinition(string Id, string Name, ThemeBase Base, IReadOnlyDictionary<string, string> Colors, bool IsBuiltIn = false)
{
    public bool IsDark => Base == ThemeBase.Dark;

    /// <summary>Nom affiché : traduit pour les thèmes livrés, tel quel pour les thèmes importés.</summary>
    public string DisplayName => IsBuiltIn ? Tr.T(Name) : Name;

    /// <summary>Toutes les couleurs du thème, complétées par celles de sa base.</summary>
    public IReadOnlyDictionary<string, string> Resolve()
    {
        var colors = new Dictionary<string, string>(BuiltInThemes.BaseOf(Base).Colors, StringComparer.Ordinal);
        foreach ((string key, string value) in Colors)
        {
            colors[key] = value;
        }

        return colors;
    }

    /// <summary>Couleurs complètes, avec la couleur d'accent donnée à la place de celle du thème.</summary>
    public IReadOnlyDictionary<string, string> Resolve(RgbColor? accent)
    {
        IReadOnlyDictionary<string, string> colors = Resolve();
        return accent is { } color ? AccentColors.Apply(colors, color, Base) : colors;
    }

    public override string ToString() => DisplayName;
}
