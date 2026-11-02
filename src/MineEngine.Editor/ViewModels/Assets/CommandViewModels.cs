using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>A parameter of a command, with its place: name, kind and, for a number, its bounds.</summary>
public sealed class CommandParameterViewModel : ObservableObject, IReorderableRow
{
    private readonly CommandAssetViewModel _owner;

    public CommandParameterViewModel(CommandAssetViewModel owner, int index, CommandParameter parameter)
    {
        _owner = owner;
        Index = index;
        Parameter = parameter;
        RemoveCommand = new RelayCommand(() => owner.RemoveParameter(Index));
        MoveUpCommand = new RelayCommand(() => owner.MoveItem(Index, Index - 1), () => Index > 0);
        MoveDownCommand = new RelayCommand(() => owner.MoveItem(Index, Index + 1), () => Index < owner.Parameters.Count - 1);
    }

    public CommandParameter Parameter { get; }

    public int Index { get; }

    public int Order => Index + 1;

    public static IReadOnlyList<Choice<CommandParameterKind>> KindChoices { get; } =
        [.. Enum.GetValues<CommandParameterKind>().Select(k => new Choice<CommandParameterKind>(k, CommandParameter.KindLabel(k)))];

    /// <summary>Name shown by the chat while typing ("amount"); lowercase letters, digits and '_'.</summary>
    public string Name
    {
        get => Parameter.Name;
        set => _owner.ChangeParameter(Index, Tr.T("Rename a parameter"), Parameter with { Name = CommandAsset.CleanName(value) is { Length: > 0 } name ? name : Parameter.Name });
    }

    public CommandParameterKind Kind
    {
        get => Parameter.Kind;
        set => _owner.ChangeParameter(Index, Tr.T("Change the kind of a parameter"), Parameter with { Kind = value });
    }

    public bool IsNumber => Parameter.IsNumber;

    public double Minimum
    {
        get => Parameter.Minimum;
        set
        {
            if (!double.IsNaN(value))
            {
                _owner.ChangeParameter(Index, Tr.T("Change the minimum"), Parameter with { Minimum = value });
            }
        }
    }

    public double Maximum
    {
        get => Parameter.Maximum;
        set
        {
            if (!double.IsNaN(value))
            {
                _owner.ChangeParameter(Index, Tr.T("Change the maximum"), Parameter with { Maximum = value });
            }
        }
    }

    /// <summary>How the player types it: "10 64 -3" for a position...</summary>
    public string Example => Tr.F("Example: {0}", Parameter.Kind switch
    {
        CommandParameterKind.Integer => "5",
        CommandParameterKind.Decimal => "2.5",
        CommandParameterKind.Boolean => "true",
        CommandParameterKind.Word => "home",
        CommandParameterKind.Text => "\"my base\"",
        CommandParameterKind.Message => Tr.T("Hello everyone"),
        CommandParameterKind.Player => "Steve, @p",
        CommandParameterKind.Entity => "@e[type=cow,limit=1]",
        CommandParameterKind.Position => "10 64 -3, ~ ~1 ~",
        CommandParameterKind.Vector => "0.5 64 2.25, ^ ^ ^2",
        CommandParameterKind.Item => "minecraft:diamond",
        CommandParameterKind.Block => "minecraft:furnace[lit=true]",
        _ => "minecraft:the_nether",
    });

    public ICommand RemoveCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }
}

/// <summary>
/// Properties of a command: its name (without "/"), who may run it (players only, or command blocks
/// and the console too), its permission level, its parameters (typed like the arguments of Minecraft:
/// numbers, words, players, positions, vectors...) in order, its message, and its event graph beside them.
/// </summary>
public sealed class CommandAssetViewModel : AssetViewModel, IReorderableList
{
    private readonly CommandAsset _command;

    public CommandAssetViewModel(CommandAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _command = model;
        AddParameterCommand = new RelayCommand(AddParameter);
        EventGraph = new OwnedEventGraph(context, model, () => _command.GraphId, v => _command.GraphId = v, nameof(CommandAsset.GraphId),
            () => Tr.F("Command /{0}", _command.CommandName), Tr.T("Create the graph of the command"));
        AddEventCommand = new RelayCommand(() =>
        {
            EventGraph.AddEvent(GraphNodeKind.OnCommand, "command", _command.CommandName);
            OnPropertyChanged(nameof(GraphsLabel));
        });
        Rebuild();
    }

    /// <summary>The graph beside the settings needs room.</summary>
    public override double EditorMaxWidth => 1500;

    public IReadOnlyList<Choice<int>> PermissionChoices => GameDataChoices.PermissionLevels;

    public IReadOnlyList<Choice<bool>> SourceChoices { get; } =
    [
        new(false, Tr.T("Players only")),
        new(true, Tr.T("Players, command blocks and the console")),
    ];

    /// <summary>Name of the command, typed after "/": it is the identifier of the element.</summary>
    public string CommandName
    {
        get => ResourceIdText;
        set => ResourceIdText = (value ?? string.Empty).Trim().TrimStart('/');
    }

