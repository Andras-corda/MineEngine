using System.Collections.Immutable;

namespace MineEngine.Assets.Puppeteer;

/// <summary>Blackboard key: a named value that nodes read and write.</summary>
public sealed record BlackboardKey(Guid Id, string Name, BlackboardKeyType Type)
{
    public const int MaxNameLength = 48;
}

/// <summary>
/// Decorator or service attached to a node, with its settings (raw text, like the values
/// of graph nodes). Immutable.
/// </summary>
public sealed record BtAttachment(Guid Id, BtNodeKind Kind, ImmutableDictionary<string, string> Values)
{
    public static BtAttachment Create(BtNodeKind kind) => new(Guid.NewGuid(), kind, DefaultValues(kind));

    public BtNodeInfo Info => BtCatalog.Get(Kind);

    /// <summary>Entered value, otherwise the default value of the catalog.</summary>
    public string GetValue(string name) =>
        Values.TryGetValue(name, out string? value) ? value : BtCatalog.Find(Kind)?.FindParameter(name)?.DefaultValue ?? string.Empty;

    public BtAttachment WithValue(string name, string value) => this with { Values = Values.SetItem(name, value ?? string.Empty) };

    internal static ImmutableDictionary<string, string> DefaultValues(BtNodeKind kind) =>
        BtCatalog.Get(kind).Parameters.ToImmutableDictionary(p => p.Name, p => p.DefaultValue);

    public override string ToString() => BtCatalog.Find(Kind)?.Title ?? Kind.ToString();
}

/// <summary>
/// A node of the tree: its kind, its position on the canvas, its settings, and the
/// decorators (above, in the order they are checked) and services attached to it.
/// </summary>
public sealed record BtNode(Guid Id, BtNodeKind Kind, double X, double Y, ImmutableDictionary<string, string> Values)
{
    public ImmutableList<BtAttachment> Decorators { get; init; } = [];

    public ImmutableList<BtAttachment> Services { get; init; } = [];

    public static BtNode Create(BtNodeKind kind, double x, double y) =>
        new(Guid.NewGuid(), kind, x, y, BtAttachment.DefaultValues(kind));

    public BtNodeInfo Info => BtCatalog.Get(Kind);

    public IEnumerable<BtAttachment> Attachments => Decorators.Concat(Services);

    public string GetValue(string name) =>
        Values.TryGetValue(name, out string? value) ? value : BtCatalog.Find(Kind)?.FindParameter(name)?.DefaultValue ?? string.Empty;

    public BtNode WithValue(string name, string value) => this with { Values = Values.SetItem(name, value ?? string.Empty) };

    public BtNode MoveTo(double x, double y) => this with { X = x, Y = y };

    public override string ToString() => BtCatalog.Find(Kind)?.Title ?? Kind.ToString();
}

/// <summary>Link from a parent (root or composite) to one of its children.</summary>
public sealed record BtLink(Guid Parent, Guid Child);

/// <summary>
/// Content of a behavior tree: its nodes and the parent → child links. Immutable, like
/// node graphs: every change produces a new tree (undo keeps the old one).
/// garde l'ancien).
/// </summary>
/// <remarks>
/// As in Unreal, the order of the children is their position on the canvas, from left
/// to right: moving a node changes its priority. There is always a root (created if
/// needed) with a single child; every node has a single parent; a link cannot close
/// a loop.
/// </remarks>
public sealed class BehaviorTree
{
    public BehaviorTree(IEnumerable<BtNode> nodes, IEnumerable<BtLink> links)
    {
        List<BtNode> list = [.. nodes.Where(n => BtCatalog.Find(n.Kind) is { IsAttachment: false }).DistinctBy(n => n.Id)];
        // A single root: the first one is kept, a missing one is created again.
        BtNode? root = list.FirstOrDefault(n => n.Kind == BtNodeKind.Root);
        list.RemoveAll(n => n.Kind == BtNodeKind.Root && !ReferenceEquals(n, root));
        if (root is null)
        {
            root = BtNode.Create(BtNodeKind.Root, 0, 0);
            list.Insert(0, root);
        }

        Nodes = [.. list];
        Root = root;
        Dictionary<Guid, BtNode> byId = Nodes.ToDictionary(n => n.Id);

        // The last link to a child wins; the root keeps only one; no loop.
        var parents = new Dictionary<Guid, Guid>();
        foreach (BtLink link in links)
        {
            if (byId.TryGetValue(link.Parent, out BtNode? parent) && parent.Info.HasChildren
                && byId.TryGetValue(link.Child, out BtNode? child) && child.Kind != BtNodeKind.Root && link.Parent != link.Child)
            {
                parents[link.Child] = link.Parent;
            }
        }

        foreach (Guid extra in parents.Where(p => p.Value == root.Id).Select(p => p.Key).SkipLast(1).ToList())
        {
            parents.Remove(extra);
        }

        foreach (Guid child in parents.Keys.ToList())
        {
            if (IsInCycle(child, parents))
            {
                parents.Remove(child);
            }
        }

        Links = [.. parents.Select(p => new BtLink(p.Value, p.Key))];
    }

