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

/// <summary>Creative tabs: the search bar is kept in the file and written in the Java code.</summary>
public sealed class CreativeTabTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    [Fact]
    public void The_search_bar_survives_a_save()
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod"));
        SampleContent.AddTo(project);
        repository.Save(project);

        ModProject reopened = repository.Open(project.Layout.RootDirectory);

        CreativeTabAsset tab = reopened.Assets.OfType<CreativeTabAsset>().Single();
        Assert.True(tab.HasSearchBar);
        Assert.Equal(3, tab.Items.Count);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task A_tab_with_a_search_bar_asks_for_it(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        SampleContent.AddTo(project);
        project.Assets.Add(new CreativeTabAsset(Guid.NewGuid(), ResourceId.Parse("plain"), "Simple") { Items = [project.Assets.OfType<CreativeTabAsset>().First().Items[0]] });
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));

        string tabs = File.ReadAllText(Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod/registry/ModCreativeTabs.java"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(tabs, @"\.withSearchBar\(\)"));
        Assert.True(tabs.IndexOf(".withSearchBar()", StringComparison.Ordinal) > tabs.IndexOf("TAB_TREASURES", StringComparison.Ordinal), tabs);
    }
}
