using MineEngine.Core.GameData;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Une valeur proposée dans une liste déroulante, avec son libellé.</summary>
public sealed class Choice<T>
{
    public Choice(T value, string label)
    {
        Value = value;
        Label = label;
    }

    public T Value { get; }

    public string Label { get; }

    public override string ToString() => Label;
}

/// <summary>Libellés français des options Minecraft proposées dans l'Inspector.</summary>
public static class GameDataChoices
{
    public static IReadOnlyList<Choice<ItemRarity>> Rarities { get; } =
    [
        new(ItemRarity.Common, Tr.T("Common (white)")),
        new(ItemRarity.Uncommon, Tr.T("Uncommon (yellow)")),
        new(ItemRarity.Rare, Tr.T("Rare (cyan)")),
        new(ItemRarity.Epic, Tr.T("Epic (purple)")),
    ];

    public static IReadOnlyList<Choice<CreativeTab>> CreativeTabs { get; } =
    [
        new(CreativeTab.BuildingBlocks, Tr.T("Building Blocks")),
        new(CreativeTab.ColoredBlocks, Tr.T("Colored Blocks")),
        new(CreativeTab.NaturalBlocks, Tr.T("Natural Blocks")),
        new(CreativeTab.FunctionalBlocks, Tr.T("Functional Blocks")),
        new(CreativeTab.RedstoneBlocks, Tr.T("Redstone")),
        new(CreativeTab.ToolsAndUtilities, Tr.T("Tools & Utilities")),
        new(CreativeTab.Combat, Tr.T("Combat")),
        new(CreativeTab.FoodAndDrinks, Tr.T("Food & Drinks")),
        new(CreativeTab.Ingredients, Tr.T("Ingredients")),
        new(CreativeTab.SpawnEggs, Tr.T("Spawn Eggs")),
    ];

    public static IReadOnlyList<Choice<BlockModelKind>> BlockModels { get; } =
    [
        new(BlockModelKind.Cube, Tr.T("Cube (one texture)")),
        new(BlockModelKind.CubeFaces, Tr.T("Cube (one texture per face)")),
        new(BlockModelKind.Column, Tr.T("Rotatable pillar (like a log)")),
        new(BlockModelKind.Cross, Tr.T("Cross (like a flower)")),
        new(BlockModelKind.Custom, Tr.T("Imported 3D model")),
    ];

    public static IReadOnlyList<Choice<BlockRenderType>> RenderTypes { get; } =
    [
        new(BlockRenderType.Solid, Tr.T("Opaque (stone, dirt)")),
        new(BlockRenderType.Cutout, Tr.T("Cutout (glass, flowers)")),
        new(BlockRenderType.CutoutMipped, Tr.T("Smooth cutout (leaves)")),
        new(BlockRenderType.Translucent, Tr.T("Translucent (ice, stained glass)")),
    ];

    public static IReadOnlyList<Choice<BlockSoundType>> Sounds { get; } =
    [
        new(BlockSoundType.Stone, Tr.T("Stone")),
        new(BlockSoundType.Wood, Tr.T("Wood")),
        new(BlockSoundType.Gravel, Tr.T("Gravel")),
        new(BlockSoundType.Grass, Tr.T("Grass")),
        new(BlockSoundType.Metal, Tr.T("Metal")),
        new(BlockSoundType.Glass, Tr.T("Glass")),
        new(BlockSoundType.Sand, Tr.T("Sand")),
        new(BlockSoundType.Wool, Tr.T("Wool")),
        new(BlockSoundType.Snow, Tr.T("Snow")),
        new(BlockSoundType.Deepslate, Tr.T("Deepslate")),
        new(BlockSoundType.Amethyst, Tr.T("Amethyst")),
        new(BlockSoundType.Copper, Tr.T("Copper")),
        new(BlockSoundType.Netherrack, Tr.T("Netherrack")),
        new(BlockSoundType.Moss, Tr.T("Moss")),
    ];

    public static IReadOnlyList<Choice<HarvestTool>> HarvestTools { get; } =
    [
        new(HarvestTool.None, Tr.T("None (by hand)")),
        new(HarvestTool.Pickaxe, Tr.T("Pickaxe")),
        new(HarvestTool.Axe, Tr.T("Axe")),
        new(HarvestTool.Shovel, Tr.T("Shovel")),
        new(HarvestTool.Hoe, Tr.T("Hoe")),
    ];

