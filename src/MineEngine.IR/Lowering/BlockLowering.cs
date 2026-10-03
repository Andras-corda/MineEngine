using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

public sealed class BlockLowering : AssetLowering<BlockAsset>
{
    public override AssetType AssetType => AssetType.Block;

    protected override void Lower(BlockAsset asset, LoweringContext context)
    {
        var settings = new IRBlockSettings
        {
            Model = asset.Model,
            RenderType = asset.RenderType,
            LightLevel = asset.LightLevel,
            Hardness = asset.Hardness,
            Resistance = asset.Resistance,
            Unbreakable = asset.Unbreakable,
            Sound = asset.Sound,
            Friction = asset.Friction,
            SpeedFactor = asset.SpeedFactor,
            JumpFactor = asset.JumpFactor,
            HarvestTool = asset.HarvestTool,
            ToolTier = asset.HarvestTool == HarvestTool.None ? ToolTier.Any : asset.ToolTier,
            RequiresCorrectTool = asset.RequiresCorrectTool,
            ExperienceMin = asset.Model == BlockModelKind.Column ? 0 : Math.Min(asset.ExperienceMin, asset.ExperienceMax),
            ExperienceMax = asset.Model == BlockModelKind.Column ? 0 : Math.Max(asset.ExperienceMin, asset.ExperienceMax),
        };

        var extraTextures = new Dictionary<BlockTextureSlot, IRTexture>();
        foreach ((BlockTextureSlot slot, Guid texture) in asset.ExtraTextures)
        {
            extraTextures[slot] = context.ResolveTexture(asset, texture);
        }

        IRItemForm? itemForm = asset.HasItemForm ? context.CreateItemForm(asset) : null;

        context.AddBlock(new IRBlock(
            asset.Id,
            asset.ResourceId,
            asset.DisplayName,
            context.ResolveTexture(asset, asset.TextureId),
            settings,
            extraTextures,
            CreateDrop(asset, itemForm is not null, context),
            itemForm));
    }

    private static IRBlockDrop CreateDrop(BlockAsset asset, bool hasItemForm, LoweringContext context) => asset.DropKind switch
    {
        BlockDropKind.Nothing => IRBlockDrop.Nothing,
        BlockDropKind.OtherItem when context.ResolveItem(asset, asset.DropItem, "Item laissé") is { } item =>
            IRBlockDrop.OtherItem(item, asset.DropMin, asset.DropMax),
        BlockDropKind.Self when hasItemForm => IRBlockDrop.Self,
        _ => IRBlockDrop.Nothing,
    };
}
