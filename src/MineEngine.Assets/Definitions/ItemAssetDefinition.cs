using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

public sealed class ItemAssetDefinition : AssetDefinition<ItemAsset>
{
    private const string MaxStackSizeKey = "maxStackSize";
    private const string TextureKey = "texture";

    public override AssetType Type => AssetType.Item;

    public override string Label => "Item";

    public override string DefaultResourceIdBase => "new_item";

    public override string DefaultDisplayName => "Nouvel item";

    protected override ItemAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName);

    protected override void ReadProperties(ItemAsset asset, JsonPropertyReader reader)
    {
        int stackSize = reader.GetInt32(MaxStackSizeKey, ItemAsset.DefaultStackSize);
        asset.MaxStackSize = Math.Clamp(stackSize, ItemAsset.MinStackSize, ItemAsset.MaxStackSizeLimit);
        asset.TexturePath = reader.GetString(TextureKey);
    }

    protected override void WriteProperties(ItemAsset asset, JsonObject properties)
    {
        properties[MaxStackSizeKey] = asset.MaxStackSize;
        properties[TextureKey] = asset.TexturePath;
    }
}
