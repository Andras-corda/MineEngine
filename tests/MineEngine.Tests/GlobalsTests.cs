using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Global variables (V0.6.7), their types and the collection, vector, decimal, direction, block state and entity nodes.</summary>
public sealed class GlobalsTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static (ModProject Project, GlobalsSample Sample) CreateProject(TemporaryDirectory temp, string loader = "neoforge", string version = "1.21.1")
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader, MinecraftVersion = version });
        ProgramSample.AddTo(project);
        return (project, GlobalsSample.AddTo(project));
    }

    [Theory]
    [InlineData("number")]
    [InlineData("list<text>")]
    [InlineData("set<position>")]
    [InlineData("map<text,list<decimal>>")]
    [InlineData("array<vector3f>")]
    public void Types_are_written_and_read_back(string text)
    {
        DataType type = DataType.Parse(text)!;

        Assert.Equal(text, type.ToStorageText());
    }

    [Fact]
    public void Collection_nodes_take_the_type_of_their_collection()
    {
        var program = new ProgramModel([]);
        DataType texts = DataType.ListOf(DataType.Text);

        NodeInfo add = ProgramPalette.Typed(GraphNodeKind.ListAdd, texts, program).Info;
        NodeInfo get = GenericNodes.Describe(GraphNodeKind.MapGet, DataType.Collection(PinType.Map, DataType.Decimal, DataType.Position), program.TypeLabel);
        NodeInfo each = GenericNodes.Describe(GraphNodeKind.ForEachInCollection, DataType.Collection(PinType.Map, DataType.Number, DataType.Text), program.TypeLabel);

        Assert.Equal(DataType.Text, add.Inputs.Single(i => i.Name == "value").Type);
        Assert.Equal(DataType.Position, get.Inputs.Single(i => i.Name == "key").Type);
        Assert.Equal(DataType.Decimal, get.Outputs.Single().Type);
        Assert.Equal(["key", "value"], each.Outputs.Select(o => o.Name));
        Assert.Equal(NodeFlow.Loop, each.Flow);
        Assert.False(GenericNodes.Accepts(GraphNodeKind.ListGet, DataType.Collection(PinType.Set, DataType.Text)));
        Assert.False(GenericNodes.Accepts(GraphNodeKind.VectorZ, DataType.Vector2f));
    }

    [Fact]
    public void The_sample_uses_every_new_node()
    {
        using var temp = new TemporaryDirectory();
        (_, GlobalsSample sample) = CreateProject(temp);
        HashSet<GraphNodeKind> used = [.. sample.Events.Graph.Nodes.Select(n => n.Kind)];

        Assert.All(Enum.GetValues<GraphNodeKind>().Where(k => k is >= GraphNodeKind.GetGlobal and <= GraphNodeKind.EntityIsPlayer), k => Assert.Contains(k, used));
    }

    [Fact]
    public void Every_kind_of_vector_has_its_own_make_node()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, GlobalsSample sample) = CreateProject(temp);
        ProgramModel program = ProgramModel.From(project.Assets);

        List<NodeTemplate> makes = [.. ProgramPalette.AvailableNodes(program, sample.Events).Where(t => t.Info.Kind == GraphNodeKind.MakeVector)];

        Assert.Equal(4, makes.Count);
        Assert.Contains(makes, t => t.Info.Outputs.Single().Type == DataType.Vector2i);
        Assert.Contains(makes, t => t.Info.Inputs.Count == 3 && t.Info.Inputs[0].Type == DataType.Decimal);
    }

    [Fact]
    public void Get_and_set_nodes_follow_the_global_and_its_scope()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, GlobalsSample sample) = CreateProject(temp);
        ProgramModel program = ProgramModel.From(project.Assets);

        List<NodeTemplate> globals = [.. ProgramPalette.AvailableNodes(program, sample.Events).Where(t => t.Info.Category == NodeCategory.Global)];
        NodeInfo getScore = globals.Single(t => t.Info.Kind == GraphNodeKind.GetGlobal && t.Target == sample.Score.Id).Info;
        NodeInfo setSeen = globals.Single(t => t.Info.Kind == GraphNodeKind.SetGlobal && t.Target == sample.Seen.Id).Info;

        Assert.Equal(sample.Globals.Count * 2, globals.Count);
        Assert.Equal("player", Assert.Single(getScore.Inputs).Name);
        Assert.Equal(sample.Seen.ValueType, Assert.Single(setSeen.Inputs).Type);
        Assert.Equal(NodeGroup.Globals, NodeCatalog.GroupOf(getScore));
    }

    [Fact]
    public void Globals_are_valid_and_lowered()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, GlobalsSample sample) = CreateProject(temp);
        var diagnostics = new DiagnosticBag();

        ModIR? ir = ModIRBuilder.CreateDefault().Build(project, diagnostics);

        Assert.True(ir is not null, string.Join('\n', diagnostics));
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(sample.Globals.Count, ir.Content.Globals.Count);
    }

    [Fact]
    public void An_entity_in_a_saved_scope_is_reported()
    {
        var global = new GlobalVariableAsset(Guid.NewGuid(), ResourceId.Parse("cible"), "cible") { ValueType = DataType.ListOf(DataType.Entity), Scope = GlobalScope.Map };
        var diagnostics = new DiagnosticBag();

        global.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
        global.Scope = GlobalScope.Session;
        diagnostics = new DiagnosticBag();
        global.Validate(diagnostics);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void A_bad_default_value_is_an_error()
    {
        var global = new GlobalVariableAsset(Guid.NewGuid(), ResourceId.Parse("vent"), "vent") { ValueType = DataType.Direction, DefaultValue = "nowhere" };
        var diagnostics = new DiagnosticBag();

        global.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Globals_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, GlobalsSample sample) = CreateProject(temp);
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        repository.Save(project);

        ModProject reopened = repository.Open(project.Layout.RootDirectory);

        foreach (GlobalVariableAsset original in sample.Globals)
        {
            var global = (GlobalVariableAsset)reopened.Assets.Find(original.Id)!;
            Assert.Equal(original.ValueType, global.ValueType);
            Assert.Equal(original.Scope, global.Scope);
            Assert.Equal(original.DefaultValue, global.DefaultValue);
        }

        var events = (GraphAsset)reopened.Assets.Find(sample.Events.Id)!;
        Assert.Equal(sample.Events.Graph.Nodes.Length, events.Graph.Nodes.Length);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Globals_become_a_java_class_used_by_the_graphs(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, _) = CreateProject(temp, loader.ToId(), version);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string root = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");

        string globals = File.ReadAllText(Path.Combine(root, "code/MineEngineGlobals.java"));
        Assert.Contains("public static int getScore(Player player)", globals);
        Assert.Contains("public static List<String> getJoueursVus(Level level)", globals);
        Assert.Contains("extends SavedData", globals);
        string behavior = string.Join('\n', Directory.GetFiles(Path.Combine(root, "behavior"), "*.java").Select(File.ReadAllText));
        Assert.Contains("MineEngineGlobals.setScore(", behavior);
        Assert.Contains("listSet(", behavior);
        Assert.Contains("for (var each_", behavior);
        Assert.Contains("for (var entry_", behavior);
        Assert.Contains("new Vector2i(", behavior);
        Assert.Contains("Direction", behavior);
        Assert.Contains("MineEngineGlobals.register();", File.ReadAllText(Directory.GetFiles(root, "*.java").Single(f => File.ReadAllText(f).Contains("@Mod(", StringComparison.Ordinal))));
    }
}
