using System.Windows;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Graphs;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// The event graph of an element (a keybind, a command), shown and edited inside its editor: created
/// the first time it is needed (one undoable step), and filled with the events of the element.
/// </summary>
public sealed class OwnedEventGraph : ObservableObject, IDisposable
{
    private readonly AssetEditingContext _context;
    private readonly Asset _owner;
    private readonly Func<Guid?> _getId;
    private readonly Action<Guid?> _setId;
    private readonly string _propertyName;
    private readonly Func<string> _name;
    private readonly string _createLabel;
    private GraphDocumentViewModel? _graph;

    /// <param name="propertyName">Property of the owner that holds the graph (its changes are undoable).</param>
    /// <param name="name">Name given to a new graph.</param>
    /// <param name="createLabel">Text of the button (and of the step to undo) that creates the graph.</param>
    public OwnedEventGraph(AssetEditingContext context, Asset owner, Func<Guid?> getId, Action<Guid?> setId, string propertyName, Func<string> name, string createLabel)
    {
        _context = context;
        _owner = owner;
        _getId = getId;
        _setId = setId;
        _propertyName = propertyName;
        _name = name;
        _createLabel = createLabel;
        CreateCommand = new RelayCommand(() => Ensure());
        ShowCommand = new RelayCommand(() => _context.SelectAsset(_getId()!.Value), () => _graph is not null);
        Refresh();
    }

    /// <summary>The graph, edited here; null until it is created.</summary>
    public GraphDocumentViewModel? Graph
    {
        get => _graph;
        private set
        {
            if (!ReferenceEquals(_graph, value))
            {
                _graph?.Dispose();
                _graph = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasGraph));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasGraph => _graph is not null;

    public string CreateLabel => _createLabel;

    public ICommand CreateCommand { get; }

    /// <summary>Selects the graph in the Dashboard (to open it in its own tab).</summary>
    public ICommand ShowCommand { get; }

    /// <summary>To call when the owner or the project changed: the graph is kept while it still exists.</summary>
    public void Refresh()
    {
        GraphAsset? asset = _getId() is { } id ? _context.Project.Assets.Find(id) as GraphAsset : null;
        if (asset is null)
        {
            Graph = null;
        }
        else if (_graph is null || _graph.Id != "graph:" + asset.Id.ToString("N"))
        {
            Graph = new GraphDocumentViewModel(asset, _context, _context.Project.Layout.ToRelativePath(_context.Project.Layout.GetAssetFilePath(asset)), (_, _) => { });
        }
    }

    /// <summary>The graph, created the first time it is needed.</summary>
    public GraphDocumentViewModel? Ensure()
    {
        if (_graph is not null)
        {
            return _graph;
        }

        GraphAsset graph = ProgramPalette.CreateGraph(GraphKind.Events,
            _context.Project.Assets.CreateUniqueResourceId(_owner.ResourceId.Value + "_events", AssetType.Graph), _name());
        graph.Folder = _owner.Folder;
        _context.History.Execute(new CompositeCommand(_createLabel,
        [
            new AddAssetCommand(_context.Project.Assets, graph, Tr.T("Event graph")),
            new PropertyChangeCommand<Guid?>(_createLabel, _owner, _propertyName, _getId, _setId, graph.Id),
        ]));
        _context.History.Seal();
        Refresh();
        return _graph;
    }

    /// <summary>Adds an event node (its field naming the owner) below the events already there, and shows it.</summary>
    public void AddEvent(GraphNodeKind kind, string field, string value)
    {
        if (Ensure() is not { } graph)
        {
            return;
        }

        GraphNodeViewModel node = graph.AddNode(new NodeChoiceViewModel(new NodeTemplate(NodeCatalog.Get(kind))), new Point(40, 40 + (graph.EventCount * 160)));
        graph.SetValue(node, field, value);
        _context.History.Seal();
        graph.ShowNode(node.Model.Id);
    }

    public void Dispose() => Graph = null;
}
