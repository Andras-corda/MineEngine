namespace MineEngine.Core.Commands;

/// <summary>Plusieurs commandes annulées et rétablies ensemble, en une seule étape.</summary>
public sealed class CompositeCommand : IUndoableCommand
{
    private readonly IReadOnlyList<IUndoableCommand> _commands;

    public CompositeCommand(string description, IReadOnlyList<IUndoableCommand> commands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(commands);
        Description = description;
        _commands = [.. commands];
    }

    public string Description { get; }

    public void Execute()
    {
        foreach (IUndoableCommand command in _commands)
        {
            command.Execute();
        }
    }

    public void Undo()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }

    public bool TryMerge(IUndoableCommand next) => false;
}
