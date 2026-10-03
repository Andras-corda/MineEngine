using System.Security.Cryptography;
using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Project;

/// <summary>
/// Importe des fichiers dans le projet : une image PNG devient un asset Texture,
/// un son OGG un asset Sound. Le fichier est copié dans Textures/ ou Sounds/ ;
/// l'asset créé n'est pas ajouté au projet (l'éditeur l'ajoute par une commande
/// annulable).
/// </summary>
public sealed class AssetFileImporter
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] OggSignature = "OggS"u8.ToArray();

    /// <param name="reservedIds">
    /// Identifiants déjà pris par d'autres fichiers du même import (pas encore ajoutés au
    /// projet) ; l'identifiant choisi y est ajouté.
    /// </param>
    public TextureAsset ImportTexture(ModProject project, string sourceFile, ISet<ResourceId>? reservedIds = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ResourceId id = UniqueId(project, sourceFile, AssetType.Texture, reservedIds);
        string path = CopyTexture(project, sourceFile, id.Value);
        return new TextureAsset(Guid.NewGuid(), id, Path.GetFileNameWithoutExtension(sourceFile), path);
    }

    /// <param name="reservedIds">Voir <see cref="ImportTexture"/>.</param>
    public SoundAsset ImportSound(ModProject project, string sourceFile, ISet<ResourceId>? reservedIds = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ResourceId id = UniqueId(project, sourceFile, AssetType.Sound, reservedIds);
        string path = CopySound(project, sourceFile, id.Value);
        return new SoundAsset(Guid.NewGuid(), id, Path.GetFileNameWithoutExtension(sourceFile), path);
    }

    /// <summary>Copie une nouvelle image pour une texture existante et retourne son chemin relatif.</summary>
    public string CopyTexture(ModProject project, string sourceFile, string baseName) =>
        CopyIntoProject(project, sourceFile, project.Layout.TexturesDirectory, baseName, ".png", PngSignature, "une image PNG");

    /// <summary>Copie un nouveau fichier pour un son existant et retourne son chemin relatif.</summary>
    public string CopySound(ModProject project, string sourceFile, string baseName) =>
        CopyIntoProject(project, sourceFile, project.Layout.SoundsDirectory, baseName, ".ogg", OggSignature, "un son OGG");

    /// <summary>Chemin absolu du fichier d'un asset, ou null s'il n'existe pas.</summary>
    public static string? GetExistingFile(ModProject project, FileAsset asset)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.FilePath.Length == 0)
        {
            return null;
        }

        string path = project.Layout.ToAbsolutePath(asset.FilePath);
        return File.Exists(path) ? path : null;
    }

    private static ResourceId UniqueId(ModProject project, string sourceFile, AssetType type, ISet<ResourceId>? reservedIds)
    {
        string stem = ResourceId.FromText(Path.GetFileNameWithoutExtension(sourceFile)).Value;
        ResourceId candidate = project.Assets.CreateUniqueResourceId(stem, type);
        for (int suffix = 2; reservedIds?.Contains(candidate) == true; suffix++)
        {
            candidate = project.Assets.CreateUniqueResourceId($"{stem}_{suffix}", type);
        }

        reservedIds?.Add(candidate);
        return candidate;
    }

    private static string CopyIntoProject(
        ModProject project, string sourceFile, string targetDirectory, string baseName, string extension, byte[] signature, string expected)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        if (!HasSignature(sourceFile, signature))
        {
            throw new InvalidDataException($"'{Path.GetFileName(sourceFile)}' n'est pas {expected} valide.");
        }

        // Le nom contient une empreinte du contenu : un nouveau fichier n'écrase jamais
        // l'ancien, ce qui permet d'annuler un remplacement.
        string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(sourceFile)))[..8];
        string targetFile = Path.Combine(targetDirectory, $"{baseName}-{hash}{extension}");
        Directory.CreateDirectory(targetDirectory);

        if (!string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(targetFile), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFile, targetFile, overwrite: true);
        }

        return project.Layout.ToRelativePath(targetFile);
    }

    private static bool HasSignature(string file, byte[] signature)
    {
        if (!File.Exists(file))
        {
            return false;
        }

        Span<byte> header = stackalloc byte[signature.Length];
        using FileStream stream = File.OpenRead(file);
        return stream.Read(header) == header.Length && header.SequenceEqual(signature);
    }
}
