using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using MineEngine.Assets;
using MineEngine.Assets.Puppeteer;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.Diagnostics;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.ViewModels.Graphs;

using MineEngine.Editor.ViewModels.Graphs;

namespace MineEngine.Editor.ViewModels.Puppeteer;

/// <summary>Entry of the Puppeteer add-node menu.</summary>
public sealed class BtChoiceViewModel
{
    public BtChoiceViewModel(BtNodeInfo info)
    {
        Info = info;
    }

    /// <summary>"Run a goal" entry for one public goal of the project.</summary>
    public BtChoiceViewModel(BtNodeInfo info, BehaviorTreeAsset goal)
        : this(info)
    {
        GoalId = goal.Id;
        GoalName = string.IsNullOrWhiteSpace(goal.DisplayName) ? goal.ResourceId.Value : goal.DisplayName;
    }

    public BtNodeInfo Info { get; }

    public BtNodeKind Kind => Info.Kind;

    public BtCategory Category => Info.Category;

    /// <summary>Goal run by the added node, or null for a plain node.</summary>
    public Guid? GoalId { get; }

    public string? GoalName { get; }

    public string Title => GoalName is null ? Info.Title : Tr.F("Goal: {0}", GoalName);

    public string Description => GoalName is null ? Info.Description : Tr.T("Runs this public goal as a branch of the tree.");

    public string CategoryLabel => BtCatalog.CategoryLabel(Info.Category);

    public override string ToString() => Title;
}

/// <summary>
/// Puppeteer tab: the editor of a behavior tree, like Unreal's. It shows the tree (nodes,
/// links, decorators and services), the details of the selected element and the
/// blackboard. Every gesture becomes a change applied by the project history: everything
/// can be undone, and undoing updates the editor.
/// </summary>
public sealed class BehaviorTreeDocumentViewModel : DocumentViewModel, INodeLibraryHost, IDisposable
{
    /// <summary>Distance between the edge of a node and the center of its pin (see the view).</summary>
    public const double PinOffset = 7;

    private readonly BehaviorTreeAsset _asset;
    private readonly AssetEditingContext _context;
    private readonly Dictionary<Guid, BtNodeViewModel> _nodes = [];
    private BtItemViewModel? _selectedItem;
    private IReadOnlyList<BtParameterViewModel> _parameters = [];
    private IReadOnlyList<GraphIssueItem> _issues = [];
    private string _paletteSearch = string.Empty;
    private string? _statusText;
    private (BtNodeViewModel Node, bool AsParent)? _pending;
    private (Guid Item, string Name)? _lastValueEdit;
    private Guid? _lastKeyEdit;
    private bool _refreshPending;

