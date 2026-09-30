using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

public sealed class BlockAssetDefinition : AssetDefinition<BlockAsset>
{
    private const string HardnessKey = "hardness";
    private const string ResistanceKey = "resistance";
    private const string TextureKey = "texture";

    public override AssetType Type => AssetType.Block;

    public override string Label => "Bloc";

    public override string DefaultResourceIdBase => "new_block";

    public override string DefaultDisplayName => "Nouveau bloc";

    protected override BlockAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName);

    protected override void ReadProperties(BlockAsset asset, JsonPropertyReader reader)
    {
        asset.Hardness = Math.Clamp(reader.GetSingle(HardnessKey, BlockAsset.DefaultHardness), 0f, BlockAsset.MaxStrength);
        asset.Resistance = Math.Clamp(reader.GetSingle(ResistanceKey, BlockAsset.DefaultResistance), 0f, BlockAsset.MaxStrength);
        asset.TexturePath = reader.GetString(TextureKey);
    }

    protected override void WriteProperties(BlockAsset asset, JsonObject properties)
    {
        properties[HardnessKey] = asset.Hardness;
        properties[ResistanceKey] = asset.Resistance;
        properties[TextureKey] = asset.TexturePath;
    }
}
