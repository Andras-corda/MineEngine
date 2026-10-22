using System.Collections.Immutable;
using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Puppeteer;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Switch, Sequence, Flip Flop, fanned-out reroutes, and the comments of Puppeteer.</summary>
public sealed class FlowNodeTests
{
    [Fact]
    public void Switch_and_sequence_get_and_lose_pins()
    {
        GraphNode sequence = GraphNode.Create(GraphNodeKind.Sequence, 0, 0);
        GraphNode numbers = GraphNode.Create(GraphNodeKind.SwitchOnNumber, 0, 0);

        Assert.Equal(3, FlowNodes.Describe(FlowNodes.AddPin(sequence)).ExecOutputs.Count);
        Assert.Equal(["0", "1", "2"], FlowNodes.Cases(FlowNodes.AddPin(numbers)));
        Assert.Equal(["0", "1", "Default"], FlowNodes.Describe(numbers).ExecOutputs.Select(p => p.Label));
        Assert.Equal(FlowNodes.CasePin(1), FlowNodes.RemovePin(numbers)!.Value.Pin);
        Assert.Null(FlowNodes.RemovePin(FlowNodes.RemovePin(sequence)!.Value.Node));

        // A number Switch keeps whole numbers only.
        Assert.Equal(["4"], FlowNodes.Cases(numbers.WithValue(FlowNodes.CasesKey, "four\n4\n4")));
    }

    [Fact]
    public void An_execution_reroute_can_fan_out_and_runs_like_a_sequence()
    {
        var builder = new GraphBuilder();
        GraphNode join = builder.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode reroute = builder.Add(Reroutes.Create(DataType.Exec, 200, 0));
        GraphNode low = builder.Add(GraphNodeKind.SendMessage, 400, 300);
        GraphNode high = builder.Add(GraphNodeKind.SendMessage, 400, 0);
        builder.Exec(join, reroute);
        NodeGraph graph = builder.Build()
            .Connect(new GraphConnection(reroute.Id, NodeInfo.ExecThen, low.Id, NodeInfo.ExecInput))
            .Connect(new GraphConnection(reroute.Id, NodeInfo.ExecThen, high.Id, NodeInfo.ExecInput));

        Assert.Equal(2, graph.OutputConnections(reroute.Id, NodeInfo.ExecThen).Count());
        NodeGraph collapsed = Reroutes.Collapse(graph);
        GraphNode sequence = Assert.Single(collapsed.Nodes, n => n.Kind == GraphNodeKind.Sequence);
        Assert.Equal(high.Id, collapsed.NextNode(sequence.Id, FlowNodes.SequencePin(0))!.Id);
        Assert.Equal(low.Id, collapsed.NextNode(sequence.Id, FlowNodes.SequencePin(1))!.Id);
    }

    [Fact]
    public async Task Switch_sequence_and_flip_flop_become_java()
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = "neoforge", MinecraftVersion = "1.21.1" });

        var builder = new GraphBuilder();
        GraphNode join = builder.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode sequence = builder.Add(GraphNodeKind.Sequence, 200, 0);
        GraphNode choice = builder.Add(GraphNode.Create(GraphNodeKind.SwitchOnText, 400, 0).WithValue(FlowNodes.CasesKey, "Steve\nAlex"));
        GraphNode name = builder.Add(GraphNodeKind.PlayerName, 200, 200);
        GraphNode hello = builder.Add(GraphNodeKind.SendMessage, 600, 0, ("message", "Hello Steve"));
        GraphNode other = builder.Add(GraphNodeKind.SendMessage, 600, 200, ("message", "Who are you?"));
        GraphNode flip = builder.Add(GraphNodeKind.FlipFlop, 400, 400);
        GraphNode tick = builder.Add(GraphNodeKind.SendMessage, 600, 400, ("message", "Tick"));
        builder.Exec(join, sequence)
            .Exec(sequence, choice, FlowNodes.SequencePin(0)).Exec(sequence, flip, FlowNodes.SequencePin(1))
            .Exec(choice, hello, FlowNodes.CasePin(0)).Exec(choice, other, FlowNodes.DefaultPin)
            .Exec(flip, tick, FlowNodes.FlipFlopA)
            .Data(join, "player", name, "player").Data(name, "name", choice, FlowNodes.ValueInput)
            .Data(join, "player", hello, "player").Data(join, "player", other, "player").Data(join, "player", tick, "player");
        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("flow"), "Flow") { Graph = builder.Build() });

        var diagnostics = new DiagnosticBag();
        ModIR? ir = ModIRBuilder.CreateDefault().Build(project, diagnostics);
        Assert.True(ir is not null && !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(ModLoaderKind.NeoForge, "1.21.1", new NeoForgeJavaEmitter()))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));

        string java = string.Join('\n', Directory.GetFiles(Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod/behavior"), "*.java")
            .Select(File.ReadAllText));
        Assert.Contains($"String switch_{choice.Id.ToString("N")[..8]} = ", java);
        Assert.Contains($"\"Steve\".equals(switch_{choice.Id.ToString("N")[..8]})", java);
        Assert.Contains("} else if (\"Alex\".equals(", java);
        Assert.Contains($"flipFlop(\"{flip.Id:N}\")", java);
        Assert.Contains("private static boolean flipFlop(String node)", java);
        Assert.True(java.IndexOf("switch_", StringComparison.Ordinal) < java.IndexOf("boolean flip_", StringComparison.Ordinal));
    }

    [Fact]
    public void Puppeteer_comments_are_never_linked_nor_reported()
    {
        BehaviorTree tree = BehaviorTree.Empty;
        BtNode wander = BtNode.Create(BtNodeKind.Wander, 0, 120);
        var comment = new BtNode(Guid.NewGuid(), BtNodeKind.Comment, -40, 60, GraphAnnotations.CommentValues(260, 120, "Idle"));
        tree = tree.AddNode(wander).AddNode(comment).Connect(tree.Root.Id, wander.Id);

        Assert.NotNull(tree.DescribeLinkProblem(tree.Root.Id, comment.Id));
        var asset = new BehaviorTreeAsset(Guid.NewGuid(), ResourceId.Parse("idle"), "Idle") { Tree = tree };
        Assert.DoesNotContain(BehaviorTreeValidator.Validate(asset, _ => null), i => i.ItemId == comment.Id);
        Assert.Equal((comment.X, comment.Y), (tree.Arranged().FindNode(comment.Id)!.X, tree.Arranged().FindNode(comment.Id)!.Y));
    }
}
