using System.Globalization;
using System.IO.Compression;

namespace MineEngine.Project.Backups;

/// <summary>Une sauvegarde de secours d'un projet.</summary>
public sealed class ProjectBackup
{
    public ProjectBackup(string file, DateTime createdAt)
    {
        File = file;
        CreatedAt = createdAt;
    }

    public string File { get; }

    public DateTime CreatedAt { get; }
}

/// <summary>
/// Sauvegardes de secours : une archive .zip du projet (Project.json, Content/,
/// Textures/, Scripts/, Graphs/) rangée dans .mineengine/backups. Seules les plus
/// récentes sont conservées.
/// </summary>
public sealed class ProjectBackupService
{
    private const string FilePrefix = "backup-";
    private const string DateFormat = "yyyyMMdd-HHmmss";
    private static readonly string[] SavedFolders = ["Content", "Textures", "Scripts", "Graphs"];

    private readonly TimeProvider _clock;

    public ProjectBackupService(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public static string GetBackupDirectory(ModProject project) =>
        Path.Combine(project.Layout.CacheDirectory, "backups");

    public IReadOnlyList<ProjectBackup> List(ModProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        string directory = GetBackupDirectory(project);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var backups = new List<ProjectBackup>();
        foreach (string file in Directory.EnumerateFiles(directory, FilePrefix + "*.zip"))
        {
            string stamp = Path.GetFileNameWithoutExtension(file)[FilePrefix.Length..];
            if (DateTime.TryParseExact(stamp, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime createdAt))
            {
                backups.Add(new ProjectBackup(file, createdAt));
            }
        }

        return [.. backups.OrderByDescending(b => b.CreatedAt)];
    }

    /// <summary>
    /// Crée une sauvegarde si la plus récente date de plus de <paramref name="minimumInterval"/>,
    /// puis ne garde que les <paramref name="keep"/> plus récentes. Retourne la sauvegarde créée, ou null.
    /// </summary>
    public ProjectBackup? CreateIfDue(ModProject project, TimeSpan minimumInterval, int keep)
    {
        ArgumentNullException.ThrowIfNull(project);
        DateTime now = _clock.GetLocalNow().DateTime;
        ProjectBackup? latest = List(project).FirstOrDefault();
        if (latest is not null && now - latest.CreatedAt < minimumInterval)
        {
            return null;
        }

        ProjectBackup backup = Create(project, now);
        Prune(project, keep);
        return backup;
    }

    public void Prune(ModProject project, int keep)
    {
        foreach (ProjectBackup old in List(project).Skip(Math.Max(keep, 1)))
        {
            File.Delete(old.File);
        }
    }

    private static ProjectBackup Create(ModProject project, DateTime now)
    {
        string directory = GetBackupDirectory(project);
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, FilePrefix + now.ToString(DateFormat, CultureInfo.InvariantCulture) + ".zip");
        string temporary = file + ".tmp";

        using (ZipArchive archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
        {
            ProjectLayout layout = project.Layout;
            if (File.Exists(layout.ProjectFile))
            {
                archive.CreateEntryFromFile(layout.ProjectFile, ProjectLayout.ProjectFileName);
            }

            foreach (string folder in SavedFolders)
            {
                string source = Path.Combine(layout.RootDirectory, folder);
                if (!Directory.Exists(source))
                {
                    continue;
                }

                foreach (string path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    archive.CreateEntryFromFile(path, layout.ToRelativePath(path));
                }
            }
        }

        File.Move(temporary, file, overwrite: true);
        return new ProjectBackup(file, now);
    }
}
