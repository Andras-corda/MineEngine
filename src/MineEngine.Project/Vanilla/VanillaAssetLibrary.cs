using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;

namespace MineEngine.Project.Vanilla;

/// <summary>Kind of a file of the game offered in the asset browser.</summary>
public enum VanillaAssetKind
{
    Texture,
    Model,
}

/// <summary>A texture or a model of Minecraft: "block/stone" (its path under textures/ or models/).</summary>
public sealed record VanillaAsset(VanillaAssetKind Kind, string Path)
{
    /// <summary>"stone".</summary>
    public string Name => Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary>"block" or "item".</summary>
    public string Folder => Path[..Math.Max(0, Path.LastIndexOf('/'))];

    /// <summary>"minecraft:block/stone", as written in a model.</summary>
    public string Reference => "minecraft:" + Path;
}

/// <summary>
/// Textures and 3D models of Minecraft, read from the client jar of the game (the launcher's
/// .minecraft/versions folder, or the cache of Gradle filled by the MDKs). The files are copied to
/// a local cache when they are first needed, so that the browser can show them.
/// </summary>
public sealed class VanillaAssetLibrary
{
    private static readonly ConcurrentDictionary<string, VanillaAssetLibrary?> Libraries = new(StringComparer.Ordinal);

    private readonly string _jar;
    private readonly string _cache;
    private readonly object _lock = new();
    private IReadOnlyList<VanillaAsset>? _assets;

    private VanillaAssetLibrary(string jar, string version, string cacheRoot)
    {
        _jar = jar;
        Version = version;
        _cache = Path.Combine(cacheRoot, version);
    }

    /// <summary>Version of the game the files come from.</summary>
    public string Version { get; }

    /// <summary>Jar read.</summary>
    public string JarPath => _jar;

    /// <summary>
    /// The library of the version (or, when its jar is not on this computer, of the nearest version
    /// found); null if no client jar of Minecraft exists here.
    /// </summary>
    public static VanillaAssetLibrary? For(string minecraftVersion, string cacheRoot) =>
        Libraries.GetOrAdd(minecraftVersion + "|" + cacheRoot, _ => Locate(minecraftVersion) is { } found ? new VanillaAssetLibrary(found.Jar, found.Version, cacheRoot) : null);

    /// <summary>The library of a given client jar (tests, or a jar chosen by hand).</summary>
    public static VanillaAssetLibrary Open(string jar, string version, string cacheRoot) => new(jar, version, cacheRoot);

    /// <summary>Textures (block/, item/) and models (block/, item/) of the game, sorted.</summary>
    public IReadOnlyList<VanillaAsset> Assets
    {
        get
        {
            lock (_lock)
            {
                return _assets ??= ReadEntries();
            }
        }
    }

    public IEnumerable<VanillaAsset> Textures => Assets.Where(a => a.Kind == VanillaAssetKind.Texture);

    public IEnumerable<VanillaAsset> Models => Assets.Where(a => a.Kind == VanillaAssetKind.Model);

    /// <summary>Copies every texture to the cache (once), so that the browser can show them all.</summary>
    public void ExtractTextures()
    {
        string marker = Path.Combine(_cache, "textures.done");
        if (File.Exists(marker))
        {
            return;
        }

        lock (_lock)
        {
            using ZipArchive zip = ZipFile.OpenRead(_jar);
            foreach (VanillaAsset texture in Textures)
            {
                Extract(zip, texture);
            }

            File.WriteAllText(marker, _jar);
        }
    }

    /// <summary>File of the asset in the cache (copied from the jar if needed), or null if it is not in the jar.</summary>
    public string? FileOf(VanillaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        string file = CachePath(asset);
        if (File.Exists(file))
        {
            return file;
        }

        lock (_lock)
        {
            using ZipArchive zip = ZipFile.OpenRead(_jar);
            return Extract(zip, asset);
        }
    }

    /// <summary>
    /// Texture that shows a model in the browser: its first texture, looked for in its parents too
    /// ("minecraft:block/stone" for the model of the stone item); null if there is none.
    /// </summary>
    public VanillaAsset? PreviewTexture(VanillaAsset model)
    {
        ArgumentNullException.ThrowIfNull(model);
        VanillaAsset? current = model;
        for (int depth = 0; depth < 6 && current is not null; depth++)
        {
            if (FileOf(current) is not { } file)
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("textures", out JsonElement textures) && textures.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in textures.EnumerateObject())
                    {
                        string value = property.Value.GetString() ?? string.Empty;
                        if (!value.StartsWith('#'))
                        {
                            return new VanillaAsset(VanillaAssetKind.Texture, Strip(value));
                        }
                    }
                }

