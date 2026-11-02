using System.Text.Json.Nodes;
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

/// <summary>Commands: typed parameters (Brigadier arguments), who runs them, and their "On command" event.</summary>
public sealed class CommandTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    [Fact]
    public void The_argument_of_v062_becomes_a_parameter()
    {
        var properties = new JsonObject { ["argument"] = "Number", ["argumentName"] = "Seconds", ["minimum"] = 1, ["maximum"] = 600 };

        var command = (CommandAsset)new CommandAssetDefinition().Read(Guid.NewGuid(), ResourceId.Parse("boost"), "Boost", properties);

        CommandParameter parameter = Assert.Single(command.Parameters);
        Assert.Equal("seconds", parameter.Name);
        Assert.Equal(CommandParameterKind.Integer, parameter.Kind);
        Assert.Equal(600, parameter.Maximum);
    }

    [Fact]
    public void The_event_of_a_command_gives_its_parameters()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, "neoforge", "1.21.1");
        GraphAsset graph = project.Assets.OfType<GraphAsset>().Single(g => g.ResourceId.Value == "send_to_events");
        GraphNode node = graph.Graph.Nodes.Single(n => n.Kind == GraphNodeKind.OnCommand);

        NodeInfo info = ProgramModel.From(project.Assets).Describe(node, graph);

        Assert.Equal(DataType.Vector3f, info.FindOutput("arg_push")!.Type);
        Assert.Equal(DataType.BlockState, info.FindOutput("arg_marker")!.Type);
        Assert.Null(info.FindOutput("number"));
        Assert.Equal(14, info.Outputs.Count);
    }

    [Fact]
    public void A_message_can_only_be_the_last_parameter()
    {
        var command = new CommandAsset(Guid.NewGuid(), ResourceId.Parse("say_twice"), "Say")
        {
            Parameters = [new CommandParameter(Guid.NewGuid(), "text", CommandParameterKind.Message), new CommandParameter(Guid.NewGuid(), "times", CommandParameterKind.Integer)],
        };
        var diagnostics = new DiagnosticBag();

        command.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Parameters_become_brigadier_arguments(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, loader.ToId(), version);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");

        string commands = File.ReadAllText(Path.Combine(java, "registry/ModCommands.java"));
        Assert.Contains(".then(Commands.argument(\"where\", BlockPosArgument.blockPos())", commands);
        Assert.Contains("DoubleArgumentType.doubleArg(0.5, 4.0)", commands);
        Assert.Contains("ItemArgument.item(event.getBuildContext())", commands);
        Assert.Contains("Vec3 arg_push = Vec3Argument.getVec3(context, \"push\");", commands);
        Assert.Contains("ServerPlayer player = source.getPlayer();", commands);
        Assert.Contains("IntegerArgumentType.integer(1, 600)", commands);

        string behavior = File.ReadAllText(Path.Combine(java, "behavior/SendToEventsBehavior.java"));
        Assert.Contains("Player target, Entity arg_who, BlockPos arg_where, Vec3 arg_push, double arg_speed,", behavior);
    }

    private static ModProject CreateProject(TemporaryDirectory temp, string loader, string version)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader, MinecraftVersion = version });
        SampleContent.AddTo(project);
        return project;
    }
}
