using System.Text.Json.Nodes;
using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
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

/// <summary>Block bases of V0.7.11 (block.md): stairs, slab, fence, door... made by the classes of the game.</summary>
public sealed class BlockBaseTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static ModProject CreateProject(TemporaryDirectory temp, ModLoaderKind loader, string version)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(Core.Identifiers.ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        SampleContent.AddTo(project);
        return project;
    }

    [Fact]
    public void A_base_ignores_the_shape_settings_of_a_plain_block()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        BlockAsset stairs = project.Assets.OfType<BlockAsset>().Single(b => b.ResourceId.Value == "oak_steps");
        var diagnostics = new DiagnosticBag();

        stairs.Validate(diagnostics);
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, new DiagnosticBag())!;

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Message.Contains("block base", StringComparison.Ordinal));
        IRBlock lowered = ir.Content.Blocks.Single(b => b.Id.Value == "oak_steps");
        Assert.Equal(BlockRotation.None, lowered.Options.Rotation);
        Assert.Equal(20, lowered.Options.Flammability);
        Assert.Equal(BlockRenderType.Cutout, ir.Content.Blocks.Single(b => b.Id.Value == "rune_door").Settings.RenderType);
        Assert.Equal(BlockModelKind.Door, ((BlockAsset)Serializer.Deserialize(Serializer.Serialize(project.Assets.OfType<BlockAsset>().Single(b => b.ResourceId.Value == "rune_door")), "rune_door")).Model);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_bases_become_the_blocks_of_the_game(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, loader, version);
        bool modern = version != "1.20.1";
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
        string resources = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources");
        string assets = Path.Combine(resources, "assets/testmod");

        string blocks = File.ReadAllText(Path.Combine(java, "registry/ModBlocks.java"));
        Assert.Contains("new FenceBlock(BlockBehaviour.Properties.of()", blocks);
        Assert.Contains("new RuneWallBlock(", blocks);
        Assert.Contains("new IronBarsBlock(", blocks);
        Assert.Contains(modern ? "new DoorBlock(BlockSetType.OAK, BlockBehaviour.Properties.of()" : "new DoorBlock(BlockBehaviour.Properties.of()", blocks);
        Assert.Contains(modern ? "new FenceGateBlock(WoodType.OAK, " : ", WoodType.OAK)", blocks);
        Assert.Contains(modern ? "new ButtonBlock(BlockSetType.OAK, 30, " : ", BlockSetType.OAK, 30, true)", blocks);
        Assert.Contains(modern ? "new PressurePlateBlock(BlockSetType.IRON, " : "new PressurePlateBlock(PressurePlateBlock.Sensitivity.MOBS, ", blocks);
        Assert.Contains("new OakStepsBlock(", blocks);
        string stairs = File.ReadAllText(Path.Combine(java, "block/OakStepsBlock.java"));
        Assert.Contains("public class OakStepsBlock extends StairBlock", stairs);
        Assert.Contains("super(Blocks.STONE.defaultBlockState(), properties);", stairs);
        Assert.Contains("getFlammability", stairs);
        Assert.DoesNotContain("FACING", stairs);
        Assert.Contains("new DoubleHighBlockItem(ModBlocks.RUNE_DOOR.get(), ", File.ReadAllText(Path.Combine(java, "registry/ModItems.java")));

        JsonObject State(string id) => JsonNode.Parse(File.ReadAllText(Path.Combine(assets, $"blockstates/{id}.json")))!.AsObject();
        Assert.Equal(40, State("oak_steps")["variants"]!.AsObject().Count);
        Assert.Equal(270, State("oak_steps")["variants"]!["facing=east,half=bottom,shape=outer_left"]!["y"]!.GetValue<int>());
        Assert.Equal(32, State("rune_door")["variants"]!.AsObject().Count);
        Assert.Equal(90, State("rune_door")["variants"]!["facing=east,half=lower,hinge=left,open=true"]!["y"]!.GetValue<int>());
        Assert.Equal(24, State("rune_button")["variants"]!.AsObject().Count);
        Assert.Equal(16, State("rune_gate")["variants"]!.AsObject().Count);
        Assert.Equal(16, State("rune_hatch")["variants"]!.AsObject().Count);
        Assert.Equal(5, State("rune_fence")["multipart"]!.AsArray().Count);
        Assert.Equal(9, State("rune_wall")["multipart"]!.AsArray().Count);
        Assert.Equal(9, State("rune_pane")["multipart"]!.AsArray().Count);
        Assert.Equal(3, State("rune_slab")["variants"]!.AsObject().Count);
        Assert.Equal(2, State("rune_plate")["variants"]!.AsObject().Count);

        JsonObject Model(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(assets, file)))!.AsObject();
        Assert.Equal("minecraft:block/door_bottom_left_open", Model("models/block/rune_door_bottom_left_open.json")["parent"]!.GetValue<string>());
        Assert.Equal("minecraft:cutout", Model("models/block/rune_door_bottom_left_open.json")["render_type"]!.GetValue<string>());
        Assert.Equal("testmod:block/rune_fence_inventory", Model("models/item/rune_fence.json")["parent"]!.GetValue<string>());
        Assert.Equal("minecraft:item/generated", Model("models/item/rune_door.json")["parent"]!.GetValue<string>());

        string data = Path.Combine(resources, "data");
        string Loot(string id) => File.ReadAllText(Directory.GetFiles(Path.Combine(data, "testmod"), id + ".json", SearchOption.AllDirectories).Single(f => f.Contains("loot", StringComparison.Ordinal)));
        Assert.Contains("\"double\"", Loot("rune_slab"));
        Assert.Contains("\"lower\"", Loot("rune_door"));
        string Tag(string name) => File.ReadAllText(Directory.GetFiles(Path.Combine(data, "minecraft/tags"), name + ".json", SearchOption.AllDirectories).Single(f => f.Contains("block", StringComparison.Ordinal)));
        Assert.Contains("testmod:rune_fence", Tag("wooden_fences"));
        Assert.Contains("testmod:rune_wall", Tag("walls"));
        Assert.Contains("testmod:rune_plate", Tag("stone_pressure_plates"));
        Assert.Contains("testmod:rune_door", Tag("wooden_doors"));

        // Step 3: the events of the slab are methods of its class, on top of the slab of the game.
        Assert.Contains("public static void onBlockRightClicked1(Level level, BlockPos position, Player player, Entity other, double amount)",
            File.ReadAllText(Path.Combine(java, "behavior/RuneEventsBehavior.java")));
        string slab = File.ReadAllText(Path.Combine(java, "block/RuneSlabBlock.java"));
        Assert.Contains("public class RuneSlabBlock extends SlabBlock", slab);
        Assert.Contains("this.registerDefaultState(this.defaultBlockState().setValue(REDSTONE_POWERED, false));", slab);
        Assert.Contains(modern
            ? "public InteractionResult useWithoutItem(BlockState state, Level level, BlockPos position, Player player, BlockHitResult hit)"
            : "public InteractionResult use(BlockState state, Level level, BlockPos position, Player player, InteractionHand hand, BlockHitResult hit)", slab);
        Assert.Contains("RuneEventsBehavior.onBlockRightClicked1(level, position, player, player, 0.0);", slab);
        Assert.Contains("level.scheduleTick(position, this, 20);", slab);
        Assert.Contains("public void tick(BlockState state, ServerLevel level, BlockPos position, RandomSource random)", slab);
        Assert.Contains("RuneEventsBehavior.onBlockRedstoneOn1(level, position, null, null, level.getBestNeighborSignal(position));", slab);
        Assert.Contains("public void onRemove(BlockState state, Level level, BlockPos position, BlockState newState, boolean moved)", slab);
        Assert.Contains("RuneEventsBehavior.onBlockPlayerWalksOn1(level, position, (Player) entity, entity, 0.0);", slab);
        Assert.Contains("RuneEventsBehavior.onBlockEntityFallsOn1(level, position, (entity instanceof Player ? (Player) entity : null), entity, distance);", slab);
        Assert.Contains("BlockPos position = hit.getBlockPos();", slab);
        Assert.Contains("new RuneSlabBlock(", blocks);
        Assert.Contains("RuneEventsBehavior.onBlockPlaced2(level, position, (placer instanceof Player ? (Player) placer : null), placer, 0.0);",
            File.ReadAllText(Path.Combine(java, "block/RuneWallBlock.java")));
    }

    [Fact]
    public void A_tick_event_of_a_block_that_does_not_tick_is_reported()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        BlockAsset slab = project.Assets.OfType<BlockAsset>().Single(b => b.ResourceId.Value == "rune_slab");
        slab.Options = slab.Options with { TickEnabled = false };
        var diagnostics = new DiagnosticBag();

        ModIRBuilder.CreateDefault().Build(project, diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("does not tick", StringComparison.Ordinal));
    }
}
