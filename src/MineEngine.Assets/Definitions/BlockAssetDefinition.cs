using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

public sealed class BlockAssetDefinition : InventoryAssetDefinition<BlockAsset>
{
    public override AssetType Type => AssetType.Block; // Type d'asset correspondant à cette définition 
    public override string Label => Tr.T("Bloc"); // label de l'asset 
    public override string DefaultResourceIdBase => "new_block"; // identifiant de ressource par défaut pour l'asset
    public override string DefaultDisplayName => Tr.T("New block"); // nom d'affichage par défaut pour l'asset

    /// <summary>
    /// Creates a new block asset with the specified ID, resource ID, and display name, allowing for the instantiation of block assets with specific properties and identifiers.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="resourceId"></param>
    /// <param name="displayName"></param>
    /// <returns></returns>
    protected override BlockAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName);

    /// <summary>
    /// Reads the properties of a block asset from a JSON object, allowing for the deserialization of block asset properties and the reconstruction of the block asset's state from stored data.
    /// </summary>
    /// <param name="asset"></param>
    /// <param name="reader"></param>
    protected override void ReadProperties(BlockAsset asset, JsonPropertyReader reader)
    {
        ReadInventory(asset, reader);

        asset.Model = reader.GetEnum("model", BlockModelKind.Cube);
        asset.RenderType = reader.GetEnum("renderType", BlockRenderType.Solid);
        asset.LightLevel = reader.GetInt32("lightLevel", 0);
        if (reader.GetObject("textures") is { } textures)
        {
            foreach (BlockTextureSlot slot in Enum.GetValues<BlockTextureSlot>().Where(s => s != BlockTextureSlot.Main))
            {
                asset.SetTexture(slot, textures.GetGuid(slot.ToString().ToLowerInvariant()));
            }
        }

        asset.Hardness = Math.Clamp(reader.GetSingle("hardness", BlockAsset.DefaultHardness), 0f, BlockAsset.MaxStrength);
        asset.Resistance = Math.Clamp(reader.GetSingle("resistance", BlockAsset.DefaultResistance), 0f, BlockAsset.MaxStrength);
        asset.Unbreakable = reader.GetBoolean("unbreakable", false);
        asset.Sound = reader.GetEnum("sound", BlockSoundType.Stone);
        asset.Friction = reader.GetSingle("friction", BlockAsset.DefaultFriction);
        asset.SpeedFactor = reader.GetSingle("speedFactor", 1f);
        asset.JumpFactor = reader.GetSingle("jumpFactor", 1f);

        asset.HarvestTool = reader.GetEnum("harvestTool", HarvestTool.None);
        asset.ToolTier = reader.GetEnum("toolTier", ToolTier.Any);
        asset.RequiresCorrectTool = reader.GetBoolean("requiresCorrectTool", false);

        if (reader.GetObject("drop") is { } drop)
        {
            asset.DropKind = drop.GetEnum("kind", BlockDropKind.Self);
            asset.DropItem = drop.GetReference("item");
            asset.DropMin = drop.GetInt32("min", 1);
            asset.DropMax = drop.GetInt32("max", 1);
        }

        if (reader.GetObject("experience") is { } experience)
        {
            asset.ExperienceMin = experience.GetInt32("min", 0);
            asset.ExperienceMax = experience.GetInt32("max", 0);
        }

        asset.HasItemFormEnabled = reader.GetBoolean("hasItem", true);
    }

    /// <summary>
    /// Writes the properties of a block asset to a JSON object, allowing for the serialization of block asset properties and the storage of the block asset's state in a structured format.
    /// </summary>
    /// <param name="asset"></param>
    /// <param name="properties"></param>
    protected override void WriteProperties(BlockAsset asset, JsonObject properties)
    {
        WriteInventory(asset, properties);

        properties["model"] = asset.Model.ToString();
        properties["renderType"] = asset.RenderType.ToString();
        properties["lightLevel"] = asset.LightLevel;
        var textures = new JsonObject();
        foreach ((BlockTextureSlot slot, Guid texture) in asset.ExtraTextures.OrderBy(t => t.Key))
        {
            textures[slot.ToString().ToLowerInvariant()] = texture.ToString("D");
        }

        properties["textures"] = textures;

        properties["hardness"] = asset.Hardness;
        properties["resistance"] = asset.Resistance;
        properties["unbreakable"] = asset.Unbreakable;
        properties["sound"] = asset.Sound.ToString();
        properties["friction"] = asset.Friction;
        properties["speedFactor"] = asset.SpeedFactor;
        properties["jumpFactor"] = asset.JumpFactor;

        properties["harvestTool"] = asset.HarvestTool.ToString();
        properties["toolTier"] = asset.ToolTier.ToString();
        properties["requiresCorrectTool"] = asset.RequiresCorrectTool;
        properties["drop"] = new JsonObject
        {
            ["kind"] = asset.DropKind.ToString(),
            ["item"] = asset.DropItem?.ToStorageText(),
            ["min"] = asset.DropMin,
            ["max"] = asset.DropMax,
        };
        properties["experience"] = new JsonObject
        {
            ["min"] = asset.ExperienceMin,
            ["max"] = asset.ExperienceMax,
        };

        properties["hasItem"] = asset.HasItemFormEnabled;
    }
}
