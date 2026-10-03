using System.Text.Json.Nodes;
using MineEngine.Core.GameData;

namespace MineEngine.Assets.Definitions;

/// <summary>Lecture et écriture des propriétés communes aux items et à la forme item des blocs.</summary>
public abstract class InventoryAssetDefinition<TAsset> : AssetDefinition<TAsset>
    where TAsset : InventoryAsset
{
    private const string TextureKey = "texture";
    private const string MaxStackSizeKey = "maxStackSize";
    private const string RarityKey = "rarity";
    private const string FireResistantKey = "fireResistant";
    private const string TooltipKey = "tooltip";
    private const string InModTabKey = "inModCreativeTab";
    private const string CreativeTabsKey = "creativeTabs";

    protected static void ReadInventory(TAsset asset, JsonPropertyReader reader)
    {
        asset.TextureId = reader.GetGuid(TextureKey);
        asset.MaxStackSize = Math.Clamp(
            reader.GetInt32(MaxStackSizeKey, InventoryAsset.DefaultStackSize), InventoryAsset.MinStackSize, InventoryAsset.MaxStackSizeLimit);
        asset.Rarity = reader.GetEnum(RarityKey, ItemRarity.Common);
        asset.FireResistant = reader.GetBoolean(FireResistantKey, false);
        asset.TooltipLines = reader.GetStringList(TooltipKey);
        asset.InModCreativeTab = reader.GetBoolean(InModTabKey, true);
        asset.CreativeTabs = reader.GetEnumSet<CreativeTab>(CreativeTabsKey);
    }

    protected static void WriteInventory(TAsset asset, JsonObject properties)
    {
        properties[TextureKey] = asset.TextureId?.ToString("D");
        properties[MaxStackSizeKey] = asset.MaxStackSize;
        properties[RarityKey] = asset.Rarity.ToString();
        properties[FireResistantKey] = asset.FireResistant;
        properties[TooltipKey] = new JsonArray([.. asset.TooltipLines.Select(l => JsonValue.Create(l))]);
        properties[InModTabKey] = asset.InModCreativeTab;
        properties[CreativeTabsKey] = new JsonArray([.. asset.CreativeTabs.Order().Select(t => JsonValue.Create(t.ToString()))]);
    }
}
