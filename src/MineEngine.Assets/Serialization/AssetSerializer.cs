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
    /// <summary>
    /// Version du format des fichiers d'assets. 2 (V0.3) : textures et items désignés
    /// par guid. Les fichiers plus anciens passent par <see cref="AssetFormatMigrator"/>.
    /// </summary>
    public const int FormatVersion = 2;
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
        };

        // Dossier du Dashboard (V0.4.1) : écrit seulement s'il y en a un, les fichiers des autres assets ne changent pas.
        if (asset.Folder.Length > 0)
        {
            root["folder"] = asset.Folder;
        }

        root["properties"] = _catalog.Get(asset.Type).WriteProperties(asset);

        return JsonFormatting.ToText(root);
    }

    /// <summary>Lit un fichier d'asset au format courant.</summary>
    public Asset Deserialize(string json, string sourceName) =>
        Deserialize(JsonFormatting.ParseObject(json, sourceName), sourceName);

    /// <summary>Lit un document JSON d'asset au format courant (déjà migré si besoin).</summary>
    public Asset Deserialize(JsonObject root, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(root);
        int version = ReadFormatVersion(root);
        if (version > FormatVersion)
        {
            throw new InvalidDataException(
                Tr.F("{0} was created by a newer version of Mine Engine (format {1}).", sourceName, version));
        }

        if (version < FormatVersion)
        {
            throw new InvalidDataException(
                Tr.F("{0} uses an old format (format {1}): open the project to convert it.", sourceName, version));
        }

        string typeName = RequireString(root, "type", sourceName);
        if (!_catalog.TryGet(typeName, out IAssetDefinition? definition))
        {
            throw new InvalidDataException(Tr.F("{0}: unknown asset type '{1}'.", sourceName, typeName));
        }

        if (!Guid.TryParse(RequireString(root, "guid", sourceName), out Guid id) || id == Guid.Empty)
        {
            throw new InvalidDataException(Tr.F("{0}: invalid internal identifier (guid).", sourceName));
        }

        string resourceIdText = RequireString(root, "resourceId", sourceName);
        if (!ResourceId.TryParse(resourceIdText, out ResourceId? resourceId))
        {
            throw new InvalidDataException(Tr.F("{0}: invalid identifier '{1}'.", sourceName, resourceIdText));
        }

        string displayName = root["displayName"]?.GetValue<string>() ?? string.Empty;
        JsonObject properties = root["properties"] as JsonObject ?? [];

        Asset asset = definition!.Read(id, resourceId!, displayName, properties);
        asset.Folder = AssetFolders.Normalize(root["folder"]?.GetValue<string>()) ?? string.Empty;
        return asset;
    }

    public static int ReadFormatVersion(JsonObject root) => root["formatVersion"]?.GetValue<int>() ?? 0;

    private static string RequireString(JsonObject root, string name, string sourceName) =>
        root[name]?.GetValue<string>()
        ?? throw new InvalidDataException(Tr.F("{0}: property '{1}' is required.", sourceName, name));
}
