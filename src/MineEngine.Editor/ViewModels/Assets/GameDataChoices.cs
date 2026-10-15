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
        new(ItemRarity.Common, Tr.T("Commun (blanc)")),
        new(ItemRarity.Uncommon, Tr.T("Peu commun (jaune)")),
        new(ItemRarity.Rare, Tr.T("Rare (cyan)")),
        new(ItemRarity.Epic, Tr.T("Épique (violet)")),
    ];

    public static IReadOnlyList<Choice<CreativeTab>> CreativeTabs { get; } =
    [
        new(CreativeTab.BuildingBlocks, Tr.T("Blocs de construction")),
        new(CreativeTab.ColoredBlocks, Tr.T("Blocs colorés")),
        new(CreativeTab.NaturalBlocks, Tr.T("Blocs naturels")),
        new(CreativeTab.FunctionalBlocks, Tr.T("Blocs fonctionnels")),
        new(CreativeTab.RedstoneBlocks, Tr.T("Redstone")),
        new(CreativeTab.ToolsAndUtilities, Tr.T("Outils et utilitaires")),
        new(CreativeTab.Combat, Tr.T("Combat")),
        new(CreativeTab.FoodAndDrinks, Tr.T("Nourriture et boissons")),
        new(CreativeTab.Ingredients, Tr.T("Ingrédients")),
        new(CreativeTab.SpawnEggs, Tr.T("Œufs d'apparition")),
    ];

    public static IReadOnlyList<Choice<BlockModelKind>> BlockModels { get; } =
    [
        new(BlockModelKind.Cube, Tr.T("Cube (une texture)")),
        new(BlockModelKind.CubeFaces, Tr.T("Cube (une texture par face)")),
        new(BlockModelKind.Column, Tr.T("Colonne orientable (comme une bûche)")),
        new(BlockModelKind.Cross, Tr.T("Croix (comme une fleur)")),
    ];

    public static IReadOnlyList<Choice<BlockRenderType>> RenderTypes { get; } =
    [
        new(BlockRenderType.Solid, Tr.T("Opaque (pierre, terre)")),
        new(BlockRenderType.Cutout, Tr.T("Découpe (verre, fleurs)")),
        new(BlockRenderType.CutoutMipped, Tr.T("Découpe lissée (feuilles)")),
        new(BlockRenderType.Translucent, Tr.T("Translucide (glace, verre teinté)")),
    ];

    public static IReadOnlyList<Choice<BlockSoundType>> Sounds { get; } =
    [
        new(BlockSoundType.Stone, Tr.T("Pierre")),
        new(BlockSoundType.Wood, Tr.T("Bois")),
        new(BlockSoundType.Gravel, Tr.T("Gravier")),
        new(BlockSoundType.Grass, Tr.T("Herbe")),
        new(BlockSoundType.Metal, Tr.T("Métal")),
        new(BlockSoundType.Glass, Tr.T("Verre")),
        new(BlockSoundType.Sand, Tr.T("Sable")),
        new(BlockSoundType.Wool, Tr.T("Laine")),
        new(BlockSoundType.Snow, Tr.T("Neige")),
        new(BlockSoundType.Deepslate, Tr.T("Ardoise des abîmes")),
        new(BlockSoundType.Amethyst, Tr.T("Améthyste")),
        new(BlockSoundType.Copper, Tr.T("Cuivre")),
        new(BlockSoundType.Netherrack, Tr.T("Netherrack")),
        new(BlockSoundType.Moss, Tr.T("Mousse")),
    ];

    public static IReadOnlyList<Choice<HarvestTool>> HarvestTools { get; } =
    [
        new(HarvestTool.None, Tr.T("Aucun (à la main)")),
        new(HarvestTool.Pickaxe, Tr.T("Pioche")),
        new(HarvestTool.Axe, Tr.T("Hache")),
        new(HarvestTool.Shovel, Tr.T("Pelle")),
        new(HarvestTool.Hoe, Tr.T("Houe")),
    ];

    public static IReadOnlyList<Choice<ToolTier>> ToolTiers { get; } =
    [
        new(ToolTier.Any, Tr.T("Tous (bois ou or)")),
        new(ToolTier.Stone, Tr.T("Pierre ou mieux")),
        new(ToolTier.Iron, Tr.T("Fer ou mieux")),
        new(ToolTier.Diamond, Tr.T("Diamant ou mieux")),
    ];

    public static IReadOnlyList<Choice<BlockDropKind>> DropKinds { get; } =
    [
        new(BlockDropKind.Self, Tr.T("Lui-même")),
        new(BlockDropKind.Nothing, Tr.T("Rien")),
        new(BlockDropKind.OtherItem, Tr.T("Un autre item")),
    ];

    public static IReadOnlyList<Choice<RecipeKind>> RecipeKinds { get; } =
    [
        new(RecipeKind.Shaped, Tr.T("Établi, disposition imposée")),
        new(RecipeKind.Shapeless, Tr.T("Établi, disposition libre")),
        new(RecipeKind.Smelting, Tr.T("Four")),
    ];

    public static IReadOnlyList<Choice<MobModel>> MobModels { get; } =
    [
        new(MobModel.Zombie, Tr.T("Humanoïde (zombie)")),
        new(MobModel.Skeleton, Tr.T("Humanoïde fin (squelette)")),
        new(MobModel.Villager, Tr.T("Villageois")),
        new(MobModel.Witch, Tr.T("Sorcière")),
        new(MobModel.Creeper, Tr.T("Creeper")),
        new(MobModel.Spider, Tr.T("Araignée")),
        new(MobModel.Cow, Tr.T("Vache")),
        new(MobModel.Pig, Tr.T("Cochon")),
        new(MobModel.Chicken, Tr.T("Poule")),
        new(MobModel.Ocelot, Tr.T("Ocelot")),
        new(MobModel.Blaze, Tr.T("Blaze")),
        new(MobModel.Slime, Tr.T("Slime")),
        new(MobModel.Silverfish, Tr.T("Poisson d'argent")),
        new(MobModel.SnowGolem, Tr.T("Golem de neige")),
    ];

    public static IReadOnlyList<Choice<MobKind>> MobKinds { get; } =
    [
        new(MobKind.Monster, Tr.T("Monstre (hostile, apparaît dans le noir)")),
        new(MobKind.Creature, Tr.T("Créature (neutre ou passive)")),
        new(MobKind.Animal, Tr.T("Animal (reproduction, bébés)")),
    ];

    public static IReadOnlyList<Choice<MobTarget>> MobTargets { get; } =
    [
        new(MobTarget.Player, Tr.T("Joueurs")),
        new(MobTarget.Villager, Tr.T("Villageois")),
        new(MobTarget.IronGolem, Tr.T("Golems de fer")),
        new(MobTarget.Animal, Tr.T("Animaux")),
        new(MobTarget.Monster, Tr.T("Monstres")),
    ];

    public static IReadOnlyList<Choice<SpawnBiome>> SpawnBiomes { get; } =
    [
        new(SpawnBiome.Overworld, Tr.T("Tout l'Overworld")),
        new(SpawnBiome.Plains, Tr.T("Plaines")),
        new(SpawnBiome.Forest, Tr.T("Forêts")),
        new(SpawnBiome.Taiga, Tr.T("Taïgas")),
        new(SpawnBiome.Jungle, Tr.T("Jungles")),
        new(SpawnBiome.Savanna, Tr.T("Savanes")),
        new(SpawnBiome.Desert, Tr.T("Désert")),
        new(SpawnBiome.Swamp, Tr.T("Marais")),
        new(SpawnBiome.Mountain, Tr.T("Montagnes")),
        new(SpawnBiome.Badlands, Tr.T("Badlands")),
        new(SpawnBiome.Beach, Tr.T("Plages")),
        new(SpawnBiome.Ocean, Tr.T("Océans")),
        new(SpawnBiome.River, Tr.T("Rivières")),
        new(SpawnBiome.Nether, Tr.T("Nether")),
        new(SpawnBiome.End, Tr.T("End")),
    ];

    public static IReadOnlyList<Choice<ItemKind>> ItemKinds { get; } =
    [
        new(ItemKind.Simple, Tr.T("Item simple")),
        new(ItemKind.Sword, Tr.T("Épée")),
        new(ItemKind.Pickaxe, Tr.T("Pioche")),
        new(ItemKind.Axe, Tr.T("Hache")),
        new(ItemKind.Shovel, Tr.T("Pelle")),
        new(ItemKind.Hoe, Tr.T("Houe")),
        new(ItemKind.Helmet, Tr.T("Casque")),
        new(ItemKind.Chestplate, Tr.T("Plastron")),
        new(ItemKind.Leggings, Tr.T("Jambières")),
        new(ItemKind.Boots, Tr.T("Bottes")),
    ];

    public static IReadOnlyList<Choice<ItemTier>> ItemTiers { get; } =
    [
        new(ItemTier.Wood, Tr.T("Bois (59 utilisations)")),
        new(ItemTier.Stone, Tr.T("Pierre (131)")),
        new(ItemTier.Iron, Tr.T("Fer (250)")),
        new(ItemTier.Gold, Tr.T("Or (32, rapide)")),
        new(ItemTier.Diamond, Tr.T("Diamant (1561)")),
        new(ItemTier.Netherite, Tr.T("Netherite (2031)")),
    ];

    public static IReadOnlyList<Choice<ArmorMaterialKind>> ArmorMaterials { get; } =
    [
        new(ArmorMaterialKind.Leather, Tr.T("Cuir")),
        new(ArmorMaterialKind.Chainmail, Tr.T("Mailles")),
        new(ArmorMaterialKind.Iron, Tr.T("Fer")),
        new(ArmorMaterialKind.Gold, Tr.T("Or")),
        new(ArmorMaterialKind.Diamond, Tr.T("Diamant")),
        new(ArmorMaterialKind.Turtle, Tr.T("Carapace de tortue")),
        new(ArmorMaterialKind.Netherite, Tr.T("Netherite")),
    ];

    public static IReadOnlyList<Choice<FoodEffectKind>> FoodEffects { get; } =
    [
        new(FoodEffectKind.Speed, Tr.T("Vitesse")),
        new(FoodEffectKind.Slowness, Tr.T("Lenteur")),
        new(FoodEffectKind.Haste, Tr.T("Célérité")),
        new(FoodEffectKind.MiningFatigue, Tr.T("Fatigue de minage")),
        new(FoodEffectKind.Strength, Tr.T("Force")),
        new(FoodEffectKind.InstantHealth, Tr.T("Soin instantané")),
        new(FoodEffectKind.InstantDamage, Tr.T("Dégâts instantanés")),
        new(FoodEffectKind.JumpBoost, Tr.T("Saut amélioré")),
        new(FoodEffectKind.Nausea, Tr.T("Nausée")),
        new(FoodEffectKind.Regeneration, Tr.T("Régénération")),
        new(FoodEffectKind.Resistance, Tr.T("Résistance")),
        new(FoodEffectKind.FireResistance, Tr.T("Résistance au feu")),
        new(FoodEffectKind.WaterBreathing, Tr.T("Apnée")),
        new(FoodEffectKind.Invisibility, Tr.T("Invisibilité")),
        new(FoodEffectKind.Blindness, Tr.T("Cécité")),
        new(FoodEffectKind.NightVision, Tr.T("Vision nocturne")),
        new(FoodEffectKind.Hunger, Tr.T("Faim")),
        new(FoodEffectKind.Weakness, Tr.T("Faiblesse")),
        new(FoodEffectKind.Poison, Tr.T("Poison")),
        new(FoodEffectKind.Wither, Tr.T("Wither")),
        new(FoodEffectKind.HealthBoost, Tr.T("Bonus de vie")),
        new(FoodEffectKind.Absorption, Tr.T("Absorption")),
        new(FoodEffectKind.Saturation, Tr.T("Saturation")),
        new(FoodEffectKind.Glowing, Tr.T("Surbrillance")),
        new(FoodEffectKind.Levitation, Tr.T("Lévitation")),
        new(FoodEffectKind.Luck, Tr.T("Chance")),
        new(FoodEffectKind.SlowFalling, Tr.T("Chute lente")),
    ];

    public static string SlotLabel(BlockTextureSlot slot) => slot switch
    {
        BlockTextureSlot.Top => Tr.T("Dessus"),
        BlockTextureSlot.Bottom => Tr.T("Dessous"),
        BlockTextureSlot.North => Tr.T("Nord"),
        BlockTextureSlot.South => Tr.T("Sud"),
        BlockTextureSlot.East => Tr.T("Est"),
        BlockTextureSlot.West => Tr.T("Ouest"),
        _ => Tr.T("Texture"),
    };
}
