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
        new(ItemRarity.Common, "Commun (blanc)"),
        new(ItemRarity.Uncommon, "Peu commun (jaune)"),
        new(ItemRarity.Rare, "Rare (cyan)"),
        new(ItemRarity.Epic, "Épique (violet)"),
    ];

    public static IReadOnlyList<Choice<CreativeTab>> CreativeTabs { get; } =
    [
        new(CreativeTab.BuildingBlocks, "Blocs de construction"),
        new(CreativeTab.ColoredBlocks, "Blocs colorés"),
        new(CreativeTab.NaturalBlocks, "Blocs naturels"),
        new(CreativeTab.FunctionalBlocks, "Blocs fonctionnels"),
        new(CreativeTab.RedstoneBlocks, "Redstone"),
        new(CreativeTab.ToolsAndUtilities, "Outils et utilitaires"),
        new(CreativeTab.Combat, "Combat"),
        new(CreativeTab.FoodAndDrinks, "Nourriture et boissons"),
        new(CreativeTab.Ingredients, "Ingrédients"),
        new(CreativeTab.SpawnEggs, "Œufs d'apparition"),
    ];

    public static IReadOnlyList<Choice<BlockModelKind>> BlockModels { get; } =
    [
        new(BlockModelKind.Cube, "Cube (une texture)"),
        new(BlockModelKind.CubeFaces, "Cube (une texture par face)"),
        new(BlockModelKind.Column, "Colonne orientable (comme une bûche)"),
        new(BlockModelKind.Cross, "Croix (comme une fleur)"),
    ];

    public static IReadOnlyList<Choice<BlockRenderType>> RenderTypes { get; } =
    [
        new(BlockRenderType.Solid, "Opaque (pierre, terre)"),
        new(BlockRenderType.Cutout, "Découpe (verre, fleurs)"),
        new(BlockRenderType.CutoutMipped, "Découpe lissée (feuilles)"),
        new(BlockRenderType.Translucent, "Translucide (glace, verre teinté)"),
    ];

    public static IReadOnlyList<Choice<BlockSoundType>> Sounds { get; } =
    [
        new(BlockSoundType.Stone, "Pierre"),
        new(BlockSoundType.Wood, "Bois"),
        new(BlockSoundType.Gravel, "Gravier"),
        new(BlockSoundType.Grass, "Herbe"),
        new(BlockSoundType.Metal, "Métal"),
        new(BlockSoundType.Glass, "Verre"),
        new(BlockSoundType.Sand, "Sable"),
        new(BlockSoundType.Wool, "Laine"),
        new(BlockSoundType.Snow, "Neige"),
        new(BlockSoundType.Deepslate, "Ardoise des abîmes"),
        new(BlockSoundType.Amethyst, "Améthyste"),
        new(BlockSoundType.Copper, "Cuivre"),
        new(BlockSoundType.Netherrack, "Netherrack"),
        new(BlockSoundType.Moss, "Mousse"),
    ];

    public static IReadOnlyList<Choice<HarvestTool>> HarvestTools { get; } =
    [
        new(HarvestTool.None, "Aucun (à la main)"),
        new(HarvestTool.Pickaxe, "Pioche"),
        new(HarvestTool.Axe, "Hache"),
        new(HarvestTool.Shovel, "Pelle"),
        new(HarvestTool.Hoe, "Houe"),
    ];

    public static IReadOnlyList<Choice<ToolTier>> ToolTiers { get; } =
    [
        new(ToolTier.Any, "Tous (bois ou or)"),
        new(ToolTier.Stone, "Pierre ou mieux"),
        new(ToolTier.Iron, "Fer ou mieux"),
        new(ToolTier.Diamond, "Diamant ou mieux"),
    ];

    public static IReadOnlyList<Choice<BlockDropKind>> DropKinds { get; } =
    [
        new(BlockDropKind.Self, "Lui-même"),
        new(BlockDropKind.Nothing, "Rien"),
        new(BlockDropKind.OtherItem, "Un autre item"),
    ];

    public static IReadOnlyList<Choice<RecipeKind>> RecipeKinds { get; } =
    [
        new(RecipeKind.Shaped, "Établi, disposition imposée"),
        new(RecipeKind.Shapeless, "Établi, disposition libre"),
        new(RecipeKind.Smelting, "Four"),
    ];

    public static IReadOnlyList<Choice<MobModel>> MobModels { get; } =
    [
        new(MobModel.Zombie, "Humanoïde (zombie)"),
        new(MobModel.Skeleton, "Humanoïde fin (squelette)"),
        new(MobModel.Villager, "Villageois"),
        new(MobModel.Witch, "Sorcière"),
        new(MobModel.Creeper, "Creeper"),
        new(MobModel.Spider, "Araignée"),
        new(MobModel.Cow, "Vache"),
        new(MobModel.Pig, "Cochon"),
        new(MobModel.Chicken, "Poule"),
        new(MobModel.Ocelot, "Ocelot"),
        new(MobModel.Blaze, "Blaze"),
        new(MobModel.Slime, "Slime"),
        new(MobModel.Silverfish, "Poisson d'argent"),
        new(MobModel.SnowGolem, "Golem de neige"),
    ];

    public static IReadOnlyList<Choice<MobKind>> MobKinds { get; } =
    [
        new(MobKind.Monster, "Monstre (hostile, apparaît dans le noir)"),
        new(MobKind.Creature, "Créature (neutre ou passive)"),
        new(MobKind.Animal, "Animal (reproduction, bébés)"),
    ];

    public static IReadOnlyList<Choice<MobTarget>> MobTargets { get; } =
    [
        new(MobTarget.Player, "Joueurs"),
        new(MobTarget.Villager, "Villageois"),
        new(MobTarget.IronGolem, "Golems de fer"),
        new(MobTarget.Animal, "Animaux"),
        new(MobTarget.Monster, "Monstres"),
    ];

    public static IReadOnlyList<Choice<SpawnBiome>> SpawnBiomes { get; } =
    [
        new(SpawnBiome.Overworld, "Tout l'Overworld"),
        new(SpawnBiome.Plains, "Plaines"),
        new(SpawnBiome.Forest, "Forêts"),
        new(SpawnBiome.Taiga, "Taïgas"),
        new(SpawnBiome.Jungle, "Jungles"),
        new(SpawnBiome.Savanna, "Savanes"),
        new(SpawnBiome.Desert, "Désert"),
        new(SpawnBiome.Swamp, "Marais"),
        new(SpawnBiome.Mountain, "Montagnes"),
        new(SpawnBiome.Badlands, "Badlands"),
        new(SpawnBiome.Beach, "Plages"),
        new(SpawnBiome.Ocean, "Océans"),
        new(SpawnBiome.River, "Rivières"),
        new(SpawnBiome.Nether, "Nether"),
        new(SpawnBiome.End, "End"),
    ];

    public static IReadOnlyList<Choice<ItemKind>> ItemKinds { get; } =
    [
        new(ItemKind.Simple, "Item simple"),
        new(ItemKind.Sword, "Épée"),
        new(ItemKind.Pickaxe, "Pioche"),
        new(ItemKind.Axe, "Hache"),
        new(ItemKind.Shovel, "Pelle"),
        new(ItemKind.Hoe, "Houe"),
        new(ItemKind.Helmet, "Casque"),
        new(ItemKind.Chestplate, "Plastron"),
        new(ItemKind.Leggings, "Jambières"),
        new(ItemKind.Boots, "Bottes"),
    ];

    public static IReadOnlyList<Choice<ItemTier>> ItemTiers { get; } =
    [
        new(ItemTier.Wood, "Bois (59 utilisations)"),
        new(ItemTier.Stone, "Pierre (131)"),
        new(ItemTier.Iron, "Fer (250)"),
        new(ItemTier.Gold, "Or (32, rapide)"),
        new(ItemTier.Diamond, "Diamant (1561)"),
        new(ItemTier.Netherite, "Netherite (2031)"),
    ];

    public static IReadOnlyList<Choice<ArmorMaterialKind>> ArmorMaterials { get; } =
    [
        new(ArmorMaterialKind.Leather, "Cuir"),
        new(ArmorMaterialKind.Chainmail, "Mailles"),
        new(ArmorMaterialKind.Iron, "Fer"),
        new(ArmorMaterialKind.Gold, "Or"),
        new(ArmorMaterialKind.Diamond, "Diamant"),
        new(ArmorMaterialKind.Turtle, "Carapace de tortue"),
        new(ArmorMaterialKind.Netherite, "Netherite"),
    ];

    public static IReadOnlyList<Choice<FoodEffectKind>> FoodEffects { get; } =
    [
        new(FoodEffectKind.Speed, "Vitesse"),
        new(FoodEffectKind.Slowness, "Lenteur"),
        new(FoodEffectKind.Haste, "Célérité"),
        new(FoodEffectKind.MiningFatigue, "Fatigue de minage"),
        new(FoodEffectKind.Strength, "Force"),
        new(FoodEffectKind.InstantHealth, "Soin instantané"),
        new(FoodEffectKind.InstantDamage, "Dégâts instantanés"),
        new(FoodEffectKind.JumpBoost, "Saut amélioré"),
        new(FoodEffectKind.Nausea, "Nausée"),
        new(FoodEffectKind.Regeneration, "Régénération"),
        new(FoodEffectKind.Resistance, "Résistance"),
        new(FoodEffectKind.FireResistance, "Résistance au feu"),
        new(FoodEffectKind.WaterBreathing, "Apnée"),
        new(FoodEffectKind.Invisibility, "Invisibilité"),
        new(FoodEffectKind.Blindness, "Cécité"),
        new(FoodEffectKind.NightVision, "Vision nocturne"),
        new(FoodEffectKind.Hunger, "Faim"),
        new(FoodEffectKind.Weakness, "Faiblesse"),
        new(FoodEffectKind.Poison, "Poison"),
        new(FoodEffectKind.Wither, "Wither"),
        new(FoodEffectKind.HealthBoost, "Bonus de vie"),
        new(FoodEffectKind.Absorption, "Absorption"),
        new(FoodEffectKind.Saturation, "Saturation"),
        new(FoodEffectKind.Glowing, "Surbrillance"),
        new(FoodEffectKind.Levitation, "Lévitation"),
        new(FoodEffectKind.Luck, "Chance"),
        new(FoodEffectKind.SlowFalling, "Chute lente"),
    ];

    public static string SlotLabel(BlockTextureSlot slot) => slot switch
    {
        BlockTextureSlot.Top => "Dessus",
        BlockTextureSlot.Bottom => "Dessous",
        BlockTextureSlot.North => "Nord",
        BlockTextureSlot.South => "Sud",
        BlockTextureSlot.East => "Est",
        BlockTextureSlot.West => "Ouest",
        _ => "Texture",
    };
}
