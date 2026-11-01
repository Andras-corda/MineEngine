namespace MineEngine.Editor.Services;

/// <summary>
/// "Are you sure?" before a deletion (V0.7.14). The user can stop it until Mine Engine is closed (box
/// of the dialog) or for good (Settings > General, <paramref name="enabled"/>).
/// </summary>
public sealed class DeleteConfirmation(IDialogService dialogs, Func<bool> enabled)
{
    private readonly IDialogService _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    private readonly Func<bool> _enabled = enabled ?? throw new ArgumentNullException(nameof(enabled));

    /// <summary>The box "do not ask again" was ticked: no more questions until Mine Engine is closed.</summary>
    public bool SkippedUntilRestart { get; private set; }

    /// <summary>True when the deletion can go on.</summary>
    public bool Ask(string message)
    {
        if (SkippedUntilRestart || !_enabled())
        {
            return true;
        }

        bool confirmed = _dialogs.ConfirmDelete(message, out bool skip);
        SkippedUntilRestart |= skip;
        return confirmed;
    }
}
