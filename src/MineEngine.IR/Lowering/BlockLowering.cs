using MineEngine.Assets;
using MineEngine.Assets.Graphs;
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
            // A door, a trapdoor and a pane have holes: they are cut out unless the user chose otherwise.
            RenderType = asset.RenderType == BlockRenderType.Solid && asset.Model is BlockModelKind.Door or BlockModelKind.Trapdoor or BlockModelKind.Pane
                ? BlockRenderType.Cutout
                : asset.RenderType,
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
            ExperienceMin = asset.Model == BlockModelKind.Column || asset.Model.IsBase() ? 0 : Math.Min(asset.ExperienceMin, asset.ExperienceMax),
            ExperienceMax = asset.Model == BlockModelKind.Column || asset.Model.IsBase() ? 0 : Math.Max(asset.ExperienceMin, asset.ExperienceMax),
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
            itemForm)
        {
            CustomModel = asset.Model.IsBase() ? null : context.ResolveCustomModel(asset),
            Appearances = asset.Model.IsBase() ? [] : context.ResolveAppearances(asset),
            Options = Normalize(asset),
            ItemTexture = asset.Options.ItemTextureId is { } item ? context.ResolveTexture(asset, item) : null,
            ParticleTexture = asset.Options.ParticleTextureId is { } particle ? context.ResolveTexture(asset, particle) : null,
            RequiredTool = asset.RequiresCorrectTool && asset.RequiredTool is not null ? context.ResolveItem(asset, asset.RequiredTool, Tr.T("Tool that harvests it")) : null,
            DropCondition = asset.RequiresCorrectTool
                ? context.ResolveFunction(asset, asset.DropCondition, Tr.T("drop condition"), returnsBoolean: true, PinType.Position)
                : null,
        });
    }

    /// <summary>The settings that cannot go together are dropped (the diagnostics already warned).</summary>
    private static BlockOptions Normalize(BlockAsset asset)
    {
        BlockOptions options = asset.Options;
        if (asset.Model.IsBase())
        {
            // The class of the game gives the shape, the rotation, the water and the states.
            options = options with
            {
                Rotation = BlockRotation.None,
                Waterloggable = false,
                HasGravity = false,
                ConnectedSides = false,
                Boxes = [],
                Properties = [],
            };
        }

        if (asset.Model == BlockModelKind.Column || options.HasGravity)
        {
            options = options with { Rotation = BlockRotation.None };
        }

        if (options.HasGravity)
        {
            options = options with { Waterloggable = false };
        }

        if (options.Rotation != BlockRotation.Horizontal)
        {
            options = options with { HasPitchRotation = false };
        }

        return options with { RedstonePower = Math.Clamp(options.RedstonePower, 0, BlockOptions.MaxRedstonePower) };
    }

    private static IRBlockDrop CreateDrop(BlockAsset asset, bool hasItemForm, LoweringContext context) => asset.DropKind switch
    {
        BlockDropKind.Nothing => IRBlockDrop.Nothing,
        BlockDropKind.Table when context.ResolveLootTable(asset, asset.DropTable) is { } table => IRBlockDrop.FromTable(table),
        BlockDropKind.OtherItem when context.ResolveItem(asset, asset.DropItem, Tr.T("Dropped item")) is { } item =>
            IRBlockDrop.OtherItem(item, asset.DropMin, asset.DropMax),
        BlockDropKind.Self when hasItemForm => IRBlockDrop.Self,
        _ => IRBlockDrop.Nothing,
    };
}
