using MineEngine.Core.Identifiers;

namespace MineEngine.Minecraft.Generation;

/// <summary>
/// Emplacements des ressources Minecraft dans un projet Gradle,
/// et références "espace:chemin" utilisées dans les fichiers JSON.
/// </summary>
public sealed class ResourcePaths
{
    public const string ResourcesRoot = "src/main/resources";

    private readonly string _lootTableFolder;
    private readonly string _blockTagFolder;
    private readonly string _itemTagFolder;
    private readonly string _recipeFolder;

    public ResourcePaths(ModId modId, MinecraftVersion minecraftVersion)
    {
        Namespace = (modId ?? throw new ArgumentNullException(nameof(modId))).Value;
        ArgumentNullException.ThrowIfNull(minecraftVersion);
        _lootTableFolder = minecraftVersion.UsesSingularDataFolders ? "loot_table" : "loot_tables";
        _blockTagFolder = minecraftVersion.UsesSingularDataFolders ? "block" : "blocks";
        _itemTagFolder = minecraftVersion.UsesSingularDataFolders ? "item" : "items";
        _recipeFolder = minecraftVersion.UsesSingularDataFolders ? "recipe" : "recipes";
    }

    public string Namespace { get; }

    private string AssetsRoot => $"{ResourcesRoot}/assets/{Namespace}";

    private string DataRoot => $"{ResourcesRoot}/data/{Namespace}";

    public string ItemModel(ResourceId id) => ItemModel(id.Value);

    /// <summary>Modèle d'un item dont l'identifiant n'est pas celui d'un asset (œuf d'apparition).</summary>
    public string ItemModel(string id) => $"{AssetsRoot}/models/item/{id}.json";

    public string BlockModel(ResourceId id) => $"{AssetsRoot}/models/block/{id}.json";

    public string BlockState(ResourceId id) => $"{AssetsRoot}/blockstates/{id}.json";

    public string ItemTexture(ResourceId id) => $"{AssetsRoot}/textures/item/{id}.png";

    public string BlockTexture(ResourceId id, string suffix = "") => $"{AssetsRoot}/textures/block/{id}{suffix}.png";

    public string EntityTexture(ResourceId id) => $"{AssetsRoot}/textures/entity/{id}.png";

    public string SoundsDefinition => $"{AssetsRoot}/sounds.json";

    /// <summary>Imported Java JSON model, referenced as "&lt;mod&gt;:custom/&lt;id&gt;".</summary>
    public string CustomModel(ResourceId id) => $"{AssetsRoot}/models/custom/{id}.json";

    /// <summary>Reference of a model of the project, as a parent of a block or item model ("magicmod:custom/altar").</summary>
    public string CustomModelReference(ResourceId id) => Reference($"custom/{id}");

    /// <summary>Texture of a 3D model, in the block atlas so that blocks and items can use it.</summary>
    public string ModelTexture(ResourceId model, string key) => $"{AssetsRoot}/textures/block/model/{model}_{key}.png";

    public string ModelTextureReference(ResourceId model, string key) => Reference($"block/model/{model}_{key}");

    /// <summary>Variant or layer texture of a mob model ("variant_1", "layer_1").</summary>
    public string EntityModelTexture(ResourceId model, string suffix) => $"{AssetsRoot}/textures/entity/model/{model}_{suffix}.png";

    public string SoundFile(ResourceId id) => $"{AssetsRoot}/sounds/{id}.ogg";

    public string Language(string locale) => $"{AssetsRoot}/lang/{locale}.json";

    public string BlockLootTable(ResourceId id) => $"{DataRoot}/{_lootTableFolder}/blocks/{id}.json";

    public string EntityLootTable(ResourceId id) => $"{DataRoot}/{_lootTableFolder}/entities/{id}.json";

    public string Recipe(ResourceId id) => $"{DataRoot}/{_recipeFolder}/{id}.json";

