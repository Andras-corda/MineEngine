using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;

namespace MineEngine.Project;

/// <summary>
/// Organisation des dossiers d'un projet sur le disque :
/// <code>
/// MyMod/
///   Project.json   paramètres du projet
///   Content/       un fichier .asset.json par asset
///   Textures/      textures PNG importées
///   Scripts/       code Java de l'utilisateur (jamais modifié par Mine Engine)
///   Graphs/        graphes nodaux (V0.4)
///   Generated/     projet NeoForge généré (réécrit à chaque build)
///   Build/         mods .jar produits
///   .mineengine/   cache local
/// </code>
/// </summary>
public sealed class ProjectLayout
{
    public const string ProjectFileName = "Project.json";

    public ProjectLayout(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public string ProjectFile => Path.Combine(RootDirectory, ProjectFileName);

    public string ContentDirectory => Path.Combine(RootDirectory, "Content");

    public string TexturesDirectory => Path.Combine(RootDirectory, "Textures");

    public string ScriptsDirectory => Path.Combine(RootDirectory, "Scripts");

    public string GraphsDirectory => Path.Combine(RootDirectory, "Graphs");

    public string GeneratedDirectory => Path.Combine(RootDirectory, "Generated");

    public string BuildDirectory => Path.Combine(RootDirectory, "Build");

    public string CacheDirectory => Path.Combine(RootDirectory, ".mineengine");

    public void EnsureDirectories()
    {
        foreach (string directory in new[]
                 {
                     RootDirectory, ContentDirectory, TexturesDirectory, ScriptsDirectory, GraphsDirectory,
                 })
        {
            Directory.CreateDirectory(directory);
        }
    }

    public string GetAssetFilePath(Asset asset) =>
        Path.Combine(ContentDirectory, asset.ResourceId.Value + AssetSerializer.FileExtension);

    /// <summary>Convertit un chemin relatif au projet ("Textures/a.png") en chemin absolu.</summary>
    public string ToAbsolutePath(string projectRelativePath) =>
        Path.GetFullPath(Path.Combine(RootDirectory, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Convertit un chemin absolu situé dans le projet en chemin relatif avec des '/'.</summary>
    public string ToRelativePath(string absolutePath) =>
        Path.GetRelativePath(RootDirectory, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
}
