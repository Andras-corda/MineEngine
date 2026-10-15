using System.Reflection;
using System.Text.Json;

namespace MineEngine.Core.Localization;

/// <summary>
/// Traductions d'une langue de l'interface. Les textes sont écrits en français dans le code
/// (langue source) ; chaque autre langue fournit un fichier Localization/&lt;code&gt;.json intégré
/// à MineEngine.Core, qui associe chaque texte français à sa traduction. Un texte absent du
/// fichier reste en français.
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

    /// <summary>Catalogue d'une langue ; la langue source n'a pas de fichier.</summary>
    public static TextCatalog For(UiLanguage language)
    {
        if (language.IsSource)
        {
            return Source;
        }

        string resource = $"MineEngine.Core.Localization.{language.Code}.json";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Translation file '{resource}' is missing.");
        Dictionary<string, string> translations = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Translation file '{resource}' is empty.");
        return new TextCatalog(language, translations);
    }

    public bool Contains(string text) => _translations.ContainsKey(text);

    /// <summary>Traduction d'un texte source, ou le texte lui-même s'il n'est pas traduit.</summary>
    public string Translate(string text) =>
        text is not null && _translations.TryGetValue(text, out string? translation) ? translation : text!;
}
