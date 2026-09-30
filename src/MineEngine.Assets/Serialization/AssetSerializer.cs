using System.Text.Json.Nodes;
using MineEngine.Assets.Definitions;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Json;

namespace MineEngine.Assets.Serialization;

/// <summary>
/// Convertit un asset en fichier JSON (un fichier par asset) et inversement.
/// Les propriétés communes sont gérées ici ; les propriétés propres à chaque
/// type sont déléguées à sa définition.
/// </summary>
public sealed class AssetSerializer
{
    public const int FormatVersion = 1;
    public const string FileExtension = ".asset.json";

    private readonly AssetCatalog _catalog;

    public AssetSerializer(AssetCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public string Serialize(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var root = new JsonObject
        {
            ["formatVersion"] = FormatVersion,
            ["guid"] = asset.Id.ToString("D"),
            ["type"] = asset.Type.ToString(),
            ["resourceId"] = asset.ResourceId.Value,
            ["displayName"] = asset.DisplayName,
            ["properties"] = _catalog.Get(asset.Type).WriteProperties(asset),
        };

        return JsonFormatting.ToText(root);
    }

    public Asset Deserialize(string json, string sourceName)
    {
        JsonObject root = JsonFormatting.ParseObject(json, sourceName);

        int version = root["formatVersion"]?.GetValue<int>() ?? 0;
        if (version > FormatVersion)
        {
            throw new InvalidDataException(
                $"{sourceName} a été créé par une version plus récente de Mine Engine (format {version}).");
        }

        string typeName = RequireString(root, "type", sourceName);
        if (!_catalog.TryGet(typeName, out IAssetDefinition? definition))
        {
            throw new InvalidDataException($"{sourceName} : type d'asset inconnu '{typeName}'.");
        }

        if (!Guid.TryParse(RequireString(root, "guid", sourceName), out Guid id) || id == Guid.Empty)
        {
            throw new InvalidDataException($"{sourceName} : identifiant interne (guid) invalide.");
        }

        string resourceIdText = RequireString(root, "resourceId", sourceName);
        if (!ResourceId.TryParse(resourceIdText, out ResourceId? resourceId))
        {
            throw new InvalidDataException($"{sourceName} : identifiant '{resourceIdText}' invalide.");
        }

        string displayName = root["displayName"]?.GetValue<string>() ?? string.Empty;
        JsonObject properties = root["properties"] as JsonObject ?? [];

        return definition!.Read(id, resourceId!, displayName, properties);
    }

    private static string RequireString(JsonObject root, string name, string sourceName) =>
        root[name]?.GetValue<string>()
        ?? throw new InvalidDataException($"{sourceName} : la propriété '{name}' est obligatoire.");
}