    /// <summary>Tree that only has its root.</summary>
    public static BehaviorTree Empty => new([], []);

    public ImmutableArray<BtNode> Nodes { get; }

    public ImmutableArray<BtLink> Links { get; }

    public BtNode Root { get; }

    public BtNode? FindNode(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);

    public BtNode? ParentOf(Guid child) =>
        Links.FirstOrDefault(l => l.Child == child) is { } link ? FindNode(link.Parent) : null;

    /// <summary>Children of a node, in execution order: from left to right, then from top to bottom.</summary>
    public IReadOnlyList<BtNode> ChildrenOf(Guid parent) =>
        [.. Links.Where(l => l.Parent == parent).Select(l => FindNode(l.Child)).OfType<BtNode>().OrderBy(n => n.X).ThenBy(n => n.Y)];

    /// <summary>Nodes reached from the root, in the order the tree visits them (root included).</summary>
    public IReadOnlyList<BtNode> ExecutionOrder()
    {
        var order = new List<BtNode>();
        Visit(Root);
        return order;

        void Visit(BtNode node)
        {
            order.Add(node);
            foreach (BtNode child in ChildrenOf(node.Id))
            {
                Visit(child);
            }
        }
    }

    /// <summary>The decorator or service with this identifier, and the node that carries it.</summary>
    public (BtNode Node, BtAttachment Attachment)? FindAttachment(Guid id)
    {
        foreach (BtNode node in Nodes)
        {
            if (node.Attachments.FirstOrDefault(a => a.Id == id) is { } attachment)
            {
                return (node, attachment);
            }
        }

        return null;
    }

    // ----- Modifications -----

    public BehaviorTree AddNode(BtNode node) => new(Nodes.Add(node), Links);

    public BehaviorTree ReplaceNode(BtNode node) => new(Nodes.Select(n => n.Id == node.Id ? node : n), Links);

    /// <summary>Removes nodes (never the root) and their links; their children stay, detached.</summary>
    public BehaviorTree RemoveNodes(IEnumerable<Guid> ids)
    {
        var removed = ids.Where(id => id != Root.Id).ToHashSet();
        return new BehaviorTree(
            Nodes.Where(n => !removed.Contains(n.Id)),
            Links.Where(l => !removed.Contains(l.Parent) && !removed.Contains(l.Child)));
    }

    public BehaviorTree MoveNodes(IReadOnlyDictionary<Guid, (double X, double Y)> positions) =>
        new(Nodes.Select(n => positions.TryGetValue(n.Id, out var p) ? n.MoveTo(p.X, p.Y) : n), Links);

    /// <summary>Adds nodes and links (duplication, paste).</summary>
    public BehaviorTree Merge(IEnumerable<BtNode> nodes, IEnumerable<BtLink> links) =>
        new(Nodes.AddRange(nodes), Links.AddRange(links));

    /// <summary>Reason why <paramref name="parent"/> cannot receive this child, or null.</summary>
    public string? DescribeLinkProblem(Guid parent, Guid child)
    {
        BtNode? from = FindNode(parent);
        BtNode? to = FindNode(child);
        if (from is null || to is null)
        {
            return Tr.T("Node not found.");
        }

        if (parent == child)
        {
            return Tr.T("A node cannot be connected to itself.");
        }

        if (!from.Info.HasChildren)
        {
            return Tr.F("\"{0}\" is a task: only the root and composites (selector, sequence...) have children.", from.Info.Title);
        }

        if (to.Kind == BtNodeKind.Root)
        {
            return Tr.T("The root cannot have a parent.");
        }

        for (BtNode? ancestor = from; ancestor is not null; ancestor = ParentOf(ancestor.Id))
        {
            if (ancestor.Id == child)
            {
                return Tr.T("This link would create a loop: the node is already above.");
            }
        }

        return null;
    }

