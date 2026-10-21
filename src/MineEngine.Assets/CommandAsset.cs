using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Chat command of the mod (V0.6.2): its name ("/heal"), who may run it, an optional argument
/// and a message sent back to the player. What it does is written in event graphs, with the
/// "On command" event, which gives the player, the position and the argument.
/// </summary>
public sealed class CommandAsset : Asset
{
    public const int MaxPermissionLevel = 4;

    private int _permissionLevel;
    private CommandArgumentKind _argument;
    private string _argumentName = "value";
    private int _minimum;
    private int _maximum = 100;
    private string _feedback = string.Empty;

    public CommandAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Command;

    /// <summary>What is typed in the chat: the identifier ("/heal").</summary>
    public string CommandName => ResourceId.Value;

    /// <summary>0: every player; 2: operators (cheats); 4: server owner.</summary>
    public int PermissionLevel
    {
        get => _permissionLevel;
        set => SetField(ref _permissionLevel, Math.Clamp(value, 0, MaxPermissionLevel));
    }

    public CommandArgumentKind Argument
    {
        get => _argument;
        set => SetField(ref _argument, value);
    }

    /// <summary>Name of the argument, shown by the chat while typing ("amount").</summary>
    public string ArgumentName
    {
        get => _argumentName;
        set => SetField(ref _argumentName, ResourceId.FromText(value ?? string.Empty).Value);
    }

    public int Minimum
    {
        get => _minimum;
        set => SetField(ref _minimum, value);
    }

    public int Maximum
    {
        get => _maximum;
        set => SetField(ref _maximum, value);
    }

    /// <summary>Message sent to the player once the command has run; empty for none.</summary>
    public string Feedback
    {
        get => _feedback;
        set => SetField(ref _feedback, value?.Trim() ?? string.Empty);
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (Argument == CommandArgumentKind.Number && Minimum > Maximum)
        {
            diagnostics.Error(Tr.T("The minimum of the argument is greater than its maximum."), ToString(), Id);
        }
    }
}
