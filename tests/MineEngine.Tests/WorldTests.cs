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
using MineEngine.Minecraft.Resources;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Plants and biomes (V0.7.0), tags (V0.7.1).</summary>
public sealed class WorldTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    /// <summary>The sample content of the tests, which ends with <see cref="WorldSample"/>.</summary>
    private static ModProject CreateProject(TemporaryDirectory temp, string loader = "neoforge", string version = "1.21.1")
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader, MinecraftVersion = version });
        SampleContent.AddTo(project);
        return project;
    }

    [Fact]
    public void Plants_biomes_and_tags_survive_a_save()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp);
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        repository.Save(project);

        ModProject reopened = repository.Open(project.Layout.RootDirectory);

        PlantAsset crop = reopened.Assets.OfType<PlantAsset>().Single(p => p.Kind == PlantKind.Crop);
        Assert.Equal(3, crop.StageTextures.Count);
        Assert.Equal("minecraft:wheat", crop.Produce?.ToString());
        PlantAsset flower = reopened.Assets.OfType<PlantAsset>().Single(p => p.ResourceId.Value == "moon_flower");
        Assert.Equal(["minecraft:plains", "moon_valley"], flower.Generation.Biomes);
        BiomeAsset biome = reopened.Assets.OfType<BiomeAsset>().Single();
        Assert.Equal(0x7FB8FF, biome.GrassColor);
        Assert.Null(biome.FoliageColor);
        Assert.Equal(new ClimateRange(-0.5, 0.1), biome.Weirdness);
        TagAsset common = reopened.Assets.OfType<TagAsset>().Single(t => t.Namespace == "c");
        Assert.True(common.Replace);
        Assert.Equal("#c:seeds/moon", common.Reference("testmod"));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Plants_biomes_and_tags_become_the_mod(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, loader.ToId(), version);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        var features = new SpawnBiomeModifierEmitter(loader == ModLoaderKind.NeoForge ? "neoforge" : "forge");
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter, features))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
        string resources = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources");
        bool singular = loader == ModLoaderKind.NeoForge;
        JsonNode Json(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(resources, path)))!;

        // Plants: a class per plant, the models of each kind, the loot of a crop, the patches.
        Assert.Contains("extends BushBlock", File.ReadAllText(Path.Combine(java, "block/MoonFlowerBlock.java")));
        Assert.Contains("extends DoublePlantBlock", File.ReadAllText(Path.Combine(java, "block/GiantReedBlock.java")));
        Assert.Contains("MAX_HEIGHT = 4", File.ReadAllText(Path.Combine(java, "block/StarCaneBlock.java")));
        Assert.Contains("getBaseSeedId", File.ReadAllText(Path.Combine(java, "block/MoonWheatBlock.java")));
        Assert.Contains(".randomTicks()", File.ReadAllText(Path.Combine(java, "registry/ModBlocks.java")));
        Assert.Equal(8, Json("assets/testmod/blockstates/moon_wheat.json")["variants"]!.AsObject().Count);
        Assert.Equal("testmod:block/moon_wheat_stage2", Json("assets/testmod/blockstates/moon_wheat.json")["variants"]!["age=7"]!["model"]!.GetValue<string>());
        Assert.Equal("testmod:block/giant_reed_top", Json("assets/testmod/blockstates/giant_reed.json")["variants"]!["half=upper"]!["model"]!.GetValue<string>());
        Assert.Contains("minecraft:wheat", Json($"data/testmod/{(singular ? "loot_table" : "loot_tables")}/blocks/moon_wheat.json").ToJsonString());
        Assert.Equal("minecraft:random_patch", Json("data/testmod/worldgen/configured_feature/star_cane_patch.json")["type"]!.GetValue<string>());
        Assert.Contains(singular ? "\"min_inclusive\":1" : "\"value\"", Json("data/testmod/worldgen/configured_feature/star_cane_patch.json").ToJsonString());
        Assert.False(File.Exists(Path.Combine(resources, "data/testmod/worldgen/configured_feature/moon_wheat_patch.json")));

        // Biome: its file, its injection in the overworld, its name.
        JsonNode biome = Json("data/testmod/worldgen/biome/moon_valley.json");
        Assert.Equal(0x7FB8FF, biome["effects"]!["grass_color"]!.GetValue<int>());
        Assert.Equal(11, biome["features"]!.AsArray().Count);
        string biomes = File.ReadAllText(Path.Combine(java, "world/ModBiomes.java"));
        Assert.Contains("\"testmod:moon_valley\"", biomes);
        Assert.Contains("minecraft:podzol", biomes);
        Assert.Contains("ModBiomes.register();", File.ReadAllText(Directory.GetFiles(java, "*.java").Single(f => File.ReadAllText(f).Contains("@Mod(", StringComparison.Ordinal))));

        // Tags: those of the tab, merged with the tags of the mod; folders of the version.
        string tags = Path.Combine(resources, "data");
        JsonNode plants = Json($"data/testmod/tags/{(singular ? "block" : "blocks")}/moon_plants.json");
        Assert.Contains("\"id\":\"othermod:thing\",\"required\":false", plants.ToJsonString());
        JsonNode smallFlowers = Json($"data/minecraft/tags/{(singular ? "item" : "items")}/small_flowers.json");
        Assert.Single(smallFlowers["values"]!.AsArray(), v => v!.ToString() == "testmod:moon_flower");
        Assert.True(Json($"data/c/tags/{(singular ? "item" : "items")}/seeds/moon.json")["replace"]!.GetValue<bool>());
        Assert.Contains("testmod:moon_valley", Json("data/minecraft/tags/worldgen/biome/is_overworld.json").ToJsonString());
        Assert.True(File.Exists(Path.Combine(tags, "testmod/tags/damage_type/moon.json")));
        Assert.Contains("TagKey.create(Registries.BIOME", File.ReadAllText(Path.Combine(java, "behavior/UsageTagsBehavior.java")));
    }

    [Theory]
    [InlineData(TagRegistry.Blocks, false, "blocks")]
    [InlineData(TagRegistry.Blocks, true, "block")]
    [InlineData(TagRegistry.EntityTypes, false, "entity_types")]
    [InlineData(TagRegistry.Biomes, true, "worldgen/biome")]
    [InlineData(TagRegistry.Functions, true, "function")]
    [InlineData(TagRegistry.DamageTypes, false, "damage_type")]
    public void Tag_folders_follow_the_version(TagRegistry registry, bool singular, string folder) =>
        Assert.Equal(folder, TagAsset.Folder(registry, singular));

    [Fact]
    public void Wrong_tags_are_reported()
    {
        var tag = new TagAsset(Guid.NewGuid(), ResourceId.Parse("bad"), "bad") { TagPath = "Bad Path", Values = ["Not An Id", "#ok:fine"] };
        var diagnostics = new DiagnosticBag();

        tag.Validate(diagnostics);

        Assert.Equal(2, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));
    }
}
