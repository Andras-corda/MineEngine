namespace MineEngine.Minecraft.Mdk;

/// <summary>
/// Un MDK (Mod Development Kit) installé dans la bibliothèque : le projet Gradle
/// officiel d'un loader pour une version de Minecraft donnée.
/// </summary>
public sealed class MdkDescriptor
{
    public MdkDescriptor(
        string id,
        ModLoaderKind loader,
        MinecraftVersion minecraftVersion,
        string loaderVersion,
        int javaVersion,
        string? gradleVersion,
        string directory,
        string source,
        DateTime installedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Id = id;
        Loader = loader;
        MinecraftVersion = minecraftVersion ?? throw new ArgumentNullException(nameof(minecraftVersion));
        LoaderVersion = loaderVersion ?? string.Empty;
        JavaVersion = javaVersion;
        GradleVersion = gradleVersion;
        Directory = Path.GetFullPath(directory);
        Source = source ?? string.Empty;
        InstalledAt = installedAt;
    }

    /// <summary>Identifiant stable, par exemple "forge-1.20.1-47.4.10".</summary>
    public string Id { get; }

    public ModLoaderKind Loader { get; }

    public MinecraftVersion MinecraftVersion { get; }

    public string LoaderVersion { get; }

    /// <summary>Version de Java utilisée pour compiler le mod.</summary>
    public int JavaVersion { get; }

    /// <summary>Version du Gradle wrapper fourni avec le MDK, si elle est connue.</summary>
    public string? GradleVersion { get; }

    public string Directory { get; }

    /// <summary>Origine du MDK (téléchargement, archive ou dossier importé).</summary>
    public string Source { get; }

    public DateTime InstalledAt { get; }

    public string DisplayName => $"{Loader.ToDisplayName()} {MinecraftVersion} ({LoaderVersion})";

    public static string CreateId(ModLoaderKind loader, MinecraftVersion minecraftVersion, string loaderVersion)
    {
        string raw = $"{loader.ToId()}-{minecraftVersion}-{loaderVersion}".ToLowerInvariant();
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(raw.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray());
    }

    public override string ToString() => DisplayName;
}
