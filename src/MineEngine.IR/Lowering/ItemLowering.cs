using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

public sealed class ItemLowering : AssetLowering<ItemAsset>
{
    /// <summary>Ticks par seconde de Minecraft.</summary>
    private const int TicksPerSecond = 20;

    public override AssetType AssetType => AssetType.Item;

    protected override void Lower(ItemAsset asset, LoweringContext context)
    {
        IRFood? food = asset.IsFood
            ? new IRFood(asset.Nutrition, asset.Saturation, asset.AlwaysEdible, asset.IsMeat)
            {
                Fast = asset.FastFood,
                Effects = [.. asset.FoodEffects.Select(e => new IRFoodEffect(e.Effect, e.DurationSeconds * TicksPerSecond, e.Amplifier, e.Probability))],
            }
            : null;

        // La durabilité d'un outil ou d'une armure vient de son matériau.
        bool equipment = asset.Kind.IsEquipment();
        context.AddItem(new IRItem(
            asset.Id,
            asset.ResourceId,
            asset.DisplayName,
            context.ResolveTexture(asset, asset.TextureId),
            context.CreateItemForm(asset),
            equipment ? 0 : asset.Durability,
            asset.HasGlint,
            food)
        {
            Kind = asset.Kind,
            Tier = asset.Tier,
            ArmorMaterial = asset.ArmorMaterial,
            AttackDamage = asset.AttackDamage,
            AttackSpeed = asset.AttackSpeed,
            Handheld = asset.Handheld,
            CraftRemainder = context.ResolveItem(asset, asset.CraftRemainder, "Item rendu après fabrication"),
            NoRepair = asset.NoRepair,
            BurnTime = asset.BurnTime,
            Tags = asset.ItemTags,
        });
    }
}
