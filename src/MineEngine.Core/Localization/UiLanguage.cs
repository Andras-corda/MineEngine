namespace MineEngine.Core.Localization;

/// <summary>
/// Langue de l'interface de Mine Engine. Les langues livrées sont intégrées à MineEngine.Core ;
/// une langue importée par l'utilisateur garde le chemin de son fichier de traduction.
/// </summary>
public sealed record UiLanguage(string Code, string NativeName, string CultureName, string? FilePath = null)
{
    /// <summary>Anglais : la langue par défaut de l'interface.</summary>
    public static UiLanguage English { get; } = new("en", "English", "en-US");

    /// <summary>Français : la langue dans laquelle les textes sont écrits dans le code.</summary>
    public static UiLanguage French { get; } = new("fr", "Français", "fr-FR");

    public static UiLanguage Default => English;

    /// <summary>Langues livrées avec Mine Engine.</summary>
    public static IReadOnlyList<UiLanguage> All { get; } = [English, French];

    /// <summary>Vrai pour la langue des textes du code, qui n'a pas besoin de traduction.</summary>
    public bool IsSource => Code == French.Code && IsBuiltIn;

    /// <summary>Vrai pour une langue livrée, faux pour une langue importée.</summary>
    public bool IsBuiltIn => FilePath is null;

    /// <summary>Langue livrée d'après son code ("en", "fr") ; la langue par défaut si le code est inconnu.</summary>
    public static UiLanguage FromCode(string? code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) ?? Default;

    public override string ToString() => NativeName;
}
