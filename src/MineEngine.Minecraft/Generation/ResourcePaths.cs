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

    public string Reference(string path) => $"{Namespace}:{path}";

    public string TooltipTranslationKey(ResourceId id, int line) => $"tooltip.{Namespace}.{id}.{line}";

    public string ItemTranslationKey(ResourceId id) => $"item.{Namespace}.{id}";

    public string BlockTranslationKey(ResourceId id) => $"block.{Namespace}.{id}";

    public string ItemTranslationKey(string id) => $"item.{Namespace}.{id}";

    public string EntityTranslationKey(ResourceId id) => $"entity.{Namespace}.{id}";

    public string SubtitleTranslationKey(ResourceId id) => $"subtitles.{Namespace}.{id}";

    public string CreativeTabTranslationKey => $"itemGroup.{Namespace}";
}
