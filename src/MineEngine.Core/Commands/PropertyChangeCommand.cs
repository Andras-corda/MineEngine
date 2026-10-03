namespace MineEngine.Core.Commands;

/// <summary>
/// Modification d'une propriété d'un objet. Des modifications successives de la
/// même propriété du même objet, rapprochées dans le temps (une saisie au clavier),
/// sont fusionnées en une seule étape d'annulation.
/// </summary>
public sealed class PropertyChangeCommand<T> : IUndoableCommand, ISealableCommand
{
    /// <summary>Délai au-delà duquel deux modifications ne sont plus fusionnées.</summary>
    public static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(2);

    private readonly object _target;
    private readonly string _propertyName;
    private readonly Action<T> _setter;
    private readonly TimeProvider _clock;
    private readonly T _oldValue;
    private T _newValue;
    private DateTimeOffset _lastChange;
    private bool _sealed;

    public PropertyChangeCommand(
        string description, object target, string propertyName, Func<T> getter, Action<T> setter, T newValue,
        TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(getter);
        Description = description;
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _setter = setter ?? throw new ArgumentNullException(nameof(setter));
        _clock = clock ?? TimeProvider.System;
        _oldValue = getter();
        _newValue = newValue;
        _lastChange = _clock.GetUtcNow();
    }

    public string Description { get; }

    public void Execute() => _setter(_newValue);

    public void Undo() => _setter(_oldValue);

    public void Seal() => _sealed = true;

    public bool TryMerge(IUndoableCommand next)
    {
        if (_sealed
            || next is not PropertyChangeCommand<T> other
            || !ReferenceEquals(other._target, _target)
            || other._propertyName != _propertyName
            || other._lastChange - _lastChange > MergeWindow)
        {
            return false;
        }

        _newValue = other._newValue;
        _lastChange = other._lastChange;
        return true;
    }
}
