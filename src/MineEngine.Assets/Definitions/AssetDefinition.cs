using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>
/// Describes an asset type: how to create a new one and how to read or write its specific properties. Adding an asset type involves implementing this class to define the behavior specific to that type.
/// </summary>
/// <typeparam name="TAsset"></typeparam>
public abstract class AssetDefinition<TAsset> : IAssetDefinition
    where TAsset : Asset
{
    public abstract AssetType Type { get; } // Objet représentant le type d'asset
    public abstract string Label { get; } // label de l'asset
    public abstract string DefaultResourceIdBase { get; } // base de l'identifiant de ressource par défaut pour l'asset
    public abstract string DefaultDisplayName { get; } // nom d'affichage par défaut pour l'asset
    
    /// <summary>
    /// Creates a new asset of the specified type with the given resource ID and display name, allowing for the instantiation of assets with specific properties and identifiers.
    /// </summary>
    /// <param name="resourceId"></param>
    /// <param name="displayName"></param>
    /// <returns></returns>
    public Asset CreateNew(ResourceId resourceId, string displayName)
    {
        return Create(Guid.NewGuid(), resourceId, displayName);
    }

    /// <summary>
    /// Reads the properties of an asset from a JSON object, allowing for the deserialization of asset properties and the reconstruction of the asset's state from stored data.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="resourceId"></param>
    /// <param name="displayName"></param>
    /// <param name="properties"></param>
    /// <returns></returns>
    public Asset Read(Guid id, ResourceId resourceId, string displayName, JsonObject properties)
    {
        TAsset asset = Create(id, resourceId, displayName);
        ReadProperties(asset, new JsonPropertyReader(properties, asset.ToString()));
        
        return asset;
    }

    /// <summary>
    /// Writes the properties of an asset to a JSON object, allowing for the serialization of asset properties and the storage of the asset's state in a structured format.
    /// </summary>
    /// <param name="asset"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public JsonObject WriteProperties(Asset asset)
    {
        if (asset is not TAsset typed)
        {
            throw new ArgumentException(Tr.F("{0} is not a {1} type asset.", asset, Type), nameof(asset));
        }

        var properties = new JsonObject();
        WriteProperties(typed, properties);

        return properties;
    }

    protected abstract TAsset Create(Guid id, ResourceId resourceId, string displayName); // Fonction abstraite pour créer un nouvel asset du type spécifié avec l'identifiant

    protected abstract void ReadProperties(TAsset asset, JsonPropertyReader reader); // Fonction abstraite pour lire les propriétés d'un asset à partir d'un objet JSON

    protected abstract void WriteProperties(TAsset asset, JsonObject properties); // Fonction abstraite pour écrire les propriétés d'un asset dans un objet JSON
}
