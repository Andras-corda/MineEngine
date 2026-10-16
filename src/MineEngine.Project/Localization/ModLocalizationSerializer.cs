using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using MineEngine.Core.Json;

namespace MineEngine.Project.Localization;

/// <summary>
/// Fichier languages.json du projet :
/// <code>
/// { "formatVersion": 1, "locales": ["en_us", "fr_fr", "de_de"],
///   "translations": { "de_de": { "item.monmod.rubis": "Rubin" } } }
/// </code>
/// Il n'est écrit que si le projet s'écarte des langues par défaut sans traduction.
/// Un fichier de langue Minecraft (clé vers texte) se lit et s'écrit aussi ici pour l'import et l'export.
/// </summary>
public static class ModLocalizationSerializer
{
    public const int FormatVersion = 1;

    public const string FileName = "languages.json";

    public static string Serialize(ModLocalization localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        var translations = new JsonObject();
        foreach (string locale in localization.Locales)
        {
            ImmutableDictionary<string, string> entries = localization.TranslationsFor(locale);
            if (entries.Count > 0)
            {
                translations[locale] = ToObject(entries);
            }
        }

        var root = new JsonObject
        {
            ["formatVersion"] = FormatVersion,
            ["locales"] = new JsonArray([.. localization.Locales.Select(l => (JsonNode?)JsonValue.Create(l))]),
            ["translations"] = translations,
        };
        return JsonFormatting.ToText(root);
    }

    public static ModLocalization Deserialize(string json, string sourceName)
    {
        JsonObject root = JsonFormatting.ParseObject(json, sourceName);
        try
        {
            if ((root["formatVersion"]?.GetValue<int>() ?? FormatVersion) > FormatVersion)
            {
                throw new InvalidDataException(Tr.F("{0} a été créé par une version plus récente de Mine Engine.", sourceName));
            }

            List<string> locales = root["locales"] is JsonArray array
                ? [.. array.Select(n => n?.GetValue<string>() ?? string.Empty)]
                : [.. ModLocalization.DefaultLocales];
            var translations = new Dictionary<string, ImmutableDictionary<string, string>>();
            if (root["translations"] is JsonObject byLocale)
            {
                foreach ((string locale, JsonNode? node) in byLocale)
                {
                    if (node is JsonObject entries)
                    {
                        translations[ModLocalization.Normalize(locale)] = ReadEntries(entries);
                    }
                }
            }

            return new ModLocalization(locales, translations);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or JsonException)
        {
            throw new InvalidDataException(Tr.F("{0} n'est pas un fichier de langues valide : {1}", sourceName, exception.Message), exception);
        }
    }

    /// <summary>Lit un fichier de langue Minecraft ({ "clé": "texte" }).</summary>
    public static ImmutableDictionary<string, string> ReadLanguageFile(string json, string sourceName)
    {
        JsonObject root = JsonFormatting.ParseObject(json, sourceName);
        try
        {
            return ReadEntries(root);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or JsonException)
        {
            throw new InvalidDataException(Tr.F("{0} n'est pas un fichier de langue Minecraft valide : {1}", sourceName, exception.Message), exception);
        }
    }

    /// <summary>Écrit un fichier de langue Minecraft, clés triées.</summary>
    public static string WriteLanguageFile(IEnumerable<KeyValuePair<string, string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return JsonFormatting.ToText(ToObject(entries));
    }

    private static ImmutableDictionary<string, string> ReadEntries(JsonObject entries) =>
        entries.Where(e => e.Value is JsonValue)
            .Select(e => KeyValuePair.Create(e.Key, e.Value!.GetValue<string>()))
            .Where(e => e.Value.Length > 0)
            .ToImmutableDictionary();

    private static JsonObject ToObject(IEnumerable<KeyValuePair<string, string>> entries)
    {
        var result = new JsonObject();
        foreach ((string key, string text) in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            result[key] = text;
        }

        return result;
    }
}
