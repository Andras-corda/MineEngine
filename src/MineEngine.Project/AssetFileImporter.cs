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

    /// <summary>Imports a PNG image held in memory (a texture embedded in a Blockbench model).</summary>
    /// <param name="name">Name of the texture, used for its identifier and its file.</param>
    /// <param name="reservedIds">Voir <see cref="ImportTexture"/>.</param>
    public TextureAsset ImportTexture(ModProject project, byte[] png, string name, ISet<ResourceId>? reservedIds = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(png);
        string stem = ResourceId.FromText(Path.GetFileNameWithoutExtension(name)).Value;
        string directory = Path.Combine(Path.GetTempPath(), "MineEngine", "embedded-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, stem + ".png");
            File.WriteAllBytes(file, png);
            return ImportTexture(project, file, reservedIds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <param name="reservedIds">Voir <see cref="ImportTexture"/>.</param>
    public SoundAsset ImportSound(ModProject project, string sourceFile, ISet<ResourceId>? reservedIds = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ResourceId id = UniqueId(project, sourceFile, AssetType.Sound, reservedIds);
        string path = CopySound(project, sourceFile, id.Value);
        return new SoundAsset(Guid.NewGuid(), id, Path.GetFileNameWithoutExtension(sourceFile), path);
    }

    /// <summary>Imports a 3D model (checked first) into the Models folder.</summary>
    /// <param name="reservedIds">Voir <see cref="ImportTexture"/>.</param>
    public ModelAsset ImportModel(ModProject project, string sourceFile, ISet<ResourceId>? reservedIds = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (ModelFormats.DescribeProblem(sourceFile) is { } problem)
        {
            throw new InvalidDataException(problem);
        }

        string name = Path.GetFileName(sourceFile);
        string stem = name[..^ModelFormats.Extension(sourceFile).Length];
        ResourceId id = UniqueId(project, stem, AssetType.Model, reservedIds);
        return new ModelAsset(Guid.NewGuid(), id, stem, CopyModel(project, sourceFile, id.Value)) { Slots = ModelFormats.ReadSlots(sourceFile) };
    }

    /// <summary>Copies a model file (checked first) for a model asset and returns its relative path.</summary>
    public string CopyModel(ModProject project, string sourceFile, string baseName)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (ModelFormats.DescribeProblem(sourceFile) is { } problem)
        {
            throw new InvalidDataException(problem);
        }

        string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(sourceFile)))[..8];
        string target = Path.Combine(project.Layout.ModelsDirectory, $"{baseName}-{hash}{ModelFormats.Extension(sourceFile)}");
        Directory.CreateDirectory(project.Layout.ModelsDirectory);
        if (!string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFile, target, overwrite: true);
        }

        return project.Layout.ToRelativePath(target);
    }

    /// <summary>Copie une nouvelle image pour une texture existante et retourne son chemin relatif.</summary>
    public string CopyTexture(ModProject project, string sourceFile, string baseName) =>
        CopyIntoProject(project, sourceFile, project.Layout.TexturesDirectory, baseName, ".png", PngSignature, Tr.T("a valid PNG image"));

    /// <summary>Copie un nouveau fichier pour un son existant et retourne son chemin relatif.</summary>
    public string CopySound(ModProject project, string sourceFile, string baseName) =>
        CopyIntoProject(project, sourceFile, project.Layout.SoundsDirectory, baseName, ".ogg", OggSignature, Tr.T("a valid OGG sound"));

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
            throw new InvalidDataException(Tr.F("'{0}' is not {1}.", Path.GetFileName(sourceFile), expected));
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
