using MineEngine.Core.Commands;

namespace MineEngine.Project.Commands;

/// <summary>Modification des paramètres du projet (nom, version, MDK...) en une seule étape annulable.</summary>
public sealed class ChangeProjectSettingsCommand : IUndoableCommand
{
    private readonly ProjectSettings _target;
    private readonly ProjectSettings _before;
    private readonly ProjectSettings _after;

    public ChangeProjectSettingsCommand(ProjectSettings target, ProjectSettings newValues)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _before = target.Clone();
        _after = (newValues ?? throw new ArgumentNullException(nameof(newValues))).Clone();
    }

    public string Description => "Modifier les paramètres du projet";

    public void Execute() => _target.CopyFrom(_after);

    public void Undo() => _target.CopyFrom(_before);

    public bool TryMerge(IUndoableCommand next) => false;
}
