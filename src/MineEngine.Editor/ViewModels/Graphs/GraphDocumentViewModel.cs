using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.Diagnostics;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Documents;

namespace MineEngine.Editor.ViewModels.Graphs;

/// <summary>Problème du graphe affiché dans le panneau Détails ; un clic montre le nœud.</summary>
public sealed record GraphIssueItem(string Message, Guid? NodeId, bool IsError);

/// <summary>
/// Onglet d'édition d'un graphe nodal : réactions aux événements, fonction, procédure,
/// classe (son constructeur), structure, interface ou énumération. Il présente le corps du
/// graphe sous forme de nœuds et de fils, et sa définition (paramètres, variables, héritage...)
/// dans un panneau à part. Chaque geste devient une modification appliquée par l'historique
/// du projet : tout est annulable, et l'annulation remet l'éditeur à jour. Les nœuds qui
/// utilisent d'autres définitions (appel, objet) suivent leurs modifications.
/// </summary>
public sealed class GraphDocumentViewModel : DocumentViewModel, INodeLibraryHost, IDisposable
{
    private readonly GraphAsset _asset;
    private readonly AssetEditingContext _context;
    private readonly Dictionary<Guid, GraphNodeViewModel> _nodes = [];
    private bool _refreshPending;
    private string _paletteSearch = string.Empty;
    private string? _statusText;
    private GraphPinViewModel? _pendingSource;
    private (Guid Node, string Pin)? _lastValueEdit;
    private IReadOnlyList<GraphIssueItem> _issues = [];

