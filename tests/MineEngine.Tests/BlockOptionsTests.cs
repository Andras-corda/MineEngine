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
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Advanced settings of the blocks (rotation, water, shape, states, redstone, fire, ore...).</summary>
public sealed class BlockOptionsTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static (ModProject Project, IReadOnlyList<BlockAsset> Blocks) CreateProject(TemporaryDirectory temp, string loader = "neoforge", string version = "1.21.1")
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader, MinecraftVersion = version });
        SampleContent.AddTo(project);
        return (project, project.Assets.OfType<BlockAsset>().Where(b => b.Options != BlockOptions.Default).ToList());
    }

    [Fact]
    public void The_options_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, IReadOnlyList<BlockAsset> blocks) = CreateProject(temp);
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        repository.Save(project);

        ModProject reopened = repository.Open(project.Layout.RootDirectory);

        foreach (BlockAsset original in blocks)
        {
            BlockOptions read = ((BlockAsset)reopened.Assets.Find(original.Id)!).Options;
            Assert.Equal(original.Options.ToJson()!.ToJsonString(), read.ToJson()!.ToJsonString());
        }
    }

    [Fact]
    public void Unreadable_options_give_the_default_ones()
    {
        Assert.Equal(BlockOptions.Default, BlockOptions.FromJson(JsonNode.Parse("""{ "Rotation": 42 }""")));
        Assert.Equal(BlockOptions.Default, BlockOptions.FromJson(null));
        Assert.Equal(0, BlockOptions.FromJson(new BlockOptions { CustomLightOpacity = true, LightOpacity = 0 }.ToJson()).LightOpacity);
    }

    [Theory]
    [InlineData("east", 12, 0, 0, 16, 8, 16)]
    [InlineData("south", 0, 0, 12, 16, 8, 16)]
    [InlineData("west", 0, 0, 0, 4, 8, 16)]
    public void A_box_drawn_for_the_north_turns_with_the_block(string direction, double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        BlockBox turned = BlockJava.Turn(new BlockBox(0, 0, 0, 16, 8, 4), direction);

        Assert.Equal(new BlockBox(minX, minY, minZ, maxX, maxY, maxZ), turned);
    }

    [Fact]
    public void Wrong_settings_are_reported()
    {
        var block = new BlockAsset(Guid.NewGuid(), ResourceId.Parse("bad"), "bad")
        {
            Options = new BlockOptions
            {
                Properties = [new BlockCustomProperty("Bad Name", BlockPropertyKind.Boolean), new BlockCustomProperty("facing", BlockPropertyKind.Boolean),
                    new BlockCustomProperty("color", BlockPropertyKind.Choice) { Values = ["red"] }],
                Boxes = [new BlockBox(8, 0, 0, 4, 16, 16)],
                Ore = new BlockOreGeneration { Enabled = true, MinHeight = 50, MaxHeight = 10 },
            },
        };
        var diagnostics = new DiagnosticBag();

        block.Validate(diagnostics);

        Assert.True(diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error) >= 5, string.Join('\n', diagnostics));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_options_become_java_and_resources(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, _) = CreateProject(temp, loader.ToId(), version);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        var features = new MineEngine.Minecraft.Resources.SpawnBiomeModifierEmitter(loader == ModLoaderKind.NeoForge ? "neoforge" : "forge");
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter, features))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
        string resources = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources");

        string machine = File.ReadAllText(Path.Combine(java, "block/RuneMachineBlock.java"));
        Assert.Contains("extends Block implements SimpleWaterloggedBlock, BeaconBeamBlock", machine);
        Assert.Contains("BlockStateProperties.ATTACH_FACE", machine);
        Assert.Contains("IntegerProperty.create(\"charge\", 0, 3)", machine);
        Assert.Contains("ModBlocks.RUNE_MACHINE_APPEARANCE", machine);
        Assert.Contains("BooleanOp.ONLY_FIRST", machine);
        Assert.Contains("return 9;", machine);
        string blocks = File.ReadAllText(Path.Combine(java, "registry/ModBlocks.java"));
        Assert.Contains("new RuneMachineBlock(", blocks);
        Assert.Contains(".mapColor(MapColor.COLOR_PURPLE)", blocks);
        Assert.Contains("FallingBlock", File.ReadAllText(Path.Combine(java, "block/StarSandBlock.java")));

        JsonObject states = JsonNode.Parse(File.ReadAllText(Path.Combine(resources, "assets/testmod/blockstates/rune_machine.json")))!["variants"]!.AsObject();
        Assert.Equal(24, states.Count); // 3 faces x 4 facings x 2 appearances
        Assert.Contains("face=ceiling,facing=east,appearance=1", states.Select(p => p.Key));
        Assert.Equal("minecraft:item/generated", JsonNode.Parse(File.ReadAllText(Path.Combine(resources, "assets/testmod/models/item/rune_machine.json")))!["parent"]!.GetValue<string>());
        string data = Path.Combine(resources, "data/testmod");
        Assert.True(File.Exists(Path.Combine(data, "worldgen/configured_feature/moon_ore_ore.json")));
        Assert.Contains("trapezoid", File.ReadAllText(Path.Combine(data, "worldgen/placed_feature/moon_ore_ore.json")));
        Assert.Contains(Directory.GetFiles(resources, "moon_ore_ore.json", SearchOption.AllDirectories), f => f.Contains("biome_modifier", StringComparison.Ordinal));
        Assert.Contains("testmod:rune_glass", File.ReadAllText(Directory.GetFiles(Path.Combine(resources, "data/minecraft/tags"), "climbable.json", SearchOption.AllDirectories).Single()));

        // V0.7.11: the ore drops only with a diamond pickaxe, and only where its function says yes.
        string ore = File.ReadAllText(Path.Combine(java, "block/MoonOreBlock.java"));
        Assert.Contains("public List<ItemStack> getDrops(BlockState state, LootParams.Builder builder)", ore);
        Assert.Contains("return com.mineengine.mods.testmod.code.OreCanDrop.oreCanDrop(level, position) ? super.getDrops(state, builder) : List.of();", ore);
        JsonNode loot = JsonNode.Parse(File.ReadAllText(Directory.GetFiles(data, "moon_ore.json", SearchOption.AllDirectories).Single(f => f.Contains("loot", StringComparison.Ordinal))))!;
        JsonNode tool = loot["pools"]![0]!["conditions"]![1]!;
        Assert.Equal("minecraft:match_tool", tool["condition"]!.GetValue<string>());
        Assert.Equal("minecraft:diamond_pickaxe", tool["predicate"]!["items"]![0]!.GetValue<string>());
    }

    [Fact]
    public void The_tool_and_the_condition_of_the_drops_are_saved()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, _) = CreateProject(temp);
        BlockAsset ore = project.Assets.OfType<BlockAsset>().Single(b => b.ResourceId.Value == "moon_ore");

        var read = (BlockAsset)Serializer.Deserialize(Serializer.Serialize(ore), "moon_ore");

        Assert.Equal("minecraft:diamond_pickaxe", read.RequiredTool!.ToStorageText());
        Assert.Equal(ore.DropCondition, read.DropCondition);
        Assert.Equal(Serializer.Serialize(ore), Serializer.Serialize(read));
    }
}
