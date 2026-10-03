using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

public sealed class ItemLowering : AssetLowering<ItemAsset>
{
    public override AssetType AssetType => AssetType.Item;

    protected override void Lower(ItemAsset asset, LoweringContext context)
    {
        IRFood? food = asset.IsFood
            ? new IRFood(asset.Nutrition, asset.Saturation, asset.AlwaysEdible, asset.IsMeat)
            : null;

        context.AddItem(new IRItem(
            asset.Id,
            asset.ResourceId,
            asset.DisplayName,
            context.ResolveTexture(asset, asset.TextureId),
            context.CreateItemForm(asset),
            asset.Durability,
            asset.HasGlint,
            food));
    }
}
