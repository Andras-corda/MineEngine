using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace MineEngine.Project.Localization;

/// <summary>
/// Langues du mod (fichiers assets/&lt;mod&gt;/lang/&lt;langue&gt;.json de Minecraft) et traductions
/// saisies par l'utilisateur. Un texte non traduit garde le nom saisi sur l'élément ; une
/// traduction remplace ce texte dans une langue. Immuable : chaque modification produit une
/// nouvelle valeur (l'historique garde l'ancienne).
/// </summary>
public sealed partial class ModLocalization
{
    /// <summary>Langues d'un nouveau projet : anglais et français.</summary>
    public static IReadOnlyList<string> DefaultLocales { get; } = ["en_us", "fr_fr"];

    public static ModLocalization Default { get; } = new(DefaultLocales, ImmutableDictionary<string, ImmutableDictionary<string, string>>.Empty);

    public ModLocalization(IEnumerable<string> locales, IReadOnlyDictionary<string, ImmutableDictionary<string, string>> translations)
    {
        ArgumentNullException.ThrowIfNull(locales);
        ArgumentNullException.ThrowIfNull(translations);
        Locales = [.. locales.Select(Normalize).Where(IsValidLocale).Distinct()];
        Translations = translations
            .Where(t => Locales.Contains(Normalize(t.Key)) && t.Value.Count > 0)
            .ToImmutableDictionary(t => Normalize(t.Key), t => t.Value);
    }

    /// <summary>Langues produites, dans l'ordre d'affichage.</summary>
    public ImmutableArray<string> Locales { get; }

    /// <summary>Traductions par langue : clé de traduction Minecraft vers texte.</summary>
    public ImmutableDictionary<string, ImmutableDictionary<string, string>> Translations { get; }

    /// <summary>Code de langue Minecraft : "en_us", "fr_fr", "pt_br", "zh_cn"...</summary>
    public static bool IsValidLocale(string? code) => code is not null && LocalePattern().IsMatch(code);

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');

    /// <summary>Traductions d'une langue (vide si aucune).</summary>
    public ImmutableDictionary<string, string> TranslationsFor(string locale) =>
        Translations.GetValueOrDefault(Normalize(locale)) ?? ImmutableDictionary<string, string>.Empty;

    /// <summary>Texte saisi pour une clé dans une langue, ou null.</summary>
    public string? Find(string locale, string key) => TranslationsFor(locale).GetValueOrDefault(key);

    /// <summary>Traduction d'une clé ; un texte vide retire la traduction (le texte par défaut revient).</summary>
    public ModLocalization WithTranslation(string locale, string key, string? text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        locale = Normalize(locale);
        ImmutableDictionary<string, string> current = TranslationsFor(locale);
        ImmutableDictionary<string, string> updated = string.IsNullOrEmpty(text) ? current.Remove(key) : current.SetItem(key, text);
        return new ModLocalization(Locales, Translations.SetItem(locale, updated));
    }

    /// <summary>Ajoute ou complète les traductions d'une langue (import d'un fichier) ; la langue est ajoutée si besoin.</summary>
    public ModLocalization WithTranslations(string locale, IEnumerable<KeyValuePair<string, string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        locale = Normalize(locale);
        ImmutableDictionary<string, string> updated = TranslationsFor(locale)
            .SetItems(entries.Where(e => !string.IsNullOrWhiteSpace(e.Key) && !string.IsNullOrEmpty(e.Value)));
        return new ModLocalization(Locales.Contains(locale) ? Locales : Locales.Add(locale), Translations.SetItem(locale, updated));
    }

    public ModLocalization WithLocale(string locale)
    {
        locale = Normalize(locale);
        if (!IsValidLocale(locale))
        {
            throw new ArgumentException(Tr.F("Code de langue invalide : « {0} » (exemples : de_de, es_es, pt_br).", locale), nameof(locale));
        }

        return Locales.Contains(locale) ? this : new ModLocalization(Locales.Add(locale), Translations);
    }

    /// <summary>Retire une langue et ses traductions.</summary>
    public ModLocalization WithoutLocale(string locale)
    {
        locale = Normalize(locale);
        return new ModLocalization(Locales.Remove(locale), Translations.Remove(locale));
    }

    [GeneratedRegex("^[a-z]{2,3}_[a-z0-9]{2,4}$")]
    private static partial Regex LocalePattern();
}
