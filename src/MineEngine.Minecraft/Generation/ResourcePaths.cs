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

    public ResourcePaths(ModId modId, MinecraftVersion minecraftVersion)
    {
        Namespace = (modId ?? throw new ArgumentNullException(nameof(modId))).Value;
        ArgumentNullException.ThrowIfNull(minecraftVersion);
        _lootTableFolder = minecraftVersion.UsesSingularDataFolders ? "loot_table" : "loot_tables";
    }

    public string Namespace { get; }

    private string AssetsRoot => $"{ResourcesRoot}/assets/{Namespace}";

    private string DataRoot => $"{ResourcesRoot}/data/{Namespace}";

    public string ItemModel(ResourceId id) => $"{AssetsRoot}/models/item/{id}.json";

    public string BlockModel(ResourceId id) => $"{AssetsRoot}/models/block/{id}.json";

    public string BlockState(ResourceId id) => $"{AssetsRoot}/blockstates/{id}.json";

    public string ItemTexture(ResourceId id) => $"{AssetsRoot}/textures/item/{id}.png";

    public string BlockTexture(ResourceId id) => $"{AssetsRoot}/textures/block/{id}.png";

    public string Language(string locale) => $"{AssetsRoot}/lang/{locale}.json";

    public string BlockLootTable(ResourceId id) => $"{DataRoot}/{_lootTableFolder}/blocks/{id}.json";

    public string Reference(string path) => $"{Namespace}:{path}";

    public string ItemTranslationKey(ResourceId id) => $"item.{Namespace}.{id}";

    public string BlockTranslationKey(ResourceId id) => $"block.{Namespace}.{id}";

    public string CreativeTabTranslationKey => $"itemGroup.{Namespace}";
}
