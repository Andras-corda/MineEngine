using System.Text.Json.Nodes;
using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Models;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

public sealed class BlockbenchTests
{
    [Fact]
    public void Blockbench_file_is_read_with_its_bones_cubes_and_embedded_texture()
    {
        using var temp = new TemporaryDirectory();
        BlockbenchModel model = BlockbenchModel.Load(BlockbenchSample.CreateFile(temp.Path));

        Assert.Equal("modded_entity", model.Format);
        Assert.Equal((64, 32), (model.TextureWidth, model.TextureHeight));
        Assert.Equal(["body", "leg0", "leg1", "leg2", "leg3"], model.Groups.Select(g => g.Name));
        Assert.Equal(["head", "tail"], model.Groups[0].Children.Select(g => g.Name));
        Assert.Equal(9, model.Cubes.Count());
        Assert.Equal(["marker"], model.SkippedElements);
        BlockbenchTexture texture = Assert.Single(model.Textures);
        Assert.Equal("cat_coat", texture.Stem);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], texture.Png![..4]);
        Assert.True(model.UsesBoxUv);
    }

    [Fact]
    public void Blockbench_model_becomes_a_java_block_model_centered_in_the_block()
    {
        using var temp = new TemporaryDirectory();
        BlockbenchModel model = BlockbenchModel.Load(BlockbenchSample.CreateFile(temp.Path));
        var problems = new List<string>();
        JsonObject java = model.ToJavaModel(t => "testmod:block/model/cat_" + t.Index, problems);

        Assert.Equal("testmod:block/model/cat_0", java["textures"]!["0"]!.GetValue<string>());
        Assert.Equal("testmod:block/model/cat_0", java["textures"]!["particle"]!.GetValue<string>());
        JsonArray elements = java["elements"]!.AsArray();
        Assert.Equal(9, elements.Count);

        // Entity models are centered on the origin: the body is moved to the middle of the block.
        JsonObject body = elements.Single(e => e!["name"]!.GetValue<string>() == "body")!.AsObject();
        Assert.Equal([6.0, 6.0, 0.0], body["from"]!.AsArray().Select(v => v!.GetValue<double>()));

        // Box UV: north face of the body (4 x 4 x 16 at 20,0), scaled from 64 x 32 to 16 x 16.
        JsonArray north = body["faces"]!["north"]!["uv"]!.AsArray();
        Assert.Equal([9.0, 8.0, 10.0, 10.0], north.Select(v => v!.GetValue<double>()));
        Assert.Equal("#0", body["faces"]!["north"]!["texture"]!.GetValue<string>());

        JsonObject whisker = elements.Single(e => e!["name"]!.GetValue<string>() == "whisker")!.AsObject();
        Assert.Equal(22.5, whisker["rotation"]!["angle"]!.GetValue<double>());
        Assert.Equal("y", whisker["rotation"]!["axis"]!.GetValue<string>());
        Assert.NotNull(java["display"]);

        // The tail group is turned: a Java block model cannot keep it.
        Assert.Contains(problems, p => p.Contains("groups", StringComparison.Ordinal));
    }

    [Fact]
    public void Texture_bindings_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        var serializer = new AssetSerializer(AssetCatalog.CreateDefault());
        var repository = new ProjectRepository(serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod"));
        var coat = new TextureAsset(Guid.NewGuid(), ResourceId.Parse("coat"), "coat", "Textures/coat.png");
        project.Assets.Add(coat);
        ModelAsset model = BlockbenchSample.AddTo(project, [coat, coat]);
        model.SetSlotTexture("0", coat.Id);
        repository.Save(project);

        ModProject reopened = repository.Open(project.Layout.RootDirectory);
        var loaded = (ModelAsset)reopened.Assets.Find(model.Id)!;
        Assert.Equal(model.Slots, loaded.Slots);
        Assert.Equal(model.Variants, loaded.Variants);
        Assert.Equal(model.Layers, loaded.Layers);
        Assert.Equal("cat_coat.png", loaded.Slots[0].Name);
        Assert.Equal(model.Id, ((MobAsset)reopened.Assets.FindByResourceId(ResourceId.Parse("alley_cat"), Core.Assets.AssetType.Mob)!).CustomModelId);
        Assert.Contains(reopened.Assets.FindReferencesTo(model.Id), r => r.Source is BlockAsset);
    }

    [Fact]
    public void A_mob_needs_a_blockbench_model_and_a_block_needs_a_model()
    {
        var registry = new AssetRegistry();
        var json = new ModelAsset(Guid.NewGuid(), ResourceId.Parse("altar"), "Altar", "Models/altar.json");
        registry.Add(json);
        registry.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("golem"), "Golem") { Model = MobModel.Custom, CustomModelId = json.Id });
        registry.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("pedestal"), "Pedestal") { Model = BlockModelKind.Custom });
        registry.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("relic"), "Relic") { CustomModelId = json.Id });

        var diagnostics = new DiagnosticBag();
        registry.Validate(diagnostics);
        Diagnostic[] errors = [.. diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)];

        Assert.Contains(errors, d => d.Message.Contains("Blockbench", StringComparison.Ordinal));
        Assert.Contains(errors, d => d.Message.Contains("choose the model of the block", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Message.Contains("Relic", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Error);

        // An item with a 3D model needs no texture of its own.
        Assert.DoesNotContain(diagnostics, d => d.Source.Contains("relic", StringComparison.Ordinal) && d.Message.Contains("texture", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mob_item_and_block_are_generated_with_the_blockbench_model(bool modern)
    {
        using var temp = new TemporaryDirectory();
        var serializer = new AssetSerializer(AssetCatalog.CreateDefault());
        var repository = new ProjectRepository(serializer, new ProjectSettingsSerializer());
        GeneratorTests.FakeBackend backend = modern
            ? new GeneratorTests.FakeBackend(ModLoaderKind.NeoForge, "1.21.1", new NeoForgeJavaEmitter())
            : new GeneratorTests.FakeBackend(ModLoaderKind.Forge, "1.20.1", new ForgeJavaEmitter());
        var settings = new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = backend.LoaderId, MinecraftVersion = backend.MinecraftVersion.Id };
        ModProject project = repository.Create(temp.Path, settings);
        var coat = new TextureAsset(Guid.NewGuid(), ResourceId.Parse("coat"), "coat", "Textures/coat.png");
        Directory.CreateDirectory(project.Layout.TexturesDirectory);
        File.WriteAllBytes(Path.Combine(project.Layout.TexturesDirectory, "coat.png"), new Minecraft.Resources.Imaging.PlaceholderTexture(new Minecraft.Resources.Imaging.PngEncoder()).GetPngBytes());
        project.Assets.Add(coat);
        BlockbenchSample.AddTo(project, [coat, coat]);

        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.True(ir is not null, string.Join('\n', diagnostics));
        IRModel model = Assert.Single(ir.Content.Models);
        Assert.Equal(2, model.Variants.Count);
        Assert.NotNull(Assert.Single(model.Textures).Texture.Png);

        string workspace = Path.Combine(temp.Path, "workspace");
        GenerationResult result = await ModGenerator.CreateDefault(backend)
            .GenerateAsync(ir, workspace, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string Read(string relative) => File.ReadAllText(Path.Combine(workspace, relative));
        string java = Path.Combine(workspace, "src/main/java/com/mineengine/mods/testmod");
        string assets = "src/main/resources/assets/testmod";

        Assert.Equal("testmod:custom/cat", JsonNode.Parse(Read($"{assets}/models/item/cat_figurine.json"))!["parent"]!.GetValue<string>());
        Assert.Equal("testmod:custom/cat", JsonNode.Parse(Read($"{assets}/models/block/cat_statue.json"))!["parent"]!.GetValue<string>());
        Assert.Equal("testmod:block/model/cat_0", JsonNode.Parse(Read($"{assets}/models/custom/cat.json"))!["textures"]!["0"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(workspace, $"{assets}/textures/block/model/cat_0.png")));
        Assert.True(File.Exists(Path.Combine(workspace, $"{assets}/textures/entity/model/cat_variant_2.png")));
        Assert.True(File.Exists(Path.Combine(workspace, $"{assets}/textures/entity/model/cat_layer_2.png")));

        string entityModel = File.ReadAllText(Path.Combine(java, "client/model/CatModel.java"));
        Assert.Contains("public class CatModel<T extends Entity> extends EntityModel<T>", entityModel);
        Assert.Contains("PartDefinition head = body.addOrReplaceChild(\"head\"", entityModel);
        Assert.Contains("head.addOrReplaceChild(\"head_r1\"", entityModel);
        Assert.Contains("LayerDefinition.create(mesh, 64, 32)", entityModel);
        Assert.Contains("this.head.yRot = 0.0f + netHeadYaw * Mth.DEG_TO_RAD;", entityModel);
        Assert.Contains(modern ? "int packedOverlay, int color)" : "float red, float green, float blue, float alpha)", entityModel);

        string renderer = File.ReadAllText(Path.Combine(java, "client/AlleyCatRenderer.java"));
        Assert.Contains("return TEXTURES[Math.floorMod(entity.getVariant(), TEXTURES.length)];", renderer);
        Assert.Contains("RenderType.eyes(this.texture)", renderer);
        Assert.Contains("new TextureLayer(this, LAYER_2, true)", renderer);

        string entity = File.ReadAllText(Path.Combine(java, "entity/AlleyCatEntity.java"));
        Assert.Contains("this.setVariant(this.random.nextInt(3));", entity);
        Assert.Contains(modern ? "builder.define(VARIANT, 0);" : "this.entityData.define(VARIANT, 0);", entity);

        string renderers = File.ReadAllText(Path.Combine(java, "client/ModEntityRenderers.java"));
        Assert.Contains("event.registerLayerDefinition(com.mineengine.mods.testmod.client.model.CatModel.LAYER_LOCATION", renderers);
    }
}