    public int PermissionLevel
    {
        get => _command.PermissionLevel;
        set => ChangeIfDifferent(Tr.T("Change who can run the command"), nameof(CommandAsset.PermissionLevel), () => _command.PermissionLevel, v => _command.PermissionLevel = v, value);
    }

    public bool AnySource
    {
        get => _command.AnySource;
        set => ChangeIfDifferent(Tr.T("Change who can run the command"), nameof(CommandAsset.AnySource), () => _command.AnySource, v => _command.AnySource = v, value);
    }

    public string Feedback
    {
        get => _command.Feedback;
        set => ChangeIfDifferent(Tr.T("Change the message"), nameof(CommandAsset.Feedback), () => _command.Feedback, v => _command.Feedback = v, (value ?? string.Empty).Trim());
    }

    /// <summary>What the player types, with its parameters ("/give_gem &lt;player&gt; &lt;amount&gt;").</summary>
    public string Usage => _command.Usage;

    public ObservableCollection<CommandParameterViewModel> Parameters { get; } = [];

    public bool HasParameters => Parameters.Count > 0;

    public ICommand AddParameterCommand { get; }

    /// <summary>The event graph of the command, edited beside its settings.</summary>
    public OwnedEventGraph EventGraph { get; }

    /// <summary>Adds the event "On command" (with the name of this command) to its graph.</summary>
    public ICommand AddEventCommand { get; }

    /// <summary>Event graphs that react to this command (event "On command" with its name).</summary>
    public string GraphsLabel
    {
        get
        {
            List<GraphAsset> graphs = [.. Context.Project.Assets.OfType<GraphAsset>()
                .Where(g => g.Graph.Nodes.Any(n => n.Kind == GraphNodeKind.OnCommand && string.Equals(n.GetValue("command")?.Trim(), _command.CommandName, StringComparison.Ordinal)))];
            return graphs.Count == 0
                ? Tr.T("No graph reacts to this command yet.")
                : string.Join(", ", graphs.Select(g => string.IsNullOrWhiteSpace(g.DisplayName) ? g.ResourceId.Value : g.DisplayName));
        }
    }

    public override IReadOnlyList<SummaryLine> SummaryLines =>
    [
        new(Tr.T("Typed in the chat"), Usage),
        new(Tr.T("Who can run it"), PermissionChoices.FirstOrDefault(c => c.Value == _command.PermissionLevel)?.Label ?? string.Empty),
        new(Tr.T("Graph"), GraphsLabel),
    ];

    public void MoveItem(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= _command.Parameters.Count || to >= _command.Parameters.Count)
        {
            return;
        }

        List<CommandParameter> parameters = [.. _command.Parameters];
        CommandParameter moved = parameters[from];
        parameters.RemoveAt(from);
        parameters.Insert(to, moved);
        SetParameters(Tr.T("Move a parameter"), parameters);
    }

    /// <summary>Replaces a parameter; a name already taken by another one is refused.</summary>
    public void ChangeParameter(int index, string description, CommandParameter parameter)
    {
        if (index >= _command.Parameters.Count || _command.Parameters[index] == parameter
            || _command.Parameters.Where((p, i) => i != index).Any(p => p.Name == parameter.Name))
        {
            Rebuild();
            return;
        }

        SetParameters(description, [.. _command.Parameters.Select((p, i) => i == index ? parameter : p)]);
    }

    public void RemoveParameter(int index) =>
        SetParameters(Tr.T("Remove a parameter"), [.. _command.Parameters.Where((_, i) => i != index)]);

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        EventGraph.Refresh();
        OnPropertyChanged(nameof(GraphsLabel));
    }

    public override void Dispose()
    {
        EventGraph.Dispose();
        base.Dispose();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(Usage));
        OnPropertyChanged(nameof(CommandName));
        OnPropertyChanged(nameof(GraphsLabel));
        if (propertyName == nameof(CommandAsset.Parameters))
        {
            Rebuild();
        }
        else if (propertyName == nameof(CommandAsset.GraphId))
        {
            EventGraph.Refresh();
        }
    }

    private void AddParameter() =>
        SetParameters(Tr.T("Add a parameter"), [.. _command.Parameters, new CommandParameter(Guid.NewGuid(), _command.NewParameterName(), CommandParameterKind.Integer)]);

    private void SetParameters(string description, IReadOnlyList<CommandParameter> parameters)
    {
        Change(description, nameof(CommandAsset.Parameters), () => _command.Parameters, v => _command.Parameters = v, parameters);
        Context.History.Seal();
    }

    private void Rebuild()
    {
        Parameters.Clear();
        for (int i = 0; i < _command.Parameters.Count; i++)
        {
            Parameters.Add(new CommandParameterViewModel(this, i, _command.Parameters[i]));
        }

        OnPropertyChanged(nameof(HasParameters));
    }
}
