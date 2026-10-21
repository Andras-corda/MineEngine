using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

public sealed class CommandLowering : AssetLowering<CommandAsset>
{
    public override AssetType AssetType => AssetType.Command;

    protected override void Lower(CommandAsset asset, LoweringContext context) =>
        context.AddCommand(new IRCommand(
            asset.Id, asset.ResourceId, asset.PermissionLevel, asset.Argument, asset.ArgumentName,
            Math.Min(asset.Minimum, asset.Maximum), Math.Max(asset.Minimum, asset.Maximum), asset.Feedback));
}

public sealed class CreativeTabLowering : AssetLowering<CreativeTabAsset>
{
    public override AssetType AssetType => AssetType.CreativeTab;

    protected override void Lower(CreativeTabAsset asset, LoweringContext context)
    {
        string role = Tr.F("Creative tab '{0}'", asset.ResourceId);
        IReadOnlyList<NamespacedId> items = [.. asset.Items.Select(i => context.ResolveItem(asset, i, role)).OfType<NamespacedId>()];
        NamespacedId? icon = context.ResolveItem(asset, asset.Icon, role) ?? items.FirstOrDefault();
        context.AddCreativeTab(new IRCreativeTab(asset.Id, asset.ResourceId, asset.DisplayName, icon, items));
    }
}

public sealed class KeybindLowering : AssetLowering<KeybindAsset>
{
    public override AssetType AssetType => AssetType.Keybind;

    protected override void Lower(KeybindAsset asset, LoweringContext context) =>
        context.AddKeybind(new IRKeybind(asset.Id, asset.ResourceId, asset.DisplayName, asset.Key.Code));
}

public sealed class EnchantmentLowering : AssetLowering<EnchantmentAsset>
{
    public override AssetType AssetType => AssetType.Enchantment;

    protected override void Lower(EnchantmentAsset asset, LoweringContext context) =>
        context.AddEnchantment(new IREnchantment(
            asset.Id, asset.ResourceId, asset.DisplayName, asset.Target, asset.Rarity, asset.MaxLevel, asset.Treasure, asset.Curse, asset.DamageBonus));
}

public sealed class FluidLowering : AssetLowering<FluidAsset>
{
    public override AssetType AssetType => AssetType.Fluid;

    protected override void Lower(FluidAsset asset, LoweringContext context) =>
        context.AddFluid(new IRFluid(asset.Id, asset.ResourceId, asset.DisplayName)
        {
            Color = asset.ColorValue,
            LightLevel = asset.LightLevel,
            FlowSpeed = asset.FlowSpeed,
            SpreadDistance = asset.SpreadDistance,
            LevelDecrease = asset.LevelDecrease,
            Infinite = asset.Infinite,
            Density = asset.Density,
            Viscosity = asset.Viscosity,
            StillTexture = context.ResolveOptionalTexture(asset, asset.StillTextureId),
            FlowingTexture = context.ResolveOptionalTexture(asset, asset.FlowingTextureId),
            BucketId = asset.HasBucket ? asset.BucketId : null,
            BucketTexture = asset.HasBucket ? context.ResolveOptionalTexture(asset, asset.BucketTextureId) : null,
        });
}

public sealed class GameRuleLowering : AssetLowering<GameRuleAsset>
{
    public override AssetType AssetType => AssetType.GameRule;

    protected override void Lower(GameRuleAsset asset, LoweringContext context) =>
        context.AddGameRule(new IRGameRule(
            asset.Id, asset.ResourceId, asset.DisplayName, asset.Kind, asset.Category, asset.DefaultBoolean, asset.DefaultNumber, asset.Description));
}

public sealed class EffectLowering : AssetLowering<EffectAsset>
{
    public override AssetType AssetType => AssetType.Effect;

    protected override void Lower(EffectAsset asset, LoweringContext context) =>
        context.AddEffect(new IREffect(asset.Id, asset.ResourceId, asset.DisplayName, asset.Category, asset.ColorValue, context.ResolveTexture(asset, asset.IconTextureId))
        {
            SpeedPercent = asset.SpeedPercent,
            AttackDamage = asset.AttackDamage,
            Armor = asset.Armor,
            MaxHealth = asset.MaxHealth,
            HealAmount = asset.HealAmount,
            DamageAmount = asset.DamageAmount,
            Interval = asset.Interval,
            PotionTicks = asset.HasPotion ? asset.PotionSeconds * 20 : 0,
        });
}
