using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

public sealed class ItemLowering : AssetLowering<ItemAsset>
{
    public override AssetType AssetType => AssetType.Item;

    protected override void Lower(ItemAsset asset, LoweringContext context) =>
        context.AddItem(new IRItem(asset.ResourceId, asset.DisplayName, context.ResolveTexture(asset), asset.MaxStackSize));
}
