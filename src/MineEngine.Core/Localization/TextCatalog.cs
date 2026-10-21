using System.Reflection;
using System.Text.Json;

namespace MineEngine.Core.Localization;

/// <summary>
/// Translations of one interface language. Texts are written in English in the code (the source
/// language); every other shipped language provides a Localization/&lt;code&gt;.json file embedded in
/// MineEngine.Core that maps each English text to its translation. A text missing from the file
/// stays in English. An imported language works the same way; a file written for an older version
/// (keyed by the former French source texts) is converted to English keys when it is read.
/// </summary>
public sealed class TextCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> LazyFrench = new(() => Embedded(UiLanguage.French.Code));

    private readonly IReadOnlyDictionary<string, string> _translations;

    private TextCatalog(UiLanguage language, IReadOnlyDictionary<string, string> translations)
    {
        Language = language;
        _translations = translations;
    }

    /// <summary>Source language: texts are shown as they are written in the code.</summary>
    public static TextCatalog Source { get; } = new(UiLanguage.English, new Dictionary<string, string>());

    /// <summary>Every English text of the interface (those of the French file, which translates them all).</summary>
    public static IEnumerable<string> SourceTexts => LazyFrench.Value.Keys;

    public UiLanguage Language { get; }

    /// <summary>Source texts that have a translation in this language.</summary>
    public IEnumerable<string> TranslatedTexts => _translations.Keys;

    /// <summary>Number of translated source texts.</summary>
    public int Count => _translations.Count;

    /// <summary>Catalog of a language; the source language has no file.</summary>
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
            return new TextCatalog(language, ToEnglishKeys(file.Translations));
        }

        return new TextCatalog(language, language.Code == UiLanguage.French.Code ? LazyFrench.Value : Embedded(language.Code));
    }

    /// <summary>
    /// Translations keyed by English source texts. A key that is a former French source text
    /// (language file made before English became the source language) is replaced by its English text.
    /// </summary>
    public static Dictionary<string, string> ToEnglishKeys(IEnumerable<KeyValuePair<string, string>> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);
        IReadOnlyDictionary<string, string> french = LazyFrench.Value;
        Dictionary<string, string> englishOf = FrenchToEnglish();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string text, string translation) in translations)
        {
            string key = !french.ContainsKey(text) && englishOf.TryGetValue(text, out string? english) ? english : text;
            result.TryAdd(key, translation);
        }

        return result;
    }

    public bool Contains(string text) => _translations.ContainsKey(text);

    /// <summary>Translation of a source text, or the text itself when it is not translated.</summary>
    public string Translate(string text) =>
        text is not null && _translations.TryGetValue(text, out string? translation) ? translation : text!;

    private static Dictionary<string, string> FrenchToEnglish()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string english, string french) in LazyFrench.Value)
        {
            result.TryAdd(french, english);
        }

        return result;
    }

    private static Dictionary<string, string> Embedded(string code)
    {
        string resource = $"MineEngine.Core.Localization.{code}.json";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Translation file '{resource}' is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Translation file '{resource}' is empty.");
    }
}