    /// <param name="createMember">Crée une définition intégrée à une classe (méthode, type imbriqué) et l'ouvre.</param>
    /// <param name="options">Options shared by the graph tabs (snapping); a new set when null.</param>
    public GraphDocumentViewModel(
        GraphAsset asset, AssetEditingContext context, string displayPath, Action<GraphKind, Guid> createMember, GraphViewOptions? options = null)
        : base("graph:" + asset.Id.ToString("N"), displayPath)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Options = options ?? new GraphViewOptions(false, _ => { });
        Definition = new DefinitionViewModel(asset, context, () => Program, createMember);
        _asset.Changed += OnAssetChanged;
        _context.Project.Assets.Changed += OnRegistryChanged;
        Sync();
    }

    /// <summary>Définitions du projet, relues après chaque modification d'un graphe.</summary>
    public ProgramModel Program => _context.Project.Assets.Program;

    /// <summary>Décrit les nœuds de ce graphe et la compatibilité des types.</summary>
    public IGraphContext Context => Program.ContextFor(_asset);

    /// <summary>Panneau "Définition" : signature, variables, héritage...</summary>
    public DefinitionViewModel Definition { get; }

    /// <summary>Snapping to the grid (button of the graph toolbar).</summary>
    public GraphViewOptions Options { get; }

    public GraphKind Kind => _asset.Kind;

    public bool IsEventGraph => _asset.Kind == GraphKind.Events;

    /// <summary>Le graphe a un corps en nœuds (sinon seul le panneau Définition s'affiche).</summary>
    public bool HasBody => _asset.Kind.HasBody();

    public string KindLabel => _asset.Kind.Label();

    public string TypeLabel(DataType type) => Program.TypeLabel(type);

    /// <summary>Demande à la vue de montrer un nœud (structure, liste des problèmes).</summary>
    public event EventHandler<Guid>? FocusRequested;

    public GraphAsset Asset => _asset;

    public Guid AssetId => _asset.Id;

    public override string FileName => string.IsNullOrWhiteSpace(_asset.DisplayName) ? _asset.ResourceId.Value : _asset.DisplayName;

    public override bool IsReadOnly => false;

    public override string DetailsBadge => _asset.Kind.Label().ToUpperInvariant();

    public ObservableCollection<GraphNodeViewModel> Nodes { get; } = [];

    public ObservableCollection<GraphConnectionViewModel> Connections { get; } = [];

    public bool IsEmpty => Nodes.Count == 0;

    public IEnumerable<GraphNodeViewModel> SelectedNodes => Nodes.Where(n => n.IsSelected);

    /// <summary>Le nœud sélectionné s'il est seul (son aide s'affiche dans Détails).</summary>
    public GraphNodeViewModel? SelectedNode => SelectedNodes.Take(2).Count() == 1 ? SelectedNodes.First() : null;

    public IReadOnlyList<GraphIssueItem> Issues
    {
        get => _issues;
        private set
        {
            if (SetProperty(ref _issues, value))
            {
                OnPropertyChanged(nameof(HasIssues));
                OnPropertyChanged(nameof(IssueSummary));
            }
        }
    }

    public bool HasIssues => _issues.Count > 0;

    public string IssueSummary
    {
        get
        {
            int errors = _issues.Count(i => i.IsError);
            int warnings = _issues.Count - errors;
            return (errors, warnings) switch
            {
                (0, 0) => Tr.T("No problems"),
                _ => string.Join(", ", new[]
                {
                    errors > 0 ? (errors == 1 ? Tr.T("1 error") : Tr.F("{0} errors", errors)) : null,
                    warnings > 0 ? (warnings == 1 ? Tr.T("1 warning") : Tr.F("{0} warnings", warnings)) : null,
                }.OfType<string>()),
            };
        }
    }

    public int EventCount => Nodes.Count(n => n.Info.IsEntryPoint);

    /// <summary>Nodes of the program (without the comments and the tooltips).</summary>
    public int NodeCount => Nodes.Count(n => !n.IsAnnotation);

    /// <summary>How long a message of the editor stays at the bottom of the graph.</summary>
    public static readonly TimeSpan StatusDuration = TimeSpan.FromSeconds(3);

    private DispatcherTimer? _statusTimer;

    /// <summary>Message passager de l'éditeur (fil refusé...), ou null ; il disparaît après <see cref="StatusDuration"/>.</summary>
    public string? StatusText
    {
        get => _statusText;
        set
        {
            SetProperty(ref _statusText, value);
            _statusTimer?.Stop();
            if (value is not null && Application.Current is not null)
            {
                _statusTimer ??= CreateStatusTimer();
                _statusTimer.Start();
            }
        }
    }

    private DispatcherTimer CreateStatusTimer()
    {
        var timer = new DispatcherTimer { Interval = StatusDuration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            StatusText = null;
        };
        return timer;
    }

    // ----- Menu "Ajouter un nœud" -----

    public string PaletteSearch
    {
        get => _paletteSearch;
        set
        {
            if (SetProperty(ref _paletteSearch, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(PaletteChoices));
            }
        }
    }

    /// <summary>Broche depuis laquelle un fil a été lâché dans le vide : le nœud ajouté s'y branchera.</summary>
    public GraphPinViewModel? PendingSource
    {
        get => _pendingSource;
        set
        {
            if (SetProperty(ref _pendingSource, value))
            {
                OnPropertyChanged(nameof(PaletteChoices));
            }
        }
    }

    /// <summary>
    /// Nœuds proposés (catalogue, puis appels, objets, champs et variables visibles depuis ce graphe),
    /// filtrés par la recherche (titre, description ou famille) et par la broche en attente.
    /// </summary>
    public IReadOnlyList<NodeChoiceViewModel> PaletteChoices
    {
        get
        {
            string search = Normalize(_paletteSearch);
            IGraphContext context = Context;
            return
            [
                .. ProgramPalette.AvailableNodes(Program, _asset)
                    .Select(TypedForPendingSource)
                    .Select(t => new NodeChoiceViewModel(t))
                    .Where(c => _pendingSource is null || FindCompatiblePin(c.Info, _pendingSource, context) is not null)
                    .Where(c => search.Length == 0
                                || Normalize(c.Title).Contains(search, StringComparison.Ordinal)
                                || Normalize(c.Description).Contains(search, StringComparison.Ordinal)
                                || Normalize(c.CategoryLabel).Contains(search, StringComparison.Ordinal))
                    .OrderBy(c => c.Category),
            ];
        }
    }

    /// <summary>
    /// A collection or vector node offered after a wire dropped in empty space takes the type of
    /// that wire (a list of texts gives "Add" a text value).
    /// </summary>
    private NodeTemplate TypedForPendingSource(NodeTemplate template) =>
        _pendingSource is { IsInput: false, IsExec: false } source
        && GenericNodes.MainInput(template.Info.Kind) is not null
        && GenericNodes.Accepts(template.Info.Kind, source.Type)
            ? ProgramPalette.Typed(template.Info.Kind, source.Type, Program)
            : template;

    /// <summary>Project assets: the Nodes window follows their changes (new functions, classes...).</summary>
    public AssetRegistry Assets => _context.Project.Assets;

    /// <summary>Every node that can be added to this graph, without filter (Nodes window).</summary>
    public IEnumerable<NodeTemplate> AvailableNodes => ProgramPalette.AvailableNodes(Program, _asset);

    /// <summary>Point of the graph at the center of its view, kept up to date by the view.</summary>
    public Point ViewCenter { get; set; }

    /// <summary>True if the node can be added here (an event cannot go in a function, a member must still exist).</summary>
    public bool CanAdd(NodeTemplate template) =>
        AvailableNodes.Any(t => t.Info.Kind == template.Info.Kind && t.Target == template.Target && t.Member == template.Member);

    public IReadOnlyList<NodeLibraryItem> LibraryItems => [.. AvailableNodes.Select(t => NodeLibraryItem.ForGraph(new NodeChoiceViewModel(t)))];

    public void AddFromLibrary(object payload)
    {
        if (payload is NodeChoiceViewModel choice)
        {
            AddNodeAtCenter(choice);
        }
    }

    /// <summary>Adds a node at the center of the view (double-click in the Nodes window).</summary>
    public GraphNodeViewModel? AddNodeAtCenter(NodeChoiceViewModel choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        if (!CanAdd(choice.Template))
        {
            return null;
        }

        PendingSource = null;
        return AddNode(choice, new Point(ViewCenter.X - 90, ViewCenter.Y - 40));
    }

    // ----- Modifications -----

    /// <summary>Ajoute un nœud ; s'il y a une broche en attente, le relie à sa première broche compatible.</summary>
    public GraphNodeViewModel AddNode(NodeChoiceViewModel choice, Point position)
    {
        ArgumentNullException.ThrowIfNull(choice);
        NodeInfo info = choice.Info;
        IGraphContext context = Context;
        GraphNode node = choice.Template.Create(Math.Round(position.X), Math.Round(position.Y));
        NodeGraph graph = _asset.Graph.AddNode(node);
        if (_pendingSource is { } source && FindCompatiblePin(info, source, context) is { } pin)
        {
            GraphConnection connection = source.IsInput
                ? new GraphConnection(node.Id, pin.Name, source.Node.Id, source.Name)
                : new GraphConnection(source.Node.Id, source.Name, node.Id, pin.Name);
            if (graph.DescribeConnectionProblem(connection, context) is null)
            {
                graph = graph.Connect(connection, context);
            }
        }

        PendingSource = null;
        Apply(graph, Tr.F("Add \"{0}\"", info.Title));
        GraphNodeViewModel added = _nodes[node.Id];
        SelectOnly(added);
        return added;
    }

    /// <summary>Relie deux broches (dans n'importe quel ordre) ; un refus est expliqué dans <see cref="StatusText"/>.</summary>
    public bool TryConnect(GraphPinViewModel first, GraphPinViewModel second)
    {
        if (first.IsInput == second.IsInput)
        {
            StatusText = Tr.T("Connect an output (on the right of a node) to an input (on the left).");
            return false;
        }

        (GraphPinViewModel output, GraphPinViewModel input) = first.IsInput ? (second, first) : (first, second);
        var connection = new GraphConnection(output.Node.Id, output.Name, input.Node.Id, input.Name);
        IGraphContext context = Context;
        NodeGraph graph = Retyped(_asset.Graph, input, output.Type, context);
        if (graph.DescribeConnectionProblem(connection, context) is { } problem)
        {
            if (WithCast(graph, output, input, context) is { } cast)
            {
                StatusText = Tr.F("A \"{0}\" node converts the value.", cast.Title);
                Apply(cast.Graph, Tr.T("Connect nodes"));
                return true;
            }

            StatusText = problem;
            return false;
        }

        StatusText = null;
        Apply(graph.Connect(connection, context), Tr.T("Connect nodes"));
        return true;
    }

    /// <summary>
    /// The graph with a cast node between the two pins (an int connected to a float gives "To
    /// float"), like the conversions inserted by Unreal; null when no cast fits.
    /// </summary>
    private (NodeGraph Graph, string Title)? WithCast(NodeGraph graph, GraphPinViewModel output, GraphPinViewModel input, IGraphContext context)
    {
        if (output.IsExec || input.IsExec || GenericNodes.CastBetween(output.Type, input.Type, context.CanFeed) is not { } kind)
        {
            return null;
        }

        NodeTemplate template = ProgramPalette.Typed(kind, output.Type, Program);
        Point middle = new((output.Anchor.X + input.Anchor.X) / 2 - 60, (output.Anchor.Y + input.Anchor.Y) / 2 - 20);
        GraphNode cast = template.Create(Math.Round(middle.X), Math.Round(middle.Y));
        var into = new GraphConnection(output.Node.Id, output.Name, cast.Id, GenericNodes.CastInput);
        var from = new GraphConnection(cast.Id, "result", input.Node.Id, input.Name);
        NodeGraph result = graph.AddNode(cast);
        if (result.DescribeConnectionProblem(into, context) is not null)
        {
            return null;
        }

        result = result.Connect(into, context);
        return result.DescribeConnectionProblem(from, context) is null ? (result.Connect(from, context), template.Info.Title) : null;
    }

    /// <summary>
    /// A wire connected to the main input of a collection or vector node gives it its type, like a
    /// wildcard pin of Unreal; the other wires of the node that no longer fit are removed.
    /// </summary>
    private static NodeGraph Retyped(NodeGraph graph, GraphPinViewModel input, DataType type, IGraphContext context)
    {
        if (graph.FindNode(input.Node.Id) is not { } node
            || GenericNodes.MainInput(node.Kind) != input.Name
            || !GenericNodes.Accepts(node.Kind, type)
            || GenericNodes.TypeOf(node) == type)
        {
            return graph;
        }

        NodeGraph retyped = graph.ReplaceNode(GenericNodes.Retype(node, type));
        foreach (GraphConnection wire in retyped.Connections.Where(c => c.FromNode == node.Id || c.ToNode == node.Id))
        {
            NodeGraph without = retyped.Disconnect(wire);
            if (without.DescribeConnectionProblem(wire, context) is not null)
            {
                retyped = without;
            }
        }

        return retyped;
    }

    /// <summary>True if a wire between the two pins can be made, directly or with a cast.</summary>
    public bool Accepts(GraphPinViewModel first, GraphPinViewModel second)
    {
        (GraphPinViewModel output, GraphPinViewModel input) = first.IsInput ? (second, first) : (first, second);
        IGraphContext context = Context;
        return output.IsExec == input.IsExec
               && (output.IsExec || context.CanFeed(output.Type, input.Type)
                   || (GenericNodes.MainInput(input.Node.Model.Kind) == input.Name && GenericNodes.Accepts(input.Node.Model.Kind, output.Type))
                   || GenericNodes.CastBetween(output.Type, input.Type, context.CanFeed) is not null);
    }

    /// <summary>Retire les fils d'une broche (Alt+clic).</summary>
    public void DisconnectPin(GraphPinViewModel pin)
    {
        NodeGraph graph = _asset.Graph.DisconnectPin(pin.Node.Id, pin.Name);
        if (graph.Connections.Length != _asset.Graph.Connections.Length)
        {
            Apply(graph, Tr.T("Disconnect a pin"));
        }
    }

    /// <summary>Largeur et hauteur d'une redirection à l'écran : son point est centré sur le double-clic.</summary>
    public const double RerouteWidth = 30;

    public const double RerouteHeight = 18;

    /// <summary>
    /// Pose une redirection sur un fil (double-clic, comme dans Unreal) : le fil passe par ce point,
    /// que l'on peut ensuite déplacer. Une seule étape annulable.
    /// </summary>
    public void InsertReroute(GraphConnectionViewModel wire, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(wire);
        GraphConnection model = wire.Model;
        GraphNode reroute = Reroutes.Create(wire.IsExec ? DataType.Exec : wire.From.Type, Math.Round(x - (RerouteWidth / 2)), Math.Round(y - (RerouteHeight / 2)));
        GraphConnection[] links = wire.IsExec
            ? [model with { ToNode = reroute.Id, ToPin = NodeInfo.ExecInput }, model with { FromNode = reroute.Id, FromPin = NodeInfo.ExecThen }]
            : [model with { ToNode = reroute.Id, ToPin = Reroutes.ValueInput }, model with { FromNode = reroute.Id, FromPin = Reroutes.ValueOutput }];
        Apply(_asset.Graph.Disconnect(model).Merge([reroute], links), Tr.T("Add a reroute"));
        SelectOnly(Nodes.FirstOrDefault(n => n.Id == reroute.Id));
    }

    /// <summary>
    /// Supprime les nœuds sélectionnés, sauf l'entrée d'une fonction (le graphe n'aurait plus de début).
    /// Une redirection supprimée laisse son fil en place : ses deux bouts sont reliés directement.
    /// </summary>
    public void DeleteSelection()
    {
        if (SelectedNodes.Any(n => n.IsEntry))
        {
            StatusText = Tr.T("The entry node cannot be deleted: it is the start of the function.");
        }

        List<Guid> ids = [.. SelectedNodes.Where(n => !n.IsEntry).Select(n => n.Id)];
        if (ids.Count > 0)
        {
            // The references of a deleted reroute go with it: their wires then start from its source.
            ids = [.. ids.Union(Reroutes.ReferencesTo(_asset.Graph, ids))];
            NodeGraph graph = Reroutes.Bypass(_asset.Graph, ids).RemoveNodes(ids);
            Apply(graph, ids.Count == 1 ? Tr.T("Delete a node") : Tr.F("Delete {0} nodes", ids.Count));
        }
    }

    // ----- Wires under the mouse, connections of the selection -----

    private GraphConnectionViewModel? _hoveredWire;

    /// <summary>Wire under the mouse: highlighted, and Backspace or Delete breaks it.</summary>
    public GraphConnectionViewModel? HoveredWire
    {
        get => _hoveredWire;
        set
        {
            GraphConnectionViewModel? old = _hoveredWire;
            if (SetProperty(ref _hoveredWire, value))
            {
                if (old is not null)
                {
                    old.IsHovered = false;
                }

                if (value is not null)
                {
                    value.IsHovered = true;
                }
            }
        }
    }

    /// <summary>Breaks one wire (Backspace on the wire under the mouse).</summary>
    public void Disconnect(GraphConnectionViewModel wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        HoveredWire = null;
        if (_asset.Graph.Connections.Contains(wire.Model))
        {
            Apply(_asset.Graph.Disconnect(wire.Model), Tr.T("Break a connection"));
        }
    }

    /// <summary>Removes every wire of the selected nodes (right-click on a node).</summary>
    public void BreakAllConnections()
    {
        var ids = SelectedNodes.Select(n => n.Id).ToHashSet();
        GraphConnection[] wires = [.. _asset.Graph.Connections.Where(c => ids.Contains(c.FromNode) || ids.Contains(c.ToNode))];
        if (wires.Length == 0)
        {
            StatusText = Tr.T("These nodes have no connection.");
            return;
        }

        HoveredWire = null;
        NodeGraph graph = wires.Aggregate(_asset.Graph, (current, wire) => current.Disconnect(wire));
        Apply(graph, wires.Length == 1 ? Tr.T("Break a connection") : Tr.F("Break {0} connections", wires.Length));
    }

    /// <summary>
    /// Refresh nodes (right-click on a node), as in Unreal: the selected nodes are described again
    /// (a function, a variable or a type may have changed), the values of inputs that no longer
    /// exist are dropped, new inputs take their default value and the wires that no longer fit
    /// are removed.
    /// </summary>
    public void RefreshSelection()
    {
        IGraphContext context = Context;
        NodeGraph graph = _asset.Graph;
        int removed = 0;
        foreach (GraphNodeViewModel selected in SelectedNodes.Where(n => !n.IsAnnotation).ToList())
        {
            if (graph.FindNode(selected.Id) is not { } node)
            {
                continue;
            }

            NodeInfo info = context.Describe(node);
            var pins = info.Inputs.Select(i => i.Name).ToHashSet();
            var values = node.Values;
            foreach (string key in values.Keys.Where(k => !pins.Contains(k) && IsFormerParameter(k)).ToList())
            {
                values = values.Remove(key);
            }

            foreach (PinInfo input in info.Inputs.Where(i => i.DefaultValue is not null && !values.ContainsKey(i.Name)))
            {
                values = values.SetItem(input.Name, input.DefaultValue!);
            }

            graph = graph.ReplaceNode(node with { Values = values });
            foreach (GraphConnection wire in graph.Connections.Where(c => c.FromNode == node.Id || c.ToNode == node.Id).ToList())
            {
                NodeGraph without = graph.Disconnect(wire);
                if (without.DescribeConnectionProblem(wire, context) is not null)
                {
                    graph = without;
                    removed++;
                }
            }
        }

        Apply(graph, Tr.T("Refresh nodes"));
        StatusText = removed == 0 ? Tr.T("Nodes refreshed.") : Tr.F("Nodes refreshed: {0} connection(s) no longer fitted and were removed.", removed);
    }

    /// <summary>
    /// A value kept for a parameter of a function, a field or a variable (named by its id) that no
    /// longer exists; the settings of the node itself (type, number of pins...) are kept.
    /// </summary>
    private static bool IsFormerParameter(string key) => Guid.TryParse(key, out _);

    /// <summary>A reference to a value reroute, placed below it and selected (like a named reroute of Unreal).</summary>
    public void CreateRerouteReference(GraphNodeViewModel reroute)
    {
        ArgumentNullException.ThrowIfNull(reroute);
        if (Reroutes.CreateReference(reroute.Model, reroute.X + 40, reroute.Y + 50) is not { } reference)
        {
            StatusText = Tr.T("Only a reroute of a value wire can have references.");
            return;
        }

        Apply(_asset.Graph.AddNode(reference), Tr.T("Add a reroute reference"));
        SelectOnly(_nodes[reference.Id]);
        StatusText = Tr.T("Connect the reference wherever the value of the reroute is needed; move it anywhere in the graph.");
    }

    /// <summary>Copie les nœuds sélectionnés (et les fils entre eux) un peu plus bas à droite.</summary>
    public void DuplicateSelection()
    {
        List<GraphNode> originals = [.. SelectedNodes.Where(n => !n.IsEntry).Select(n => n.Model)];
        if (originals.Count == 0)
        {
            return;
        }

        Dictionary<Guid, Guid> map = originals.ToDictionary(n => n.Id, _ => Guid.NewGuid());
        // A copied tooltip describes the copies of its nodes (or the originals that were not copied).
        List<GraphNode> copies =
        [
            .. originals.Select(n => n.Kind == GraphNodeKind.Tooltip
                ? GraphAnnotations.WithTargets(n, GraphAnnotations.Targets(n).Select(t => map.GetValueOrDefault(t, t))) with { Id = map[n.Id], X = n.X + 40, Y = n.Y + 40 }
                : n with { Id = map[n.Id], X = n.X + 40, Y = n.Y + 40 }),
        ];
        IEnumerable<GraphConnection> links = _asset.Graph.Connections
            .Where(c => map.ContainsKey(c.FromNode) && map.ContainsKey(c.ToNode))
            .Select(c => c with { FromNode = map[c.FromNode], ToNode = map[c.ToNode] });
        Apply(_asset.Graph.Merge(copies, links), originals.Count == 1 ? Tr.T("Duplicate a node") : Tr.F("Duplicate {0} nodes", originals.Count));

        foreach (GraphNodeViewModel node in Nodes)
        {
            node.IsSelected = map.ContainsValue(node.Id);
        }

        OnSelectionChanged();
    }

    /// <summary>Écrit dans le graphe la position des nœuds déplacés à la souris (une étape annulable).</summary>
    /// <param name="gridSize">Pas de la grille sur laquelle caler les nœuds déplacés ; 0 pour ne pas les caler.</param>
    public void CommitMove(double gridSize = 0)
    {
        Dictionary<Guid, (double X, double Y)> moved = Nodes
            .Where(n => Math.Abs(n.X - n.Model.X) > 0.01 || Math.Abs(n.Y - n.Model.Y) > 0.01)
            .ToDictionary(n => n.Id, n => (Snap(n.X, gridSize), Snap(n.Y, gridSize)));
        if (moved.Count > 0)
        {
            Apply(_asset.Graph.MoveNodes(moved), moved.Count == 1 ? Tr.T("Move a node") : Tr.F("Move {0} nodes", moved.Count));
        }
    }

    private static double Snap(double value, double gridSize) =>
        gridSize > 0 ? Math.Round(value / gridSize) * gridSize : Math.Round(value);

    /// <summary>Places nodes at the given positions in one step (alignment, distribution).</summary>
    public void MoveNodesTo(IReadOnlyDictionary<Guid, (double X, double Y)> positions, string description)
    {
        ArgumentNullException.ThrowIfNull(positions);
        Dictionary<Guid, (double X, double Y)> moved = positions
            .Where(p => _asset.Graph.FindNode(p.Key) is { } node && (Math.Abs(node.X - p.Value.X) > 0.01 || Math.Abs(node.Y - p.Value.Y) > 0.01))
            .ToDictionary(p => p.Key, p => (Math.Round(p.Value.X), Math.Round(p.Value.Y)));
        if (moved.Count > 0)
        {
            Apply(_asset.Graph.MoveNodes(moved), description);
        }
    }

    // ----- Comments and tooltips -----

    /// <summary>Frames <paramref name="area"/> (the selected nodes) with a comment, like C in Blueprints.</summary>
    public GraphNodeViewModel AddComment(Rect area)
    {
        GraphNode comment = GraphAnnotations.CreateComment(area.X, area.Y, area.Width, area.Height, Tr.T("Comment"));
        Apply(_asset.Graph.AddNode(comment), Tr.T("Add a comment"));
        GraphNodeViewModel added = _nodes[comment.Id];
        SelectOnly(added);
        return added;
    }

    /// <summary>Adds a tooltip bubble at <paramref name="position"/> for the selected nodes; its message is set in Properties.</summary>
    public GraphNodeViewModel? AddTooltip(Point position)
    {
        List<Guid> targets = [.. SelectedNodes.Where(n => !n.IsTooltip).Select(n => n.Id)];
        if (targets.Count == 0)
        {
            StatusText = Tr.T("Select the nodes (or the comment) to describe, then press T.");
            return null;
        }

        GraphNode tooltip = GraphAnnotations.CreateTooltip(targets, position.X, position.Y, Tr.T("Describe these nodes"));
        Apply(_asset.Graph.AddNode(tooltip), Tr.T("Add a tooltip"));
        GraphNodeViewModel added = _nodes[tooltip.Id];
        SelectOnly(added);
        StatusText = Tr.T("Write the message of the tooltip in Properties: it is shown when the mouse is over these nodes.");
        return added;
    }

    /// <summary>Writes the size (and the corner, for the left and top edges) of a comment resized with the mouse (one step).</summary>
    public void ResizeComment(GraphNodeViewModel comment, double gridSize = 0)
    {
        ArgumentNullException.ThrowIfNull(comment);
        if (_asset.Graph.FindNode(comment.Id) is { Kind: GraphNodeKind.Comment } current)
        {
            GraphNode resized = GraphAnnotations.Resize(current, Snap(comment.Width, gridSize), Snap(comment.Height, gridSize))
                .MoveTo(Snap(comment.X, gridSize), Snap(comment.Y, gridSize));
            Apply(_asset.Graph.ReplaceNode(resized), Tr.T("Resize a comment"));
        }
    }

    /// <summary>One more output on a Switch (the next case) or a Sequence.</summary>
    public void AddPin(GraphNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_asset.Graph.FindNode(node.Id) is { } current && FlowNodes.IsDynamic(current.Kind))
        {
            Apply(_asset.Graph.ReplaceNode(FlowNodes.AddPin(current)), Tr.T("Add a pin"));
        }
    }

    /// <summary>Removes the last output of a Switch or a Sequence, with its wire.</summary>
    public void RemovePin(GraphNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_asset.Graph.FindNode(node.Id) is { } current && FlowNodes.RemovePin(current) is { } removed)
        {
            Apply(_asset.Graph.DisconnectPin(node.Id, removed.Pin).ReplaceNode(removed.Node), Tr.T("Remove a pin"));
        }
    }

    /// <summary>Valeur saisie sur une entrée. Les frappes successives sur le même champ forment une seule étape.</summary>
    public void SetValue(GraphNodeViewModel node, string pin, string value)
    {
        if (node.Model.GetValue(pin) == value || _asset.Graph.FindNode(node.Id) is not { } current)
        {
            return;
        }

        bool sameField = _lastValueEdit == (node.Id, pin);
        Apply(_asset.Graph.ReplaceNode(current.WithValue(pin, value)), Tr.F("Change \"{0}\"", node.Title), merge: sameField);
        _lastValueEdit = (node.Id, pin);
    }

    // ----- Sélection -----

    public void SelectOnly(GraphNodeViewModel? node)
    {
        foreach (GraphNodeViewModel other in Nodes)
        {
            other.IsSelected = ReferenceEquals(other, node);
        }

        OnSelectionChanged();
    }

    public void SelectAll()
    {
        foreach (GraphNodeViewModel node in Nodes)
        {
            node.IsSelected = true;
        }

        OnSelectionChanged();
    }

    /// <summary>À appeler après une modification de la sélection par la vue (Ctrl+clic, rectangle).</summary>
    public void OnSelectionChanged() => OnPropertyChanged(nameof(SelectedNode));

    public override void Navigate(OutlineItem item)
    {
        if (item.NodeId is { } id)
        {
            ShowNode(id);
        }
    }

    /// <summary>Sélectionne un nœud et demande à la vue de le montrer.</summary>
    public void ShowNode(Guid id)
    {
        if (_nodes.TryGetValue(id, out GraphNodeViewModel? node))
        {
            SelectOnly(node);
            FocusRequested?.Invoke(this, id);
        }
    }

    /// <summary>Les modifications du graphe sont enregistrées avec le projet (Ctrl+S).</summary>
    public override bool Save() => true;

    public void Dispose()
    {
        _asset.Changed -= OnAssetChanged;
        _context.Project.Assets.Changed -= OnRegistryChanged;
        foreach (GraphConnectionViewModel connection in Connections)
        {
            connection.Dispose();
        }
    }

    // ----- Interne -----

    private void Apply(NodeGraph graph, string description, bool merge = false)
    {
        UndoHistory history = _context.History;
        if (!merge)
        {
            history.Seal();
            _lastValueEdit = null;
        }

        history.Execute(new PropertyChangeCommand<NodeGraph>(
            description, _asset, nameof(GraphAsset.Graph), () => _asset.Graph, v => _asset.Graph = v, graph));
        if (!merge)
        {
            history.Seal();
        }
    }

    private void OnAssetChanged(object? sender, AssetChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GraphAsset.Graph):
                Sync();
                break;
            case nameof(Asset.DisplayName) or nameof(Asset.ResourceId):
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DetailsTitle));
                ScheduleRefresh();
                break;
            default:
                // Signature, variables, héritage... : les nœuds et le panneau Définition suivent.
                ScheduleRefresh();
                break;
        }
    }

    /// <summary>Un autre graphe a changé (fonction appelée, classe utilisée...) : nœuds et types à revoir.</summary>
    private void OnRegistryChanged(object? sender, AssetRegistryChangedEventArgs e)
    {
        if (e.Asset is GraphAsset && !ReferenceEquals(e.Asset, _asset))
        {
            ScheduleRefresh();
        }
    }

    /// <summary>Regroupe les mises à jour d'une même saisie en une seule, une fois l'interface libre.</summary>
    private void ScheduleRefresh()
    {
        if (_refreshPending)
        {
            return;
        }

        _refreshPending = true;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _refreshPending = false;
            Definition.Refresh();
            Sync();
        });
    }

    /// <summary>Met les ViewModels en accord avec le graphe de l'asset, en gardant ceux des nœuds inchangés.</summary>
    private void Sync()
    {
        NodeGraph graph = _asset.Graph;
        var ids = graph.Nodes.Select(n => n.Id).ToHashSet();
        foreach (GraphNodeViewModel stale in Nodes.Where(n => !ids.Contains(n.Id)).ToList())
        {
            Nodes.Remove(stale);
            _nodes.Remove(stale.Id);
        }

        IGraphContext context = Context;
        foreach (GraphNode node in graph.Nodes)
        {
            NodeInfo info = context.Describe(node);
            if (_nodes.TryGetValue(node.Id, out GraphNodeViewModel? existing) && existing.Info.Signature == info.Signature)
            {
                existing.Update(node);
            }
            else
            {
                // Nouveau nœud, ou nœud dont les broches ont changé (paramètre ajouté à la fonction appelée...).
                var created = new GraphNodeViewModel(this, node, info, _context);
                if (existing is not null)
                {
                    created.IsSelected = existing.IsSelected;
                    Nodes[Nodes.IndexOf(existing)] = created;
                }
                else
                {
                    Nodes.Add(created);
                }

                _nodes[node.Id] = created;
            }
        }

        // Wires that did not change keep their ViewModel: only added, removed or re-pinned wires are rebuilt.
        var connected = new HashSet<GraphPinViewModel>();
        var wanted = new Dictionary<GraphConnection, (GraphPinViewModel From, GraphPinViewModel To)>();
        foreach (GraphConnection connection in graph.Connections)
        {
            GraphPinViewModel? from = _nodes.GetValueOrDefault(connection.FromNode)?.FindPin(connection.FromPin, input: false);
            GraphPinViewModel? to = _nodes.GetValueOrDefault(connection.ToNode)?.FindPin(connection.ToPin, input: true);
            if (from is not null && to is not null)
            {
                wanted[connection] = (from, to);
                connected.Add(from);
                connected.Add(to);
            }
        }

        for (int i = Connections.Count - 1; i >= 0; i--)
        {
            GraphConnectionViewModel old = Connections[i];
            if (wanted.TryGetValue(old.Model, out var pins) && ReferenceEquals(pins.From, old.From) && ReferenceEquals(pins.To, old.To))
            {
                wanted.Remove(old.Model);
            }
            else
            {
                old.Dispose();
                Connections.RemoveAt(i);
            }
        }

        foreach ((GraphConnection connection, (GraphPinViewModel from, GraphPinViewModel to)) in wanted)
        {
            Connections.Add(new GraphConnectionViewModel(connection, from, to));
        }

        foreach (GraphPinViewModel pin in Nodes.SelectMany(n => n.AllPins))
        {
            pin.IsConnected = connected.Contains(pin);
        }

        // Messages of the tooltips, shown over the nodes they describe.
        ILookup<Guid, string> tooltips = graph.Nodes
            .Where(n => n.Kind == GraphNodeKind.Tooltip)
            .SelectMany(t => GraphAnnotations.Targets(t).Select(target => (Target: target, Text: GraphAnnotations.Text(t))))
            .Where(t => t.Text.Length > 0)
            .ToLookup(t => t.Target, t => t.Text);
        foreach (GraphNodeViewModel node in Nodes)
        {
            node.AttachedTooltip = tooltips[node.Id].Any() ? string.Join("\n\n", tooltips[node.Id]) : null;
        }

        IReadOnlyList<GraphIssue> issues = ProgramValidator.Validate(_asset, Program);
        ILookup<Guid?, GraphIssue> byNode = issues.ToLookup(i => i.NodeId);
        foreach (GraphNodeViewModel node in Nodes)
        {
            node.SetIssues([.. byNode[node.Id]]);
        }

        Issues = [.. issues.Where(i => i.Severity != DiagnosticSeverity.Info)
            .Select(i => new GraphIssueItem(i.Message, i.NodeId, i.Severity == DiagnosticSeverity.Error))];
        Outline = [.. Nodes.Where(n => n.Info.IsEntryPoint)
            .OrderBy(n => n.Y).ThenBy(n => n.X)
            .Select(n => new OutlineItem(n.Title, OutlineKind.Event, 0, 0, n.Id, n.IsEntry ? Tr.T("input") : Tr.T("event")))];
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EventCount));
        OnPropertyChanged(nameof(NodeCount));
        OnPropertyChanged(nameof(SelectedNode));
    }

    /// <summary>Broche du nœud <paramref name="info"/> qui peut se brancher sur <paramref name="source"/>, ou null.</summary>
    private static PinInfo? FindCompatiblePin(NodeInfo info, GraphPinViewModel source, IGraphContext context)
    {
        if (source.IsExec)
        {
            return source.IsInput
                ? info.ExecOutputs.FirstOrDefault()
                : info.HasExecInput ? new PinInfo(NodeInfo.ExecInput, string.Empty, DataType.Exec) : null;
        }

        return source.IsInput
            ? info.Outputs.FirstOrDefault(o => context.CanFeed(o.Type, source.Type))
            : info.Inputs.FirstOrDefault(i => i.Connectable && context.CanFeed(source.Type, i.Type));
    }

    /// <summary>Minuscules sans accents, pour une recherche tolérante.</summary>
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString().Trim();
    }
}
