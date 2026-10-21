using MineEngine.Assets;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.Core.IO;
using MineEngine.Core.Json;
using MineEngine.Project.Localization;
using MineEngine.Project.Migration;
using MineEngine.Project.Serialization;

namespace MineEngine.Project;

/// <summary>Création, ouverture et enregistrement des projets sur le disque.</summary>
public sealed class ProjectRepository
{
    private const string ProjectGitIgnore = "build/\nrun/\n.gradle/\n.mineengine/\n";

    private readonly AssetSerializer _assetSerializer;
    private readonly ProjectSettingsSerializer _settingsSerializer;
    private readonly AssetFormatMigrator _migrator = new();

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
            throw new IOException(Tr.F("The folder '{0}' already exists and is not empty.", rootDirectory));
        }

        var layout = new ProjectLayout(rootDirectory, ProjectLayout.DefaultProjectFileName(Path.GetFileName(rootDirectory)));
        layout.EnsureDirectories();
        AtomicFile.WriteAllText(Path.Combine(layout.RootDirectory, ".gitignore"), ProjectGitIgnore);

        var project = new ModProject(layout, settings, new AssetRegistry());
        Save(project);
        return project;
    }

    /// <summary>
    /// Ouvre un projet à partir de son fichier .mineengine ou de son dossier. Un projet
    /// d'une version antérieure (Project.json) est d'abord converti à la nouvelle organisation.
    /// </summary>
    public ModProject Open(string projectFileOrDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFileOrDirectory);

        string rootDirectory = Directory.Exists(projectFileOrDirectory)
            ? projectFileOrDirectory
            : Path.GetDirectoryName(Path.GetFullPath(projectFileOrDirectory))!;
        IReadOnlyList<string> layoutNotes = [];
        if (LegacyLayoutMigrator.NeedsMigration(rootDirectory))
        {
            layoutNotes = new LegacyLayoutMigrator().Migrate(rootDirectory);
        }

        string? requestedFile = File.Exists(projectFileOrDirectory)
            && projectFileOrDirectory.EndsWith(ProjectLayout.ProjectFileExtension, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(projectFileOrDirectory)
            : null;
        var layout = new ProjectLayout(rootDirectory, requestedFile);

        if (!File.Exists(layout.ProjectFile))
        {
            throw new FileNotFoundException(Tr.F("No Mine Engine project (*{0}) in '{1}'.", ProjectLayout.ProjectFileExtension, rootDirectory));
        }

        ProjectSettings settings = _settingsSerializer.Deserialize(File.ReadAllText(layout.ProjectFile), layout.ProjectFileName);

        var documents = new List<AssetDocument>();
        if (Directory.Exists(layout.ElementsDirectory))
        {
            foreach (string file in Directory
                         .EnumerateFiles(layout.ElementsDirectory, "*" + AssetSerializer.FileExtension, SearchOption.AllDirectories)
                         .Order(StringComparer.Ordinal))
            {
                string sourceName = layout.ToRelativePath(file);
                documents.Add(new AssetDocument(JsonFormatting.ParseObject(File.ReadAllText(file), sourceName), sourceName));
            }
        }

        // Les projets des versions précédentes sont convertis en mémoire ; ils sont réécrits
        // au nouveau format au prochain enregistrement.
        AssetMigrationResult migration = _migrator.Migrate(documents);
        var assets = new AssetRegistry();
        foreach (AssetDocument document in migration.Documents)
        {
            assets.Add(_assetSerializer.Deserialize(document.Root, document.SourceName));
        }

        layout.EnsureDirectories();
        var project = new ModProject(layout, settings, assets) { LoadNotes = [.. layoutNotes, .. migration.Notes] };
        string languages = Path.Combine(layout.RootDirectory, ModLocalizationSerializer.FileName);
        if (File.Exists(languages))
        {
            project.Localization = ModLocalizationSerializer.Deserialize(File.ReadAllText(languages), ModLocalizationSerializer.FileName);
        }

        if (migration.HasChanges)
        {
            project.MarkDirty();
        }

        return project;
    }

    /// <summary>Enregistre les paramètres et tous les assets, puis supprime les fichiers d'assets obsolètes.</summary>
    public void Save(ModProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        ProjectLayout layout = project.Layout;
        layout.EnsureDirectories();

        AtomicFile.WriteAllText(layout.ProjectFile, _settingsSerializer.Serialize(project.Settings));

        // Langues du mod : le fichier n'existe que si le projet s'écarte des langues par défaut.
        string languages = Path.Combine(layout.RootDirectory, ModLocalizationSerializer.FileName);
        ModLocalization localization = project.Localization;
        if (localization.Translations.IsEmpty && localization.Locales.SequenceEqual(ModLocalization.DefaultLocales))
        {
            File.Delete(languages);
        }
        else
        {
            AtomicFile.WriteAllText(languages, ModLocalizationSerializer.Serialize(localization));
        }

        var writtenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Asset asset in project.Assets)
        {
            string path = layout.GetAssetFilePath(asset);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteAllText(path, _assetSerializer.Serialize(asset));
            writtenFiles.Add(Path.GetFullPath(path));
        }

        foreach (string file in Directory.EnumerateFiles(
                     layout.ElementsDirectory, "*" + AssetSerializer.FileExtension, SearchOption.AllDirectories))
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
