using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Commands;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.ViewModels.Graphs;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Gui;

/// <summary>A point in pixels of the GUI (a slot of the player's inventory).</summary>
public sealed record GuiPoint(int X, int Y);

/// <summary>
/// GUI tab (V0.8.0, like the GUI editor of MCreator): the window of the GUI on a view of the game,
/// its components moved and resized with the mouse (snapped on the chosen grid), added from the
/// toolbox on the left, listed and edited on the right with the size and the options of the GUI.
/// Every gesture is an undoable step of the project history; undoing updates the editor.
/// </summary>
public sealed class GuiDocumentViewModel : DocumentViewModel, IDisposable
{
    private readonly GuiAsset _asset;
    private readonly AssetEditingContext _context;
    private readonly Func<GraphAsset, GraphDocumentViewModel>? _graphEditor;
    private GuiComponentViewModel? _selected;
    private bool _isEventsTab;
    private bool _isNodesTab;
    private GraphDocumentViewModel? _nodes;

    /// <summary>Component copied with Ctrl+C, pasted with Ctrl+V in this GUI or another one.</summary>
    private static GuiComponent? _clipboard;

    public GuiDocumentViewModel(GuiAsset asset, AssetEditingContext context, string displayPath, Func<GraphAsset, GraphDocumentViewModel>? graphEditor = null)
        : base(IdFor(asset.Id), displayPath)
    {
        _graphEditor = graphEditor;
        ShowNodesCommand = new RelayCommand(() => Tab(nodes: true));
        CreateNodesGraphCommand = new RelayCommand(CreateNodesGraph, () => NodesGraph is null);
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _asset.Changed += OnAssetChanged;
        AddComponentCommand = new RelayCommand<GuiComponentKind>(AddComponent);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => _selected is not null);
        DuplicateSelectedCommand = new RelayCommand(DuplicateSelected, () => _selected is not null);
        BringForwardCommand = new RelayCommand(() => Reorder(+1), () => _selected is not null && Components.IndexOf(_selected) < Components.Count - 1);
        EditSelectedCommand = new RelayCommand(() => Edit(_selected!), () => _selected is not null);
        RenameSelectedCommand = new RelayCommand(() => Rename(_selected!), () => _selected is not null);
        CopyCommand = new RelayCommand(() => _clipboard = _selected!.Model, () => _selected is not null);
        PasteCommand = new RelayCommand(Paste, () => _clipboard is not null);
        ShowDesignCommand = new RelayCommand(() => Tab());
        ShowEventsCommand = new RelayCommand(() => Tab(events: true));
        SendBackwardCommand = new RelayCommand(() => Reorder(-1), () => _selected is not null && Components.IndexOf(_selected) > 0);
        Sync();
        SyncEvents();
    }

    public static string IdFor(Guid assetId) => "gui:" + assetId.ToString("N");

    public GuiAsset Asset => _asset;

    public override string FileName => string.IsNullOrWhiteSpace(_asset.DisplayName) ? _asset.ResourceId.Value : _asset.DisplayName;

    public override bool IsReadOnly => false;

    public override string DetailsBadge => "GUI";

    /// <summary>Components, from the back to the front.</summary>
    public ObservableCollection<GuiComponentViewModel> Components { get; } = [];

    /// <summary>The kinds of the toolbox, in its order.</summary>
    public IReadOnlyList<GuiComponentKind> Kinds { get; } = Enum.GetValues<GuiComponentKind>();

    public GuiComponentViewModel? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
            {
                return;
            }

            if (_selected is not null)
            {
                _selected.IsSelected = false;
            }

            _selected = value;
            if (_selected is not null)
            {
                _selected.IsSelected = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedEvent));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasSelection => _selected is not null;

    public ICommand AddComponentCommand { get; }

    /// <summary>Edits the selected component: its text on the canvas, or the texture of an image (double-click).</summary>
    public ICommand EditSelectedCommand { get; }

    /// <summary>Changes the name (identifier) of the selected component.</summary>
    public ICommand RenameSelectedCommand { get; }

    public ICommand CopyCommand { get; }

    public ICommand PasteCommand { get; }

    public ICommand ShowDesignCommand { get; }

    public ICommand ShowEventsCommand { get; }

    /// <summary>The Events tab (the methods of the GUI in Java or as graphs) rather than the Design one.</summary>
    public bool IsEventsTab
    {
        get => _isEventsTab;
        set
        {
            if (SetProperty(ref _isEventsTab, value))
            {
                OnPropertyChanged(nameof(IsDesignTab));
            }
        }
    }

    public bool IsDesignTab => !_isEventsTab && !_isNodesTab;

    /// <summary>The Nodes tab (V0.8.1): the graph of the GUI, with its event nodes and its components.</summary>
    public bool IsNodesTab => _isNodesTab;

    public ICommand ShowNodesCommand { get; }

    public ICommand CreateNodesGraphCommand { get; }

    /// <summary>The graph of the GUI (an event graph that belongs to it), or null before it is created.</summary>
    public GraphAsset? NodesGraph =>
        _context.Project.Assets.OfType<GraphAsset>().FirstOrDefault(g => g.ElementId == _asset.Id && g.Kind == GraphKind.Events);

    public bool HasNodesGraph => NodesGraph is not null;

    /// <summary>The editor of the graph of the GUI, shown in the Nodes tab.</summary>
    public GraphDocumentViewModel? NodesDocument
    {
        get
        {
            GraphAsset? graph = NodesGraph;
            if (_nodes is not null && _nodes.Asset != graph)
            {
                (_nodes as IDisposable)?.Dispose();
                _nodes = null;
            }

            if (_nodes is null && graph is not null && _graphEditor is not null)
            {
                _nodes = _graphEditor(graph);
            }

            return _nodes;
        }
    }

    private void Tab(bool nodes = false, bool events = false)
    {
        _isNodesTab = nodes;
        IsEventsTab = events;
        OnPropertyChanged(nameof(IsNodesTab));
        OnPropertyChanged(nameof(IsDesignTab));
        OnPropertyChanged(nameof(HasNodesGraph));
        OnPropertyChanged(nameof(NodesDocument));
    }

    /// <summary>Creates the graph of the GUI with its first node, "GUI: opened" (one step to undo).</summary>
    private void CreateNodesGraph()
    {
        var graph = new GraphAsset(Guid.NewGuid(),
            _context.Project.Assets.CreateUniqueResourceId(_asset.ResourceId.Value + "_nodes", AssetType.Graph),
            Tr.F("{0} (nodes)", FileName))
        {
            Kind = GraphKind.Events,
            ElementId = _asset.Id,
            Folder = _asset.Folder,
            Graph = NodeGraph.Empty.AddNode(GraphNode.Create(NodeCatalog.Get(GraphNodeKind.OnGuiOpened), 0, 0)),
        };
        Execute(new MineEngine.Assets.Commands.AddAssetCommand(_context.Project.Assets, graph, Tr.T("Graph")));
        Tab(nodes: true);
    }

    /// <summary>The events of the GUI (opened, tick, closed) then those of its components (V0.8.0 step 3).</summary>
    public ObservableCollection<ElementMethodViewModel> Events { get; } = [];

    /// <summary>The event of the selected component (a button, a checkbox, a text input or a slot), or null.</summary>
    public ElementMethodViewModel? SelectedEvent =>
        _selected is null ? null : Events.FirstOrDefault(e => e.Slot.Key == GuiMethodSlots.ComponentKey(_selected.Model));

    public ICommand DeleteSelectedCommand { get; }

    public ICommand DuplicateSelectedCommand { get; }

    public ICommand BringForwardCommand { get; }

    public ICommand SendBackwardCommand { get; }

    /// <summary>Textures of the project for an image ("None" first).</summary>
    public IReadOnlyList<Choice<Guid?>> TextureChoices =>
    [
        new Choice<Guid?>(null, Tr.T("(none)")),
        .. _context.Project.Assets.OfType<TextureAsset>().OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new Choice<Guid?>(t.Id, string.IsNullOrWhiteSpace(t.DisplayName) ? t.ResourceId.Value : t.DisplayName)),
    ];

    // ----- The GUI -----

    public int Width
    {
        get => _asset.Width;
        set => Set(nameof(GuiAsset.Width), () => _asset.Width, v => _asset.Width = v, Math.Clamp(value, GuiAsset.MinSize, GuiAsset.MaxSize), Tr.T("Change the size of the GUI"));
    }

    public int Height
    {
        get => _asset.Height;
        set => Set(nameof(GuiAsset.Height), () => _asset.Height, v => _asset.Height = v, Math.Clamp(value, GuiAsset.MinSize, GuiAsset.MaxSize), Tr.T("Change the size of the GUI"));
    }

    public bool HasInventory
    {
        get => _asset.HasInventory;
        set => Set(nameof(GuiAsset.HasInventory), () => _asset.HasInventory, v => _asset.HasInventory = v, value, Tr.T("Show the player's inventory"));
    }

    public int InventoryOffsetX
    {
        get => _asset.InventoryOffsetX;
        set => Set(nameof(GuiAsset.InventoryOffsetX), () => _asset.InventoryOffsetX, v => _asset.InventoryOffsetX = v, value, Tr.T("Move the player's inventory"));
    }

    public int InventoryOffsetY
    {
        get => _asset.InventoryOffsetY;
        set => Set(nameof(GuiAsset.InventoryOffsetY), () => _asset.InventoryOffsetY, v => _asset.InventoryOffsetY = v, value, Tr.T("Move the player's inventory"));
    }

    public bool PauseGame
    {
        get => _asset.PauseGame;
        set => Set(nameof(GuiAsset.PauseGame), () => _asset.PauseGame, v => _asset.PauseGame = v, value, Tr.T("Pause the game"));
    }

    public bool RenderBackground
    {
        get => _asset.RenderBackground;
        set => Set(nameof(GuiAsset.RenderBackground), () => _asset.RenderBackground, v => _asset.RenderBackground = v, value, Tr.T("Darken the background"));
    }

    // ----- Editor options -----

    public bool SnapToGrid
    {
        get => _asset.SnapToGrid;
        set => Set(nameof(GuiAsset.SnapToGrid), () => _asset.SnapToGrid, v => _asset.SnapToGrid = v, value, Tr.T("Snap on the grid"));
    }

    public int GridX
    {
        get => _asset.GridX;
        set => Set(nameof(GuiAsset.GridX), () => _asset.GridX, v => _asset.GridX = v, Math.Clamp(value, 1, 64), Tr.T("Change the grid"));
    }

    public int GridY
    {
        get => _asset.GridY;
        set => Set(nameof(GuiAsset.GridY), () => _asset.GridY, v => _asset.GridY = v, Math.Clamp(value, 1, 64), Tr.T("Change the grid"));
    }

    public int GridOffsetX
    {
        get => _asset.GridOffsetX;
        set => Set(nameof(GuiAsset.GridOffsetX), () => _asset.GridOffsetX, v => _asset.GridOffsetX = v, Math.Clamp(value, 0, 63), Tr.T("Change the grid"));
    }

    public int GridOffsetY
    {
        get => _asset.GridOffsetY;
        set => Set(nameof(GuiAsset.GridOffsetY), () => _asset.GridOffsetY, v => _asset.GridOffsetY = v, Math.Clamp(value, 0, 63), Tr.T("Change the grid"));
    }

    /// <summary>Slots of the player's inventory (3 rows and the hotbar), in pixels of the GUI, when it is shown.</summary>
    public IReadOnlyList<GuiPoint> InventorySlots
    {
        get
        {
            if (!_asset.HasInventory)
            {
                return [];
            }

            (int x, int y) = _asset.InventoryOrigin;
            var slots = new List<GuiPoint>();
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 9; column++)
                {
                    slots.Add(new GuiPoint(x + column * 18, y + row * 18));
                }
            }

            for (int column = 0; column < 9; column++)
            {
                slots.Add(new GuiPoint(x + column * 18, y + 58));
            }

            return slots;
        }
    }

    // ----- Gestures of the canvas -----

    /// <summary>A drag starts: what it changes until <see cref="EndGesture"/> is a single step to undo.</summary>
    public void BeginGesture() => _context.History.Seal();

    public void EndGesture() => _context.History.Seal();

    /// <summary>Moves a component to a position of the GUI (snapped on the grid when it is on).</summary>
    public void MoveTo(GuiComponentViewModel component, double x, double y)
    {
        (int snappedX, int snappedY) = _asset.Snap(x, y);
        Update(component.Id, c => c with { X = snappedX, Y = snappedY }, Tr.T("Move the component"), merge: true);
    }

    /// <summary>Resizes a component from its top left corner (snapped on the grid when it is on).</summary>
    public void ResizeTo(GuiComponentViewModel component, double right, double bottom)
    {
        (int snappedRight, int snappedBottom) = _asset.Snap(right, bottom);
        Update(component.Id, c => c with { Width = Math.Max(4, snappedRight - c.X), Height = Math.Max(4, snappedBottom - c.Y) }, Tr.T("Resize the component"), merge: true);
    }

    /// <summary>Moves the selected component by some pixels (arrow keys).</summary>
    public void Nudge(int dx, int dy)
    {
        if (_selected is not null)
        {
            Update(_selected.Id, c => c with { X = c.X + dx, Y = c.Y + dy }, Tr.T("Move the component"));
        }
    }

    public bool IsNameUsed(string name, Guid except) =>
        _asset.Components.Any(c => c.Id != except && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Size of the file of a texture (16 x 16 without one), read in the header of the PNG.</summary>
    public (int Width, int Height) TextureSize(Guid? textureId)
    {
        if (TexturePath(textureId) is not { } path)
        {
            return (16, 16);
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            byte[] header = new byte[24];
            if (stream.Read(header, 0, 24) < 24)
            {
                return (16, 16);
            }

            int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
            int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4));
            return width > 0 && height > 0 ? (width, height) : (16, 16);
        }
        catch (IOException)
        {
            return (16, 16);
        }
    }

    public string? TexturePath(Guid? textureId) =>
        textureId is { } id && _context.Project.Assets.Find(id) is TextureAsset texture
            ? AssetFileImporter.GetExistingFile(_context.Project, texture)
            : null;

    /// <summary>Changes a component, as an undoable step (merged with the previous one during a drag).</summary>
    public void Update(Guid id, Func<GuiComponent, GuiComponent> change, string description, bool merge = false)
    {
        IReadOnlyList<GuiComponent> components = [.. _asset.Components.Select(c => c.Id == id ? change(c).Normalized() : c)];
        if (!components.SequenceEqual(_asset.Components))
        {
            ApplyComponents(components, description, merge);
        }
    }

    /// <summary>The GUI is saved with the project, like the other elements.</summary>
    public override bool Save() => true;

    public override void Navigate(OutlineItem item)
    {
    }

    public void Dispose()
    {
        _asset.Changed -= OnAssetChanged;
        (_nodes as IDisposable)?.Dispose();
    }

    // ----- Interne -----

    private void AddComponent(GuiComponentKind kind)
    {
        (int x, int y) = FreeSpot(kind);
        GuiComponent component = GuiComponent.Create(kind, _asset.NewComponentName(kind), x, y);
        ApplyComponents([.. _asset.Components, component], Tr.F("Add a component \"{0}\"", GuiComponentViewModel.LabelOf(kind)));
        Selected = Components.FirstOrDefault(c => c.Id == component.Id);
    }

    /// <summary>
    /// First place, from the top left, where a new component overlaps neither another one nor the player's
    /// inventory (on the grid when the snap is on); the top left corner when the GUI is full.
    /// </summary>
    private (int X, int Y) FreeSpot(GuiComponentKind kind)
    {
        (int width, int height) = GuiComponent.DefaultSize(kind);
        var taken = _asset.Components.Select(c => (c.X, c.Y, c.Width, c.Height)).ToList();
        if (_asset.HasInventory)
        {
            (int ix, int iy) = _asset.InventoryOrigin;
            taken.Add((ix, iy, GuiAsset.InventoryWidth, GuiAsset.InventoryHeight));
        }

        int stepX = _asset.SnapToGrid ? _asset.GridX : 1;
        int stepY = _asset.SnapToGrid ? _asset.GridY : 1;
        for (int y = 6; y + height <= _asset.Height - 4; y += Math.Max(stepY, 2))
        {
            for (int x = 8; x + width <= _asset.Width - 4; x += Math.Max(stepX, 2))
            {
                (int sx, int sy) = _asset.Snap(x, y);
                bool free = taken.All(t => sx + width + 2 <= t.X || t.X + t.Width + 2 <= sx || sy + height + 2 <= t.Y || t.Y + t.Height + 2 <= sy);
                if (free && sx >= 0 && sy >= 0)
                {
                    return (sx, sy);
                }
            }
        }

        return (0, 0);
    }

    /// <summary>A text is typed on the canvas; an image opens the texture browser; a slot changes its kind.</summary>
    public void Edit(GuiComponentViewModel component)
    {
        Selected = component;
        switch (component.Kind)
        {
            case GuiComponentKind.Image:
                var browser = new AssetBrowserViewModel(_context, AssetBrowserKind.Texture, component.TextureId, component.Name, Tr.T("(none)"));
                if (_context.Dialogs.ShowAssetBrowser(browser))
                {
                    component.TextureId = browser.Result;
                }

                break;
            case GuiComponentKind.Slot:
                component.SlotMode = component.SlotMode switch
                {
                    GuiSlotMode.Input => GuiSlotMode.Output,
                    GuiSlotMode.Output => GuiSlotMode.Display,
                    _ => GuiSlotMode.Input,
                };
                break;
            default:
                component.BeginEdit();
                break;
        }
    }

    private void Rename(GuiComponentViewModel component)
    {
        string? name = _context.Dialogs.AskText(Tr.T("Rename the component"), Tr.T("Name used by the code and the nodes (letters, digits and _):"), component.Name);
        if (name is not null)
        {
            component.Name = name;
            if (component.HasNameError)
            {
                _context.Dialogs.ShowError(Tr.T("Rename the component"), component.NameError!);
            }
        }
    }

    private void Paste()
    {
        if (_clipboard is not { } copied)
        {
            return;
        }

        string name = IsNameUsed(copied.Name, Guid.Empty) ? _asset.NewComponentName(copied.Kind) : copied.Name;
        GuiComponent pasted = copied with
        {
            Id = Guid.NewGuid(),
            Name = name,
            X = Math.Clamp(copied.X + Math.Max(_asset.GridX, 4), 0, Math.Max(0, _asset.Width - copied.Width)),
            Y = Math.Clamp(copied.Y + Math.Max(_asset.GridY, 4), 0, Math.Max(0, _asset.Height - copied.Height)),
        };
        ApplyComponents([.. _asset.Components, pasted], Tr.T("Paste the component"));
        Selected = Components.FirstOrDefault(c => c.Id == pasted.Id);
    }

    private void DeleteSelected()
    {
        if (_selected is not { } selected)
        {
            return;
        }

        // The component goes with its event (and the graph of the event): one step to undo.
        string description = Tr.T("Delete the component");
        string key = GuiMethodSlots.ComponentKey(selected.Model);
        var commands = new List<IUndoableCommand>
        {
            new PropertyChangeCommand<IReadOnlyList<GuiComponent>>(
                description, _asset, nameof(GuiAsset.Components), () => _asset.Components, v => _asset.Components = v,
                [.. _asset.Components.Where(c => c.Id != selected.Id)]),
        };
        if (_asset.FindMethod(key) is { } method)
        {
            commands.Add(new PropertyChangeCommand<IReadOnlyList<ElementMethod>>(
                description, _asset, nameof(GuiAsset.Methods), () => _asset.Methods, v => _asset.Methods = v,
                [.. _asset.Methods.Where(m => m.Slot != key)]));
            if (method.GraphId is { } graphId && _context.Project.Assets.Find(graphId) is { } graph)
            {
                commands.Add(new RemoveAssetCommand(_context.Project.Assets, graph, Tr.T("Graph")));
            }
        }

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(description, commands));
    }

    private void DuplicateSelected()
    {
        if (_selected is not { } selected)
        {
            return;
        }

        GuiComponent copy = selected.Model with
        {
            Id = Guid.NewGuid(),
            Name = _asset.NewComponentName(selected.Kind),
            X = selected.Model.X + Math.Max(_asset.GridX, 4),
            Y = selected.Model.Y + Math.Max(_asset.GridY, 4),
        };
        ApplyComponents([.. _asset.Components, copy], Tr.T("Duplicate the component"));
        Selected = Components.FirstOrDefault(c => c.Id == copy.Id);
    }

    private void Reorder(int direction)
    {
        if (_selected is not { } selected)
        {
            return;
        }

        List<GuiComponent> components = [.. _asset.Components];
        int index = components.FindIndex(c => c.Id == selected.Id);
        int target = index + direction;
        if (index < 0 || target < 0 || target >= components.Count)
        {
            return;
        }

        (components[index], components[target]) = (components[target], components[index]);
        ApplyComponents(components, direction > 0 ? Tr.T("Bring the component forward") : Tr.T("Send the component backward"));
    }

    private void Set<T>(string property, Func<T> get, Action<T> set, T value, string description)
    {
        if (!EqualityComparer<T>.Default.Equals(get(), value))
        {
            Execute(new PropertyChangeCommand<T>(description, _asset, property, get, set, value));
        }
    }

    private void ApplyComponents(IReadOnlyList<GuiComponent> components, string description, bool merge = false) =>
        Execute(new PropertyChangeCommand<IReadOnlyList<GuiComponent>>(
            description, _asset, nameof(GuiAsset.Components), () => _asset.Components, v => _asset.Components = v, components), merge);

    private void Execute(IUndoableCommand command, bool merge = false)
    {
        UndoHistory history = _context.History;
        if (!merge)
        {
            history.Seal();
        }

        history.Execute(command);
        if (!merge)
        {
            history.Seal();
        }
    }

    private void OnAssetChanged(object? sender, AssetChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GuiAsset.Components))
        {
            Sync();
            SyncEvents();
        }
        else if (e.PropertyName == nameof(GuiAsset.Methods))
        {
            foreach (ElementMethodViewModel method in Events)
            {
                method.Refresh();
            }
        }
        else
        {
            OnPropertyChanged(e.PropertyName);
        }

        if (e.PropertyName is nameof(GuiAsset.HasInventory) or nameof(GuiAsset.InventoryOffsetX) or nameof(GuiAsset.InventoryOffsetY)
            or nameof(GuiAsset.Width) or nameof(GuiAsset.Height))
        {
            OnPropertyChanged(nameof(InventorySlots));
        }

        if (e.PropertyName is nameof(GuiAsset.SnapToGrid) or nameof(GuiAsset.GridX) or nameof(GuiAsset.GridY)
            or nameof(GuiAsset.GridOffsetX) or nameof(GuiAsset.GridOffsetY) or nameof(GuiAsset.Width) or nameof(GuiAsset.Height))
        {
            GridChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The grid or the size changed: the canvas draws its grid again.</summary>
    public event EventHandler? GridChanged;

    /// <summary>The events follow the components: the ones of a removed component go, a renamed one gets its new title.</summary>
    private void SyncEvents()
    {
        IReadOnlyList<MethodSlot> slots = GuiMethodSlots.For(_asset);
        if (Events.Select(e => (e.Slot.Key, e.Slot.Title)).SequenceEqual(slots.Select(s => (s.Key, s.Title))))
        {
            return;
        }

        Events.Clear();
        foreach (MethodSlot slot in slots)
        {
            Events.Add(new ElementMethodViewModel(_asset, _context, slot, () => _asset.Methods, v => _asset.Methods = v));
        }

        OnPropertyChanged(nameof(SelectedEvent));
    }

    /// <summary>The list follows the components of the GUI, keeping the view model (and the selection) of each one.</summary>
    private void Sync()
    {
        Guid? selected = _selected?.Id;
        IReadOnlyList<GuiComponent> models = _asset.Components;
        if (Components.Select(c => c.Id).SequenceEqual(models.Select(m => m.Id)))
        {
            for (int i = 0; i < models.Count; i++)
            {
                Components[i].Model = models[i];
            }

            return;
        }

        Dictionary<Guid, GuiComponentViewModel> existing = Components.ToDictionary(c => c.Id);
        Components.Clear();
        foreach (GuiComponent model in models)
        {
            if (existing.TryGetValue(model.Id, out GuiComponentViewModel? vm))
            {
                vm.Model = model;
            }
            else
            {
                vm = new GuiComponentViewModel(this, model);
            }

            Components.Add(vm);
        }

        _selected = null;
        Selected = Components.FirstOrDefault(c => c.Id == selected);
    }
}
