using MineEngine.Minecraft.Generation;
using MineEngine.Project;
using MineEngine.Project.Commands;

namespace MineEngine.Editor.Services;

/// <summary>Rôle d'un fichier du projet, qui décide s'il se modifie dans l'éditeur de code.</summary>
public enum ProjectFileKind
{
    /// <summary>Code ou ressource écrit par l'utilisateur : Mine Engine n'y touche jamais.</summary>
    Source,

    /// <summary>Produit par Mine Engine et réécrit à chaque compilation : lecture seule tant qu'il n'est pas verrouillé.</summary>
    Generated,

    /// <summary>Fichier généré verrouillé par l'utilisateur : il se modifie à la main et n'est plus réécrit.</summary>
    Locked,

    /// <summary>Fichier .mineengine : il se modifie dans Projet > Paramètres du projet.</summary>
    ProjectFile,

    /// <summary>Produit par Gradle ou par le jeu (build/, run/, .gradle/).</summary>
    BuildOutput,
}

/// <summary>
/// État des fichiers d'un projet ouvert : généré, verrouillé ou écrit par l'utilisateur
/// (d'après la liste des fichiers générés et les fichiers verrouillés du projet).
/// Verrouiller passe par l'historique : la commande s'annule comme les autres.
/// </summary>
public sealed class ProjectFiles : IDisposable
{
    private readonly ModProject _project;
    private GeneratedFileManifest _manifest = GeneratedFileManifest.Empty;
    private DateTime _manifestStamp = DateTime.MinValue;

    public ProjectFiles(ModProject project)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _project.History.Changed += OnHistoryChanged;
    }

    /// <summary>Le statut d'un fichier a pu changer (verrouillage, annulation, nouvelle génération).</summary>
    public event EventHandler? Changed;

    public ProjectLayout Layout => _project.Layout;

    public ProjectFileKind KindOf(string fullPath)
    {
        ProjectLayout layout = _project.Layout;
        if (string.Equals(Path.GetFullPath(fullPath), layout.ProjectFile, StringComparison.OrdinalIgnoreCase))
        {
            return ProjectFileKind.ProjectFile;
        }

        if (new[] { layout.GradleBuildDirectory, layout.RunDirectory, Path.Combine(layout.RootDirectory, ".gradle"), layout.CacheDirectory }
            .Any(d => ProjectLayout.IsWithin(fullPath, d)))
        {
            return ProjectFileKind.BuildOutput;
        }

        string relative = layout.ToRelativePath(fullPath);
        if (_project.Settings.IsLocked(relative))
        {
            return ProjectFileKind.Locked;
        }

        return Manifest().Contains(relative) ? ProjectFileKind.Generated : ProjectFileKind.Source;
    }

    public void Lock(string fullPath) =>
        _project.History.Execute(ChangeLockedFilesCommand.Lock(_project.Settings, _project.Layout.ToRelativePath(fullPath)));

    public void Unlock(string fullPath) =>
        _project.History.Execute(ChangeLockedFilesCommand.Unlock(_project.Settings, _project.Layout.ToRelativePath(fullPath)));

    /// <summary>À appeler après une génération : la liste des fichiers générés est relue.</summary>
    public void Refresh()
    {
        _manifestStamp = DateTime.MinValue;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _project.History.Changed -= OnHistoryChanged;

    /// <summary>La liste n'est relue que si le fichier a changé depuis la dernière lecture.</summary>
    private GeneratedFileManifest Manifest()
    {
        string file = GeneratedFileManifest.PathIn(_project.Layout.WorkspaceDirectory);
        DateTime stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
        if (stamp != _manifestStamp)
        {
            _manifest = GeneratedFileManifest.Load(_project.Layout.WorkspaceDirectory);
            _manifestStamp = stamp;
        }

        return _manifest;
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
