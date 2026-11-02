using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>A trigger of a keybind (pressed, released, held...): its event node, how often the project uses it, and its setting.</summary>
public sealed class KeyTriggerViewModel : ObservableObject
{
    private int _usages;

    public KeyTriggerViewModel(GraphNodeKind kind, ICommand add)
    {
        Kind = kind;
        AddCommand = add;
        NodeInfo info = NodeCatalog.Get(kind);
        Title = info.Title;
        Description = info.Description;
    }

    public GraphNodeKind Kind { get; }

    public string Title { get; }

    public string Description { get; }

    /// <summary>Adds the event to the graph of the keybind.</summary>
    public ICommand AddCommand { get; }

    /// <summary>Its time is the hold time of the keybind (held, held then released).</summary>
    public bool UsesHoldTime => Kind is GraphNodeKind.OnKeyHeld or GraphNodeKind.OnKeyHoldReleased;

    public bool UsesPulse => Kind == GraphNodeKind.OnKeyPulse;

    public bool UsesCombo => Kind == GraphNodeKind.OnKeyCombo;

    /// <summary>Event nodes of this trigger for this keybind, in every graph of the project.</summary>
    public int Usages
    {
        get => _usages;
        set
        {
            if (SetProperty(ref _usages, value))
            {
                OnPropertyChanged(nameof(UsageLabel));
                OnPropertyChanged(nameof(IsUsed));
            }
        }
    }

    public bool IsUsed => _usages > 0;

    public string UsageLabel => _usages == 0 ? Tr.T("not used yet") : Tr.F("used {0} time(s)", _usages);
}

/// <summary>
/// Properties of a keybind: its key (chosen in a list, or pressed on the keyboard or the mouse like in
/// Unreal), its triggers with their settings (hold time, pulse interval, combo key), and its event
/// graph shown beside them, where a click on a trigger adds its event.
/// </summary>
public sealed class KeybindAssetViewModel : AssetViewModel
{
    private readonly KeybindAsset _keybind;

    public KeybindAssetViewModel(KeybindAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _keybind = model;
        KeyChoices = [.. KeybindAsset.Keys.Select(k => new Choice<string>(k.Name, PhysicalKeys.Label(k)))];
        Triggers = [.. NodeCatalog.KeyEvents.Select(kind => new KeyTriggerViewModel(kind, new RelayCommand(() => AddTrigger(kind))))];
        EventGraph = new OwnedEventGraph(context, model, () => _keybind.GraphId, v => _keybind.GraphId = v, nameof(KeybindAsset.GraphId),
            () => Tr.F("Events of the key {0}", _keybind.DisplayName), Tr.T("Create the graph of the keybind"));
        RefreshUsages();
    }

    /// <summary>The graph beside the triggers needs room.</summary>
    public override double EditorMaxWidth => 1500;

    /// <summary>Keys and mouse buttons, with the labels of the keyboard of the user.</summary>
    public IReadOnlyList<Choice<string>> KeyChoices { get; }

    public string DefaultKey
    {
        get => _keybind.DefaultKey;
        set => ChangeIfDifferent(Tr.T("Change the key"), nameof(KeybindAsset.DefaultKey), () => _keybind.DefaultKey, v => _keybind.DefaultKey = v, value ?? "R");
    }

    public string ComboKey
    {
        get => _keybind.ComboKey;
        set => ChangeIfDifferent(Tr.T("Change the combo key"), nameof(KeybindAsset.ComboKey), () => _keybind.ComboKey, v => _keybind.ComboKey = v, value ?? "LEFT_SHIFT");
    }

    public double HoldSeconds
    {
        get => _keybind.HoldSeconds;
        set => ChangeNumber(Tr.T("Change the hold time"), nameof(KeybindAsset.HoldSeconds), value, () => _keybind.HoldSeconds, v => _keybind.HoldSeconds = v,
            v => Math.Clamp(Math.Round(v, 2), KeybindAsset.MinSeconds, KeybindAsset.MaxSeconds));
    }

    public double PulseSeconds
    {
        get => _keybind.PulseSeconds;
        set => ChangeNumber(Tr.T("Change the pulse interval"), nameof(KeybindAsset.PulseSeconds), value, () => _keybind.PulseSeconds, v => _keybind.PulseSeconds = v,
            v => Math.Clamp(Math.Round(v, 2), KeybindAsset.MinSeconds, KeybindAsset.MaxSeconds));
    }

    public double MinSeconds => KeybindAsset.MinSeconds;

    public double MaxSeconds => KeybindAsset.MaxSeconds;

    public IReadOnlyList<KeyTriggerViewModel> Triggers { get; }

    /// <summary>The event graph of the keybind, edited beside its triggers.</summary>
    public OwnedEventGraph EventGraph { get; }

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        EventGraph.Refresh();
        RefreshUsages();
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
        if (propertyName == nameof(KeybindAsset.GraphId))
        {
            EventGraph.Refresh();
        }
        else if (propertyName == nameof(Core.Assets.Asset.ResourceId))
        {
            RefreshUsages();
        }
    }

    private void RefreshUsages()
    {
        string id = _keybind.ResourceId.Value;
        List<GraphNode> nodes = [.. Context.Project.Assets.OfType<GraphAsset>().SelectMany(g => g.Graph.Nodes)
            .Where(n => NodeCatalog.Find(n.Kind)?.DirectEvent == Core.GameData.DirectEventKind.Key && n.GetValue("key")?.Trim() == id)];
        foreach (KeyTriggerViewModel trigger in Triggers)
        {
            trigger.Usages = nodes.Count(n => n.Kind == trigger.Kind);
        }
    }

    /// <summary>Adds the event of a trigger to the graph of the keybind, below the events already there.</summary>
    private void AddTrigger(GraphNodeKind kind)
    {
        EventGraph.AddEvent(kind, "key", _keybind.ResourceId.Value);
        RefreshUsages();
    }
}
