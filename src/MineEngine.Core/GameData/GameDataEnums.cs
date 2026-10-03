namespace MineEngine.Core.GameData;

// Vocabulaire Minecraft partagé par les assets, le Mod IR et les générateurs.
// Les noms des valeurs suivent ceux du jeu ; les libellés français sont dans l'éditeur.

/// <summary>Rareté d'un item : ne change que la couleur de son nom.</summary>
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
}

/// <summary>Onglets vanilla de l'inventaire créatif (Minecraft 1.20 et plus).</summary>
public enum CreativeTab
{
    BuildingBlocks,
    ColoredBlocks,
    NaturalBlocks,
    FunctionalBlocks,
    RedstoneBlocks,
    ToolsAndUtilities,
    Combat,
    FoodAndDrinks,
    Ingredients,
    SpawnEggs,
}

/// <summary>Forme d'un bloc.</summary>
public enum BlockModelKind
{
    /// <summary>Cube avec la même texture sur les six faces.</summary>
    Cube,

    /// <summary>Cube avec une texture par face.</summary>
    CubeFaces,

    /// <summary>Colonne orientable comme une bûche : extrémités et côtés.</summary>
    Column,

    /// <summary>Deux plans en croix, comme une fleur ou une pousse.</summary>
    Cross,
}

/// <summary>Emplacement de texture d'un bloc.</summary>
public enum BlockTextureSlot
{
    Main,
    Top,
    Bottom,
    North,
    South,
    East,
    West,
}

/// <summary>Gestion de la transparence au rendu.</summary>
public enum BlockRenderType
{
    Solid,
    Cutout,
    CutoutMipped,
    Translucent,
}

/// <summary>Jeu de sons vanilla (casse, pose, pas...).</summary>
public enum BlockSoundType
{
    Stone,
    Wood,
    Gravel,
    Grass,
    Metal,
    Glass,
    Sand,
    Wool,
    Snow,
    Deepslate,
    Amethyst,
    Copper,
    Netherrack,
    Moss,
}

/// <summary>Outil le plus efficace pour casser un bloc.</summary>
public enum HarvestTool
{
    None,
    Pickaxe,
    Axe,
    Shovel,
    Hoe,
}

/// <summary>Niveau d'outil minimum pour récupérer un bloc.</summary>
public enum ToolTier
{
    Any,
    Stone,
    Iron,
    Diamond,
}

/// <summary>Ce que le bloc laisse quand on le casse.</summary>
public enum BlockDropKind
{
    Self,
    Nothing,
    OtherItem,
}

/// <summary>Apparence d'un mob : modèle 3D d'une créature de Minecraft.</summary>
public enum MobModel
{
    Zombie,
    Skeleton,
    Villager,
    Witch,
    Creeper,
    Spider,
    Cow,
    Pig,
    Chicken,
    Ocelot,
    Blaze,
    Slime,
    Silverfish,
    SnowGolem,
}

/// <summary>Famille d'un mob : décide de sa classe Java, de son apparition et de sa reproduction.</summary>
public enum MobKind
{
    /// <summary>Hostile : apparaît dans le noir, disparaît en mode paisible.</summary>
    Monster,

    /// <summary>Créature neutre ou passive, sans reproduction.</summary>
    Creature,

    /// <summary>Animal : apparaît sur l'herbe, peut se reproduire et avoir des bébés.</summary>
    Animal,
}

/// <summary>Comportements d'IA ("goals" de Minecraft) qu'un mob peut combiner.</summary>
public enum MobGoalKind
{
    Swim,
    Panic,
    MeleeAttack,
    LeapAtTarget,
    RandomStroll,
    LookAtPlayer,
    RandomLookAround,
    Tempt,
    Breed,
    FollowParent,
    AvoidEntity,
    FleeSun,
    MoveTowardsTarget,
    HurtByTarget,
    AttackNearest,
}

/// <summary>Famille de créatures visée par un comportement (cibler, fuir...).</summary>
public enum MobTarget
{
    Player,
    Villager,
    IronGolem,
    Animal,
    Monster,
}

/// <summary>Biomes (ou familles de biomes) où un mob peut apparaître naturellement.</summary>
public enum SpawnBiome
{
    Overworld,
    Plains,
    Forest,
    Taiga,
    Jungle,
    Savanna,
    Desert,
    Swamp,
    Mountain,
    Badlands,
    Beach,
    Ocean,
    River,
    Nether,
    End,
}

/// <summary>Type de recette.</summary>
public enum RecipeKind
{
    /// <summary>Établi, disposition imposée.</summary>
    Shaped,

    /// <summary>Établi, disposition libre.</summary>
    Shapeless,

    /// <summary>Four.</summary>
    Smelting,
}
