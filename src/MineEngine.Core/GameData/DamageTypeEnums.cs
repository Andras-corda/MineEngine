namespace MineEngine.Core.GameData;

// Vocabulary of the damage types of the game (V0.7.10); the names follow the ones of its data files.

/// <summary>How the difficulty changes the damage.</summary>
public enum DamageScaling
{
    /// <summary>The same in every difficulty.</summary>
    Never,

    /// <summary>Scaled when a living entity other than a player causes it (most damages of the game).</summary>
    WhenCausedByLivingNonPlayer,

    /// <summary>Always scaled by the difficulty.</summary>
    Always,
}

/// <summary>Sound played when an entity takes the damage.</summary>
public enum DamageEffects
{
    Hurt,
    Thorns,
    Drowning,
    Burning,
    Poking,
    Freezing,
}

/// <summary>How the death message is chosen.</summary>
public enum DeathMessageType
{
    /// <summary>The message of the damage type.</summary>
    Default,

    /// <summary>The messages of a fall (from a ladder, vines, a high place...).</summary>
    FallVariants,

    /// <summary>The "intentional game design" message of the beds in the Nether.</summary>
    IntentionalGameDesign,
}

/// <summary>Property of a damage type, given by a tag of the game.</summary>
public enum DamageTypeFlag
{
    BypassesArmor,
    BypassesShield,
    BypassesInvulnerability,
    BypassesEffects,
    BypassesEnchantments,
    IsFire,
    IsProjectile,
    IsExplosion,
    IsFall,
    IsDrowning,
    IsFreezing,
    IsLightning,
    NoKnockback,
    NoImpact,
}

/// <summary>What a drop table is for (its "type" in the game): it sets which conditions can be checked.</summary>
public enum LootTableKind
{
    Generic,
    Block,
    Entity,
    Chest,
    Fishing,
}

/// <summary>What a drop table entry gives.</summary>
public enum LootEntryKind
{
    /// <summary>An item, or an item of a tag.</summary>
    Item,

    /// <summary>Nothing: a weighted chance that the roll gives nothing.</summary>
    Empty,
}

/// <summary>Silk touch on the tool that breaks the block.</summary>
public enum LootSilkTouch
{
    Any,
    Required,
    Forbidden,
}
