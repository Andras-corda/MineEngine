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

/// <summary>Types of TypesVar.md (char, long, float, byte, UUID, rich text...), the casts and the automatic casts.</summary>
public sealed class TypesTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static (ModProject Project, TypesSample Sample) CreateProject(TemporaryDirectory temp, string loader = "neoforge", string version = "1.21.1")
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader, MinecraftVersion = version });
        return (project, TypesSample.AddTo(project));
    }

    [Theory]
    [InlineData(PinType.Number, PinType.Float, GraphNodeKind.CastToFloat)]
    [InlineData(PinType.Decimal, PinType.Number, GraphNodeKind.CastToNumber)]
    [InlineData(PinType.Number, PinType.Char, GraphNodeKind.CastToChar)]
    [InlineData(PinType.Text, PinType.Identifier, GraphNodeKind.CastToIdentifier)]
    [InlineData(PinType.Entity, PinType.Player, GraphNodeKind.CastToPlayer)]
    [InlineData(PinType.Text, PinType.TextComponent, GraphNodeKind.CastToComponent)]
    [InlineData(PinType.Uuid, PinType.Text, GraphNodeKind.CastToText)]
    public void A_cast_is_found_between_two_types(PinType from, PinType to, GraphNodeKind expected)
    {
        Assert.Equal(expected, GenericNodes.CastBetween(new DataType(from), new DataType(to), TypeRules.CanFeedBuiltIn));
    }

    [Fact]
    public void Types_without_conversion_have_no_cast()
    {
        Assert.Null(GenericNodes.CastBetween(DataType.Position, DataType.Fluid, TypeRules.CanFeedBuiltIn));
        Assert.Null(GenericNodes.CastBetween(DataType.Player, DataType.LivingEntity, TypeRules.CanFeedBuiltIn) is { } kind ? kind : null);
        Assert.True(TypeRules.CanFeedBuiltIn(DataType.Player, DataType.LivingEntity));
        Assert.True(TypeRules.CanFeedBuiltIn(DataType.LivingEntity, DataType.Entity));
    }

    [Fact]
    public void A_cast_takes_the_type_of_its_wire()
    {
        var program = new ProgramModel([]);

        NodeInfo cast = ProgramPalette.Typed(GraphNodeKind.CastToFloat, DataType.Long, program).Info;

        Assert.Equal(DataType.Long, Assert.Single(cast.Inputs).Type);
        Assert.Equal(DataType.Float, Assert.Single(cast.Outputs).Type);
        Assert.Equal(NodeGroup.Conversions, NodeCatalog.GroupOf(cast));
    }

    [Theory]
    [InlineData(PinType.Char, "ab", false)]
    [InlineData(PinType.Char, "a", true)]
    [InlineData(PinType.Byte, "200", false)]
    [InlineData(PinType.Long, "9000000000", true)]
    [InlineData(PinType.Identifier, "Minecraft:Stone", false)]
    [InlineData(PinType.Identifier, "mymod:ruby_ore", true)]
    [InlineData(PinType.Hand, "off_hand", true)]
    [InlineData(PinType.Rarity, "legendary", false)]
    public void Values_typed_on_an_input_are_checked(PinType kind, string value, bool valid)
    {
        string? problem = GraphValidator.DescribeValueProblem(new PinInfo("v", "V", new DataType(kind)), value);

        Assert.Equal(valid, problem is null);
    }

    [Fact]
    public void The_sample_uses_every_new_node()
    {
        using var temp = new TemporaryDirectory();
        (_, TypesSample sample) = CreateProject(temp);
        HashSet<GraphNodeKind> used = [.. sample.Events.Graph.Nodes.Select(n => n.Kind)];

        Assert.All(Enum.GetValues<GraphNodeKind>().Where(k => k is >= GraphNodeKind.CastToNumber and <= GraphNodeKind.GameTime), k => Assert.Contains(k, used));
    }

    [Fact]
    public void The_sample_is_valid_and_its_globals_survive_a_save()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, TypesSample sample) = CreateProject(temp);
        var diagnostics = new DiagnosticBag();

        ModIR? ir = ModIRBuilder.CreateDefault().Build(project, diagnostics);

        Assert.True(ir is not null, string.Join('\n', diagnostics));
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        repository.Save(project);
        ModProject reopened = repository.Open(project.Layout.RootDirectory);
        foreach (GlobalVariableAsset original in sample.Globals)
        {
            var global = (GlobalVariableAsset)reopened.Assets.Find(original.Id)!;
            Assert.Equal(original.ValueType, global.ValueType);
            Assert.Equal(original.DefaultValue, global.DefaultValue);
        }
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_types_become_java(ModLoaderKind loader, string version)
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
        Assert.Contains("char lettre = 'z';", globals);
        Assert.Contains("(-12L)", globals);
        Assert.Contains("1.5f", globals);
        Assert.Contains("EquipmentSlot.HEAD", globals);
        Assert.Contains("InteractionHand.OFF_HAND", globals);
        string behavior = File.ReadAllText(Directory.GetFiles(Path.Combine(root, "behavior"), "UsageTypesBehavior.java").Single());
        Assert.Contains("((float) (5))", behavior);
        Assert.Contains("((char) (65))", behavior);
        Assert.Contains("toLong(\"123\")", behavior);
        Assert.Contains("toPlayer(", behavior);
        Assert.Contains("Component.literal(", behavior);
        Assert.Contains("level.getGameTime()", behavior);
    }
}
