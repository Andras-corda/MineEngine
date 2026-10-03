using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

public sealed class ItemAssetDefinition : InventoryAssetDefinition<ItemAsset>
{
    private const string DurabilityKey = "durability";
    private const string GlintKey = "glint";
    private const string FoodKey = "food";

    public override AssetType Type => AssetType.Item;

    public override string Label => "Item";

    public override string DefaultResourceIdBase => "new_item";

    public override string DefaultDisplayName => "Nouvel item";

    protected override ItemAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName);

    protected override void ReadProperties(ItemAsset asset, JsonPropertyReader reader)
    {
        ReadInventory(asset, reader);
        asset.Durability = reader.GetInt32(DurabilityKey, 0);
        asset.HasGlint = reader.GetBoolean(GlintKey, false);

        JsonPropertyReader? food = reader.GetObject(FoodKey);
        asset.IsFood = food is not null;
        if (food is not null)
        {
            asset.Nutrition = food.GetInt32("nutrition", 4);
            asset.Saturation = food.GetSingle("saturation", 0.3f);
            asset.AlwaysEdible = food.GetBoolean("alwaysEdible", false);
            asset.IsMeat = food.GetBoolean("meat", false);
        }
    }

    protected override void WriteProperties(ItemAsset asset, JsonObject properties)
    {
        WriteInventory(asset, properties);
        properties[DurabilityKey] = asset.Durability;
        properties[GlintKey] = asset.HasGlint;
        properties[FoodKey] = asset.IsFood
            ? new JsonObject
            {
                ["nutrition"] = asset.Nutrition,
                ["saturation"] = asset.Saturation,
                ["alwaysEdible"] = asset.AlwaysEdible,
                ["meat"] = asset.IsMeat,
            }
            : null;
    }
}
