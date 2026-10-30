using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

public sealed class ItemLowering : AssetLowering<ItemAsset>
{
    /// <summary>Ticks par seconde de Minecraft.</summary>
    private const int TicksPerSecond = 20;

    private static readonly NamespacedId PiglinLoved = NamespacedId.TryParse("minecraft:piglin_loved", out NamespacedId? tag) ? tag! : throw new InvalidOperationException();

    public override AssetType AssetType => AssetType.Item;

    /// <summary>
    /// Methods of the class of the item (V0.7.13): a method in Java keeps its code; a nodal method needs
    /// its graph (a function, or a procedure for an event). A binding the item does not use is left out.
    /// </summary>
    private static List<IRElementMethod> Methods(ItemAsset asset, LoweringContext context)
    {
        var methods = new List<IRElementMethod>();
        foreach (ElementMethod method in asset.Methods.DistinctBy(m => m.Slot))
        {
            if (ItemMethodSlots.Find(method.Slot) is not { } slot || !Uses(asset, slot.Key))
            {
                continue;
            }

            if (method.Language == MethodLanguage.Java)
            {
                methods.Add(new IRElementMethod(slot.Key, slot.JavaName, slot.Return, method.JavaBody, null));
            }
            else if (method.GraphId is { } id && context.Project.Assets.Find(id) is GraphAsset graph && graph.Kind == slot.GraphKind)
            {
                methods.Add(new IRElementMethod(slot.Key, slot.JavaName, slot.Return, null, id));
            }
            else
            {
                context.Diagnostics.Error(Tr.F("The method \"{0}\" of the item has no graph.", slot.Title), asset.ToString(), asset.Id);
            }
        }

        return methods;
    }

    private static bool Uses(ItemAsset asset, string slot) => slot switch
    {
        ItemMethodSlots.BurnTime or ItemMethodSlots.BurnCondition => asset.BurnTime > 0,
        ItemMethodSlots.CompostCondition => asset.CompostChance > 0,
        _ => true,
    };

    /// <summary>A ranged item is a simple item that is not a food; its ammunition is the one of its projectile by default.</summary>
    private static IRRanged? Ranged(ItemAsset asset, LoweringContext context)
    {
        ItemRanged ranged = asset.Ranged;
        if (ranged.Kind == RangedItemKind.None || asset.Kind != ItemKind.Simple || asset.IsFood)
        {
            return null;
        }

        string vanilla = ranged.Projectile switch
        {
            RangedProjectile.SpectralArrow => "minecraft:spectral_arrow",
            RangedProjectile.Snowball => "minecraft:snowball",
            RangedProjectile.Egg => "minecraft:egg",
            _ => "minecraft:arrow",
        };
        NamespacedId ammo = context.ResolveItem(asset, ranged.Ammo, Tr.T("Ammunition")) ?? (NamespacedId.TryParse(vanilla, out NamespacedId? id) ? id! : throw new InvalidOperationException());
        return new IRRanged(ranged.Kind, ranged.Projectile, ammo, ranged.NeedsAmmo, ranged.Mode, ranged.RateTicks, ranged.Speed, ranged.Damage);
    }

    protected override void Lower(ItemAsset asset, LoweringContext context)
    {
        IRFood? food = asset.IsFood
            ? new IRFood(asset.Nutrition, asset.Saturation, asset.AlwaysEdible, asset.IsMeat)
            {
                EatTicks = Math.Max(1, (int)Math.Round(asset.EatSeconds * TicksPerSecond)),
                Animation = asset.UseAnimation,
                Result = context.ResolveItem(asset, asset.EatResult, Tr.T("Item left once eaten")),
                Effects = [.. asset.FoodEffects.Select(e => new IRFoodEffect(e.Effect, e.DurationSeconds * TicksPerSecond, e.Amplifier, e.Probability))],
            }
            : null;

        // La durabilité d'un outil ou d'une armure vient de son matériau.
        bool equipment = asset.Kind.IsEquipment();
        bool damageable = equipment || asset.HasDurability;
        ItemRemainderKind remainder = asset.Remainder switch
        {
            ItemRemainderKind.ItselfDamaged when !damageable => ItemRemainderKind.Itself,
            ItemRemainderKind.OtherItem when asset.CraftRemainder is null => ItemRemainderKind.Consumed,
            _ => asset.Remainder,
        };
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
            CustomModel = context.ResolveCustomModel(asset),
            Appearances = context.ResolveAppearances(asset),
            CraftRemainder = remainder == ItemRemainderKind.OtherItem
                ? context.ResolveItem(asset, asset.CraftRemainder, Tr.T("Item returned after crafting"))
                : null,
            Remainder = remainder,
            GuiTexture = asset.UsesCustomModel && asset.GuiTextureId is { } gui ? context.ResolveTexture(asset, gui) : null,
            GlintCondition = asset.HasGlint
                ? context.ResolveFunction(asset, asset.GlintCondition, Tr.T("glint condition"), returnsBoolean: true, PinType.Entity)
                : null,
            RepairItem = !asset.NoRepair && asset.RepairItem is { } repair ? context.ResolveIngredient(asset, repair, Tr.T("Repair item")) : null,
            HarvestsAnyBlock = asset.HarvestsAnyBlock,
            BurnTimeFunction = asset.BurnTime > 0
                ? context.ResolveFunction(asset, asset.BurnTimeFunction, Tr.T("burn time"), returnsBoolean: false, PinType.Entity)
                : null,
            BurnCondition = asset.BurnTime > 0
                ? context.ResolveFunction(asset, asset.BurnCondition, Tr.T("burning condition"), returnsBoolean: true, PinType.Entity)
                : null,
            CompostChance = asset.CompostChance / 100f,
            CompostCondition = asset.CompostChance > 0
                ? context.ResolveFunction(asset, asset.CompostCondition, Tr.T("composting condition"), returnsBoolean: true, PinType.Entity)
                : null,
            PiglinCurrency = asset.PiglinCurrency,
            Ranged = Ranged(asset, context),
            Methods = Methods(asset, context),
            CustomTier = asset.UsesCustomTier
                ? new IRToolTier(asset.CustomTier.Uses, asset.CustomTier.Speed, asset.CustomTier.Enchantability, asset.CustomTier.MiningLevel)
                : null,
            NoRepair = asset.NoRepair,
            BurnTime = asset.BurnTime,
            // Piglins pick up and admire what is in this tag of the game.
            Tags = asset.PiglinCurrency && !asset.ItemTags.Contains(PiglinLoved) ? [.. asset.ItemTags, PiglinLoved] : asset.ItemTags,
        });
    }
}
