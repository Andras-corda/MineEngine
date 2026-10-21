using System.Globalization;
using System.Text;
using System.Windows.Threading;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Graphs;

/// <summary>
/// An editor whose nodes are listed in the Nodes window: a node graph or a Puppeteer tree. The
/// payload of an item is what the editor adds (dragged onto it, or double-clicked).
/// </summary>
public interface INodeLibraryHost
{
    /// <summary>Project assets: the list is read again when a graph or a tree changes (new function, public goal...).</summary>
    AssetRegistry Assets { get; }

    /// <summary>Nodes that can be added to this editor now.</summary>
    IReadOnlyList<NodeLibraryItem> LibraryItems { get; }

    /// <summary>Adds the node of an item at the center of the view (or, for a decorator, on the selected node).</summary>
    void AddFromLibrary(object payload);
}

/// <summary>
/// The Nodes window (V0.6.2): the nodes that can be added to the active editor, sorted by domain
/// (events, logic, blocks, items... or composites, tasks, decorators for a Puppeteer tree) and
/// filtered by a search. A node is added by a double-click or dragged onto the editor. The window
/// follows the active tab and stays empty when it is neither a graph nor a tree.
/// </summary>
public sealed class NodeLibraryViewModel : ObservableObject
{
    /// <summary>Format of the drag and drop data: the payload of a <see cref="NodeLibraryItem"/>.</summary>
    public const string DragFormat = "MineEngine.GraphNodeChoice";

    private INodeLibraryHost? _document;
    private AssetRegistry? _registry;
    private string _search = string.Empty;
    private NodeGroupItem? _selectedGroup;
    private IReadOnlyList<NodeLibraryItem> _all = [];
    private IReadOnlyList<NodeGroupItem> _groups = [];
    private bool _refreshPending;

    /// <summary>Editor that receives the nodes, or null when the active tab has no nodes.</summary>
    public INodeLibraryHost? Document
    {
        get => _document;
        set
        {
            if (!SetProperty(ref _document, value))
            {
                return;
            }

            if (_registry is not null)
            {
                _registry.Changed -= OnRegistryChanged;
            }

            _registry = value?.Assets;
            if (_registry is not null)
            {
                _registry.Changed += OnRegistryChanged;
            }

            OnPropertyChanged(nameof(HasDocument));
            Refresh();
        }
    }

    public bool HasDocument => _document is not null;

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Items));
            }
        }
    }

    /// <summary>"All", then each domain that has nodes in this editor.</summary>
    public IReadOnlyList<NodeGroupItem> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    public NodeGroupItem? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                OnPropertyChanged(nameof(Items));
            }
        }
    }

    /// <summary>Nodes of the selected domain matching the search (title, description or domain).</summary>
    public IReadOnlyList<NodeLibraryItem> Items
    {
        get
        {
            string search = Normalize(_search);
            string? group = _selectedGroup?.Key;
            return
            [
                .. _all.Where(i => group is null || i.GroupKey == group)
                    .Where(i => search.Length == 0
                                || Normalize(i.Title).Contains(search, StringComparison.Ordinal)
                                || Normalize(i.Description).Contains(search, StringComparison.Ordinal)
                                || Normalize(i.GroupLabel).Contains(search, StringComparison.Ordinal)),
            ];
        }
    }

    /// <summary>Adds the node of an item to the editor (double-click).</summary>
    public void Add(NodeLibraryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _document?.AddFromLibrary(item.Payload);
    }

    /// <summary>Reads again the nodes available in the editor (another graph defined a function, a tree became a goal...).</summary>
    public void Refresh()
    {
        _refreshPending = false;
        _all = _document is null ? [] : [.. _document.LibraryItems.OrderBy(i => i.GroupOrder)];

        string? selected = _selectedGroup?.Key;
        Groups =
        [
            new NodeGroupItem(null, Tr.T("All"), _all.Count),
            .. _all.GroupBy(i => (i.GroupKey, i.GroupLabel)).Select(g => new NodeGroupItem(g.Key.GroupKey, g.Key.GroupLabel, g.Count())),
        ];
        _selectedGroup = _groups.FirstOrDefault(g => g.Key == selected) ?? _groups[0];
        OnPropertyChanged(nameof(SelectedGroup));
        OnPropertyChanged(nameof(Items));
    }

    // Changes come in bursts (one per edit): the list is read again once, after them.
    private void OnRegistryChanged(object? sender, AssetRegistryChangedEventArgs e)
    {
        if (e.Asset is not (GraphAsset or BehaviorTreeAsset) || _refreshPending)
        {
            return;
        }

        _refreshPending = true;
        Dispatcher.CurrentDispatcher.BeginInvoke(() =>
        {
            if (_refreshPending)
            {
                Refresh();
            }
        }, DispatcherPriority.Background);
    }

    private static string Normalize(string text)
    {
        string decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string([.. decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
    }
}

/// <summary>A domain of the Nodes window; a null key means every node.</summary>
/// <param name="Key">Key of the domain: a <see cref="NodeGroup"/> name, or "bt:" and a Puppeteer category.</param>
public sealed record NodeGroupItem(string? Key, string Label, int Count);

/// <summary>A node of the Nodes window.</summary>
/// <param name="Payload">What the editor adds (a graph node choice, a Puppeteer choice).</param>
/// <param name="GroupKey">Domain of the node, which also gives its color.</param>
/// <param name="GroupOrder">Position of the domain in the list.</param>
public sealed record NodeLibraryItem(object Payload, string Title, string Description, string GroupKey, string GroupLabel, int GroupOrder)
{
    /// <summary>Start of the domain key of the functions of a library ("lib:" and its name).</summary>
    public const string LibraryKeyPrefix = "lib:";

    /// <summary>Item of a node of a graph, in the domain of its kind.</summary>
    public static NodeLibraryItem ForGraph(NodeChoiceViewModel choice)
    {
        // A function of a library: a domain of its own, named after the library, after the others.
        if (choice.Info.Library is { } library)
        {
            return new NodeLibraryItem(choice, choice.Title, choice.Description, LibraryKeyPrefix + library, library, 1000);
        }

        NodeGroup group = NodeCatalog.GroupOf(choice.Info);
        return new NodeLibraryItem(choice, choice.Title, choice.Description, group.ToString(), NodeCatalog.GroupLabel(group), (int)group);
    }
}
