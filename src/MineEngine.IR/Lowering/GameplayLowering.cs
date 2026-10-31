using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
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

/// <summary>Damage type (V0.7.10): "{victim}" and "{killer}" become the arguments of the death message of the game.</summary>
public sealed class DamageTypeLowering : AssetLowering<DamageTypeAsset>
{
    public override AssetType AssetType => AssetType.DamageType;

    public static string GameMessage(string message) =>
        message.Replace("%", "%%", StringComparison.Ordinal).Replace("{victim}", "%1$s", StringComparison.Ordinal).Replace("{killer}", "%2$s", StringComparison.Ordinal);

    protected override void Lower(DamageTypeAsset asset, LoweringContext context) =>
        context.AddDamageType(new IRDamageType(
            asset.Id, asset.ResourceId, asset.DisplayName, GameMessage(asset.DeathMessage), GameMessage(asset.KillerMessage),
            asset.Exhaustion, asset.Scaling, asset.Effects, asset.MessageType, asset.Flags));
}

/// <summary>Drop table (V0.7.10): the items of the entries become identifiers of the game (or tags).</summary>
public sealed class LootTableLowering : AssetLowering<LootTableAsset>
{
    public override AssetType AssetType => AssetType.LootTable;

    protected override void Lower(LootTableAsset asset, LoweringContext context) =>
        context.AddLootTable(new IRLootTable(asset.Id, asset.ResourceId, asset.Kind, [.. asset.Pools.Select(p => new IRLootPool(
            p.RollsMin, p.RollsMax, p.BonusRolls,
            [.. p.Entries.Select(e => Entry(asset, e, context)).OfType<IRLootEntry>()]))]));

    private static IRLootEntry? Entry(LootTableAsset asset, LootEntry entry, LoweringContext context)
    {
        IRIngredient? item = null;
        if (entry.Kind == LootEntryKind.Item)
        {
            if (entry.Item is null || context.ResolveIngredient(asset, entry.Item, Tr.T("Drop table entry")) is not { } resolved)
            {
                return null;
            }

            item = resolved;
        }

        return new IRLootEntry(item, entry.Weight, entry.CountMin, entry.CountMax)
        {
            Chance = entry.Chance,
            KilledByPlayer = entry.KilledByPlayer,
            SilkTouch = entry.SilkTouch,
            SurvivesExplosion = entry.SurvivesExplosion,
            LootingBonus = entry.LootingBonus,
            SmeltIfOnFire = entry.SmeltIfOnFire,
            EnchantRandomly = entry.EnchantRandomly,
        };
    }
}

public sealed class GlobalVariableLowering : AssetLowering<GlobalVariableAsset>
{
    public override AssetType AssetType => AssetType.GlobalVariable;

    protected override void Lower(GlobalVariableAsset asset, LoweringContext context) =>
        context.AddGlobal(new IRGlobalVariable(asset.Id, asset.VariableName, asset.ValueType, asset.Scope, asset.DefaultValue));
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
