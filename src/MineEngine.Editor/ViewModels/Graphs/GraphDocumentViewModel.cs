using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
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
/// Onglet d'édition d'un graphe nodal. Il présente le graphe de l'asset sous forme de
/// nœuds et de fils, et traduit chaque geste (ajout, branchement, déplacement, saisie)
/// en un nouveau graphe appliqué par l'historique du projet : tout est annulable, et
/// l'annulation remet l'éditeur à jour comme n'importe quelle modification.
/// </summary>
public sealed class GraphDocumentViewModel : DocumentViewModel, IDisposable
{
    private readonly GraphAsset _asset;
    private readonly AssetEditingContext _context;
    private readonly Dictionary<Guid, GraphNodeViewModel> _nodes = [];
    private string _paletteSearch = string.Empty;
    private string? _statusText;
    private GraphPinViewModel? _pendingSource;
    private (Guid Node, string Pin)? _lastValueEdit;
    private IReadOnlyList<GraphIssueItem> _issues = [];

    public GraphDocumentViewModel(GraphAsset asset, AssetEditingContext context, string displayPath)
        : base("graph:" + asset.Id.ToString("N"), displayPath)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _asset.Changed += OnAssetChanged;
        Sync();
    }

    /// <summary>Demande à la vue de montrer un nœud (structure, liste des problèmes).</summary>
    public event EventHandler<Guid>? FocusRequested;

    public GraphAsset Asset => _asset;

    public Guid AssetId => _asset.Id;

    public override string FileName => string.IsNullOrWhiteSpace(_asset.DisplayName) ? _asset.ResourceId.Value : _asset.DisplayName;

    public override bool IsReadOnly => false;

    public override string DetailsBadge => "GRAPHE";

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
                (0, 0) => "Aucun problème",
                _ => string.Join(", ", new[]
                {
                    errors > 0 ? Plural(errors, "erreur", "erreurs") : null,
                    warnings > 0 ? Plural(warnings, "avertissement", "avertissements") : null,
                }.OfType<string>()),
            };
        }
    }

    public int EventCount => Nodes.Count(n => n.Category == NodeCategory.Event);

    /// <summary>Message passager de l'éditeur (fil refusé...), ou null.</summary>
    public string? StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
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

    /// <summary>Nœuds proposés, filtrés par la recherche (titre, description ou famille) et par la broche en attente.</summary>
    public IReadOnlyList<NodeChoiceViewModel> PaletteChoices
    {
        get
        {
            string search = Normalize(_paletteSearch);
            return
            [
                .. NodeCatalog.All
                    .Where(n => _pendingSource is null || FindCompatiblePin(n, _pendingSource) is not null)
                    .Where(n => search.Length == 0
                                || Normalize(n.Title).Contains(search, StringComparison.Ordinal)
                                || Normalize(n.Description).Contains(search, StringComparison.Ordinal)
                                || Normalize(NodeCatalog.CategoryLabel(n.Category)).Contains(search, StringComparison.Ordinal))
                    .OrderBy(n => n.Category)
                    .Select(n => new NodeChoiceViewModel(n)),
            ];
        }
    }

    // ----- Modifications -----

    /// <summary>Ajoute un nœud ; s'il y a une broche en attente, le relie à sa première broche compatible.</summary>
    public GraphNodeViewModel AddNode(NodeInfo info, Point position)
    {
        ArgumentNullException.ThrowIfNull(info);
        GraphNode node = GraphNode.Create(info.Kind, Math.Round(position.X), Math.Round(position.Y));
        NodeGraph graph = _asset.Graph.AddNode(node);
        if (_pendingSource is { } source && FindCompatiblePin(info, source) is { } pin)
        {
            GraphConnection connection = source.IsInput
                ? new GraphConnection(node.Id, pin.Name, source.Node.Id, source.Name)
                : new GraphConnection(source.Node.Id, source.Name, node.Id, pin.Name);
            if (graph.DescribeConnectionProblem(connection) is null)
            {
                graph = graph.Connect(connection);
            }
        }

        PendingSource = null;
        Apply(graph, $"Ajouter « {info.Title} »");
        GraphNodeViewModel added = _nodes[node.Id];
        SelectOnly(added);
        return added;
    }

    /// <summary>Relie deux broches (dans n'importe quel ordre) ; un refus est expliqué dans <see cref="StatusText"/>.</summary>
    public bool TryConnect(GraphPinViewModel first, GraphPinViewModel second)
    {
        if (first.IsInput == second.IsInput)
        {
            StatusText = "Reliez une sortie (à droite d'un nœud) à une entrée (à gauche).";
            return false;
        }

        (GraphPinViewModel output, GraphPinViewModel input) = first.IsInput ? (second, first) : (first, second);
        var connection = new GraphConnection(output.Node.Id, output.Name, input.Node.Id, input.Name);
        if (_asset.Graph.DescribeConnectionProblem(connection) is { } problem)
        {
            StatusText = problem;
            return false;
        }

        StatusText = null;
        Apply(_asset.Graph.Connect(connection), "Relier des nœuds");
        return true;
    }

    /// <summary>Retire les fils d'une broche (Alt+clic).</summary>
    public void DisconnectPin(GraphPinViewModel pin)
    {
        NodeGraph graph = _asset.Graph.DisconnectPin(pin.Node.Id, pin.Name);
        if (graph.Connections.Length != _asset.Graph.Connections.Length)
        {
            Apply(graph, "Débrancher une broche");
        }
    }

    public void DeleteSelection()
    {
        List<Guid> ids = [.. SelectedNodes.Select(n => n.Id)];
        if (ids.Count > 0)
        {
            Apply(_asset.Graph.RemoveNodes(ids), ids.Count == 1 ? "Supprimer un nœud" : $"Supprimer {ids.Count} nœuds");
        }
    }

    /// <summary>Copie les nœuds sélectionnés (et les fils entre eux) un peu plus bas à droite.</summary>
    public void DuplicateSelection()
    {
        List<GraphNode> originals = [.. SelectedNodes.Select(n => n.Model)];
        if (originals.Count == 0)
        {
            return;
        }

        Dictionary<Guid, Guid> map = originals.ToDictionary(n => n.Id, _ => Guid.NewGuid());
        List<GraphNode> copies = [.. originals.Select(n => n with { Id = map[n.Id], X = n.X + 40, Y = n.Y + 40 })];
        IEnumerable<GraphConnection> links = _asset.Graph.Connections
            .Where(c => map.ContainsKey(c.FromNode) && map.ContainsKey(c.ToNode))
            .Select(c => c with { FromNode = map[c.FromNode], ToNode = map[c.ToNode] });
        Apply(_asset.Graph.Merge(copies, links), originals.Count == 1 ? "Dupliquer un nœud" : $"Dupliquer {originals.Count} nœuds");

        foreach (GraphNodeViewModel node in Nodes)
        {
            node.IsSelected = map.ContainsValue(node.Id);
        }

        OnSelectionChanged();
    }

    /// <summary>Écrit dans le graphe la position des nœuds déplacés à la souris (une étape annulable).</summary>
    public void CommitMove()
    {
        Dictionary<Guid, (double X, double Y)> moved = Nodes
            .Where(n => Math.Abs(n.X - n.Model.X) > 0.01 || Math.Abs(n.Y - n.Model.Y) > 0.01)
            .ToDictionary(n => n.Id, n => (Math.Round(n.X), Math.Round(n.Y)));
        if (moved.Count > 0)
        {
            Apply(_asset.Graph.MoveNodes(moved), moved.Count == 1 ? "Déplacer un nœud" : $"Déplacer {moved.Count} nœuds");
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
        Apply(_asset.Graph.ReplaceNode(current.WithValue(pin, value)), $"Modifier « {node.Title} »", merge: sameField);
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
                break;
        }
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

        foreach (GraphNode node in graph.Nodes)
        {
            if (_nodes.TryGetValue(node.Id, out GraphNodeViewModel? existing))
            {
                existing.Update(node);
            }
            else
            {
                var created = new GraphNodeViewModel(this, node, _context);
                _nodes[node.Id] = created;
                Nodes.Add(created);
            }
        }

        foreach (GraphConnectionViewModel old in Connections)
        {
            old.Dispose();
        }

        Connections.Clear();
        var connected = new HashSet<GraphPinViewModel>();
        foreach (GraphConnection connection in graph.Connections)
        {
            GraphPinViewModel? from = _nodes.GetValueOrDefault(connection.FromNode)?.FindPin(connection.FromPin, input: false);
            GraphPinViewModel? to = _nodes.GetValueOrDefault(connection.ToNode)?.FindPin(connection.ToPin, input: true);
            if (from is not null && to is not null)
            {
                Connections.Add(new GraphConnectionViewModel(connection, from, to));
                connected.Add(from);
                connected.Add(to);
            }
        }

        foreach (GraphPinViewModel pin in Nodes.SelectMany(n => n.AllPins))
        {
            pin.IsConnected = connected.Contains(pin);
        }

        IReadOnlyList<GraphIssue> issues = GraphValidator.Validate(graph);
        ILookup<Guid?, GraphIssue> byNode = issues.ToLookup(i => i.NodeId);
        foreach (GraphNodeViewModel node in Nodes)
        {
            node.SetIssues([.. byNode[node.Id]]);
        }

        Issues = [.. issues.Where(i => i.Severity != DiagnosticSeverity.Info)
            .Select(i => new GraphIssueItem(i.Message, i.NodeId, i.Severity == DiagnosticSeverity.Error))];
        Outline = [.. Nodes.Where(n => n.Category == NodeCategory.Event)
            .OrderBy(n => n.Y).ThenBy(n => n.X)
            .Select(n => new OutlineItem(n.Title, OutlineKind.Event, 0, 0, n.Id, "événement"))];
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EventCount));
        OnPropertyChanged(nameof(SelectedNode));
    }

    /// <summary>Broche du nœud <paramref name="info"/> qui peut se brancher sur <paramref name="source"/>, ou null.</summary>
    private static PinInfo? FindCompatiblePin(NodeInfo info, GraphPinViewModel source)
    {
        if (source.IsExec)
        {
            return source.IsInput
                ? info.ExecOutputs.FirstOrDefault()
                : info.HasExecInput ? new PinInfo(NodeInfo.ExecInput, string.Empty, PinType.Exec) : null;
        }

        return source.IsInput
            ? info.Outputs.FirstOrDefault(o => o.Type.CanFeed(source.Type))
            : info.Inputs.FirstOrDefault(i => i.Connectable && source.Type.CanFeed(i.Type));
    }

    private static string Plural(int count, string one, string many) =>
        string.Create(CultureInfo.CurrentCulture, $"{count} {(count == 1 ? one : many)}");

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
