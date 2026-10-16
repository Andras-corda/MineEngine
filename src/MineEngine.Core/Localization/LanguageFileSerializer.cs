using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MineEngine.Core.Json;

namespace MineEngine.Core.Localization;

/// <summary>Contenu d'un fichier de langue : la langue et ses traductions valides.</summary>
public sealed record LanguageFile(UiLanguage Language, IReadOnlyDictionary<string, string> Translations, int RejectedCount);

/// <summary>
/// Format d'un fichier de langue (.json) :
/// <code>
/// { "formatVersion": 1, "code": "de", "name": "Deutsch", "culture": "de-DE",
///   "translations": { "Nouveau bloc": "Neuer Block", "{0} élément(s)": "{0} Element(e)" } }
/// </code>
/// Les clés sont les textes français du code, les valeurs leur traduction. Une traduction vide
/// ou qui n'a pas les mêmes valeurs {0}, {1}... que le texte source est écartée.
/// </summary>
public static partial class LanguageFileSerializer
{
    public const int FormatVersion = 1;

    public static LanguageFile Read(string json, string sourceName, string? filePath = null)
    {
        JsonObject root = JsonFormatting.ParseObject(json ?? string.Empty, sourceName);
        try
        {
            int version = root["formatVersion"]?.GetValue<int>() ?? FormatVersion;
            if (version > FormatVersion)
            {
                throw new InvalidDataException(Tr.F("{0} a été créé par une version plus récente de Mine Engine.", sourceName));
            }

            string code = (root["code"]?.GetValue<string>() ?? string.Empty).Trim().ToLowerInvariant();
            if (!CodePattern().IsMatch(code))
            {
                throw new InvalidDataException(Tr.F("Code de langue invalide dans {0} : « {1} » (exemples : de, es, pt-br).", sourceName, code));
            }

            string name = (root["name"]?.GetValue<string>() ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                throw new InvalidDataException(Tr.F("{0} n'indique pas le nom de la langue.", sourceName));
            }

            string culture = (root["culture"]?.GetValue<string>() ?? code).Trim();
            if (!IsKnownCulture(culture))
            {
                culture = UiLanguage.English.CultureName;
            }

            var translations = new Dictionary<string, string>(StringComparer.Ordinal);
            int rejected = 0;
            if (root["translations"] is JsonObject entries)
            {
                foreach ((string text, JsonNode? value) in entries)
                {
                    string translation = value?.GetValue<string>() ?? string.Empty;
                    if (translation.Length == 0 || Placeholders(text) != Placeholders(translation))
                    {
                        rejected++;
                        continue;
                    }

                    translations[text] = translation;
                }
            }

            return new LanguageFile(new UiLanguage(code, name, culture, filePath), translations, rejected);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or JsonException)
        {
            throw new InvalidDataException(Tr.F("{0} n'est pas un fichier de langue valide : {1}", sourceName, exception.Message), exception);
        }
    }

    public static string Write(UiLanguage language, IEnumerable<KeyValuePair<string, string>> translations)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(translations);
        var entries = new JsonObject();
        foreach ((string text, string translation) in translations.OrderBy(t => t.Key, StringComparer.Ordinal))
        {
            entries[text] = translation;
        }

        var root = new JsonObject
        {
            ["formatVersion"] = FormatVersion,
            ["code"] = language.Code,
            ["name"] = language.NativeName,
            ["culture"] = language.CultureName,
            ["translations"] = entries,
        };
        return JsonFormatting.ToText(root);
    }

    /// <summary>Valeurs {0}, {1}... d'un texte, triées : deux textes compatibles ont la même liste.</summary>
    public static string Placeholders(string text) =>
        string.Join(",", PlaceholderPattern().Matches(text ?? string.Empty).Select(m => m.Groups[1].Value).Distinct().Order());

    private static bool IsKnownCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name) is { } culture && !string.IsNullOrEmpty(culture.Name);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^[a-z]{2,3}(-[a-z0-9]{2,8})?$")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderPattern();
}
