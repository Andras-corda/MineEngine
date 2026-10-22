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
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Scopes of the global variables (scopeGlobalVariable.md).</summary>
public sealed class ScopesTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static (ModProject Project, ScopesSample Sample) CreateProject(TemporaryDirectory temp, string loader = "neoforge", string version = "1.21.1")
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader, MinecraftVersion = version });
        return (project, ScopesSample.AddTo(project));
    }

    [Fact]
    public void Every_scope_has_a_snake_case_name_that_is_not_translated()
    {
        Assert.Equal(Enum.GetValues<GlobalScope>().Length, GlobalVariableAsset.OrderedScopes.Count);
        Assert.All(GlobalVariableAsset.OrderedScopes, s => Assert.Matches("^(player|entity|global|world)_[a-z]+$", GlobalVariableAsset.ScopeLabel(s)));
        Assert.Equal(GlobalVariableAsset.OrderedScopes.Count, GlobalVariableAsset.OrderedScopes.Select(GlobalVariableAsset.ScopeLabel).Distinct().Count());
    }

    [Theory]
    [InlineData(GlobalScope.PlayerTeam, "player")]
    [InlineData(GlobalScope.EntityOwner, "entity")]
    [InlineData(GlobalScope.WorldBiome, "position")]
    [InlineData(GlobalScope.GlobalServer, null)]
    public void The_nodes_ask_for_the_key_of_the_scope(GlobalScope scope, string? input)
    {
        var global = new GlobalVariableAsset(Guid.NewGuid(), ResourceId.Parse("valeur"), "valeur") { Scope = scope };
        var program = new ProgramModel([], [global]);
        var node = new GraphNode(Guid.NewGuid(), GraphNodeKind.GetGlobal, 0, 0, System.Collections.Immutable.ImmutableDictionary<string, string>.Empty) { Target = global.Id };

        NodeInfo info = program.Describe(node, new GraphAsset(Guid.NewGuid(), ResourceId.Parse("g"), "g"));

        Assert.Equal(input, info.Inputs.SingleOrDefault()?.Name);
    }

    [Fact]
    public void Sessions_are_not_saved()
    {
        Assert.False(new GlobalVariableAsset(Guid.NewGuid(), ResourceId.Parse("a"), "a") { Scope = GlobalScope.PlayerSession }.IsSaved);
        Assert.True(new GlobalVariableAsset(Guid.NewGuid(), ResourceId.Parse("b"), "b") { Scope = GlobalScope.PlayerAccount }.IsSaved);
        Assert.Equal(GlobalStorage.Keyed, GlobalsJava.Storage(GlobalScope.WorldStructure));
        Assert.Equal(GlobalStorage.Static, GlobalsJava.Storage(GlobalScope.GlobalServer));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Every_scope_becomes_java(ModLoaderKind loader, string version)
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
        Assert.Contains("keyTeam(level, key)", globals);
        Assert.Contains("keyStructure(level, key)", globals);
        Assert.Contains("class ServerData", globals);
        Assert.Contains("FMLPaths.CONFIGDIR", globals);
        Assert.Contains("onServerStopping", globals);
        string behavior = File.ReadAllText(Path.Combine(root, "behavior/UsagePorteesBehavior.java"));
        Assert.Contains("MineEngineGlobals.setPorteeWorldBiome(level, player.blockPosition(), ", behavior);
        Assert.Contains("\"mineengine_guild\"", behavior);
    }
}
