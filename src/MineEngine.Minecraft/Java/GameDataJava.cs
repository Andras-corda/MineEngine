using MineEngine.Core.GameData;

namespace MineEngine.Minecraft.Java;

/// <summary>Correspondance entre le vocabulaire Mine Engine et les constantes Java de Minecraft.</summary>
public static class GameDataJava
{
    public static string Rarity(ItemRarity rarity) => "Rarity." + rarity switch
    {
        ItemRarity.Uncommon => "UNCOMMON",
        ItemRarity.Rare => "RARE",
        ItemRarity.Epic => "EPIC",
        _ => "COMMON",
    };

    public static string CreativeTab(CreativeTab tab) => "CreativeModeTabs." + tab switch
    {
        Core.GameData.CreativeTab.BuildingBlocks => "BUILDING_BLOCKS",
        Core.GameData.CreativeTab.ColoredBlocks => "COLORED_BLOCKS",
        Core.GameData.CreativeTab.NaturalBlocks => "NATURAL_BLOCKS",
        Core.GameData.CreativeTab.FunctionalBlocks => "FUNCTIONAL_BLOCKS",
        Core.GameData.CreativeTab.RedstoneBlocks => "REDSTONE_BLOCKS",
        Core.GameData.CreativeTab.ToolsAndUtilities => "TOOLS_AND_UTILITIES",
        Core.GameData.CreativeTab.Combat => "COMBAT",
        Core.GameData.CreativeTab.FoodAndDrinks => "FOOD_AND_DRINKS",
        Core.GameData.CreativeTab.Ingredients => "INGREDIENTS",
        _ => "SPAWN_EGGS",
    };

    public static string Sound(BlockSoundType sound) => "SoundType." + sound switch
    {
        BlockSoundType.Wood => "WOOD",
        BlockSoundType.Gravel => "GRAVEL",
        BlockSoundType.Grass => "GRASS",
        BlockSoundType.Metal => "METAL",
        BlockSoundType.Glass => "GLASS",
        BlockSoundType.Sand => "SAND",
        BlockSoundType.Wool => "WOOL",
        BlockSoundType.Snow => "SNOW",
        BlockSoundType.Deepslate => "DEEPSLATE",
        BlockSoundType.Amethyst => "AMETHYST",
        BlockSoundType.Copper => "COPPER",
        BlockSoundType.Netherrack => "NETHERRACK",
        BlockSoundType.Moss => "MOSS",
        _ => "STONE",
    };

    /// <summary>Constante MobEffects d'un effet (même nom en 1.20.1 et en 1.21.1).</summary>
    public static string Effect(FoodEffectKind effect) => "MobEffects." + effect switch
    {
        FoodEffectKind.Speed => "MOVEMENT_SPEED",
        FoodEffectKind.Slowness => "MOVEMENT_SLOWDOWN",
        FoodEffectKind.Haste => "DIG_SPEED",
        FoodEffectKind.MiningFatigue => "DIG_SLOWDOWN",
        FoodEffectKind.Strength => "DAMAGE_BOOST",
        FoodEffectKind.InstantHealth => "HEAL",
        FoodEffectKind.InstantDamage => "HARM",
        FoodEffectKind.JumpBoost => "JUMP",
        FoodEffectKind.Nausea => "CONFUSION",
        FoodEffectKind.Regeneration => "REGENERATION",
        FoodEffectKind.Resistance => "DAMAGE_RESISTANCE",
        FoodEffectKind.FireResistance => "FIRE_RESISTANCE",
        FoodEffectKind.WaterBreathing => "WATER_BREATHING",
        FoodEffectKind.Invisibility => "INVISIBILITY",
        FoodEffectKind.Blindness => "BLINDNESS",
        FoodEffectKind.NightVision => "NIGHT_VISION",
        FoodEffectKind.Hunger => "HUNGER",
        FoodEffectKind.Weakness => "WEAKNESS",
        FoodEffectKind.Poison => "POISON",
        FoodEffectKind.Wither => "WITHER",
        FoodEffectKind.HealthBoost => "HEALTH_BOOST",
        FoodEffectKind.Absorption => "ABSORPTION",
        FoodEffectKind.Saturation => "SATURATION",
        FoodEffectKind.Glowing => "GLOWING",
        FoodEffectKind.Levitation => "LEVITATION",
        FoodEffectKind.Luck => "LUCK",
        _ => "SLOW_FALLING",
    };
}