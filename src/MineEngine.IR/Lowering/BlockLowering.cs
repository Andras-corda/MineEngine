using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

public sealed class BlockLowering : AssetLowering<BlockAsset>
{
    public override AssetType AssetType => AssetType.Block;

    protected override void Lower(BlockAsset asset, LoweringContext context) =>
        context.AddBlock(new IRBlock(
            asset.ResourceId, asset.DisplayName, context.ResolveTexture(asset), asset.Hardness, asset.Resistance));
}
