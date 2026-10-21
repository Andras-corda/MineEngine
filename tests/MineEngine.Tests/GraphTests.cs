using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Lowering;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>V0.4 : graphes nodaux, validation, compilation en IR et génération Java des événements.</summary>
public sealed class GraphTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static readonly ContentReference Diamond = Ref("minecraft:diamond");

    // ----- Critère de réussite de la V0.4 -----

    [Fact]
    public void PlayerInteract_HasItem_SpawnItem_compiles_to_a_conditional_spawn()
    {
        GraphAsset graph = SampleContent.CreateAltarGraph(Ref("minecraft:emerald"), Diamond);
        Assert.DoesNotContain(GraphValidator.Validate(graph.Graph), i => i.Severity != DiagnosticSeverity.Info);

        IREventHandler handler = Assert.Single(new GraphCompiler(graph, new ProgramModel([graph]), (r, _) => r.Id).CompileEvents());

        Assert.Equal(GraphNodeKind.OnRightClickBlock, handler.Event);
        IRIfStatement branch = Assert.IsType<IRIfStatement>(Assert.Single(handler.Body));
        IRCallExpression condition = Assert.IsType<IRCallExpression>(branch.Condition);
        Assert.Equal(GraphNodeKind.HasItem, condition.Kind);
        Assert.Equal(new IREventValue(PinType.Player, "player"), condition.Arguments[0]);
        Assert.Equal(new IRNumberLiteral(3), condition.Arguments[2]);
        IRActionStatement spawn = Assert.IsType<IRActionStatement>(Assert.Single(branch.Then));
        Assert.Equal(GraphNodeKind.SpawnItem, spawn.Action);
        Assert.Equal(new IREventValue(PinType.Position, "position"), spawn.Arguments[0]);
        Assert.Empty(branch.Else);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1", "NeoForge.EVENT_BUS", "net.neoforged.neoforge.event.entity.player.PlayerInteractEvent", "ResourceLocation.parse(id)")]
    [InlineData(ModLoaderKind.Forge, "1.21.1", "MinecraftForge.EVENT_BUS", "net.minecraftforge.event.entity.player.PlayerInteractEvent", "ResourceLocation.parse(id)")]
    [InlineData(ModLoaderKind.Forge, "1.20.1", "MinecraftForge.EVENT_BUS", "net.minecraftforge.event.entity.player.PlayerInteractEvent", "new ResourceLocation(id)")]
    public async Task Graphs_generate_an_event_class_registered_by_the_main_class(
        ModLoaderKind loader, string version, string bus, string eventImport, string resourceLocation)
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateSampleAsync(temp.Path, loader, version);
        string Read(string relative) => File.ReadAllText(Path.Combine(workspace, "src/main/java/com/mineengine/mods/testmod", relative));

        string main = Read("TestMod.java");
        Assert.Contains("import com.mineengine.mods.testmod.behavior.RubyAltarBehavior;", main);
        Assert.Contains("RubyAltarBehavior.register();", main);
        Assert.Contains("AllNodesBehavior.register();", main);

        string altar = Read("behavior/RubyAltarBehavior.java");
        Assert.Contains($"import {eventImport};", altar);
        Assert.Contains($"{bus}.addListener(RubyAltarBehavior::onRightClickBlock1);", altar);
        Assert.Contains("private static void onRightClickBlock1(PlayerInteractEvent.RightClickBlock event) {", altar);
        Assert.Contains("if (event.getLevel().isClientSide() || event.getHand() != InteractionHand.MAIN_HAND) {", altar);
        Assert.Contains("if (player.getInventory().countItem(item(\"testmod:ruby\")) >= 3) {", altar);
        Assert.Contains("spawnItem(level, position, item(\"minecraft:diamond\"), 1);", altar);
        Assert.Contains($"return BuiltInRegistries.ITEM.get({resourceLocation});", altar);

        string all = Read("behavior/AllNodesBehavior.java");
        Assert.Contains("    private static void onBlockBroken1(BlockEvent.BreakEvent event) {", all);
        Assert.Contains("        Player player = event.getPlayer();", all);
        Assert.Contains("Player player = event.getPlayer();", all);
        Assert.Contains("event.setCanceled(true);", all);
        Assert.Contains("setBlock(level, position, block(\"testmod:ruby_ore\"));", all);
        Assert.Contains("giveItem(player, item(\"testmod:ruby\"), randomBetween(level, 1, 3));", all);
        Assert.Contains("} else if (level.random.nextInt(100) < 25) {", all);
        Assert.Contains("explode(level, position.offset(0, 1, 0), 1);", all);
        Assert.Contains("playSound(level, position, sound(\"testmod:goblin_hurt\"));", all);
        Assert.Contains("if (randomBetween(level, 1, 10) > 5) {", all);
        Assert.Contains("sendMessage(player, (\"Bienvenue \\\"h\\u00e9ros\\\" \" + player.getName().getString()));", all);
        Assert.Contains("sendMessage(player, String.valueOf(randomBetween(level, 1, 10)));", all);
        Assert.Contains("if (player.isShiftKeyDown()) {", all);
        Assert.Contains("if (!(false)) {", all);
        Assert.Contains("removeItem(player, item(\"minecraft:dirt\"), 2);", all);
        Assert.Contains("spawnItem(level, player.blockPosition(), item(\"minecraft:diamond\"), 1);", all);
        Assert.DoesNotContain("(-2) == (-2)", all);
        Assert.Contains("PlayerEvent.PlayerLoggedInEvent event", all);
        Assert.Contains("PlayerInteractEvent.RightClickItem event", all);
    }

    // ----- Règles de branchement -----

    [Fact]
    public void Connections_follow_blueprint_rules()
    {
        var builder = new GraphBuilder();
        GraphNode click = builder.Add(GraphNodeKind.OnRightClickBlock, 0, 0);
        GraphNode message = builder.Add(GraphNodeKind.SendMessage, 300, 0);
        GraphNode number = builder.Add(GraphNodeKind.NumberValue, 0, 200);
        GraphNode text = builder.Add(GraphNodeKind.TextValue, 0, 300);
        GraphNode give = builder.Add(GraphNodeKind.GiveItem, 300, 200);
        NodeGraph graph = builder.Build();

        Assert.NotNull(graph.DescribeConnectionProblem(new GraphConnection(click.Id, "position", message.Id, "player")));
        Assert.NotNull(graph.DescribeConnectionProblem(new GraphConnection(text.Id, "value", give.Id, "count")));
        Assert.NotNull(graph.DescribeConnectionProblem(new GraphConnection(number.Id, "value", message.Id, NodeInfo.ExecInput)));
        Assert.NotNull(graph.DescribeConnectionProblem(new GraphConnection(click.Id, "player", number.Id, "value")));
        Assert.Null(graph.DescribeConnectionProblem(new GraphConnection(number.Id, "value", message.Id, "message")));

        // Une entrée de valeur ne garde qu'un fil ; une sortie d'exécution aussi.
        graph = graph.Connect(new GraphConnection(number.Id, "value", message.Id, "message"))
            .Connect(new GraphConnection(text.Id, "value", message.Id, "message"))
            .Connect(new GraphConnection(click.Id, NodeInfo.ExecThen, message.Id, NodeInfo.ExecInput))
            .Connect(new GraphConnection(click.Id, NodeInfo.ExecThen, give.Id, NodeInfo.ExecInput));

        Assert.Equal(text.Id, graph.InputConnection(message.Id, "message")!.FromNode);
        Assert.Equal(give.Id, graph.NextNode(click.Id, NodeInfo.ExecThen)!.Id);
        Assert.Equal(2, graph.Connections.Length);

        NodeGraph removed = graph.RemoveNodes([text.Id]);
        Assert.Single(removed.Connections);
    }

    [Fact]
    public void Validation_reports_problems_on_the_faulty_nodes()
    {
        var builder = new GraphBuilder();
        GraphNode join = builder.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode click = builder.Add(GraphNodeKind.OnRightClickBlock, 0, 300);
        GraphNode cancel = builder.Add(GraphNodeKind.CancelEvent, 300, 0);
        GraphNode give = builder.Add(GraphNodeKind.GiveItem, 600, 0, ("count", "beaucoup"), ("item", "#minecraft:planks"));
        GraphNode orphan = builder.Add(GraphNodeKind.SendMessage, 300, 300);
        builder.Exec(join, cancel).Exec(cancel, give).Data(click, "player", give, "player");
        NodeGraph graph = builder.Build();

        IReadOnlyList<GraphIssue> issues = GraphValidator.Validate(graph);

        Assert.Contains(issues, i => i.NodeId == cancel.Id && i.Severity == DiagnosticSeverity.Error && i.Message.Contains("cannot be canceled"));
        Assert.Contains(issues, i => i.NodeId == give.Id && i.Message.Contains("whole number"));
        Assert.Contains(issues, i => i.NodeId == give.Id && i.Message.Contains("tag"));
        Assert.Contains(issues, i => i.NodeId == give.Id && i.Message.Contains("other") is false && i.Message.Contains("Right-click"));
        Assert.Contains(issues, i => i.NodeId == orphan.Id && i.Severity == DiagnosticSeverity.Warning);
        Assert.Contains(issues, i => i.NodeId == orphan.Id && i.Message.Contains("must be connected"));
        Assert.Contains(issues, i => i.NodeId == click.Id && i.Severity == DiagnosticSeverity.Info);
    }

    [Fact]
    public void An_execution_loop_is_an_error()
    {
        var builder = new GraphBuilder();
        GraphNode join = builder.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode first = builder.Add(GraphNodeKind.Explosion, 300, 0);
        GraphNode second = builder.Add(GraphNodeKind.Explosion, 600, 0);
        builder.Exec(join, first).Exec(first, second).Exec(second, first)
            .Data(join, "position", first, "position").Data(join, "position", second, "position");

        Assert.Contains(GraphValidator.Validate(builder.Build()), i => i.Severity == DiagnosticSeverity.Error && i.Message.Contains("loop"));
    }

    [Fact]
    public void A_value_loop_is_refused()
    {
        var builder = new GraphBuilder();
        GraphNode a = builder.Add(GraphNodeKind.Not, 0, 0);
        GraphNode b = builder.Add(GraphNodeKind.Not, 300, 0);
        builder.Data(a, "result", b, "value");

        Assert.Contains("loop", builder.Build().DescribeConnectionProblem(new GraphConnection(b.Id, "result", a.Id, "value")));
    }

    // ----- Projet -----

    [Fact]
    public void Graphs_survive_a_save_and_reload_and_follow_renamed_items()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        SampleContent.AddTo(project);
        Repository.Save(project);

        ModProject reopened = Repository.Open(project.Layout.RootDirectory);
        var original = (GraphAsset)project.Assets.FindByResourceId(ResourceId.Parse("all_nodes"), AssetType.Graph)!;
        var loaded = (GraphAsset)reopened.Assets.FindByResourceId(ResourceId.Parse("all_nodes"), AssetType.Graph)!;
        Assert.Equal(original.Graph.Nodes.Length, loaded.Graph.Nodes.Length);
        Assert.Equal(original.Graph.Connections.ToHashSet(), loaded.Graph.Connections.ToHashSet());
        Assert.All(original.Graph.Nodes, n => Assert.Equal(n.Values.OrderBy(v => v.Key), loaded.Graph.FindNode(n.Id)!.Values.OrderBy(v => v.Key)));
        Assert.True(File.Exists(Path.Combine(project.Layout.ElementsDirectory, "Graphs", "all_nodes.asset.json")));

        // Le rubis est utilisé par les deux graphes, par guid : le renommer ne casse rien.
        Asset ruby = reopened.Assets.FindByResourceId(ResourceId.Parse("ruby"), AssetType.Item)!;
        Assert.Contains(reopened.Assets.FindReferencesTo(ruby.Id), r => r.Source.Type == AssetType.Graph);
        ruby.ResourceId = ResourceId.Parse("red_gem");
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(reopened, diagnostics)!;
        Assert.True(ir is not null, string.Join('\n', diagnostics));
        Assert.Equal(7, ir.Content.Behaviors.Count); // With the event graph of LibrarySample.
    }

    [Fact]
    public void A_graph_without_events_produces_no_behavior()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("empty"), "Vide"));
        var diagnostics = new DiagnosticBag();

        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;

        Assert.Empty(ir.Content.Behaviors);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("no event"));
    }

    // ----- Outils -----

    private static ProjectRepository Repository => new(Serializer, new ProjectSettingsSerializer());

    private static ContentReference Ref(string id) =>
        ContentReference.TryParseStorageText(id, out ContentReference? reference) ? reference! : throw new ArgumentException(id);

    private static ModProject CreateProject(string directory, ModLoaderKind loader = ModLoaderKind.NeoForge, string version = "1.21.1") =>
        Repository.Create(directory, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });

    private static async Task<string> GenerateSampleAsync(string directory, ModLoaderKind loader, string version)
    {
        ModProject project = CreateProject(directory, loader, version);
        SampleContent.AddTo(project);
        var diagnostics = new DiagnosticBag();
        ModIR? ir = ModIRBuilder.CreateDefault().Build(project, diagnostics);
        Assert.True(ir is not null, string.Join('\n', diagnostics));

        string workspace = Path.Combine(directory, "workspace");
        IFileEmitter emitter = loader == ModLoaderKind.Forge ? new ForgeJavaEmitter() : new NeoForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new Backend(loader, version, emitter))
            .GenerateAsync(ir!, workspace, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return workspace;
    }

    private sealed class Backend : IModLoaderBackend
    {
        private readonly IFileEmitter _emitter;

        public Backend(ModLoaderKind loader, string version, IFileEmitter emitter)
        {
            _emitter = emitter;
            Mdk = new MdkDescriptor("test", loader, MinecraftVersion.Parse(version), "0", 21, null, Path.GetTempPath(), "test", DateTime.Now);
        }

        public string LoaderId => Mdk.Loader.ToId();

        public string DisplayName => Mdk.DisplayName;

        public MdkDescriptor Mdk { get; }

        public MinecraftVersion MinecraftVersion => Mdk.MinecraftVersion;

        public IReadOnlyList<string> BuildTasks => ["build"];

        public IReadOnlyList<string> RunClientTasks => ["runClient"];

        public Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<IFileEmitter> CreateEmitters() => [_emitter];

        public string GetOutputJarPath(string workspaceDirectory, IRModInfo mod) => string.Empty;
    }
}
