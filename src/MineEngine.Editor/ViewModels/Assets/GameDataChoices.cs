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
