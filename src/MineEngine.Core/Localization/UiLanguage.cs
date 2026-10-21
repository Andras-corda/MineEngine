namespace MineEngine.Core.Localization;

/// <summary>
/// Interface language of Mine Engine. Shipped languages are embedded in MineEngine.Core; a
/// language imported by the user keeps the path of its translation file.
/// </summary>
public sealed record UiLanguage(string Code, string NativeName, string CultureName, string? FilePath = null)
{
    /// <summary>English: the default interface language, in which the texts are written in the code.</summary>
    public static UiLanguage English { get; } = new("en", "English", "en-US");

    /// <summary>French: translated by Localization/fr.json.</summary>
    public static UiLanguage French { get; } = new("fr", "Français", "fr-FR");

    public static UiLanguage Default => English;

    /// <summary>Languages shipped with Mine Engine.</summary>
    public static IReadOnlyList<UiLanguage> All { get; } = [English, French];

    /// <summary>True for the language of the texts in the code, which needs no translation.</summary>
    public bool IsSource => Code == English.Code && IsBuiltIn;

    /// <summary>True for a shipped language, false for an imported one.</summary>
    public bool IsBuiltIn => FilePath is null;

    /// <summary>Shipped language from its code ("en", "fr"); the default language when the code is unknown.</summary>
    public static UiLanguage FromCode(string? code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) ?? Default;

    public override string ToString() => NativeName;
}
