using System.Text.Json.Nodes;
using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Json;
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

/// <summary>Options d'items et de blocs inspirées de MCreator : sérialisation et génération.</summary>
public sealed class ContentOptionsTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    [Fact]
    public void All_item_and_block_options_survive_a_save()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path, "neoforge", "1.21.1");

        foreach (Asset original in project.Assets)
        {
            Asset copy = Serializer.Deserialize(Serializer.Serialize(original), "test");
            Assert.Equal(Serializer.Serialize(original), Serializer.Serialize(copy));
        }

        var ore = (BlockAsset)Serializer.Deserialize(Serializer.Serialize(Find(project, "ruby_ore")), "test");
        Assert.Equal(HarvestTool.Pickaxe, ore.HarvestTool);
        Assert.Equal(ToolTier.Iron, ore.ToolTier);
        Assert.Equal(3, ore.DropMax);

        var sword = (ItemAsset)Serializer.Deserialize(Serializer.Serialize(Find(project, "magic_sword")), "test");
        Assert.Equal(["Forgée dans les étoiles", "Ne pas laisser aux enfants"], sword.TooltipLines);
        Assert.Contains(CreativeTab.Combat, sword.CreativeTabs);
    }

    [Fact]
    public void Old_asset_files_without_the_new_options_are_still_readable()
    {
        const string oldBlock = """
            {"formatVersion":1,"guid":"3f9c6a1e-8d2b-4b7e-9f10-2c5d7a0be21a","type":"Block","resourceId":"old",
             "displayName":"Ancien","properties":{"hardness":2,"resistance":4,"texture":null}}
            """;
        AssetMigrationResult migration = new AssetFormatMigrator().Migrate(
            [new AssetDocument(JsonFormatting.ParseObject(oldBlock, "old"), "old")]);
        var block = (BlockAsset)Serializer.Deserialize(migration.Documents[0].Root, "old");

        Assert.Equal(2f, block.Hardness);
        Assert.Equal(BlockModelKind.Cube, block.Model);
        Assert.Equal(BlockDropKind.Self, block.DropKind);
        Assert.True(block.HasItemForm);
        Assert.True(block.InModCreativeTab);
    }

    [Fact]
    public async Task Modern_versions_generate_the_new_java_options()
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateAsync(temp.Path, ModLoaderKind.NeoForge, "1.21.1", new NeoForgeJavaEmitter());
        string Read(string relative) => File.ReadAllText(Path.Combine(workspace, relative));

        string items = Read("src/main/java/com/mineengine/mods/testmod/registry/ModItems.java");
        Assert.Contains(".durability(250)", items);
        Assert.DoesNotContain(".durability(250)\n                .stacksTo", items);
        Assert.Contains(".rarity(Rarity.RARE)", items);
        Assert.Contains(".fireResistant()", items);
        Assert.Contains("public boolean isFoil(ItemStack stack)", items);
        Assert.Contains("Item.TooltipContext context", items);
        Assert.Contains("tooltip.add(Component.translatable(\"tooltip.testmod.magic_sword.1\"));", items);
        Assert.Contains(".food(new FoodProperties.Builder().nutrition(8).saturationModifier(0.8f).alwaysEdible().build())", items);

        string blocks = Read("src/main/java/com/mineengine/mods/testmod/registry/ModBlocks.java");
        Assert.Contains("new DropExperienceBlock(UniformInt.of(2, 5), BlockBehaviour.Properties.of()", blocks);
        Assert.Contains("new RotatedPillarBlock(BlockBehaviour.Properties.of()", blocks);
        Assert.Contains(".lightLevel(state -> 15)", blocks);
        Assert.Contains(".strength(-1.0f, 3600000.0f)", blocks);
        Assert.Contains(".noCollission()", blocks);
        Assert.Contains(".friction(0.98f)", blocks);

        string tabs = Read("src/main/java/com/mineengine/mods/testmod/registry/ModCreativeTabs.java");
        Assert.Contains("if (event.getTabKey() == CreativeModeTabs.COMBAT)", tabs);
        Assert.Contains("event.accept(ModItems.MAGIC_SWORD.get());", tabs);
        Assert.DoesNotContain("SLIPPERY_LAMP", Read("src/main/java/com/mineengine/mods/testmod/registry/ModItems.java"));

        Assert.Contains("testmod:cooked_beast", Read("src/main/resources/data/minecraft/tags/item/meat.json"));
        Assert.Contains("testmod:ruby_ore", Read("src/main/resources/data/minecraft/tags/block/mineable/pickaxe.json"));
        Assert.Contains("testmod:ruby_ore", Read("src/main/resources/data/minecraft/tags/block/needs_iron_tool.json"));
        Assert.Contains("testmod:star_log", Read("src/main/resources/data/minecraft/tags/block/mineable/axe.json"));

        JsonNode flower = JsonNode.Parse(Read("src/main/resources/assets/testmod/models/block/star_flower.json"))!;
        Assert.Equal("minecraft:block/cross", flower["parent"]!.GetValue<string>());
        Assert.Equal("minecraft:cutout", flower["render_type"]!.GetValue<string>());

        JsonNode log = JsonNode.Parse(Read("src/main/resources/assets/testmod/blockstates/star_log.json"))!;
        Assert.Equal(90, log["variants"]!["axis=x"]!["x"]!.GetValue<int>());

        JsonNode loot = JsonNode.Parse(Read("src/main/resources/data/testmod/loot_table/blocks/ruby_ore.json"))!;
        JsonNode entry = loot["pools"]![0]!["entries"]![0]!;
        Assert.Equal("testmod:ruby", entry["name"]!.GetValue<string>());
        Assert.Equal(3, entry["functions"]![0]!["count"]!["max"]!.GetValue<int>());
        Assert.False(File.Exists(Path.Combine(workspace, "src/main/resources/data/testmod/loot_table/blocks/slippery_lamp.json")));

        JsonNode lang = JsonNode.Parse(Read("src/main/resources/assets/testmod/lang/fr_fr.json"))!;
        Assert.Equal("Brille dans le noir", lang["tooltip.testmod.star_flower.0"]!.GetValue<string>());
    }

    [Fact]
    public async Task Minecraft_1_20_1_uses_the_older_api_and_plural_tag_folders()
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateAsync(temp.Path, ModLoaderKind.Forge, "1.20.1", new ForgeJavaEmitter());
        string Read(string relative) => File.ReadAllText(Path.Combine(workspace, relative));

        string items = Read("src/main/java/com/mineengine/mods/testmod/registry/ModItems.java");
        Assert.Contains(".saturationMod(0.8f).meat().alwaysEat()", items);
        Assert.Contains("Level level, List<Component> tooltip", items);

        string blocks = Read("src/main/java/com/mineengine/mods/testmod/registry/ModBlocks.java");
        Assert.Contains(", UniformInt.of(2, 5))", blocks);

        Assert.True(File.Exists(Path.Combine(workspace, "src/main/resources/data/minecraft/tags/blocks/mineable/pickaxe.json")));
        Assert.False(Directory.Exists(Path.Combine(workspace, "src/main/resources/data/minecraft/tags/items")));
    }

    private static Asset Find(ModProject project, string id) => project.Assets.FindByResourceId(ResourceId.Parse(id), AssetType.Item)!;

    private static ModProject CreateProject(string directory, string loaderId, string minecraftVersion)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        var settings = new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loaderId, MinecraftVersion = minecraftVersion };
        ModProject project = repository.Create(directory, settings);
        SampleContent.AddTo(project);
        return project;
    }

    private static async Task<string> GenerateAsync(string directory, ModLoaderKind loader, string version, IFileEmitter javaEmitter)
    {
        ModProject project = CreateProject(directory, loader.ToId(), version);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.NotNull(ir);

        string workspace = Path.Combine(directory, "workspace");
        GenerationResult result = await ModGenerator.CreateDefault(new TestBackend(loader, version, javaEmitter))
            .GenerateAsync(ir, workspace, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return workspace;
    }

    private sealed class TestBackend : IModLoaderBackend
    {
        private readonly IFileEmitter _javaEmitter;

        public TestBackend(ModLoaderKind loader, string version, IFileEmitter javaEmitter)
        {
            _javaEmitter = javaEmitter;
            Mdk = new MdkDescriptor("test", loader, MinecraftVersion.Parse(version), "0", 21, null, Path.GetTempPath(), "test", DateTime.Now);
        }

        public string LoaderId => Mdk.Loader.ToId();

        public string DisplayName => Mdk.DisplayName;

        public MdkDescriptor Mdk { get; }

        public MinecraftVersion MinecraftVersion => Mdk.MinecraftVersion;

        public IReadOnlyList<string> GeneratedSourceRoots => ["src/main/java", "src/main/resources"];

        public IReadOnlyList<string> BuildTasks => ["build"];

        public IReadOnlyList<string> RunClientTasks => ["runClient"];

        public Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<IFileEmitter> CreateEmitters() => [_javaEmitter];

        public string GetOutputJarPath(string workspaceDirectory, IRModInfo mod) => string.Empty;
    }
}
