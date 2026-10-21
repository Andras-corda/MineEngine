using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace MineEngine.Project.Migration;

/// <summary>
/// Convertit un projet des versions antérieures à la 0.4.2 (Project.json, Content/,
/// Scripts/, Generated/) vers l'organisation actuelle, où le projet est lui-même le
/// projet Gradle du mod. Une archive de l'ancien projet est d'abord rangée dans
/// .mineengine/backups ; le dossier Generated/ est supprimé (il sera régénéré), sauf
/// ses mondes de test qui passent dans run/.
/// </summary>
public sealed class LegacyLayoutMigrator
{
    private static readonly string[] ArchivedEntries = ["Project.json", "Content", "Textures", "Sounds", "Scripts", "Graphs"];

    private static readonly Regex PackageDeclaration = new(@"^\s*package\s+([A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*)\s*;",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private readonly TimeProvider _clock;

    public LegacyLayoutMigrator(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public static bool NeedsMigration(string rootDirectory) =>
        File.Exists(Path.Combine(rootDirectory, ProjectLayout.LegacyProjectFileName))
        && !Directory.EnumerateFiles(rootDirectory, "*" + ProjectLayout.ProjectFileExtension).Any();

    /// <summary>Convertit le projet ; retourne les remarques à afficher à l'utilisateur.</summary>
    public IReadOnlyList<string> Migrate(string rootDirectory)
    {
        string root = Path.GetFullPath(rootDirectory);
        var layout = new ProjectLayout(root);
        var notes = new List<string>();

        string backup = Archive(root, layout);
        notes.Add(Tr.F("Project converted to the new layout (like an MCreator workspace). Old version saved in {0}.", layout.ToRelativePath(backup)));

        File.Move(layout.LegacyProjectFile, layout.ProjectFile);

        MoveContent(root, layout);
        MoveScripts(root, layout, notes);
        MoveGenerated(root, layout, notes);
        return notes;
    }

    private string Archive(string root, ProjectLayout layout)
    {
        string directory = Path.Combine(layout.CacheDirectory, "backups");
        Directory.CreateDirectory(directory);
        string stamp = _clock.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string file = Path.Combine(directory, $"avant-0.4.2-{stamp}.zip");

        using ZipArchive archive = ZipFile.Open(file, ZipArchiveMode.Create);
        foreach (string entry in ArchivedEntries.Select(e => Path.Combine(root, e)))
        {
            if (File.Exists(entry))
            {
                archive.CreateEntryFromFile(entry, layout.ToRelativePath(entry));
            }
            else if (Directory.Exists(entry))
            {
                foreach (string path in Directory.EnumerateFiles(entry, "*", SearchOption.AllDirectories))
                {
                    archive.CreateEntryFromFile(path, layout.ToRelativePath(path));
                }
            }
        }

        return file;
    }

    /// <summary>Content/Items/x.asset.json devient elements/items/x.asset.json.</summary>
    private static void MoveContent(string root, ProjectLayout layout)
    {
        string content = Path.Combine(root, "Content");
        if (!Directory.Exists(content))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(content, "*", SearchOption.AllDirectories).ToList())
        {
            string relative = Path.GetRelativePath(content, file);
            string? folder = Path.GetDirectoryName(relative);
            string target = Path.Combine(layout.ElementsDirectory, string.IsNullOrEmpty(folder) ? string.Empty : folder.ToLowerInvariant(),
                Path.GetFileName(file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(file, target, overwrite: true);
        }

        Directory.Delete(content, recursive: true);
    }

    /// <summary>Les classes Java de Scripts/ rejoignent src/main/java, dans le dossier de leur package.</summary>
    private static void MoveScripts(string root, ProjectLayout layout, List<string> notes)
    {
        string scripts = Path.Combine(root, "Scripts");
        if (!Directory.Exists(scripts))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(scripts, "*.java", SearchOption.AllDirectories).ToList())
        {
            Match package = PackageDeclaration.Match(File.ReadAllText(file));
            string folder = package.Success ? package.Groups[1].Value.Replace('.', Path.DirectorySeparatorChar) : string.Empty;
            string target = Path.Combine(layout.JavaSourceDirectory, folder, Path.GetFileName(file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(file, target, overwrite: false);
            notes.Add(Tr.F("{0} moved to {1}.", layout.ToRelativePath(file), layout.ToRelativePath(target)));
        }

        if (!Directory.EnumerateFileSystemEntries(scripts, "*", SearchOption.AllDirectories).Any(File.Exists))
        {
            Directory.Delete(scripts, recursive: true);
        }
    }

    /// <summary>Generated/ est régénéré à la racine ; seuls les mondes de test (run/) sont gardés.</summary>
    private static void MoveGenerated(string root, ProjectLayout layout, List<string> notes)
    {
        string generated = Path.Combine(root, "Generated");
        if (!Directory.Exists(generated))
        {
            return;
        }

        string oldRun = Path.Combine(generated, "run");
        if (Directory.Exists(oldRun) && !Directory.Exists(layout.RunDirectory))
        {
            Directory.Move(oldRun, layout.RunDirectory);
        }

        try
        {
            Directory.Delete(generated, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            notes.Add(Tr.T("The old Generated/ folder could not be deleted (file open in Gradle?). It can be deleted by hand."));
        }
    }
}
