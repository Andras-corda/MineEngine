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
}
