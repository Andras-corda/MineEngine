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

/// <summary>Camera of the player (V0.7.4): nodes, server calls and the message sent to the client.</summary>
public sealed class CameraTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    [Fact]
    public void Camera_nodes_have_their_own_domain()
    {
        List<NodeInfo> camera = [.. NodeCatalog.All.Where(n => NodeCatalog.GroupOf(n) == NodeGroup.Camera)];

        Assert.Equal(14, camera.Count);
        Assert.Equal(10, camera.Count(n => n.Category == NodeCategory.Action));
        Assert.All(camera, n => Assert.Equal(PinType.Player, n.Inputs[0].Type.Kind));
        Assert.Equal(DataType.Vector3f, NodeCatalog.Get(GraphNodeKind.CameraDirection).Outputs[0].Type);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Camera_nodes_become_the_mod(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        string java = await GenerateAsync(temp, loader, version, SampleContent.AddTo);

        string behavior = File.ReadAllText(Path.Combine(java, "behavior/CameraTricksBehavior.java"));
        Assert.Contains("import com.mineengine.mods.testmod.network.MineEngineCamera;", behavior);
        Assert.Contains("MineEngineCamera.setRotation(player, ((double) Mth.wrapDegrees(player.getYRot())), ((double) player.getXRot()));", behavior);
        Assert.Contains("MineEngineCamera.shake(player, player.getEyePosition().y, 0.5);", behavior);
        Assert.Contains("MineEngineCamera.tilt(player, player.getLookAngle().x);", behavior);
        Assert.Contains("MineEngineCamera.reset(player);", behavior);

        string camera = File.ReadAllText(Path.Combine(java, "network/MineEngineCamera.java"));
        Assert.Contains("server.setCamera(entity == null ? server : entity);", camera);
        Assert.Contains("private static void sendTo(ServerPlayer player, int action, float a, float b)", camera);
        Assert.Contains(loader == ModLoaderKind.NeoForge ? "PacketDistributor.sendToPlayer(player" : "PacketDistributor.PLAYER.with(", camera);
        Assert.Contains(version == "1.20.1" ? "NetworkRegistry.newSimpleChannel(new ResourceLocation(" : "ResourceLocation.parse(", camera);
        string client = File.ReadAllText(Path.Combine(java, "client/MineEngineCameraClient.java"));
        Assert.Contains(loader == ModLoaderKind.NeoForge ? "import net.neoforged.neoforge.client.event.ViewportEvent;" : "import net.minecraftforge.client.event.ViewportEvent;", client);
        Assert.Contains("event.setFOV(event.getFOV() / zoom);", client);

        string main = File.ReadAllText(Directory.GetFiles(java, "*.java").Single(f => File.ReadAllText(f).Contains("@Mod(", StringComparison.Ordinal)));
        Assert.Contains("MineEngineCamera.register(modEventBus);", main);
        Assert.Contains("addListener(MineEngineCameraClient::onComputeCameraAngles);", main);
    }

    [Fact]
    public async Task A_mod_without_camera_action_has_no_camera_class()
    {
        using var temp = new TemporaryDirectory();
        string java = await GenerateAsync(temp, ModLoaderKind.NeoForge, "1.21.1", project =>
        {
            var graph = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("looks"), "Regards") { Kind = GraphKind.Events };
            project.Assets.Add(graph);
            var builder = new ProgramGraphBuilder(graph, () => ProgramModel.From(project.Assets));
            GraphNode use = builder.Add(GraphNodeKind.OnUseItem, 0, 0);
            GraphNode yaw = builder.Add(GraphNodeKind.CameraYaw, 0, 200);
            GraphNode round = builder.Add(GraphNodeKind.RoundDecimal, 200, 200);
            GraphNode text = builder.Add(GraphNodeKind.NumberToText, 300, 200);
            GraphNode message = builder.Add(GraphNodeKind.SendMessage, 400, 0);
            builder.Data(use, "player", yaw, "player").Data(yaw, "yaw", round, "a").Data(round, "value", text, "number").Data(text, "text", message, "message")
                .Data(use, "player", message, "player").Exec(use, message);
            builder.Commit();
        });

        Assert.Contains("getYRot()", File.ReadAllText(Path.Combine(java, "behavior/LooksBehavior.java")));
        Assert.False(Directory.Exists(Path.Combine(java, "network")));
        Assert.DoesNotContain("MineEngineCamera", File.ReadAllText(Directory.GetFiles(java, "*.java").Single(f => File.ReadAllText(f).Contains("@Mod(", StringComparison.Ordinal))));
    }

    /// <summary>Generates the Java sources of a project and gives the folder of its package.</summary>
    private static async Task<string> GenerateAsync(TemporaryDirectory temp, ModLoaderKind loader, string version, Action<ModProject> addContent)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        addContent(project);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
    }
}
