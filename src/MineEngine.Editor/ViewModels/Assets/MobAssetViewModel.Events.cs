using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Events and triggers tab of the mob (V0.7.9, step 2): each event of the mob, connected or not to
/// a node of a graph. A node names the mob by its identifier, like the systems of the Sparkle nodes.
/// </summary>
public sealed partial class MobAssetViewModel
{
    /// <summary>The events of the mob (struck by lightning, hurt, tamed...), none used by default.</summary>
    public IReadOnlyList<MobEventViewModel> Events { get; private set; } = [];

    /// <summary>
    /// Connects an event: its node is added to the events graph of the mob (created when needed), in
    /// a single step that can be undone, then the graph opens on that node.
    /// </summary>
    public void AddMobEvent(MobEventKind kind)
    {
        string mobId = _mob.ResourceId.Value;
        GraphAsset? graph = Context.Project.Assets.OfType<GraphAsset>()
            .FirstOrDefault(g => g.Kind == GraphKind.Events && g.Graph.Nodes.Any(n => IsOwnEvent(n, mobId)));
        int existing = graph?.Graph.Nodes.Count(n => NodeCatalog.Find(n.Kind)?.Category == NodeCategory.Event) ?? 0;
        NodeInfo info = NodeCatalog.ForMobEvent(kind);
        GraphNode node = GraphNode.Create(info.Kind, 0, existing * 220).WithValue("mob", mobId);

        if (graph is null)
        {
            ResourceId id = Context.Project.Assets.CreateUniqueResourceId(mobId + "_events", AssetType.Graph);
            graph = new GraphAsset(Guid.NewGuid(), id, Tr.F("{0} events", Header)) { Folder = _mob.Folder };
            graph.Graph = graph.Graph.AddNode(node);
            Context.History.Execute(new AddAssetCommand(Context.Project.Assets, graph, Tr.T("Graph")));
        }
        else
        {
            GraphAsset target = graph;
            Context.History.Execute(new PropertyChangeCommand<NodeGraph>(
                Tr.F("Connect \"{0}\"", info.Title), target, nameof(GraphAsset.Graph), () => target.Graph, v => target.Graph = v, target.Graph.AddNode(node)));
        }

        Context.History.Seal();
        Context.Navigation.Open(graph, node.Id);
    }

    private void InitializeEvents()
    {
        Events = [.. Enum.GetValues<MobEventKind>().Select(k => new MobEventViewModel(this, k))];
        RefreshMobEvents();
    }

    internal void OpenMobEvent(GraphAsset graph, GraphNode node) => Context.Navigation.Open(graph, node.Id);

    /// <summary>The node names this mob ("storm_wolf" or "mymod:storm_wolf").</summary>
    private static bool IsOwnEvent(GraphNode node, string mobId)
    {
        if (NodeCatalog.Find(node.Kind)?.MobEvent is null)
        {
            return false;
        }

        string value = (node.GetValue("mob") ?? string.Empty).Trim();
        return value[(value.IndexOf(':', StringComparison.Ordinal) + 1)..] == mobId;
    }

    private void RefreshMobEvents()
    {
        string mobId = _mob.ResourceId.Value;
        List<(GraphAsset Graph, GraphNode Node)> links = [.. Context.Project.Assets.OfType<GraphAsset>()
            .SelectMany(g => g.Graph.Nodes.Where(n => IsOwnEvent(n, mobId)).Select(n => (g, n)))];
        foreach (MobEventViewModel row in Events)
        {
            row.SetLinks([.. links.Where(l => NodeCatalog.Find(l.Node.Kind)?.MobEvent == row.Kind)]);
        }
    }
}

/// <summary>An event of the mob in its Events and triggers tab.</summary>
public sealed class MobEventViewModel : EventLinkViewModel
{
    public MobEventViewModel(MobAssetViewModel owner, MobEventKind kind)
        : base(NodeCatalog.ForMobEvent(kind), Tr.T("Mob: "), Tr.T("Add this event to the mob's event graph"), () => owner.AddMobEvent(kind), owner.OpenMobEvent)
    {
        Kind = kind;
    }

    public MobEventKind Kind { get; }
}
