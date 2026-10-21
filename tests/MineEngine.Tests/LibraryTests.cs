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
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Function libraries, comments and tooltips of the graphs.</summary>
public sealed class LibraryTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static (ModProject Project, LibrarySample Sample) CreateProject(TemporaryDirectory temp)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = "neoforge", MinecraftVersion = "1.21.1" });
        return (project, LibrarySample.AddTo(project));
    }

    [Fact]
    public void Library_functions_are_offered_everywhere_under_the_name_of_the_library()
    {
        using var temp = new TemporaryDirectory();
        (_, LibrarySample sample) = CreateProject(temp);
        var other = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("ailleurs"), "Ailleurs") { Kind = GraphKind.Procedure, Folder = "Autre/Dossier" };
        sample.Project.Assets.Add(other);

        List<NodeTemplate> calls = [.. ProgramPalette.AvailableNodes(sample.Program, other).Where(t => t.Info.Library is not null)];

        Assert.Equal(2, calls.Count);
        Assert.All(calls, t => Assert.Equal("Outils", t.Info.Library));
        Assert.All(calls, t => Assert.Equal(NodeCategory.Library, t.Info.Category));
        Assert.Equal(NodeFlow.Pure, calls.Single(t => t.Target == sample.Double.Id).Info.Flow);
        Assert.Equal(NodeFlow.Step, calls.Single(t => t.Target == sample.Greet.Id).Info.Flow);
    }

    [Fact]
    public void A_library_and_annotations_are_valid_and_the_annotations_change_nothing()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, LibrarySample sample) = CreateProject(temp);
        var diagnostics = new DiagnosticBag();

        ModIR? ir = ModIRBuilder.CreateDefault().Build(project, diagnostics);

        Assert.True(ir is not null, string.Join('\n', diagnostics));
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        IRClass library = Assert.Single(ir.Content.Definitions.OfType<IRClass>(), c => c.Id == sample.Library.Id);
        Assert.All(ir.Content.Definitions.OfType<IRFunction>().Where(f => f.OwnerId == library.Id), f => Assert.True(f.IsStatic));
        Assert.Equal(sample.Events.Graph.Nodes.Length - 2, Reroutes.Collapse(sample.Events.Graph).Nodes.Length);
    }

    [Fact]
    public void A_library_member_must_stay_public_and_static()
    {
        using var temp = new TemporaryDirectory();
        (_, LibrarySample sample) = CreateProject(temp);
        sample.Greet.Access = AccessLevel.Private;

        Assert.Contains(ProgramValidator.Validate(sample.Greet, sample.Program), i => i.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Comments_and_tooltips_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, LibrarySample sample) = CreateProject(temp);
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        repository.Save(project);

        ModProject reopened = repository.Open(project.Layout.RootDirectory);
        var events = (GraphAsset)reopened.Assets.Find(sample.Events.Id)!;
        GraphNode comment = events.Graph.FindNode(sample.Comment.Id)!;
        GraphNode tooltip = events.Graph.FindNode(sample.Tooltip.Id)!;

        Assert.Equal("Accueil", GraphAnnotations.Text(comment));
        Assert.Equal(GraphAnnotations.Size(sample.Comment), GraphAnnotations.Size(comment));
        Assert.Equal(GraphAnnotations.Targets(sample.Tooltip), GraphAnnotations.Targets(tooltip));
        Assert.Equal(GraphKind.Library, ((GraphAsset)reopened.Assets.Find(sample.Library.Id)!).Kind);
    }

    [Fact]
    public async Task A_library_becomes_a_java_class_of_static_methods()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, _) = CreateProject(temp);
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, new DiagnosticBag())!;
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(ModLoaderKind.NeoForge, "1.21.1", new NeoForgeJavaEmitter()))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string root = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");

        string library = File.ReadAllText(Path.Combine(root, "code/Outils.java"));
        Assert.Contains("public class Outils {", library);
        Assert.Contains("public static int doubler(Level level, int nombre) {", library);
        Assert.Contains("public static void saluer(Level level, Player joueur) {", library);
        string behavior = string.Join('\n', Directory.GetFiles(Path.Combine(root, "behavior"), "*.java").Select(File.ReadAllText));
        Assert.Contains("Outils.saluer(", behavior);
        Assert.Contains("Outils.doubler(", behavior);
    }
}
