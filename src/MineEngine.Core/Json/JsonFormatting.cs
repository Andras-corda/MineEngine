using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MineEngine.Core.Json;

/// <summary>
/// Format JSON commun à tous les fichiers écrits par Mine Engine : indenté,
/// fins de ligne LF, accents conservés. Deux sauvegardes identiques produisent
/// donc un fichier identique, ce qui garde les diffs Git lisibles.
/// </summary>
public static class JsonFormatting
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToText(JsonNode node) => node.ToJsonString(Options) + "\n";

    public static JsonObject ParseObject(string json, string sourceName)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"JSON invalide dans {sourceName} : {exception.Message}", exception);
        }

        return node as JsonObject
            ?? throw new InvalidDataException($"{sourceName} doit contenir un objet JSON.");
    }
}
