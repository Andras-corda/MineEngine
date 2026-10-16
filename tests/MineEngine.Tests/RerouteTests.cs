using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Lowering;
using MineEngine.IR.Model;

namespace MineEngine.Tests;

/// <summary>V0.5.4 : redirections (« reroute ») sur les fils, sans effet sur le programme.</summary>
public sealed class RerouteTests
{
    /// <summary>
    /// Connexion d'un joueur → message ; le fil d'exécution passe par deux redirections,
    /// le fil du joueur par une.
    /// </summary>
    private static (NodeGraph Graph, GraphNode Join, GraphNode Message, GraphNode First, GraphNode Second, GraphNode Value) Sample()
    {
        var builder = new GraphBuilder();
        GraphNode join = builder.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode message = builder.Add(GraphNodeKind.SendMessage, 600, 0);
        GraphNode first = builder.Add(Reroutes.Create(DataType.Exec, 200, 40));
        GraphNode second = builder.Add(Reroutes.Create(DataType.Exec, 400, 40));
        GraphNode value = builder.Add(Reroutes.Create(DataType.Player, 300, 120));
        builder.Exec(join, first).Exec(first, second).Exec(second, message)
            .Data(join, "player", value, Reroutes.ValueInput)
            .Data(value, Reroutes.ValueOutput, message, "player");
        return (builder.Build(), join, message, first, second, value);
    }

    [Fact]
    public void Reroutes_take_the_type_of_their_wire()
    {
        GraphNode exec = Reroutes.Create(DataType.Exec, 0, 0);
        GraphNode number = Reroutes.Create(DataType.Number, 0, 0);

        Assert.Equal(DataType.Exec, Reroutes.TypeOf(exec));
        Assert.Equal(DataType.Number, Reroutes.TypeOf(number));

        NodeInfo execInfo = CatalogGraphContext.Instance.Describe(exec);
        Assert.True(execInfo.HasExecInput);
        Assert.Single(execInfo.ExecOutputs);
        Assert.Empty(execInfo.Inputs);

        NodeInfo numberInfo = CatalogGraphContext.Instance.Describe(number);
        Assert.True(numberInfo.IsPure);
        Assert.Equal(DataType.Number, Assert.Single(numberInfo.Inputs).Type);
        Assert.Equal(DataType.Number, Assert.Single(numberInfo.Outputs).Type);
    }

    [Fact]
    public void A_reroute_only_accepts_wires_of_its_type()
    {
        var builder = new GraphBuilder();
        GraphNode join = builder.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode number = builder.Add(Reroutes.Create(DataType.Number, 200, 0));
        NodeGraph graph = builder.Build();

        Assert.NotNull(graph.DescribeConnectionProblem(new GraphConnection(join.Id, "player", number.Id, Reroutes.ValueInput)));
        Assert.NotNull(graph.DescribeConnectionProblem(new GraphConnection(join.Id, NodeInfo.ExecThen, number.Id, NodeInfo.ExecInput)));
    }

    [Fact]
    public void Collapse_links_the_real_ends_directly()
    {
        (NodeGraph graph, GraphNode join, GraphNode message, _, _, _) = Sample();

        NodeGraph collapsed = Reroutes.Collapse(graph);

        Assert.Equal(2, collapsed.Nodes.Length);
        Assert.Contains(new GraphConnection(join.Id, NodeInfo.ExecThen, message.Id, NodeInfo.ExecInput), collapsed.Connections);
        Assert.Contains(new GraphConnection(join.Id, "player", message.Id, "player"), collapsed.Connections);
        Assert.Equal(2, collapsed.Connections.Length);
    }

    [Fact]
    public void Removing_one_reroute_keeps_the_wire()
    {
        (NodeGraph graph, GraphNode join, GraphNode message, GraphNode first, GraphNode second, _) = Sample();

        NodeGraph bypassed = Reroutes.Bypass(graph, [first.Id]);

        Assert.Null(bypassed.FindNode(first.Id));
        Assert.Contains(new GraphConnection(join.Id, NodeInfo.ExecThen, second.Id, NodeInfo.ExecInput), bypassed.Connections);
        Assert.Contains(new GraphConnection(second.Id, NodeInfo.ExecThen, message.Id, NodeInfo.ExecInput), bypassed.Connections);
    }

    [Fact]
    public void Reroutes_change_neither_validation_nor_compilation()
    {
        (NodeGraph graph, GraphNode join, GraphNode message, _, _, _) = Sample();
        Assert.DoesNotContain(GraphValidator.Validate(graph), i => i.Severity != DiagnosticSeverity.Info);

        var asset = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("welcome"), "Bienvenue") { Graph = graph };
        IREventHandler handler = Assert.Single(new GraphCompiler(asset, new ProgramModel([asset]), (r, _) => r.Id).CompileEvents());

        Assert.Equal(join.Id, handler.NodeId);
        IRActionStatement action = Assert.IsType<IRActionStatement>(Assert.Single(handler.Body));
        Assert.Equal(message.Id, action.NodeId);
        Assert.Equal(new IREventValue(DataType.Player, "player"), action.Arguments[0]);
    }

    [Fact]
    public void A_dangling_reroute_leaves_the_input_unconnected()
    {
        var builder = new GraphBuilder();
        GraphNode join = builder.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode message = builder.Add(GraphNodeKind.SendMessage, 400, 0);
        GraphNode value = builder.Add(Reroutes.Create(DataType.Player, 200, 100));
        builder.Exec(join, message).Data(value, Reroutes.ValueOutput, message, "player");

        Assert.Contains(GraphValidator.Validate(builder.Build()), i => i.Severity == DiagnosticSeverity.Error && i.NodeId == message.Id);
    }

    [Fact]
    public void Reroutes_are_saved_with_their_type()
    {
        (NodeGraph graph, _, _, _, _, GraphNode value) = Sample();
        var serializer = new AssetSerializer(AssetCatalog.CreateDefault());
        var asset = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("welcome"), "Bienvenue") { Graph = graph };

        var read = (GraphAsset)serializer.Deserialize(serializer.Serialize(asset), "welcome.asset.json");

        Assert.Equal(5, read.Graph.Nodes.Length);
        Assert.Equal(DataType.Player, Reroutes.TypeOf(read.Graph.FindNode(value.Id)!));
        Assert.Equal(graph.Connections.Length, read.Graph.Connections.Length);
    }
}
