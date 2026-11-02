using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>Kind of a parameter of a command: an argument of Minecraft's commands (Brigadier).</summary>
public enum CommandParameterKind
{
    /// <summary>A whole number between a minimum and a maximum: "5".</summary>
    Integer,

    /// <summary>A decimal number between a minimum and a maximum: "2.5".</summary>
    Decimal,

    /// <summary>true or false.</summary>
    Boolean,

    /// <summary>One word: "home".</summary>
    Word,

    /// <summary>A word, or a text in quotes: "\"my base\"".</summary>
    Text,

    /// <summary>Everything typed after it, spaces included (only as the last parameter).</summary>
    Message,

    /// <summary>A connected player: "Steve", "@p".</summary>
    Player,

    /// <summary>One entity: "@e[type=cow,limit=1]".</summary>
    Entity,

    /// <summary>A position of a block: "10 64 -3", "~ ~1 ~".</summary>
    Position,

    /// <summary>A point with decimals (a 3D vector): "0.5 64 2.25", "^ ^ ^2".</summary>
    Vector,

    /// <summary>An item: "minecraft:diamond".</summary>
    Item,

    /// <summary>A block with its properties: "minecraft:furnace[lit=true]".</summary>
    Block,

    /// <summary>A dimension (a world): "minecraft:the_nether".</summary>
    Dimension,
}

/// <summary>A parameter of a command: its name (shown by the chat while typing), its kind and, for a number, its bounds.</summary>
public sealed record CommandParameter(Guid Id, string Name, CommandParameterKind Kind, double Minimum = 0, double Maximum = 100)
{
    /// <summary>Name of its output on the "On command" event, and of its variable in Java.</summary>
    public string PinName => "arg_" + Name;

    public bool IsNumber => Kind is CommandParameterKind.Integer or CommandParameterKind.Decimal;

    /// <summary>Type of its value in the graphs.</summary>
    public DataType Type => Kind switch
    {
        CommandParameterKind.Integer => PinType.Number,
        CommandParameterKind.Decimal => DataType.Decimal,
        CommandParameterKind.Boolean => PinType.Boolean,
        CommandParameterKind.Word or CommandParameterKind.Text or CommandParameterKind.Message => PinType.Text,
        CommandParameterKind.Player => PinType.Player,
        CommandParameterKind.Entity => DataType.Entity,
        CommandParameterKind.Position => PinType.Position,
        CommandParameterKind.Vector => DataType.Vector3f,
        CommandParameterKind.Item => PinType.Item,
        CommandParameterKind.Block => DataType.BlockState,
        _ => DataType.World,
    };

    public static string KindLabel(CommandParameterKind kind) => kind switch
    {
        CommandParameterKind.Integer => Tr.T("Whole number"),
        CommandParameterKind.Decimal => Tr.T("Decimal number"),
        CommandParameterKind.Boolean => Tr.T("True / false"),
        CommandParameterKind.Word => Tr.T("Word"),
        CommandParameterKind.Text => Tr.T("Text (in quotes)"),
        CommandParameterKind.Message => Tr.T("Message (rest of the line)"),
        CommandParameterKind.Player => Tr.T("Player"),
        CommandParameterKind.Entity => Tr.T("Entity"),
        CommandParameterKind.Position => Tr.T("Block position (x y z)"),
        CommandParameterKind.Vector => Tr.T("Vector 3 (x y z, decimals)"),
        CommandParameterKind.Item => Tr.T("Item"),
        CommandParameterKind.Block => Tr.T("Block (with properties)"),
        _ => Tr.T("Dimension"),
    };
}

/// <summary>
/// Chat command of the mod (V0.6.2): its name ("/heal"), who may run it, its parameters (typed like
/// the arguments of Minecraft's commands: numbers, words, players, positions, vectors...) and a message
/// sent back. What it does is written in event graphs, with the "On command" event, which gives the
/// player, the position and the value of each parameter.
/// </summary>
public sealed class CommandAsset : Asset
{
    public const int MaxPermissionLevel = 4;

    private int _permissionLevel;
    private bool _anySource;
    private IReadOnlyList<CommandParameter> _parameters = [];
    private string _feedback = string.Empty;
    private Guid? _graphId;

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

    /// <summary>
    /// False: only players run it. True: command blocks and the console too; the player of the event is
    /// then empty, and the position is the one of the block or of the world spawn.
    /// </summary>
    public bool AnySource
    {
        get => _anySource;
        set => SetField(ref _anySource, value);
    }

    /// <summary>Parameters typed after the name, in order.</summary>
    public IReadOnlyList<CommandParameter> Parameters
    {
        get => _parameters;
        set
        {
            IReadOnlyList<CommandParameter> list = value is null ? [] : [.. value];
            if (!_parameters.SequenceEqual(list))
            {
                _parameters = list;
                RaiseChanged(nameof(Parameters));
            }
        }
    }

    /// <summary>Message sent to the player once the command has run; empty for none.</summary>
    public string Feedback
    {
        get => _feedback;
        set => SetField(ref _feedback, value?.Trim() ?? string.Empty);
    }

    /// <summary>Event graph shown in the editor of the command (created from it), or null.</summary>
    public Guid? GraphId
    {
        get => _graphId;
        set => SetField(ref _graphId, value);
    }

    /// <summary>Usage shown to the player: "/give_gem &lt;player&gt; &lt;amount&gt;".</summary>
    public string Usage => "/" + string.Join(' ', [CommandName, .. Parameters.Select(p => $"<{p.Name}>")]);

    /// <summary>A name for a new parameter that no other one has ("value", "value2"...).</summary>
    public string NewParameterName(string baseName = "value")
    {
        string name = baseName;
        for (int i = 2; Parameters.Any(p => p.Name == name); i++)
        {
            name = baseName + i;
        }

        return name;
    }

    /// <summary>Makes a parameter name usable by the chat and by Java: lowercase letters, digits and '_'.</summary>
    public static string CleanName(string? name) => ResourceId.FromText(name ?? string.Empty).Value;

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        foreach (CommandParameter parameter in Parameters.Where(p => p.IsNumber && p.Minimum > p.Maximum))
        {
            diagnostics.Error(Tr.F("The minimum of the parameter \"{0}\" is greater than its maximum.", parameter.Name), ToString(), Id);
        }

        foreach (string name in Parameters.GroupBy(p => p.Name).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            diagnostics.Error(Tr.F("Two parameters are named \"{0}\".", name), ToString(), Id);
        }

        if (Parameters.Take(Parameters.Count - 1).Any(p => p.Kind == CommandParameterKind.Message))
        {
            diagnostics.Error(Tr.T("A message takes the rest of the line: it can only be the last parameter."), ToString(), Id);
        }
    }
}
