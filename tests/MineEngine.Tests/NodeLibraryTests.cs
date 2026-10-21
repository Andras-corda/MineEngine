using MineEngine.Assets.Graphs;

namespace MineEngine.Tests;

public sealed class NodeLibraryTests
{
    [Fact]
    public void Every_catalog_node_has_a_domain_of_the_nodes_window()
    {
        Assert.All(NodeCatalog.All.Where(n => n.Category == NodeCategory.Event), n => Assert.Equal(NodeGroup.Events, NodeCatalog.GroupOf(n)));
        Assert.Equal(NodeGroup.Block, NodeCatalog.GroupOf(NodeCatalog.Get(GraphNodeKind.SetBlock)));
        Assert.Equal(NodeGroup.Item, NodeCatalog.GroupOf(NodeCatalog.Get(GraphNodeKind.CanBeSmelted)));
        Assert.Equal(NodeGroup.Player, NodeCatalog.GroupOf(NodeCatalog.Get(GraphNodeKind.MainHandItem)));

        // Only the nodes of the program (calls, objects, variables) are left in Program.
        Assert.DoesNotContain(NodeCatalog.All, n => NodeCatalog.GroupOf(n) == NodeGroup.Program && n.Kind < GraphNodeKind.RemoveBlock
                                                    && n.Category is NodeCategory.Action or NodeCategory.Condition);
    }

    [Fact]
    public void The_node_library_sample_uses_every_block_and_item_node()
    {
        NodeGraph graph = NodeLibrarySample.BuildGraph();
        HashSet<GraphNodeKind> used = [.. graph.Nodes.Select(n => n.Kind)];

        Assert.All(NodeLibrarySample.Nodes, n => Assert.Contains(n.Kind, used));
        Assert.All(NodeLibrarySample.Events, n => Assert.Contains(n.Kind, used));
        Assert.True(NodeLibrarySample.Nodes.Count >= 100);
        Assert.Equal(5, NodeLibrarySample.Events.Count);
    }
}