    public static IReadOnlyList<Choice<ToolTier>> ToolTiers { get; } =
    [
        new(ToolTier.Any, Tr.T("Any (wood or gold)")),
        new(ToolTier.Stone, Tr.T("Stone or better")),
        new(ToolTier.Iron, Tr.T("Iron or better")),
        new(ToolTier.Diamond, Tr.T("Diamond or better")),
    ];

    public static IReadOnlyList<Choice<BlockDropKind>> DropKinds { get; } =
    [
        new(BlockDropKind.Self, Tr.T("Itself")),
        new(BlockDropKind.Nothing, Tr.T("Nothing")),
        new(BlockDropKind.OtherItem, Tr.T("Another item")),
    ];

    public static IReadOnlyList<Choice<RecipeKind>> RecipeKinds { get; } =
    [
        new(RecipeKind.Shaped, Tr.T("Crafting table, fixed pattern")),
        new(RecipeKind.Shapeless, Tr.T("Crafting table, free pattern")),
        new(RecipeKind.Smelting, Tr.T("Furnace")),
    ];

    public static IReadOnlyList<Choice<MobModel>> MobModels { get; } =
    [
        new(MobModel.Zombie, Tr.T("Humanoid (zombie)")),
        new(MobModel.Skeleton, Tr.T("Thin humanoid (skeleton)")),
        new(MobModel.Villager, Tr.T("Villagers")),
        new(MobModel.Witch, Tr.T("Witch")),
        new(MobModel.Creeper, Tr.T("Creeper")),
        new(MobModel.Spider, Tr.T("Spider")),
        new(MobModel.Cow, Tr.T("Cow")),
        new(MobModel.Pig, Tr.T("Pig")),
        new(MobModel.Chicken, Tr.T("Chicken")),
        new(MobModel.Ocelot, Tr.T("Ocelot")),
        new(MobModel.Blaze, Tr.T("Blaze")),
        new(MobModel.Slime, Tr.T("Slime")),
        new(MobModel.Silverfish, Tr.T("Silverfish")),
        new(MobModel.SnowGolem, Tr.T("Snow Golem")),
        new(MobModel.Custom, Tr.T("3D model (Blockbench)")),
    ];

    public static IReadOnlyList<Choice<CommandArgumentKind>> CommandArguments { get; } =
    [
        new(CommandArgumentKind.None, Tr.T("None")),
        new(CommandArgumentKind.Number, Tr.T("A whole number")),
        new(CommandArgumentKind.Word, Tr.T("One word")),
        new(CommandArgumentKind.Message, Tr.T("A message (the rest of the line)")),
        new(CommandArgumentKind.Player, Tr.T("A player")),
    ];

    public static IReadOnlyList<Choice<int>> PermissionLevels { get; } =
    [
        new(0, Tr.T("Every player")),
        new(2, Tr.T("Operators (cheats)")),
        new(4, Tr.T("Server owner")),
    ];

    public static IReadOnlyList<Choice<EnchantmentTarget>> EnchantmentTargets { get; } =
    [
        new(EnchantmentTarget.Weapon, Tr.T("Weapons (swords, axes)")),
        new(EnchantmentTarget.Sword, Tr.T("Swords")),
        new(EnchantmentTarget.Armor, Tr.T("All armor")),
        new(EnchantmentTarget.Helmet, Tr.T("Helmets")),
        new(EnchantmentTarget.Chestplate, Tr.T("Chestplates")),
        new(EnchantmentTarget.Leggings, Tr.T("Leggings")),
        new(EnchantmentTarget.Boots, Tr.T("Boots")),
        new(EnchantmentTarget.Tool, Tr.T("Mining tools")),
        new(EnchantmentTarget.Bow, Tr.T("Bows")),
        new(EnchantmentTarget.Crossbow, Tr.T("Crossbows")),
        new(EnchantmentTarget.Trident, Tr.T("Tridents")),
        new(EnchantmentTarget.FishingRod, Tr.T("Fishing rods")),
        new(EnchantmentTarget.Breakable, Tr.T("Anything with durability")),
    ];

