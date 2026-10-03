using System.Text.Json;
using System.Text.Json.Nodes;
using MineEngine.Core.Assets;

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

    public double GetDouble(string name, double defaultValue) => Get(name, defaultValue, n => n.GetValue<double>());

    /// <summary>Identifiant interne d'un asset ; null si absent ou illisible.</summary>
    public Guid? GetGuid(string name) =>
        Guid.TryParse(GetString(name), out Guid id) && id != Guid.Empty ? id : null;

    /// <summary>Référence à un item ou un son ("asset:guid", "minecraft:diamond", "#minecraft:planks").</summary>
    public ContentReference? GetReference(string name) =>
        ContentReference.TryParseStorageText(GetString(name), out ContentReference? reference) ? reference : null;

    /// <summary>Tableau d'objets ; liste vide s'il est absent.</summary>
    public IReadOnlyList<JsonPropertyReader> GetObjectList(string name) =>
        _properties[name] is JsonArray array
            ? array.OfType<JsonObject>().Select((o, i) => new JsonPropertyReader(o, $"{_sourceName}.{name}[{i}]")).ToList()
            : [];

    /// <summary>Tableau de références ; les cases vides ou illisibles donnent null.</summary>
    public IReadOnlyList<ContentReference?> GetReferenceList(string name) =>
        _properties[name] is JsonArray array
            ? array.Select(n => n is JsonValue value && value.TryGetValue(out string? text)
                                && ContentReference.TryParseStorageText(text, out ContentReference? reference)
                    ? reference
                    : null)
                .ToList()
            : [];

    public bool GetBoolean(string name, bool defaultValue) => Get(name, defaultValue, n => n.GetValue<bool>());

    public string? GetString(string name) => Get<string?>(name, null, n => n.GetValue<string>());

    /// <summary>Valeur d'énumération écrite par son nom ; un nom inconnu donne la valeur par défaut.</summary>
    public TEnum GetEnum<TEnum>(string name, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        string? text = GetString(name);
        return text is not null && Enum.TryParse(text, ignoreCase: true, out TEnum value) && Enum.IsDefined(value)
            ? value
            : defaultValue;
    }

    public IReadOnlyList<string> GetStringList(string name) =>
        Get<IReadOnlyList<string>>(name, [], n => n.AsArray().Select(e => e!.GetValue<string>()).ToList());

    /// <summary>Liste de valeurs d'énumération ; les noms inconnus sont ignorés.</summary>
    public IReadOnlySet<TEnum> GetEnumSet<TEnum>(string name)
        where TEnum : struct, Enum =>
        GetStringList(name)
            .Select(t => Enum.TryParse(t, ignoreCase: true, out TEnum value) && Enum.IsDefined(value) ? (TEnum?)value : null)
            .OfType<TEnum>()
            .ToHashSet();

    /// <summary>Sous-objet ; null s'il est absent.</summary>
    public JsonPropertyReader? GetObject(string name) =>
        _properties[name] is JsonObject child ? new JsonPropertyReader(child, $"{_sourceName}.{name}") : null;

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
