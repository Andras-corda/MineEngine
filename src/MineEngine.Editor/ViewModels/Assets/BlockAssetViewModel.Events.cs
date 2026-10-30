using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Events and triggers tab of the block (V0.7.11): each event of the block, connected or not to a node
/// of a graph. A node names the block by its identifier, like the events of the mobs.
/// </summary>
public sealed partial class BlockAssetViewModel
{
    private IReadOnlyList<BlockEventViewModel>? _events;

    /// <summary>The events of the block (right-clicked, placed, redstone...), none used by default.</summary>
    public IReadOnlyList<BlockEventViewModel> Events
    {
        get
        {
            if (_events is null)
            {
                _events = [.. Enum.GetValues<BlockEventKind>().Select(k => new BlockEventViewModel(this, k))];
                RefreshBlockEvents();
            }

            return _events;
        }
    }

    /// <summary>
    /// Connects an event: its node is added to the events graph of the block (created when needed), in a
    /// single step that can be undone, then the graph opens on that node.
    /// </summary>
    public void AddBlockEvent(BlockEventKind kind)
    {
        string blockId = _block.ResourceId.Value;
        GraphAsset? graph = Context.Project.Assets.OfType<GraphAsset>()
            .FirstOrDefault(g => g.Kind == GraphKind.Events && g.Graph.Nodes.Any(n => IsOwnEvent(n, blockId)));
        int existing = graph?.Graph.Nodes.Count(n => NodeCatalog.Find(n.Kind)?.Category == NodeCategory.Event) ?? 0;
        NodeInfo info = NodeCatalog.ForBlockEvent(kind);
        GraphNode node = GraphNode.Create(info.Kind, 0, existing * 220).WithValue("block", blockId);

        if (graph is null)
        {
            ResourceId id = Context.Project.Assets.CreateUniqueResourceId(blockId + "_events", AssetType.Graph);
            graph = new GraphAsset(Guid.NewGuid(), id, Tr.F("{0} events", Header)) { Folder = _block.Folder };
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

    internal void OpenBlockEvent(GraphAsset graph, GraphNode node) => Context.Navigation.Open(graph, node.Id);

    /// <summary>The node names this block ("rune_door" or "mymod:rune_door").</summary>
    private static bool IsOwnEvent(GraphNode node, string blockId)
    {
        if (NodeCatalog.Find(node.Kind)?.BlockEvent is null)
        {
            return false;
        }

        string value = (node.GetValue("block") ?? string.Empty).Trim();
        return value[(value.IndexOf(':', StringComparison.Ordinal) + 1)..] == blockId;
    }

    private void RefreshBlockEvents()
    {
        if (_events is null)
        {
            return;
        }

        string blockId = _block.ResourceId.Value;
        List<(GraphAsset Graph, GraphNode Node)> links = [.. Context.Project.Assets.OfType<GraphAsset>()
            .SelectMany(g => g.Graph.Nodes.Where(n => IsOwnEvent(n, blockId)).Select(n => (g, n)))];
        foreach (BlockEventViewModel row in _events)
        {
            row.SetLinks([.. links.Where(l => NodeCatalog.Find(l.Node.Kind)?.BlockEvent == row.Kind)]);
        }
    }
}

/// <summary>An event of the block in its Events and triggers tab.</summary>
public sealed class BlockEventViewModel : EventLinkViewModel
{
    public BlockEventViewModel(BlockAssetViewModel owner, BlockEventKind kind)
        : base(NodeCatalog.ForBlockEvent(kind), Tr.T("Block: "), Tr.T("Add this event to the block's event graph"), () => owner.AddBlockEvent(kind), owner.OpenBlockEvent)
    {
        Kind = kind;
    }

    public BlockEventKind Kind { get; }
}