                current = root.TryGetProperty("parent", out JsonElement parent) && parent.GetString() is { } name
                    ? new VanillaAsset(VanillaAssetKind.Model, Strip(name))
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return null;
    }

    private static string Strip(string reference) => reference.StartsWith("minecraft:", StringComparison.Ordinal) ? reference["minecraft:".Length..] : reference;

    private IReadOnlyList<VanillaAsset> ReadEntries()
    {
        var assets = new List<VanillaAsset>();
        using ZipArchive zip = ZipFile.OpenRead(_jar);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string name = entry.FullName;
            if (Parse(name) is { } asset)
            {
                assets.Add(asset);
            }
        }

        return [.. assets.OrderBy(a => a.Kind).ThenBy(a => a.Path, StringComparer.Ordinal)];
    }

    /// <summary>The asset of an entry of the jar ("assets/minecraft/textures/block/stone.png"), or null.</summary>
    public static VanillaAsset? Parse(string entry)
    {
        const string Textures = "assets/minecraft/textures/";
        const string Models = "assets/minecraft/models/";
        if (entry.StartsWith(Textures, StringComparison.Ordinal) && entry.EndsWith(".png", StringComparison.Ordinal))
        {
            string path = entry[Textures.Length..^4];
            return path.StartsWith("block/", StringComparison.Ordinal) || path.StartsWith("item/", StringComparison.Ordinal)
                ? new VanillaAsset(VanillaAssetKind.Texture, path)
                : null;
        }

        if (entry.StartsWith(Models, StringComparison.Ordinal) && entry.EndsWith(".json", StringComparison.Ordinal))
        {
            string path = entry[Models.Length..^5];
            return path.StartsWith("block/", StringComparison.Ordinal) || path.StartsWith("item/", StringComparison.Ordinal)
                ? new VanillaAsset(VanillaAssetKind.Model, path)
                : null;
        }

        return null;
    }

    private string CachePath(VanillaAsset asset) => Path.Combine(_cache, asset.Kind == VanillaAssetKind.Texture ? "textures" : "models",
        asset.Path.Replace('/', Path.DirectorySeparatorChar) + (asset.Kind == VanillaAssetKind.Texture ? ".png" : ".json"));

    private string? Extract(ZipArchive zip, VanillaAsset asset)
    {
        string entryName = "assets/minecraft/" + (asset.Kind == VanillaAssetKind.Texture ? "textures/" : "models/") + asset.Path
                           + (asset.Kind == VanillaAssetKind.Texture ? ".png" : ".json");
        if (zip.GetEntry(entryName) is not { } entry)
        {
            return null;
        }

        string file = CachePath(asset);
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            entry.ExtractToFile(file, overwrite: true);
        }

        return file;
    }

    /// <summary>Client jar of the version, or of the nearest version found on this computer.</summary>
    private static (string Jar, string Version)? Locate(string version)
    {
        List<(string Jar, string Version)> found = [.. Candidates()];
        return found.FirstOrDefault(c => c.Version == version) is { Jar: not null } exact
            ? exact
            : found.OrderByDescending(c => SharedPrefix(c.Version, version)).ThenByDescending(c => c.Version, StringComparer.Ordinal).FirstOrDefault() is { Jar: not null } near
                ? near
                : null;
    }

    private static int SharedPrefix(string a, string b)
    {
        int i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i])
        {
            i++;
        }

        return i;
    }

    private static IEnumerable<(string Jar, string Version)> Candidates()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string launcher = Path.Combine(appData, ".minecraft", "versions");
        if (Directory.Exists(launcher))
        {
            foreach (string folder in Directory.EnumerateDirectories(launcher))
            {
                string version = Path.GetFileName(folder);
                string jar = Path.Combine(folder, version + ".jar");
                // Only the versions of the game itself (not "1.20.1-forge-47.4.10").
                if (File.Exists(jar) && version.All(c => char.IsDigit(c) || c == '.'))
                {
                    yield return (jar, version);
                }
            }
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string forge = Path.Combine(home, ".gradle", "caches", "forge_gradle", "minecraft_repo", "versions");
        if (Directory.Exists(forge))
        {
            foreach (string folder in Directory.EnumerateDirectories(forge))
            {
                string version = Path.GetFileName(folder);
                foreach (string name in new[] { "client-extra.jar", "client.jar" })
                {
                    string jar = Path.Combine(folder, name);
                    if (File.Exists(jar))
                    {
                        yield return (jar, version);
                        break;
                    }
                }
            }
        }
    }
}
