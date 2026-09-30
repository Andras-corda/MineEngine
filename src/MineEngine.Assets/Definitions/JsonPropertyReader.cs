using System.Text.Json;
using System.Text.Json.Nodes;

namespace MineEngine.Assets.Definitions;

/// <summary>
/// Lecture tolérante des propriétés d'un asset : une propriété absente prend
/// sa valeur par défaut, une propriété de mauvais type lève une erreur claire.
/// </summary>
public sealed class JsonPropertyReader
{
    private readonly JsonObject _properties;
    private readonly string _sourceName;

    public JsonPropertyReader(JsonObject properties, string sourceName)
    {
        _properties = properties;
        _sourceName = sourceName;
    }

    public int GetInt32(string name, int defaultValue) => Get(name, defaultValue, n => n.GetValue<int>());

    public float GetSingle(string name, float defaultValue) => Get(name, defaultValue, n => n.GetValue<float>());

    public string? GetString(string name) => Get<string?>(name, null, n => n.GetValue<string>());

    private T Get<T>(string name, T defaultValue, Func<JsonNode, T> read)
    {
        JsonNode? node = _properties[name];
        if (node is null)
        {
            return defaultValue;
        }

        try
        {
            return read(node);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or JsonException)
        {
            throw new InvalidDataException(
                $"{_sourceName} : la propriété '{name}' a une valeur invalide ({node.ToJsonString()}).", exception);
        }
    }
}
