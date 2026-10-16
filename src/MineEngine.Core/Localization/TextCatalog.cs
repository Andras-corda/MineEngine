using System.Reflection;
using System.Text.Json;

namespace MineEngine.Core.Localization;

/// <summary>
/// Traductions d'une langue de l'interface. Les textes sont écrits en français dans le code
/// (langue source) ; chaque autre langue fournit un fichier Localization/&lt;code&gt;.json intégré
/// à MineEngine.Core, qui associe chaque texte français à sa traduction. Un texte absent du
/// fichier reste en français. Une langue importée complète l'anglais : un texte qu'elle ne
/// traduit pas s'affiche en anglais, puis en français s'il n'existe pas non plus en anglais.
/// </summary>
public sealed class TextCatalog
{
    private readonly IReadOnlyDictionary<string, string> _translations;

    private TextCatalog(UiLanguage language, IReadOnlyDictionary<string, string> translations)
    {
        Language = language;
        _translations = translations;
    }

    /// <summary>Langue source : les textes sont rendus tels quels.</summary>
    public static TextCatalog Source { get; } = new(UiLanguage.French, new Dictionary<string, string>());

    public UiLanguage Language { get; }

    /// <summary>Textes source qui ont une traduction dans cette langue.</summary>
    public IEnumerable<string> TranslatedTexts => _translations.Keys;

    /// <summary>Nombre de textes source traduits.</summary>
    public int Count => _translations.Count;

    /// <summary>Catalogue d'une langue ; la langue source n'a pas de fichier.</summary>
    public static TextCatalog For(UiLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (language.IsSource)
        {
            return Source;
        }

        if (!language.IsBuiltIn)
        {
            LanguageFile file = LanguageFileSerializer.Read(File.ReadAllText(language.FilePath!), Path.GetFileName(language.FilePath!));
            var merged = new Dictionary<string, string>(Embedded(UiLanguage.English.Code), StringComparer.Ordinal);
            foreach ((string text, string translation) in file.Translations)
            {
                merged[text] = translation;
            }

            return new TextCatalog(language, merged);
        }

        return new TextCatalog(language, Embedded(language.Code));
    }

    public bool Contains(string text) => _translations.ContainsKey(text);

    /// <summary>Traduction d'un texte source, ou le texte lui-même s'il n'est pas traduit.</summary>
    public string Translate(string text) =>
        text is not null && _translations.TryGetValue(text, out string? translation) ? translation : text!;

    private static Dictionary<string, string> Embedded(string code)
    {
        string resource = $"MineEngine.Core.Localization.{code}.json";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Translation file '{resource}' is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Translation file '{resource}' is empty.");
    }
}
