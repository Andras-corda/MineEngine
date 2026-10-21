using MineEngine.Core.Commands;

namespace MineEngine.Project.Commands;

/// <summary>
/// Verrouille ou déverrouille un fichier généré. Un fichier verrouillé se modifie à la
/// main et n'est plus réécrit par Mine Engine (comme le "code verrouillé" de MCreator).
/// </summary>
public sealed class ChangeLockedFilesCommand : IUndoableCommand
{
    private readonly ProjectSettings _settings;
    private readonly IReadOnlyList<string> _before;
    private readonly IReadOnlyList<string> _after;

    private ChangeLockedFilesCommand(ProjectSettings settings, IEnumerable<string> lockedFiles, string description)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _before = settings.LockedFiles;
        _after = [.. lockedFiles];
        Description = description;
    }

    public string Description { get; }

    public static ChangeLockedFilesCommand Lock(ProjectSettings settings, string projectRelativePath) =>
        new(settings, settings.LockedFiles.Append(projectRelativePath), Tr.F("Lock {0}", Path.GetFileName(projectRelativePath)));

    public static ChangeLockedFilesCommand Unlock(ProjectSettings settings, string projectRelativePath) =>
        new(settings,
            settings.LockedFiles.Where(f => !string.Equals(f, projectRelativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)),
            Tr.F("Unlock {0}", Path.GetFileName(projectRelativePath)));

    public void Execute() => _settings.LockedFiles = _after;

    public void Undo() => _settings.LockedFiles = _before;

    public bool TryMerge(IUndoableCommand next) => false;
}