    /// <summary>
    /// Links a parent to a child: the former parent of the child loses it, and the former
    /// child of the root is detached when the parent is the root. Invalid link: exception
    /// (check first with <see cref="DescribeLinkProblem"/>).
    /// </summary>
    public BehaviorTree Connect(Guid parent, Guid child)
    {
        if (DescribeLinkProblem(parent, child) is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        IEnumerable<BtLink> kept = Links.Where(l => l.Child != child && !(parent == Root.Id && l.Parent == Root.Id));
        return new BehaviorTree(Nodes, kept.Append(new BtLink(parent, child)));
    }

    /// <summary>Detaches a node from its parent.</summary>
    public BehaviorTree DisconnectParent(Guid child) => new(Nodes, Links.Where(l => l.Child != child));

    /// <summary>Detaches all the children of a node.</summary>
    public BehaviorTree DisconnectChildren(Guid parent) => new(Nodes, Links.Where(l => l.Parent != parent));

    /// <summary>Horizontal gap between two columns laid out by <see cref="Arranged"/>.</summary>
    public const double LayoutColumn = 250;

    /// <summary>Vertical gap between two levels laid out by <see cref="Arranged"/>.</summary>
    public const double LayoutRow = 200;

    /// <summary>
    /// Tree laid out neatly: every leaf in its own column, every parent centered above its
    /// children, one level per depth (higher when pieces are attached above the nodes).
    /// The execution order does not change. Nodes not connected are placed on the right.
    /// </summary>
    public BehaviorTree Arranged()
    {
        var positions = new Dictionary<Guid, (double X, double Y)>();
        double column = 0;
        Place(Root, 0);
        double stray = 0;
        foreach (BtNode node in Nodes.Where(n => !positions.ContainsKey(n.Id)))
        {
            positions[node.Id] = ((column + 1) * LayoutColumn, stray);
            stray += LayoutRow / 2 + 40 * node.Decorators.Count;
        }

        return MoveNodes(positions);

        double Place(BtNode node, double y)
        {
            IReadOnlyList<BtNode> children = ChildrenOf(node.Id);
            double x;
            if (children.Count == 0)
            {
                x = column++ * LayoutColumn;
            }
            else
            {
                // Children go down by the height of the tallest stack of decorators of their level.
                double childY = y + LayoutRow + node.Services.Count * 40 + children.Max(c => c.Decorators.Count) * 40;
                List<double> childX = [.. children.Select(c => Place(c, childY))];
                x = (childX[0] + childX[^1]) / 2;
            }

            positions[node.Id] = (x, y);
            return x;
        }
    }

    /// <summary>Attaches a decorator or a service to a node (depending on the category of its kind).</summary>
    public BehaviorTree AddAttachment(Guid node, BtAttachment attachment) => Update(node, n => attachment.Info.Category == BtCategory.Service
        ? n with { Services = n.Services.Add(attachment) }
        : n with { Decorators = n.Decorators.Add(attachment) });

    public BehaviorTree ReplaceAttachment(BtAttachment attachment) =>
        FindAttachment(attachment.Id) is { } found
            ? Update(found.Node.Id, n => n with
            {
                Decorators = n.Decorators.Select(a => a.Id == attachment.Id ? attachment : a).ToImmutableList(),
                Services = n.Services.Select(a => a.Id == attachment.Id ? attachment : a).ToImmutableList(),
            })
            : this;

    public BehaviorTree RemoveAttachment(Guid id) =>
        FindAttachment(id) is { } found
            ? Update(found.Node.Id, n => n with
            {
                Decorators = n.Decorators.RemoveAll(a => a.Id == id),
                Services = n.Services.RemoveAll(a => a.Id == id),
            })
            : this;

    /// <summary>Moves a decorator or a service up (-1) or down (+1) among those of its node.</summary>
    public BehaviorTree MoveAttachment(Guid id, int offset)
    {
        if (FindAttachment(id) is not { } found)
        {
            return this;
        }

        return Update(found.Node.Id, n => n with { Decorators = Shift(n.Decorators), Services = Shift(n.Services) });

        ImmutableList<BtAttachment> Shift(ImmutableList<BtAttachment> list)
        {
            int index = list.FindIndex(a => a.Id == id);
            int target = index + offset;
            if (index < 0 || target < 0 || target >= list.Count)
            {
                return list;
            }

            return list.RemoveAt(index).Insert(target, list[index]);
        }
    }

    private BehaviorTree Update(Guid node, Func<BtNode, BtNode> change) =>
        new(Nodes.Select(n => n.Id == node ? change(n) : n), Links);

    private static bool IsInCycle(Guid start, Dictionary<Guid, Guid> parents)
    {
        var seen = new HashSet<Guid>();
        for (Guid current = start; parents.TryGetValue(current, out Guid parent); current = parent)
        {
            if (parent == start)
            {
                return true;
            }

            if (!seen.Add(parent))
            {
                // Loop higher up, without this node: it will be broken while handling one of its members.
                return false;
            }
        }

        return false;
    }
}
