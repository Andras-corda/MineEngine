using MineEngine.Assets;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.Core.IO;
using MineEngine.Project.Serialization;

namespace MineEngine.Project;

/// <summary>Création, ouverture et enregistrement des projets sur le disque.</summary>
public sealed class ProjectRepository
{
    private const string ProjectGitIgnore = "Generated/\nBuild/\n.mineengine/\n";

    private readonly AssetSerializer _assetSerializer;
    private readonly ProjectSettingsSerializer _settingsSerializer;

    public ProjectRepository(AssetSerializer assetSerializer, ProjectSettingsSerializer settingsSerializer)
    {
        _assetSerializer = assetSerializer ?? throw new ArgumentNullException(nameof(assetSerializer));
        _settingsSerializer = settingsSerializer ?? throw new ArgumentNullException(nameof(settingsSerializer));
    }

    /// <summary>Crée un projet vide dans un nouveau dossier portant le nom du mod, sous <paramref name="parentDirectory"/>.</summary>
    public ModProject Create(string parentDirectory, ProjectSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);
        ArgumentNullException.ThrowIfNull(settings);

        string rootDirectory = GetProjectDirectory(parentDirectory, settings);
        if (Directory.Exists(rootDirectory) && Directory.EnumerateFileSystemEntries(rootDirectory).Any())
        {
            throw new IOException($"Le dossier '{rootDirectory}' existe déjà et n'est pas vide.");
        }

        var layout = new ProjectLayout(rootDirectory);
        layout.EnsureDirectories();
        AtomicFile.WriteAllText(Path.Combine(layout.RootDirectory, ".gitignore"), ProjectGitIgnore);

        var project = new ModProject(layout, settings, new AssetRegistry());
        Save(project);
        return project;
    }

    /// <summary>Ouvre un projet à partir de son fichier Project.json ou de son dossier.</summary>
    public ModProject Open(string projectFileOrDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFileOrDirectory);

        string rootDirectory = Directory.Exists(projectFileOrDirectory)
            ? projectFileOrDirectory
            : Path.GetDirectoryName(Path.GetFullPath(projectFileOrDirectory))!;
        var layout = new ProjectLayout(rootDirectory);

        if (!File.Exists(layout.ProjectFile))
        {
            throw new FileNotFoundException($"Aucun fichier {ProjectLayout.ProjectFileName} dans '{rootDirectory}'.");
        }

        ProjectSettings settings = _settingsSerializer.Deserialize(
            File.ReadAllText(layout.ProjectFile), ProjectLayout.ProjectFileName);

        var assets = new AssetRegistry();
        if (Directory.Exists(layout.ContentDirectory))
        {
            foreach (string file in Directory.EnumerateFiles(layout.ContentDirectory, "*" + AssetSerializer.FileExtension)
                         .Order(StringComparer.Ordinal))
            {
                string sourceName = "Content/" + Path.GetFileName(file);
                assets.Add(_assetSerializer.Deserialize(File.ReadAllText(file), sourceName));
            }
        }

        layout.EnsureDirectories();
        return new ModProject(layout, settings, assets);
    }

    /// <summary>Enregistre les paramètres et tous les assets, puis supprime les fichiers d'assets obsolètes.</summary>
    public void Save(ModProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        ProjectLayout layout = project.Layout;
        layout.EnsureDirectories();

        AtomicFile.WriteAllText(layout.ProjectFile, _settingsSerializer.Serialize(project.Settings));

        var writtenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Asset asset in project.Assets)
        {
            string path = layout.GetAssetFilePath(asset);
            AtomicFile.WriteAllText(path, _assetSerializer.Serialize(asset));
            writtenFiles.Add(Path.GetFullPath(path));
        }

        foreach (string file in Directory.EnumerateFiles(layout.ContentDirectory, "*" + AssetSerializer.FileExtension))
        {
            if (!writtenFiles.Contains(Path.GetFullPath(file)))
            {
                File.Delete(file);
            }
        }

        project.MarkSaved();
    }

    /// <summary>Dossier dans lequel <see cref="Create"/> placera le projet.</summary>
    public static string GetProjectDirectory(string parentDirectory, ProjectSettings settings) =>
        Path.Combine(parentDirectory, ToFolderName(settings.ModName, settings.ModId));

    private static string ToFolderName(string modName, ModId modId)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new string((modName ?? string.Empty).Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
        return cleaned.Length > 0 ? cleaned : modId.Value;
    }
}