    /// <summary>Modificateur de biome propre au loader ("forge" ou "neoforge").</summary>
    public string BiomeModifier(string loaderNamespace, string name) =>
        $"{DataRoot}/{loaderNamespace}/biome_modifier/{name}.json";

    /// <summary>Tag de blocs de Minecraft ("mineable/pickaxe", "needs_iron_tool"...), complété par le mod.</summary>
    public string VanillaBlockTag(string tag) => $"{ResourcesRoot}/data/minecraft/tags/{_blockTagFolder}/{tag}.json";

    /// <summary>Tag d'items de Minecraft ("meat"...), complété par le mod.</summary>
    public string VanillaItemTag(string tag) => $"{ResourcesRoot}/data/minecraft/tags/{_itemTagFolder}/{tag}.json";

    /// <summary>Tag d'items de n'importe quel espace de noms ("c:gems/ruby", "minecraft:piglin_loved").</summary>
    public string ItemTag(NamespacedId tag) => $"{ResourcesRoot}/data/{tag.Namespace}/tags/{_itemTagFolder}/{tag.Path}.json";

    /// <summary>Data map NeoForge (1.21) : "furnace_fuels" pour les combustibles.</summary>
    public string NeoForgeItemDataMap(string name) => $"{ResourcesRoot}/data/neoforge/data_maps/item/{name}.json";

    public string Reference(string path) => $"{Namespace}:{path}";

    public string TooltipTranslationKey(ResourceId id, int line) => $"tooltip.{Namespace}.{id}.{line}";

    public string ItemTranslationKey(ResourceId id) => $"item.{Namespace}.{id}";

    public string BlockTranslationKey(ResourceId id) => $"block.{Namespace}.{id}";

    public string ItemTranslationKey(string id) => $"item.{Namespace}.{id}";

    public string EntityTranslationKey(ResourceId id) => $"entity.{Namespace}.{id}";

    public string SubtitleTranslationKey(ResourceId id) => $"subtitles.{Namespace}.{id}";

    public string CreativeTabTranslationKey => $"itemGroup.{Namespace}";

    /// <summary>Title of a creative tab of the project (V0.6.2).</summary>
    public string CreativeTabTranslationKeyFor(ResourceId id) => $"itemGroup.{Namespace}.{id}";

    /// <summary>Name of a keybind in the Controls screen.</summary>
    public string KeyTranslationKey(ResourceId id) => $"key.{Namespace}.{id}";

    /// <summary>Section of the keybinds of the mod in the Controls screen.</summary>
    public string KeyCategoryTranslationKey => $"key.categories.{Namespace}";

    public string EnchantmentTranslationKey(ResourceId id) => $"enchantment.{Namespace}.{id}";

    public string FluidTypeTranslationKey(ResourceId id) => $"fluid_type.{Namespace}.{id}";

    public string EffectTranslationKey(ResourceId id) => $"effect.{Namespace}.{id}";

    /// <summary>Name of a potion: the key has only the path of the potion ("item.minecraft.potion.effect.frenzy").</summary>
    public static string PotionTranslationKey(string form, ResourceId id) => $"item.minecraft.{form}.effect.{id}";

    /// <summary>Name of a game rule in the "Edit game rules" screen.</summary>
    public static string GameRuleTranslationKey(ResourceId id) => $"gamerule.{id}";

    /// <summary>Enchantment of the mod as data (1.21).</summary>
    public string Enchantment(ResourceId id) => $"{DataRoot}/enchantment/{id}.json";

    /// <summary>Tag of enchantments of Minecraft (1.21), to which the enchantments of the mod are added.</summary>
    public string VanillaEnchantmentTag(string tag) => $"{ResourcesRoot}/data/minecraft/tags/enchantment/{tag}.json";

    public string MobEffectTexture(ResourceId id) => $"{AssetsRoot}/textures/mob_effect/{id}.png";
}
