using System.Text.Json.Nodes;
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

/// <summary>Appearances of items, blocks and mobs, switched while the game runs by the appearance nodes.</summary>
public sealed class AppearanceTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static ModProject CreateProject(TemporaryDirectory temp, string loader, string version)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader, MinecraftVersion = version });
        SampleContent.AddTo(project);
        return project;
    }

    [Fact]
    public void Appearances_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, "neoforge", "1.21.1");
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        repository.Save(project);

        ModProject reopened = repository.Open(project.Layout.RootDirectory);
        var ruby = (ItemAsset)reopened.Assets.FindByResourceId(ResourceId.Parse("ruby"), Core.Assets.AssetType.Item)!;
        var original = (ItemAsset)project.Assets.FindByResourceId(ResourceId.Parse("ruby"), Core.Assets.AssetType.Item)!;
        Assert.Equal(original.Appearances, ruby.Appearances);
        Assert.Contains(original.GetReferences(), r => r.Role == Tr.F("appearance {0}", 2) && r.Kind == Core.Assets.AssetReferenceKind.Model);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Appearances_become_models_block_states_and_textures(ModLoaderKind loader, string version)
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

        string resources = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources/assets/testmod");
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
        JsonNode Json(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(resources, path)))!;

        // Item: one override per appearance, by custom model data.
        JsonArray overrides = Json("models/item/ruby.json")["overrides"]!.AsArray();
        Assert.Equal([1, 2], overrides.Select(o => o!["predicate"]!["custom_model_data"]!.GetValue<int>()));
        Assert.Equal("testmod:item/ruby_appearance_2", overrides[1]!["model"]!.GetValue<string>());
        Assert.StartsWith("testmod:custom/", Json("models/item/ruby_appearance_2.json")["parent"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(resources, "textures/item/ruby_appearance_1.png")));

        // Blocks: a variant per value of "appearance" (and per axis for a column).
        JsonObject oreStates = Json("blockstates/ruby_ore.json")["variants"]!.AsObject();
        Assert.Equal(["appearance=0", "appearance=1", "appearance=2"], oreStates.Select(p => p.Key));
        Assert.Equal("testmod:block/ruby_ore_appearance_1", oreStates["appearance=1"]!["model"]!.GetValue<string>());
        Assert.Contains("axis=x,appearance=2", Json("blockstates/star_log.json")["variants"]!.AsObject().Select(p => p.Key));
        string blocks = File.ReadAllText(Path.Combine(java, "registry/ModBlocks.java"));
        Assert.Contains("IntegerProperty.create(\"appearance\", 0, 2)", blocks);
        Assert.Contains("builder.add(RUBY_ORE_APPEARANCE);", blocks);

        // Mobs: their textures, numbered after the random variants of the model.
        string goblin = File.ReadAllText(Path.Combine(java, "entity/GoblinEntity.java"));
        Assert.Contains("implements MineEngineAppearance", goblin);
        Assert.Contains("this.setVariant(appearance <= 0 || appearance > 2 ? 0 : 1 + appearance - 1);", goblin);
        string cat = File.ReadAllText(Path.Combine(java, "entity/AlleyCatEntity.java"));
        Assert.Contains("this.random.nextInt(3)", cat);
        Assert.Contains("return this.getVariant() < 3 ? 0 : this.getVariant() - 3 + 1;", cat);
        Assert.True(File.Exists(Path.Combine(java, "entity/MineEngineAppearance.java")));
        Assert.True(File.Exists(Path.Combine(resources, "textures/entity/goblin_appearance_1.png")));
    }
}