    public BehaviorTreeDocumentViewModel(BehaviorTreeAsset asset, AssetEditingContext context, string displayPath)
        : base(IdFor(asset.Id), displayPath)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _asset.Changed += OnAssetChanged;
        _context.Project.Assets.Changed += OnRegistryChanged;
        SyncKeys();
        Sync();
    }

    public static string IdFor(Guid assetId) => "puppeteer:" + assetId.ToString("N");

    public BehaviorTreeAsset Asset => _asset;

    public Guid AssetId => _asset.Id;

    public AssetRegistry Assets => _context.Project.Assets;

    public AssetEditingContext Context => _context;

    public override string FileName => string.IsNullOrWhiteSpace(_asset.DisplayName) ? _asset.ResourceId.Value : _asset.DisplayName;

    public override bool IsReadOnly => false;

    public override string DetailsBadge => "PUPPETEER";

    public ObservableCollection<BtNodeViewModel> Nodes { get; } = [];

    public ObservableCollection<BtLinkViewModel> Links { get; } = [];

    public ObservableCollection<BlackboardKeyViewModel> Keys { get; } = [];

    /// <summary>Mobs that use this tree.</summary>
    public IReadOnlyList<string> UsedBy =>
        [.. Assets.OfType<MobAsset>().Where(m => m.BehaviorTreeId == _asset.Id).Select(m => string.IsNullOrWhiteSpace(m.DisplayName) ? m.ResourceId.Value : m.DisplayName)];

    public string UsedByText => UsedBy.Count == 0 ? Tr.T("No mob (choose this tree in the AI of a mob)") : string.Join(", ", UsedBy);

    public int TaskCount => Nodes.Count(n => n.Category == BtCategory.Task);

    public bool IsEmpty => Nodes.Count <= 1;

    public event EventHandler<Guid>? FocusRequested;

    // ----- Selection and details -----

    public IEnumerable<BtNodeViewModel> SelectedNodes => Nodes.Where(n => n.IsSelected);

    /// <summary>Node, decorator or service whose settings are shown in Details (single selection).</summary>
    public BtItemViewModel? SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedNode));
                OnPropertyChanged(nameof(SelectedAttachment));
                RebuildParameters();
            }
        }
    }

    public bool HasSelection => _selectedItem is not null;

    public BtNodeViewModel? SelectedNode => _selectedItem as BtNodeViewModel;

    public BtAttachmentViewModel? SelectedAttachment => _selectedItem as BtAttachmentViewModel;

    public IReadOnlyList<BtParameterViewModel> Parameters
    {
        get => _parameters;
        private set
        {
            if (SetProperty(ref _parameters, value))
            {
                OnPropertyChanged(nameof(HasParameters));
            }
        }
    }

    public bool HasParameters => _parameters.Count > 0;

    /// <summary>Decorators offered by "Add a decorator": conditions, then modifiers.</summary>
    public IReadOnlyList<BtChoiceViewModel> DecoratorChoices { get; } =
        [.. BtCatalog.InCategory(BtCategory.Decorator).OrderBy(i => i.IsCondition ? 0 : 1).Select(i => new BtChoiceViewModel(i))];

    public IReadOnlyList<BtChoiceViewModel> ServiceChoices { get; } =
        [.. BtCatalog.InCategory(BtCategory.Service).Select(i => new BtChoiceViewModel(i))];

    // ----- Problems -----

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

    /// <summary>Transient message of the editor (refused link...), or null.</summary>
    public string? StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    // ----- Menu d'ajout -----

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

    /// <summary>
    /// Link dragged from a pin and dropped in empty space: the chosen node will be connected to
    /// it, as a child (<c>AsParent</c> true: the pin was the bottom one) or as a parent.
    /// </summary>
    public (BtNodeViewModel Node, bool AsParent)? Pending
    {
        get => _pending;
        set
        {
            _pending = value;
            OnPropertyChanged(nameof(PaletteChoices));
        }
    }

    /// <summary>Composites, then tasks, filtered by the search (and by the pending link).</summary>
    public IReadOnlyList<BtChoiceViewModel> PaletteChoices
    {
        get
        {
            string search = Normalize(_paletteSearch);
            bool parentOnly = _pending is { AsParent: false };
            BtNodeInfo runGoal = BtCatalog.Get(BtNodeKind.RunGoal);
            return
            [
                .. BtCatalog.All
                    .Where(i => i.Category is BtCategory.Composite or BtCategory.Task)
                    .Where(i => !parentOnly || i.HasChildren)
                    .Select(i => new BtChoiceViewModel(i))
                    .Concat(parentOnly ? [] : PublicGoals.Select(g => new BtChoiceViewModel(runGoal, g)))
                    .Where(c => search.Length == 0
                                || Normalize(c.Title).Contains(search, StringComparison.Ordinal)
                                || Normalize(c.Description).Contains(search, StringComparison.Ordinal)
                                || Normalize(c.CategoryLabel).Contains(search, StringComparison.Ordinal))
                    .OrderBy(c => c.Category),
            ];
        }
    }

    // ----- Tree changes -----

    /// <summary>Public goals of the project that this tree can run (all but itself).</summary>
    public IReadOnlyList<BehaviorTreeAsset> PublicGoals =>
        [.. Assets.OfType<BehaviorTreeAsset>().Where(t => t.IsPublicGoal && t.Id != _asset.Id)
            .OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>The tree can be run by other trees ("Run a goal").</summary>
    public bool IsPublicGoal
    {
        get => _asset.IsPublicGoal;
        set
        {
            if (value != _asset.IsPublicGoal)
            {
                _context.History.Seal();
                _context.History.Execute(new PropertyChangeCommand<bool>(
                    value ? Tr.T("Make the tree a public goal") : Tr.T("Make the tree private"),
                    _asset, nameof(BehaviorTreeAsset.IsPublicGoal), () => _asset.IsPublicGoal, v => _asset.IsPublicGoal = v, value));
                _context.History.Seal();
            }
        }
    }

    // ----- Nodes window -----

    /// <summary>Point of the tree at the center of its view, kept up to date by the view.</summary>
    public Point ViewCenter { get; set; }

    /// <summary>Composites, tasks, public goals, then decorators and services (added on a node).</summary>
    public IReadOnlyList<NodeLibraryItem> LibraryItems
    {
        get
        {
            BtNodeInfo runGoal = BtCatalog.Get(BtNodeKind.RunGoal);
            static NodeLibraryItem Item(BtChoiceViewModel choice, string key, string label, int order) =>
                new(choice, choice.Title, choice.Description, "bt:" + key, label, order);
            return
            [
                .. BtCatalog.InCategory(BtCategory.Composite).Select(i => Item(new BtChoiceViewModel(i), nameof(BtCategory.Composite), Tr.T("Composites"), 0)),
                .. BtCatalog.InCategory(BtCategory.Task).Where(i => i.Kind != BtNodeKind.RunGoal)
                    .Select(i => Item(new BtChoiceViewModel(i), nameof(BtCategory.Task), Tr.T("Tasks"), 1)),
                .. PublicGoals.Select(g => Item(new BtChoiceViewModel(runGoal, g), "Goal", Tr.T("Public goals"), 2)),
                .. DecoratorChoices.Select(c => Item(c, nameof(BtCategory.Decorator), Tr.T("Decorators"), 3)),
                .. ServiceChoices.Select(c => Item(c, nameof(BtCategory.Service), Tr.T("Services"), 4)),
            ];
        }
    }

    /// <summary>
    /// A composite, a task or a goal is added at the center of the view; a decorator or a service
    /// goes on the selected node.
    /// </summary>
    public void AddFromLibrary(object payload)
    {
        if (payload is not BtChoiceViewModel choice)
        {
            return;
        }

        if (choice.Category is BtCategory.Decorator or BtCategory.Service)
        {
            if ((SelectedNode ?? SelectedAttachment?.Owner) is { } node)
            {
                AddAttachment(node, choice.Kind);
            }
            else
            {
                StatusText = Tr.T("Select a node first: decorators and services go on a node (or drop them onto it).");
            }

            return;
        }

        Pending = null;
        AddNode(choice, new Point(ViewCenter.X - 110, ViewCenter.Y - 30));
    }

    /// <summary>Adds a node (from a palette entry); when a link is pending, connects it.</summary>
    public void AddNode(BtChoiceViewModel choice, Point position)
    {
        ArgumentNullException.ThrowIfNull(choice);
        AddNode(choice.Kind, position, choice.GoalId);
    }

    /// <summary>Adds a node; when a link is pending, connects it.</summary>
    public void AddNode(BtNodeKind kind, Point position, Guid? goal = null)
    {
        BtNode node = BtNode.Create(kind, Math.Round(position.X), Math.Round(position.Y));
        if (goal is { } goalId)
        {
            node = node.WithValue(BtCatalog.Goal, goalId.ToString("D"));
        }

        BehaviorTree tree = _asset.Tree.AddNode(node);
        if (_pending is { } pending)
        {
            (Guid parent, Guid child) = pending.AsParent ? (pending.Node.Id, node.Id) : (node.Id, pending.Node.Id);
            if (tree.DescribeLinkProblem(parent, child) is null)
            {
                tree = tree.Connect(parent, child);
            }
        }

        Pending = null;
        Apply(tree, Tr.F("Add \"{0}\"", node.Info.Title));
        if (_nodes.TryGetValue(node.Id, out BtNodeViewModel? added))
        {
            SelectOnly(added);
        }
    }

    /// <summary>Links two nodes: the first one becomes the parent of the second. A refusal is shown in <see cref="StatusText"/>.</summary>
    public bool TryConnect(BtNodeViewModel parent, BtNodeViewModel child)
    {
        if (_asset.Tree.DescribeLinkProblem(parent.Id, child.Id) is { } problem)
        {
            StatusText = problem;
            return false;
        }

        StatusText = null;
        Apply(_asset.Tree.Connect(parent.Id, child.Id), Tr.T("Connect nodes"));
        return true;
    }

    /// <summary>Detaches a node from its parent (Alt+click on its top pin).</summary>
    public void DisconnectParent(BtNodeViewModel node)
    {
        if (_asset.Tree.ParentOf(node.Id) is not null)
        {
            Apply(_asset.Tree.DisconnectParent(node.Id), Tr.T("Detach a node"));
        }
    }

    /// <summary>Detaches all the children of a node (Alt+click on its bottom pin).</summary>
    public void DisconnectChildren(BtNodeViewModel node)
    {
        if (_asset.Tree.ChildrenOf(node.Id).Count > 0)
        {
            Apply(_asset.Tree.DisconnectChildren(node.Id), Tr.T("Detach the children"));
        }
    }

    /// <summary>Deletes the selected attached piece, otherwise the selected nodes (never the root).</summary>
    public void DeleteSelection()
    {
        if (SelectedAttachment is { } attachment)
        {
            RemoveAttachment(attachment);
            return;
        }

        if (SelectedNodes.Any(n => n.IsRoot))
        {
            StatusText = Tr.T("The root cannot be deleted: it is the start of the tree.");
        }

        List<Guid> ids = [.. SelectedNodes.Where(n => !n.IsRoot).Select(n => n.Id)];
        if (ids.Count > 0)
        {
            Apply(_asset.Tree.RemoveNodes(ids), ids.Count == 1 ? Tr.T("Delete a node") : Tr.F("Delete {0} nodes", ids.Count));
        }
    }

    /// <summary>Copies the selected nodes (with their attached pieces and the links between them) a little lower on the right.</summary>
    public void DuplicateSelection()
    {
        List<BtNode> originals = [.. SelectedNodes.Where(n => !n.IsRoot).Select(n => n.Model)];
        if (originals.Count == 0)
        {
            return;
        }

        Dictionary<Guid, BtNode> copies = originals.ToDictionary(n => n.Id, n => BehaviorTreeAsset.CloneNode(n, n.X + 40, n.Y + 40));
        IEnumerable<BtLink> links = _asset.Tree.Links
            .Where(l => copies.ContainsKey(l.Parent) && copies.ContainsKey(l.Child))
            .Select(l => new BtLink(copies[l.Parent].Id, copies[l.Child].Id));
        Apply(_asset.Tree.Merge(copies.Values, links), originals.Count == 1 ? Tr.T("Duplicate a node") : Tr.F("Duplicate {0} nodes", originals.Count));

        var created = copies.Values.Select(c => c.Id).ToHashSet();
        foreach (BtNodeViewModel node in Nodes)
        {
            node.IsSelected = created.Contains(node.Id);
        }

        OnSelectionChanged();
    }

    /// <summary>Writes the position of the nodes moved with the mouse (one undoable step); the order of the children may change.</summary>
    public void CommitMove(double gridSize = 0)
    {
        Dictionary<Guid, (double X, double Y)> moved = Nodes
            .Where(n => Math.Abs(n.X - n.Model.X) > 0.01 || Math.Abs(n.Y - n.Model.Y) > 0.01)
            .ToDictionary(n => n.Id, n => (Snap(n.X, gridSize), Snap(n.Y, gridSize)));
        if (moved.Count > 0)
        {
            Apply(_asset.Tree.MoveNodes(moved), moved.Count == 1 ? Tr.T("Move a node") : Tr.F("Move {0} nodes", moved.Count));
        }
    }

    /// <summary>Lays the tree out neatly (see <see cref="BehaviorTree.Arranged"/>), in one undoable step.</summary>
    public void ArrangeTree() => Apply(_asset.Tree.Arranged(), Tr.T("Arrange the tree"));

    // ----- Decorators and services -----

    public void AddAttachment(BtNodeViewModel node, BtNodeKind kind)
    {
        if (node.IsRoot)
        {
            StatusText = Tr.T("The root carries no decorator or service: put them on the node that follows it.");
            return;
        }

        BtAttachment attachment = BtAttachment.Create(kind);
        Apply(_asset.Tree.AddAttachment(node.Id, attachment), Tr.F("Add \"{0}\"", attachment.Info.Title));
        if (_nodes.GetValueOrDefault(node.Id)?.FindAttachment(attachment.Id) is { } added)
        {
            Select(added);
        }
    }

    public void RemoveAttachment(BtAttachmentViewModel attachment)
    {
        Apply(_asset.Tree.RemoveAttachment(attachment.Id), Tr.F("Remove \"{0}\"", attachment.Info.Title));
        if (_nodes.GetValueOrDefault(attachment.Owner.Id) is { } owner)
        {
            SelectOnly(owner);
        }
    }

    public void MoveAttachment(BtAttachmentViewModel attachment, int offset)
    {
        BehaviorTree tree = _asset.Tree.MoveAttachment(attachment.Id, offset);
        if (!ReferenceEquals(tree, _asset.Tree))
        {
            Apply(tree, Tr.T("Change the order"));
        }
    }

    /// <summary>Setting of a node or of an attached piece. Successive keystrokes in the same field form a single step.</summary>
    public void SetValue(BtItemViewModel item, string name, string value)
    {
        if (item.GetValue(name) == value)
        {
            return;
        }

        BehaviorTree tree = _asset.Tree;
        BehaviorTree changed;
        if (item is BtAttachmentViewModel && tree.FindAttachment(item.Id) is { } found)
        {
            changed = tree.ReplaceAttachment(found.Attachment.WithValue(name, value));
        }
        else if (tree.FindNode(item.Id) is { } node)
        {
            changed = tree.ReplaceNode(node.WithValue(name, value));
        }
        else
        {
            return;
        }

        bool sameField = _lastValueEdit == (item.Id, name);
        Apply(changed, Tr.F("Change \"{0}\"", item.Info.Title), merge: sameField);
        _lastValueEdit = (item.Id, name);
    }

    // ----- Blackboard -----

    public void AddKey(BlackboardKeyType type)
    {
        string baseName = type switch
        {
            BlackboardKeyType.Entity => "target",
            BlackboardKeyType.Position => "position",
            BlackboardKeyType.Number => "number",
            _ => "flag",
        };
        string name = baseName;
        for (int i = 2; _asset.Keys.Any(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase)); i++)
        {
            name = baseName + i.ToString(CultureInfo.InvariantCulture);
        }

        ApplyKeys([.. _asset.Keys, new BlackboardKey(Guid.NewGuid(), name, type)], Tr.F("Add the key \"{0}\"", name));
    }

    /// <summary>Renames a key; nodes follow (they refer to it by its identifier).</summary>
    public void RenameKey(BlackboardKeyViewModel key, string name)
    {
        string trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed.Length > BlackboardKey.MaxNameLength)
        {
            StatusText = Tr.T("The name of a key must have between 1 and 48 characters.");
            return;
        }

        if (key.Model.Name == trimmed)
        {
            return;
        }

        StatusText = null;
        bool same = _lastKeyEdit == key.Id;
        ApplyKeys([.. _asset.Keys.Select(k => k.Id == key.Id ? k with { Name = trimmed } : k)], Tr.T("Rename a key"), merge: same);
        _lastKeyEdit = key.Id;
    }

    public void ChangeKeyType(BlackboardKeyViewModel key, BlackboardKeyType type) =>
        ApplyKeys([.. _asset.Keys.Select(k => k.Id == key.Id ? k with { Type = type } : k)], Tr.T("Change the type of a key"));

    /// <summary>Deletes a key; the settings that used it are reported as problems.</summary>
    public void RemoveKey(BlackboardKeyViewModel key) =>
        ApplyKeys([.. _asset.Keys.Where(k => k.Id != key.Id)], Tr.F("Delete the key \"{0}\"", key.Model.Name));

    // ----- Selection -----

    public void SelectOnly(BtNodeViewModel? node)
    {
        foreach (BtNodeViewModel other in Nodes)
        {
            other.IsSelected = ReferenceEquals(other, node);
            foreach (BtAttachmentViewModel attachment in other.Attachments)
            {
                attachment.IsSelected = false;
            }
        }

        SelectedItem = node;
    }

    /// <summary>Selects an attached piece alone (its settings are shown).</summary>
    public void Select(BtAttachmentViewModel attachment)
    {
        foreach (BtNodeViewModel node in Nodes)
        {
            node.IsSelected = false;
            foreach (BtAttachmentViewModel other in node.Attachments)
            {
                other.IsSelected = ReferenceEquals(other, attachment);
            }
        }

        SelectedItem = attachment;
    }

    public void SelectAll()
    {
        foreach (BtNodeViewModel node in Nodes)
        {
            node.IsSelected = true;
        }

        OnSelectionChanged();
    }

    /// <summary>To be called after a selection change made by the view (Ctrl+click, rectangle).</summary>
    public void OnSelectionChanged()
    {
        foreach (BtAttachmentViewModel attachment in Nodes.SelectMany(n => n.Attachments))
        {
            attachment.IsSelected = false;
        }

        List<BtNodeViewModel> selected = [.. SelectedNodes.Take(2)];
        SelectedItem = selected.Count == 1 ? selected[0] : null;
    }

    /// <summary>Selects a node or an attached piece and asks the view to show it.</summary>
    public void Show(Guid id)
    {
        if (_nodes.TryGetValue(id, out BtNodeViewModel? node))
        {
            SelectOnly(node);
            FocusRequested?.Invoke(this, id);
        }
        else if (Nodes.SelectMany(n => n.Attachments).FirstOrDefault(a => a.Id == id) is { } attachment)
        {
            Select(attachment);
            FocusRequested?.Invoke(this, attachment.Owner.Id);
        }
    }

    public override void Navigate(OutlineItem item)
    {
        if (item.NodeId is { } id)
        {
            Show(id);
        }
    }

    /// <summary>Changes to the tree are saved with the project (Ctrl+S).</summary>
    public override bool Save() => true;

    public void Dispose()
    {
        _asset.Changed -= OnAssetChanged;
        _context.Project.Assets.Changed -= OnRegistryChanged;
        foreach (BtLinkViewModel link in Links)
        {
            link.Dispose();
        }
    }

    // ----- Interne -----

    private void Apply(BehaviorTree tree, string description, bool merge = false)
    {
        UndoHistory history = _context.History;
        if (!merge)
        {
            history.Seal();
            _lastValueEdit = null;
            _lastKeyEdit = null;
        }

        history.Execute(new PropertyChangeCommand<BehaviorTree>(
            description, _asset, nameof(BehaviorTreeAsset.Tree), () => _asset.Tree, v => _asset.Tree = v, tree));
        if (!merge)
        {
            history.Seal();
        }
    }

    private void ApplyKeys(IReadOnlyList<BlackboardKey> keys, string description, bool merge = false)
    {
        UndoHistory history = _context.History;
        if (!merge)
        {
            history.Seal();
            _lastValueEdit = null;
        }

        history.Execute(new PropertyChangeCommand<IReadOnlyList<BlackboardKey>>(
            description, _asset, nameof(BehaviorTreeAsset.Keys), () => _asset.Keys, v => _asset.Keys = v, keys));
        if (!merge)
        {
            history.Seal();
        }
    }

    private void OnAssetChanged(object? sender, AssetChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BehaviorTreeAsset.Tree):
                Sync();
                break;
            case nameof(BehaviorTreeAsset.Keys):
                SyncKeys();
                Sync();
                break;
            case nameof(BehaviorTreeAsset.IsPublicGoal):
                OnPropertyChanged(nameof(IsPublicGoal));
                break;
            case nameof(Asset.DisplayName) or nameof(Asset.ResourceId):
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DetailsTitle));
                break;
        }
    }

    /// <summary>A mob changed tree, a sound was renamed: the "Used by" list and the summaries follow.</summary>
    private void OnRegistryChanged(object? sender, AssetRegistryChangedEventArgs e)
    {
        if (ReferenceEquals(e.Asset, _asset) || _refreshPending)
        {
            return;
        }

        _refreshPending = true;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _refreshPending = false;
            OnPropertyChanged(nameof(UsedBy));
            OnPropertyChanged(nameof(UsedByText));
            OnPropertyChanged(nameof(PublicGoals));
            Sync();
            foreach (BtNodeViewModel node in Nodes)
            {
                node.Refresh();
            }
        });
    }

    private void SyncKeys()
    {
        Dictionary<Guid, BlackboardKeyViewModel> existing = Keys.ToDictionary(k => k.Id);
        Keys.Clear();
        foreach (BlackboardKey key in _asset.Keys)
        {
            if (existing.TryGetValue(key.Id, out BlackboardKeyViewModel? kept))
            {
                kept.Update(key);
                Keys.Add(kept);
            }
            else
            {
                Keys.Add(new BlackboardKeyViewModel(this, key));
            }
        }
    }

    /// <summary>Brings the ViewModels in line with the tree of the asset, keeping those of unchanged nodes.</summary>
    private void Sync()
    {
        BehaviorTree tree = _asset.Tree;
        Guid? selectedId = _selectedItem?.Id;
        var ids = tree.Nodes.Select(n => n.Id).ToHashSet();
        foreach (BtNodeViewModel stale in Nodes.Where(n => !ids.Contains(n.Id)).ToList())
        {
            Nodes.Remove(stale);
            _nodes.Remove(stale.Id);
        }

        foreach (BtNode node in tree.Nodes)
        {
            if (_nodes.TryGetValue(node.Id, out BtNodeViewModel? existing))
            {
                existing.Update(node);
            }
            else
            {
                var created = new BtNodeViewModel(this, node);
                Nodes.Add(created);
                _nodes[node.Id] = created;
            }
        }

        foreach (BtLinkViewModel old in Links)
        {
            old.Dispose();
        }

        Links.Clear();
        foreach (BtLink link in tree.Links)
        {
            if (_nodes.TryGetValue(link.Parent, out BtNodeViewModel? parent) && _nodes.TryGetValue(link.Child, out BtNodeViewModel? child))
            {
                Links.Add(new BtLinkViewModel(parent, child));
            }
        }

        IReadOnlyList<BtNode> order = tree.ExecutionOrder();
        var reachable = order.Select(n => n.Id).ToHashSet();
        Dictionary<Guid, int> rank = order.Select((n, i) => (n.Id, i)).ToDictionary(p => p.Id, p => p.i);
        foreach (BtNodeViewModel node in Nodes)
        {
            node.Order = rank.GetValueOrDefault(node.Id);
            node.IsReachable = reachable.Contains(node.Id);
        }

        IReadOnlyList<BtIssue> issues = BehaviorTreeValidator.Validate(_asset, id => Assets.Find(id) as BehaviorTreeAsset);
        ILookup<Guid?, BtIssue> byItem = issues.ToLookup(i => i.ItemId);
        foreach (BtNodeViewModel node in Nodes)
        {
            node.SetIssues([.. byItem[node.Id]]);
            foreach (BtAttachmentViewModel attachment in node.Attachments)
            {
                attachment.SetIssues([.. byItem[attachment.Id]]);
            }
        }

        foreach (BlackboardKeyViewModel key in Keys)
        {
            string id = key.Id.ToString("D");
            key.Usages = tree.Nodes.Sum(n => Count(n.Info, n.GetValue) + n.Attachments.Sum(a => Count(a.Info, a.GetValue)));

            int Count(BtNodeInfo info, Func<string, string> value) =>
                info.Parameters.Count(p => p.Type == BtParameterType.Key && value(p.Name) == id);
        }

        Issues = [.. issues.Where(i => i.Severity != DiagnosticSeverity.Info)
            .Select(i => new GraphIssueItem(i.Message, i.ItemId, i.Severity == DiagnosticSeverity.Error))];
        Outline = [.. order.Skip(1).Select((n, i) => new OutlineItem($"{i + 1}. {n.Info.Title}", OutlineKind.Event, 0, 0, n.Id, BtCatalog.CategoryLabel(n.Info.Category).ToLowerInvariant()))];

        // The selection survives the update when the element still exists.
        BtItemViewModel? reselected = selectedId is { } sid
            ? _nodes.GetValueOrDefault(sid) ?? (BtItemViewModel?)Nodes.SelectMany(n => n.Attachments).FirstOrDefault(a => a.Id == sid)
            : null;
        if (!ReferenceEquals(reselected, _selectedItem))
        {
            SelectedItem = reselected;
            if (reselected is BtAttachmentViewModel attachment)
            {
                attachment.IsSelected = true;
            }
        }
        else
        {
            foreach (BtParameterViewModel parameter in _parameters)
            {
                parameter.Refresh();
            }
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TaskCount));
    }

    private void RebuildParameters() =>
        Parameters = _selectedItem is null
            ? []
            : [.. _selectedItem.Info.Parameters.Select(p => new BtParameterViewModel(_selectedItem, p, _context))];

    private static double Snap(double value, double gridSize) =>
        gridSize > 0 ? Math.Round(value / gridSize) * gridSize : Math.Round(value);

    /// <summary>Lowercase without accents, for a tolerant search.</summary>
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
