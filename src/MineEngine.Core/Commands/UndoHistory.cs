namespace MineEngine.Core.Commands;

/// <summary>Historique des modifications : exécute les commandes et permet de les annuler ou de les rétablir.</summary>
public sealed class UndoHistory
{
    private readonly int _capacity;
    private readonly LinkedList<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();
    private bool _isApplying;

    public UndoHistory(int capacity = 500)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    /// <summary>Déclenché après chaque exécution, annulation, rétablissement ou effacement.</summary>
    public event EventHandler? Changed;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? UndoDescription => _undo.Last?.Value.Description;

    public string? RedoDescription => _redo.TryPeek(out IUndoableCommand? command) ? command.Description : null;

    /// <summary>Exécute une commande et l'enregistre. Rétablir n'est plus possible ensuite.</summary>
    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_isApplying)
        {
            throw new InvalidOperationException("Une commande ne peut pas en exécuter une autre pendant une annulation.");
        }

        Apply(command.Execute);
        _redo.Clear();

        if (_undo.Last is { } last && last.Value.TryMerge(command))
        {
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _undo.AddLast(command);
        if (_undo.Count > _capacity)
        {
            _undo.RemoveFirst();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undo.Last is not { } node)
        {
            return;
        }

        _undo.RemoveLast();
        Apply(node.Value.Undo);
        _redo.Push(node.Value);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!_redo.TryPop(out IUndoableCommand? command))
        {
            return;
        }

        Apply(command.Execute);
        _undo.AddLast(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Empêche la commande la plus récente d'absorber les suivantes (fin d'une saisie).</summary>
    public void Seal()
    {
        if (_undo.Last?.Value is ISealableCommand sealable)
        {
            sealable.Seal();
        }
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(Action action)
    {
        _isApplying = true;
        try
        {
            action();
        }
        finally
        {
            _isApplying = false;
        }
    }
}

/// <summary>Commande qui peut refuser d'absorber les suivantes une fois "scellée".</summary>
public interface ISealableCommand
{
    void Seal();
}
