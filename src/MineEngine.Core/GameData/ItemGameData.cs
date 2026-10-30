namespace MineEngine.Core.GameData;

// Vocabulaire Minecraft propre aux items (V0.4.1). Les noms suivent ceux du jeu.

/// <summary>Classe Java de l'item : simple, outil, arme ou pièce d'armure.</summary>
public enum ItemKind
{
    Simple,
    Sword,
    Pickaxe,
    Axe,
    Shovel,
    Hoe,
    Helmet,
    Chestplate,
    Leggings,
    Boots,
}

/// <summary>Matériau d'un outil (Tiers de Minecraft) : durabilité, vitesse, enchantabilité, réparation.</summary>
public enum ItemTier
{
    Wood,
    Stone,
    Iron,
    Gold,
    Diamond,
    Netherite,

    /// <summary>Durability, speed, enchantability and mining level chosen in the editor (V0.7.12).</summary>
    Custom,
}

/// <summary>Animation of the player while using the item (UseAnim of Minecraft), for a food (V0.7.12).</summary>
public enum ItemUseAnimation
{
    Eat,
    Drink,
    None,
    Block,
    Bow,
    Spear,
    Crossbow,
    Spyglass,
    TootHorn,
    Brush,
}

/// <summary>What the item leaves in the crafting grid, and in the fuel slot of a furnace (V0.7.12).</summary>
public enum ItemRemainderKind
{
    /// <summary>Nothing: the item is used up.</summary>
    Consumed,

    /// <summary>Another item, like the empty bucket after a cake.</summary>
    OtherItem,

    /// <summary>The item itself stays.</summary>
    Itself,

    /// <summary>The item stays and loses one point of durability; it breaks at the end.</summary>
    ItselfDamaged,
}

/// <summary>How a ranged item shoots (V0.7.12): drawn like a bow or a crossbow, thrown, or a fishing rod.</summary>
public enum RangedItemKind
{
    None,
    Bow,
    Crossbow,
    Throw,
    FishingRod,
}

/// <summary>What a ranged item shoots.</summary>
public enum RangedProjectile
{
    Arrow,
    SpectralArrow,
    Snowball,
    Egg,
}

/// <summary>When a ranged item shoots: on release (the longer it is held, the stronger), at once, or while held.</summary>
public enum RangedFireMode
{
    Charge,
    Instant,
    Automatic,
}

/// <summary>Matériau d'une armure (ArmorMaterials de Minecraft) : défense, durabilité, apparence portée.</summary>
public enum ArmorMaterialKind
{
    Leather,
    Chainmail,
    Iron,
    Gold,
    Diamond,
    Turtle,
    Netherite,
}

/// <summary>Effet de statut donné en mangeant (MobEffects de Minecraft).</summary>
public enum FoodEffectKind
{
    Speed,
    Slowness,
    Haste,
    MiningFatigue,
    Strength,
    InstantHealth,
    InstantDamage,
    JumpBoost,
    Nausea,
    Regeneration,
    Resistance,
    FireResistance,
    WaterBreathing,
    Invisibility,
    Blindness,
    NightVision,
    Hunger,
    Weakness,
    Poison,
    Wither,
    HealthBoost,
    Absorption,
    Saturation,
    Glowing,
    Levitation,
    Luck,
    SlowFalling,
}

/// <summary>Événement propre à un item, relié à un graphe (onglet Événements de l'éditeur d'item).</summary>
public enum ItemEventKind
{
    /// <summary>Clic droit dans le vide (méthode use).</summary>
    RightClick,

    /// <summary>Clic droit sur un bloc (useOn).</summary>
    UseOnBlock,

    /// <summary>Le joueur frappe une créature avec l'item (hurtEnemy).</summary>
    HitEntity,

    /// <summary>Le joueur casse un bloc avec l'item (mineBlock).</summary>
    BlockMined,

    /// <summary>Fin de l'utilisation : l'item est mangé ou bu (finishUsingItem).</summary>
    Consumed,

    /// <summary>L'item est fabriqué (onCraftedBy).</summary>
    Crafted,

    /// <summary>À chaque tick tant que l'item est tenu en main (inventoryTick).</summary>
    HeldTick,

    // ----- V0.7.12 (item.md) -----

    /// <summary>The player right-clicks a creature with the item (interactLivingEntity).</summary>
    UseOnEntity,

    /// <summary>The item runs out of durability and breaks in the hands of a player.</summary>
    Broken,

    /// <summary>The player drops the item (onDroppedByPlayer).</summary>
    Dropped,

    /// <summary>The player picks the item up from the ground (event of the loader).</summary>
    PickedUp,

    /// <summary>The player starts eating the item (first tick of the use).</summary>
    EatingStarted,

    /// <summary>The player stops eating before the end (releaseUsing).</summary>
    EatingStopped,

    /// <summary>Each tick while the player eats the item (onUseTick).</summary>
    EatingTick,

    /// <summary>Each tick while the player swings the item in hand.</summary>
    ToolSwingTick,

    /// <summary>The player swings the item (once per swing).</summary>
    ToolSwung,

    /// <summary>A ranged item starts charging (bow drawn).</summary>
    ChargeStarted,

    /// <summary>A ranged item that charges reaches its full power.</summary>
    Charged,

    /// <summary>Each tick while a ranged item charges or shoots constantly.</summary>
    ChargingTick,

    /// <summary>A ranged item shoots a projectile.</summary>
    Shot,
}

public static class ItemKindExtensions
{
    public static bool IsTool(this ItemKind kind) => kind is ItemKind.Sword or ItemKind.Pickaxe or ItemKind.Axe or ItemKind.Shovel or ItemKind.Hoe;

    public static bool IsArmor(this ItemKind kind) => kind is ItemKind.Helmet or ItemKind.Chestplate or ItemKind.Leggings or ItemKind.Boots;

    /// <summary>Les outils, armes et armures ne s'empilent pas et tirent leur durabilité de leur matériau.</summary>
    public static bool IsEquipment(this ItemKind kind) => kind.IsTool() || kind.IsArmor();

    /// <summary>Dégâts ajoutés au matériau et vitesse d'attaque des outils de Minecraft (valeurs du fer).</summary>
    public static (float Damage, float Speed) DefaultCombat(this ItemKind kind) => kind switch
    {
        ItemKind.Sword => (3f, -2.4f),
        ItemKind.Pickaxe => (1f, -2.8f),
        ItemKind.Axe => (6f, -3.1f),
        ItemKind.Shovel => (1.5f, -3f),
        ItemKind.Hoe => (-2f, -1f),
        _ => (0f, 0f),
    };
}

/// <summary>What runs an event of an event graph directly (V0.6.2): a command or a keybind of the mod.</summary>
public enum DirectEventKind
{
    Command,
    Key,
}
