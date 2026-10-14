using MineEngine.Assets.Graphs;

namespace MineEngine.Tests;

/// <summary>Construction compacte d'un graphe dans les tests.</summary>
internal sealed class GraphBuilder
{
    private NodeGraph _graph = NodeGraph.Empty;

    public GraphNode Add(GraphNodeKind kind, double x, double y, params (string Pin, string Value)[] values)
    {
        GraphNode node = GraphNode.Create(kind, x, y);
        foreach ((string pin, string value) in values)
        {
            node = node.WithValue(pin, value);
        }

        _graph = _graph.AddNode(node);
        return node;
    }

    public GraphBuilder Exec(GraphNode from, GraphNode to, string pin = NodeInfo.ExecThen)
    {
        _graph = _graph.Connect(new GraphConnection(from.Id, pin, to.Id, NodeInfo.ExecInput));
        return this;
    }

    public GraphBuilder Data(GraphNode from, string fromPin, GraphNode to, string toPin)
    {
        _graph = _graph.Connect(new GraphConnection(from.Id, fromPin, to.Id, toPin));
        return this;
    }

    public NodeGraph Build() => _graph;
}
