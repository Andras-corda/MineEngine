using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>Base typée des définitions d'assets : évite les conversions dans chaque sous-classe.</summary>
public abstract class AssetDefinition<TAsset> : IAssetDefinition
    where TAsset : Asset
{
    public abstract AssetType Type { get; }

    public abstract string Label { get; }

    public abstract string DefaultResourceIdBase { get; }

    public abstract string DefaultDisplayName { get; }

    public Asset CreateNew(ResourceId resourceId, string displayName) =>
        Create(Guid.NewGuid(), resourceId, displayName);

    public Asset Read(Guid id, ResourceId resourceId, string displayName, JsonObject properties)
    {
        TAsset asset = Create(id, resourceId, displayName);
        ReadProperties(asset, new JsonPropertyReader(properties, asset.ToString()));
        return asset;
    }

    public JsonObject WriteProperties(Asset asset)
    {
        if (asset is not TAsset typed)
        {
            throw new ArgumentException($"{asset} n'est pas un asset de type {Type}.", nameof(asset));
        }

        var properties = new JsonObject();
        WriteProperties(typed, properties);
        return properties;
    }

    protected abstract TAsset Create(Guid id, ResourceId resourceId, string displayName);

    protected abstract void ReadProperties(TAsset asset, JsonPropertyReader reader);

    protected abstract void WriteProperties(TAsset asset, JsonObject properties);
}