    public static IReadOnlyList<Choice<EnchantmentRarity>> EnchantmentRarities { get; } =
    [
        new(EnchantmentRarity.Common, Tr.T("Common")),
        new(EnchantmentRarity.Uncommon, Tr.T("Uncommon")),
        new(EnchantmentRarity.Rare, Tr.T("Rare")),
        new(EnchantmentRarity.VeryRare, Tr.T("Very rare")),
    ];

    public static IReadOnlyList<Choice<GameRuleKind>> GameRuleKinds { get; } =
    [
        new(GameRuleKind.Boolean, Tr.T("True or false")),
        new(GameRuleKind.Number, Tr.T("Number")),
    ];

    public static IReadOnlyList<Choice<GameRuleCategory>> GameRuleCategories { get; } =
    [
        new(GameRuleCategory.Player, Tr.T("Player")),
        new(GameRuleCategory.Mobs, Tr.T("Mobs")),
        new(GameRuleCategory.Spawning, Tr.T("Spawning")),
        new(GameRuleCategory.Drops, Tr.T("Drops")),
        new(GameRuleCategory.Updates, Tr.T("World updates")),
        new(GameRuleCategory.Chat, Tr.T("Chat")),
        new(GameRuleCategory.Misc, Tr.T("Miscellaneous")),
    ];

    public static IReadOnlyList<Choice<EffectCategory>> EffectCategories { get; } =
    [
        new(EffectCategory.Beneficial, Tr.T("Beneficial")),
        new(EffectCategory.Harmful, Tr.T("Harmful")),
        new(EffectCategory.Neutral, Tr.T("Neutral")),
    ];

    public static IReadOnlyList<Choice<MobKind>> MobKinds { get; } =
    [
        new(MobKind.Monster, Tr.T("Monster (hostile, spawns in the dark)")),
        new(MobKind.Creature, Tr.T("Creature (neutral or passive)")),
        new(MobKind.Animal, Tr.T("Animal (breeding, babies)")),
    ];

    public static IReadOnlyList<Choice<MobTarget>> MobTargets { get; } =
    [
        new(MobTarget.Player, Tr.T("Players")),
        new(MobTarget.Villager, Tr.T("Villagers")),
        new(MobTarget.IronGolem, Tr.T("Iron Golems")),
        new(MobTarget.Animal, Tr.T("Animals")),
        new(MobTarget.Monster, Tr.T("Monsters")),
    ];

    public static IReadOnlyList<Choice<SpawnBiome>> SpawnBiomes { get; } =
    [
        new(SpawnBiome.Overworld, Tr.T("Whole Overworld")),
        new(SpawnBiome.Plains, Tr.T("Plains")),
        new(SpawnBiome.Forest, Tr.T("Forests")),
        new(SpawnBiome.Taiga, Tr.T("Taigas")),
        new(SpawnBiome.Jungle, Tr.T("Jungles")),
        new(SpawnBiome.Savanna, Tr.T("Savannas")),
        new(SpawnBiome.Desert, Tr.T("Deserts")),
        new(SpawnBiome.Swamp, Tr.T("Swamps")),
        new(SpawnBiome.Mountain, Tr.T("Mountains")),
        new(SpawnBiome.Badlands, Tr.T("Badlands")),
        new(SpawnBiome.Beach, Tr.T("Beaches")),
        new(SpawnBiome.Ocean, Tr.T("Oceans")),
        new(SpawnBiome.River, Tr.T("Rivers")),
        new(SpawnBiome.Nether, Tr.T("Nether")),
        new(SpawnBiome.End, Tr.T("End")),
    ];

    public static IReadOnlyList<Choice<ItemKind>> ItemKinds { get; } =
    [
        new(ItemKind.Simple, Tr.T("Simple item")),
        new(ItemKind.Sword, Tr.T("Sword")),
        new(ItemKind.Pickaxe, Tr.T("Pickaxe")),
        new(ItemKind.Axe, Tr.T("Axe")),
        new(ItemKind.Shovel, Tr.T("Shovel")),
        new(ItemKind.Hoe, Tr.T("Hoe")),
        new(ItemKind.Helmet, Tr.T("Helmet")),
        new(ItemKind.Chestplate, Tr.T("Chestplate")),
        new(ItemKind.Leggings, Tr.T("Leggings")),
        new(ItemKind.Boots, Tr.T("Boots")),
    ];

