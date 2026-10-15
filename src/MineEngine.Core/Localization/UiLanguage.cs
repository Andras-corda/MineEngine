namespace MineEngine.Core.Localization;

/// <summary>Langue de l'interface de Mine Engine.</summary>
public sealed record UiLanguage(string Code, string NativeName, string CultureName)
{
    /// <summary>Anglais : la langue par défaut de l'interface.</summary>
    public static UiLanguage English { get; } = new("en", "English", "en-US");

    /// <summary>Français : la langue dans laquelle les textes sont écrits dans le code.</summary>
    public static UiLanguage French { get; } = new("fr", "Français", "fr-FR");

    public static UiLanguage Default => English;

    public static IReadOnlyList<UiLanguage> All { get; } = [English, French];

    /// <summary>Vrai pour la langue des textes du code, qui n'a pas besoin de traduction.</summary>
    public bool IsSource => Code == French.Code;

    /// <summary>Langue d'après son code ("en", "fr") ; la langue par défaut si le code est inconnu.</summary>
    public static UiLanguage FromCode(string? code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) ?? Default;

    public override string ToString() => NativeName;
}
