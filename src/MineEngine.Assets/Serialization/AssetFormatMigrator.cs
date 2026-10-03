using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Serialization;

/// <summary>Fichier d'asset lu sur le disque, avant sa conversion en objet.</summary>
public sealed class AssetDocument
{
    public AssetDocument(JsonObject root, string sourceName)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        SourceName = sourceName;
    }

    public JsonObject Root { get; }

    public string SourceName { get; }
}

/// <summary>Résultat d'une migration : documents au format courant et ce qui a changé.</summary>
public sealed class AssetMigrationResult
{
    public AssetMigrationResult(IReadOnlyList<AssetDocument> documents, IReadOnlyList<string> notes)
    {
        Documents = documents;
        Notes = notes;
    }

    public IReadOnlyList<AssetDocument> Documents { get; }

    /// <summary>Changements effectués, à afficher à l'utilisateur ; vide si rien n'a changé.</summary>
    public IReadOnlyList<string> Notes { get; }

    public bool HasChanges => Notes.Count > 0;
}

/// <summary>
/// Convertit les fichiers d'assets des anciennes versions au format courant. Travaille
/// sur l'ensemble du projet, car certaines conversions relient des assets entre eux :
/// <list type="bullet">
/// <item>format 1 (V0.1, V0.2) : chaque chemin de texture devient un asset Texture
/// (une seule par image), désigné par son guid ;</item>
/// <item>format 1 : l'item laissé par un bloc ("ruby") devient une référence vers l'item du
/// projet portant cet identifiant, ou vers l'item de Minecraft.</item>
/// </list>
/// </summary>
public sealed partial class AssetFormatMigrator
{
    private const int TexturesAsAssetsVersion = 2;

    public AssetMigrationResult Migrate(IReadOnlyList<AssetDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var notes = new List<string>();
        var result = new List<AssetDocument>(documents);

        List<AssetDocument> outdated = documents
            .Where(d => AssetSerializer.ReadFormatVersion(d.Root) < TexturesAsAssetsVersion)
            .ToList();
        if (outdated.Count > 0)
        {
            var textures = new TextureCollector(documents);
            foreach (AssetDocument document in outdated)
            {
                MigrateTextures(document, textures);
                MigrateBlockDrop(document, documents);
                document.Root["formatVersion"] = TexturesAsAssetsVersion;
            }

            result.AddRange(textures.Created);
            notes.Add($"{outdated.Count} asset(s) convertis au format de la V0.3.");
            if (textures.Created.Count > 0)
            {
                notes.Add($"{textures.Created.Count} texture(s) transformée(s) en assets Texture.");
            }
        }

        return new AssetMigrationResult(result, notes);
    }

    private static void MigrateTextures(AssetDocument document, TextureCollector textures)
    {
        if (document.Root["properties"] is not JsonObject properties)
        {
            return;
        }

        if (ReadString(properties, "texture") is { } mainPath)
        {
            properties["texture"] = textures.GetOrCreate(mainPath);
        }

        if (properties["textures"] is JsonObject slots)
        {
            foreach (string slot in slots.Select(p => p.Key).ToList())
            {
                slots[slot] = ReadString(slots, slot) is { } path ? textures.GetOrCreate(path) : null;
            }
        }
    }

    /// <summary>"ruby" désigne l'item du projet s'il existe ; sinon l'identifiant est gardé tel quel.</summary>
    private static void MigrateBlockDrop(AssetDocument document, IReadOnlyList<AssetDocument> all)
    {
        if (document.Root["properties"]?["drop"] is not JsonObject drop)
        {
            return;
        }

        string? text = ReadString(drop, "item");
        if (text is null || !NamespacedId.TryParse(text, out NamespacedId? id))
        {
            drop["item"] = null;
            return;
        }

        AssetDocument? target = text.Contains(':')
            ? null
            : all.FirstOrDefault(d => ReadString(d.Root, "resourceId") == id!.Path
                                      && ReadString(d.Root, "type") is nameof(AssetType.Item) or nameof(AssetType.Block));
        drop["item"] = target is not null && ReadString(target.Root, "guid") is { } guid
            ? ContentReference.ToAsset(Guid.Parse(guid)).ToStorageText()
            : ContentReference.ToId(id!).ToStorageText();
    }

    private static string? ReadString(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    [GeneratedRegex("-[0-9a-f]{8}$")]
    private static partial Regex ImportHashSuffix();

    /// <summary>Crée un asset Texture par image distincte et retient son guid.</summary>
    private sealed class TextureCollector
    {
        private readonly Dictionary<string, string> _guidByPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _usedIds;

        public TextureCollector(IReadOnlyList<AssetDocument> existing)
        {
            _usedIds = existing
                .Where(d => ReadString(d.Root, "type") == nameof(AssetType.Texture))
                .Select(d => ReadString(d.Root, "resourceId"))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
        }

        public List<AssetDocument> Created { get; } = [];

        public string GetOrCreate(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (_guidByPath.TryGetValue(normalized, out string? guid))
            {
                return guid;
            }

            guid = Guid.NewGuid().ToString("D");
            _guidByPath[normalized] = guid;

            string name = ImportHashSuffix().Replace(Path.GetFileNameWithoutExtension(normalized), string.Empty);
            string id = UniqueId(ResourceId.FromText(name).Value);
            var root = new JsonObject
            {
                ["formatVersion"] = AssetSerializer.FormatVersion,
                ["guid"] = guid,
                ["type"] = nameof(AssetType.Texture),
                ["resourceId"] = id,
                ["displayName"] = name,
                ["properties"] = new JsonObject { ["file"] = normalized },
            };
            Created.Add(new AssetDocument(root, $"Texture '{id}' (migration)"));
            return guid;
        }

        private string UniqueId(string stem)
        {
            string candidate = stem;
            for (int suffix = 2; !_usedIds.Add(candidate); suffix++)
            {
                string suffixText = "_" + suffix;
                candidate = stem[..Math.Min(stem.Length, ResourceId.MaxLength - suffixText.Length)] + suffixText;
            }

            return candidate;
        }
    }
}
