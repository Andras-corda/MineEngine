using System.Text.Json;
using System.Text.Json.Nodes;
using MineEngine.Core.Json;

namespace MineEngine.Core.Theming;

/// <summary>
/// Format d'un fichier de thème (.json) :
/// <code>
/// { "formatVersion": 1, "name": "Mon thème", "base": "dark", "colors": { "AccentBrush": "#2F7FD8", ... } }
/// </code>
/// "base" vaut "dark" ou "light" ; les couleurs absentes viennent de cette base et les clés
/// inconnues sont ignorées, pour qu'un thème reste lisible d'une version à l'autre.
/// </summary>
public static class ThemeFileSerializer
{
    public const int FormatVersion = 1;

    /// <summary>Lit un thème ; lève InvalidDataException si le fichier n'est pas un thème valide.</summary>
    public static ThemeDefinition Read(string json, string id, string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        JsonObject root = JsonFormatting.ParseObject(json ?? string.Empty, sourceName);
        try
        {
            int version = root["formatVersion"]?.GetValue<int>() ?? FormatVersion;
            if (version > FormatVersion)
            {
                throw new InvalidDataException(Tr.F("{0} was created by a newer version of Mine Engine.", sourceName));
            }

            string name = root["name"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                throw new InvalidDataException(Tr.F("{0} does not give the theme name.", sourceName));
            }

            ThemeBase themeBase = (root["base"]?.GetValue<string>() ?? "dark").Trim().ToLowerInvariant() switch
            {
                "dark" => ThemeBase.Dark,
                "light" => ThemeBase.Light,
                string other => throw new InvalidDataException(Tr.F("Unknown theme base in {0}: {1} (expected: dark or light).", sourceName, other)),
            };

            var colors = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root["colors"] is JsonObject colorNodes)
            {
                foreach ((string key, JsonNode? value) in colorNodes)
                {
                    if (!ThemeColorKeys.IsKnown(key))
                    {
                        continue;
                    }

                    string text = value?.GetValue<string>() ?? string.Empty;
                    if (!RgbColor.TryParse(text, out RgbColor color))
                    {
                        throw new InvalidDataException(Tr.F("Invalid color for {0} in {1}: {2} (expected: #RRGGBB).", key, sourceName, text));
                    }

                    colors[key] = color.ToString();
                }
            }

            return new ThemeDefinition(id, name, themeBase, colors);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or JsonException)
        {
            throw new InvalidDataException(Tr.F("{0} is not a valid theme file: {1}", sourceName, exception.Message), exception);
        }
    }

    /// <summary>Écrit un thème avec toutes ses couleurs, pour qu'il serve de point de départ complet.</summary>
    public static string Write(ThemeDefinition theme, RgbColor? accent = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var colors = new JsonObject();
        IReadOnlyDictionary<string, string> resolved = theme.Resolve(accent);
        foreach (string key in ThemeColorKeys.All)
        {
            if (resolved.TryGetValue(key, out string? value))
            {
                colors[key] = value;
            }
        }

        var root = new JsonObject
        {
            ["formatVersion"] = FormatVersion,
            ["name"] = theme.DisplayName,
            ["base"] = theme.IsDark ? "dark" : "light",
            ["colors"] = colors,
        };
        return JsonFormatting.ToText(root);
    }
}
