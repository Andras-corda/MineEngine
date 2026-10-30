namespace MineEngine.Core.GameData;

/// <summary>How a size of a mob is given (V0.7.9): a fixed number, or the result of a function of the project.</summary>
public enum MobValueMode
{
    Fixed,
    Dynamic,
}

/// <summary>Type of creature for the game (enchantments like Smite and Bane of Arthropods, potions, raids).</summary>
public enum MobCreatureType
{
    Undefined,
    Undead,
    Arthropod,
    Illager,
    Water,
}

/// <summary>Damage a mob does not take.</summary>
public enum MobImmunity
{
    Fire,
    Lava,
    Lightning,
    Arrows,
    Fall,
    PlayerMelee,
    Drowning,
    Trident,
    Potions,
    Cactus,
    Explosion,
    Anvil,
    DragonBreath,
    Wither,
}

/// <summary>What hurts a mob more than the others.</summary>
public enum MobVulnerability
{
    /// <summary>Burns in sunlight, like a zombie.</summary>
    Sun,

    /// <summary>Hurt under the sky at night.</summary>
    Moon,

    /// <summary>Hurt by water and rain, like an enderman.</summary>
    Water,

    /// <summary>Takes twice the damage of magic (harming potions...).</summary>
    Potions,
}

/// <summary>Dimension a mob spawns in naturally.</summary>
public enum MobDimension
{
    Overworld,
    Nether,
    End,
}

/// <summary>Size of the groups a mob spawns in.</summary>
public enum MobGroupMode
{
    Fixed,
    Range,

    /// <summary>The maximum is given by a function of the project.</summary>
    Dynamic,
}

/// <summary>Color of the health bar of a boss (the seven colors of the game).</summary>
public enum MobBossColor
{
    Pink,
    Blue,
    Red,
    Green,
    Yellow,
    Purple,
    White,
}

/// <summary>Look of the health bar of a boss: plain, or cut in notches.</summary>
public enum MobBossOverlay
{
    Progress,
    Notched6,
    Notched10,
    Notched12,
    Notched20,
}

/// <summary>
/// Vehicle a mob can be (V0.7.9, step 3): placed by its own item instead of spawning, ridden by a
/// player. A boat floats on water; a minecart is placed on a rail and runs along it.
/// </summary>
public enum MobVehicleKind
{
    None,
    Boat,
    Minecart,
}

/// <summary>
/// Event of a mob of the mod (V0.7.9, Entity.md): its graph runs on the server when it happens to
/// the mob. "Once" events happen a single time in the life of the mob.
/// </summary>
public enum MobEventKind
{
    StruckByLightning,
    Falls,

    /// <summary>The mob appears in the world (once: a mob loaded again does not count).</summary>
    Spawns,

    /// <summary>The mob dies (once).</summary>
    Dies,

    PlayerCollides,
    Tick,
    Hurt,
    RightClicked,
    KillsEntity,
    RideStarts,
    RideStops,
    RiddenTick,

    /// <summary>The mob is tamed (once).</summary>
    Tamed,

    EntityCollides,
}
