using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>Command of the mod (V0.6.2); its handlers are the "On command" events naming it.</summary>
public sealed record IRCommand(
    Guid SourceAssetId, ResourceId Id, int PermissionLevel, CommandArgumentKind Argument, string ArgumentName, int Minimum, int Maximum, string Feedback);

/// <summary>Creative tab (V0.6.2): title, icon and items in order.</summary>
public sealed record IRCreativeTab(Guid SourceAssetId, ResourceId Id, string Title, NamespacedId? Icon, IReadOnlyList<NamespacedId> Items);

/// <summary>Keybind (V0.6.2): name in the Controls screen and default key (GLFW code).</summary>
public sealed record IRKeybind(Guid SourceAssetId, ResourceId Id, string Name, int KeyCode);

/// <summary>Enchantment (V0.6.3).</summary>
public sealed record IREnchantment(
    Guid SourceAssetId, ResourceId Id, string Name, EnchantmentTarget Target, EnchantmentRarity Rarity, int MaxLevel, bool Treasure, bool Curse, float DamageBonus)
{
    public int Weight => Rarity switch
    {
        EnchantmentRarity.Common => 10,
        EnchantmentRarity.Uncommon => 5,
        EnchantmentRarity.Rare => 2,
        _ => 1,
    };
}

/// <summary>Fluid (V0.6.3): its block has the identifier of the fluid; its bucket "&lt;id&gt;_bucket".</summary>
public sealed record IRFluid(Guid SourceAssetId, ResourceId Id, string Name)
{
    public int Color { get; init; } = 0x3F76E4;

    public int LightLevel { get; init; }

    public int FlowSpeed { get; init; } = 5;

    public int SpreadDistance { get; init; } = 4;

    public int LevelDecrease { get; init; } = 1;

    public bool Infinite { get; init; } = true;

    public int Density { get; init; } = 1000;

    public int Viscosity { get; init; } = 1000;

    /// <summary>Texture of the still fluid, or null for the water texture tinted by the color.</summary>
    public IRTexture? StillTexture { get; init; }

    public IRTexture? FlowingTexture { get; init; }

    /// <summary>Bucket of the fluid, or null when it has none.</summary>
    public string? BucketId { get; init; }

    /// <summary>Texture of the bucket, or null for the water bucket.</summary>
    public IRTexture? BucketTexture { get; init; }
}

/// <summary>Game rule (V0.6.3).</summary>
/// <summary>Global variable of the mod (V0.6.7): stored by the class MineEngineGlobals according to its scope.</summary>
public sealed record IRGlobalVariable(Guid Id, string Name, DataType Type, GlobalScope Scope, string DefaultValue)
{
    public bool IsPerPlayer => Scope is GlobalScope.PlayerLifetime or GlobalScope.PlayerPersistent;
}

public sealed record IRGameRule(
    Guid SourceAssetId, ResourceId Id, string Name, GameRuleKind Kind, GameRuleCategory Category, bool DefaultBoolean, int DefaultNumber, string Description);

/// <summary>Status effect (V0.6.3), with its potion when it has one.</summary>
public sealed record IREffect(Guid SourceAssetId, ResourceId Id, string Name, EffectCategory Category, int Color, IRTexture Icon)
{
    public int SpeedPercent { get; init; }

    public float AttackDamage { get; init; }

    public float Armor { get; init; }

    public float MaxHealth { get; init; }

    public float HealAmount { get; init; }

    public float DamageAmount { get; init; }

    public int Interval { get; init; } = 40;

    public bool HasPeriodicAction => HealAmount > 0 || DamageAmount > 0;

    /// <summary>Duration of the potion in ticks, or 0 when the effect has no potion.</summary>
    public int PotionTicks { get; init; }
}
