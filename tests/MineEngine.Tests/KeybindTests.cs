using MineEngine.Assets;
using MineEngine.Assets.Definitions;
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

/// <summary>Keybinds: mouse buttons, triggers (released, held, pulse, held then released, combo) and their settings.</summary>
public sealed class KeybindTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    [Fact]
    public void The_settings_of_a_keybind_survive_a_save()
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod"));
        SampleContent.AddTo(project);
        repository.Save(project);

        KeybindAsset charge = repository.Open(project.Layout.RootDirectory).Assets.OfType<KeybindAsset>().Single(k => k.ResourceId.Value == "charge");

        Assert.True(charge.Key.IsMouse);
        Assert.Equal(1, charge.Key.Code);
        Assert.Equal(15, charge.HoldTicks);
        Assert.Equal(5, charge.PulseTicks);
        Assert.Equal("LEFT_CONTROL", charge.ComboKey);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1", "ClientTickEvent.Post")]
    [InlineData(ModLoaderKind.Forge, "1.21.1", "TickEvent.ClientTickEvent.Post")]
    [InlineData(ModLoaderKind.Forge, "1.20.1", "event.phase != TickEvent.Phase.END")]
    public async Task The_client_sends_only_the_events_the_graphs_use(ModLoaderKind loader, string version, string tick)
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        SampleContent.AddTo(project);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");

        string keybinds = File.ReadAllText(Path.Combine(java, "client/ModKeybinds.java"));
        Assert.Contains("InputConstants.Type.MOUSE, 1,", keybinds);
        Assert.Contains(tick, keybinds);
        // "dash" only has "On key pressed" (1); "charge" has the five other triggers (2 + 4 + 8 + 16 + 32).
        Assert.Contains("\"dash\", free, 20, 10, 340, false, 1);", keybinds);
        Assert.Contains("\"charge\", free, 15, 5, 341, false, 62);", keybinds);

        string commands = File.ReadAllText(Path.Combine(java, "registry/ModCommands.java"));
        Assert.Contains("case \"hold_released\" -> {", commands);
        Assert.Contains("(player, level, position, seconds, number);", commands);
    }
}