    public static IReadOnlyList<Choice<ItemTier>> ItemTiers { get; } =
    [
        new(ItemTier.Wood, Tr.T("Wood (59 uses)")),
        new(ItemTier.Stone, Tr.T("Stone (131)")),
        new(ItemTier.Iron, Tr.T("Iron (250)")),
        new(ItemTier.Gold, Tr.T("Gold (32, fast)")),
        new(ItemTier.Diamond, Tr.T("Diamond (1561)")),
        new(ItemTier.Netherite, Tr.T("Netherite (2031)")),
    ];

    public static IReadOnlyList<Choice<ArmorMaterialKind>> ArmorMaterials { get; } =
    [
        new(ArmorMaterialKind.Leather, Tr.T("Leather")),
        new(ArmorMaterialKind.Chainmail, Tr.T("Chainmail")),
        new(ArmorMaterialKind.Iron, Tr.T("Iron")),
        new(ArmorMaterialKind.Gold, Tr.T("Gold")),
        new(ArmorMaterialKind.Diamond, Tr.T("Diamond")),
        new(ArmorMaterialKind.Turtle, Tr.T("Turtle Shell")),
        new(ArmorMaterialKind.Netherite, Tr.T("Netherite")),
    ];

    public static IReadOnlyList<Choice<FoodEffectKind>> FoodEffects { get; } =
    [
        new(FoodEffectKind.Speed, Tr.T("Speed")),
        new(FoodEffectKind.Slowness, Tr.T("Slowness")),
        new(FoodEffectKind.Haste, Tr.T("Haste")),
        new(FoodEffectKind.MiningFatigue, Tr.T("Mining Fatigue")),
        new(FoodEffectKind.Strength, Tr.T("Strength")),
        new(FoodEffectKind.InstantHealth, Tr.T("Instant Health")),
        new(FoodEffectKind.InstantDamage, Tr.T("Instant Damage")),
        new(FoodEffectKind.JumpBoost, Tr.T("Jump Boost")),
        new(FoodEffectKind.Nausea, Tr.T("Nausea")),
        new(FoodEffectKind.Regeneration, Tr.T("Regeneration")),
        new(FoodEffectKind.Resistance, Tr.T("Resistance")),
        new(FoodEffectKind.FireResistance, Tr.T("Fire Resistance")),
        new(FoodEffectKind.WaterBreathing, Tr.T("Water Breathing")),
        new(FoodEffectKind.Invisibility, Tr.T("Invisibility")),
        new(FoodEffectKind.Blindness, Tr.T("Blindness")),
        new(FoodEffectKind.NightVision, Tr.T("Night Vision")),
        new(FoodEffectKind.Hunger, Tr.T("Hunger")),
        new(FoodEffectKind.Weakness, Tr.T("Weakness")),
        new(FoodEffectKind.Poison, Tr.T("Poison")),
        new(FoodEffectKind.Wither, Tr.T("Wither")),
        new(FoodEffectKind.HealthBoost, Tr.T("Health Boost")),
        new(FoodEffectKind.Absorption, Tr.T("Absorption")),
        new(FoodEffectKind.Saturation, Tr.T("Saturation")),
        new(FoodEffectKind.Glowing, Tr.T("Glowing")),
        new(FoodEffectKind.Levitation, Tr.T("Levitation")),
        new(FoodEffectKind.Luck, Tr.T("Chance")),
        new(FoodEffectKind.SlowFalling, Tr.T("Slow Falling")),
    ];

    public static string SlotLabel(BlockTextureSlot slot) => slot switch
    {
        BlockTextureSlot.Top => Tr.T("Top"),
        BlockTextureSlot.Bottom => Tr.T("Bottom"),
        BlockTextureSlot.North => Tr.T("Front (north)"),
        BlockTextureSlot.South => Tr.T("Back (south)"),
        BlockTextureSlot.East => Tr.T("Right (east)"),
        BlockTextureSlot.West => Tr.T("Left (west)"),
        _ => Tr.T("Texture"),
    };
}
