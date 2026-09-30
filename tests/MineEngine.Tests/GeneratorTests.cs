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
using MineEngine.Minecraft.Backends;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

public sealed class GeneratorTests
{
    [Fact]
    public void Project_is_lowered_to_sorted_IR_with_placeholder_textures()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateSampleProject(temp.Path, "neoforge", "1.21.1");
        var diagnostics = new DiagnosticBag();

        ModIR? ir = ModIRBuilder.CreateDefault().Build(project, diagnostics);

        Assert.NotNull(ir);
        Assert.Equal(["magic_sword", "ruby"], ir.Content.Items.Select(i => i.Id.Value));
        Assert.Equal("ruby_block", Assert.Single(ir.Content.Blocks).Id.Value);
        Assert.All(ir.Content.Items, i => Assert.True(i.Texture.IsPlaceholder));
        Assert.Equal(3, diagnostics.WarningCount);
    }

    [Fact]
    public async Task NeoForge_generation_writes_resources_and_java_sources()
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateAsync(temp.Path, new FakeBackend(ModLoaderKind.NeoForge, "1.21.1", new NeoForgeJavaEmitter()));
        string Read(string relative) => File.ReadAllText(Path.Combine(workspace, relative));

        JsonNode itemModel = JsonNode.Parse(Read("src/main/resources/assets/testmod/models/item/magic_sword.json"))!;
        Assert.Equal("testmod:item/magic_sword", itemModel["textures"]!["layer0"]!.GetValue<string>());

        JsonNode blockState = JsonNode.Parse(Read("src/main/resources/assets/testmod/blockstates/ruby_block.json"))!;
        Assert.Equal("testmod:block/ruby_block", blockState["variants"]![""]!["model"]!.GetValue<string>());

        Assert.True(File.Exists(Path.Combine(workspace, "src/main/resources/data/testmod/loot_table/blocks/ruby_block.json")));

        JsonNode lang = JsonNode.Parse(Read("src/main/resources/assets/testmod/lang/fr_fr.json"))!;
        Assert.Equal("Épée magique", lang["item.testmod.magic_sword"]!.GetValue<string>());
        Assert.Equal("Bloc de rubis", lang["block.testmod.ruby_block"]!.GetValue<string>());

        byte[] png = File.ReadAllBytes(Path.Combine(workspace, "src/main/resources/assets/testmod/textures/item/ruby.png"));
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);

        string items = Read("src/main/java/com/mineengine/mods/testmod/registry/ModItems.java");
        Assert.Contains("\"magic_sword\", new Item.Properties().stacksTo(1));", items);
        Assert.Contains("\"ruby_block\", ModBlocks.RUBY_BLOCK);", items);

        string blocks = Read("src/main/java/com/mineengine/mods/testmod/registry/ModBlocks.java");
        Assert.Contains("BlockBehaviour.Properties.of().strength(3.0f, 9.5f));", blocks);

        string main = Read("src/main/java/com/mineengine/mods/testmod/TestMod.java");
        Assert.Contains("@Mod(TestMod.MOD_ID)", main);
        Assert.Contains("public TestMod(IEventBus modEventBus)", main);
    }

    [Fact]
    public async Task Forge_1_20_1_generation_uses_forge_registries_and_plural_loot_tables()
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateAsync(temp.Path, new FakeBackend(ModLoaderKind.Forge, "1.20.1", new ForgeJavaEmitter()));
        string Read(string relative) => File.ReadAllText(Path.Combine(workspace, relative));

        Assert.True(File.Exists(Path.Combine(workspace, "src/main/resources/data/testmod/loot_tables/blocks/ruby_block.json")));

        string items = Read("src/main/java/com/mineengine/mods/testmod/registry/ModItems.java");
        Assert.Contains("import net.minecraftforge.registries.ForgeRegistries;", items);
        Assert.Contains("DeferredRegister.create(ForgeRegistries.ITEMS, TestMod.MOD_ID);", items);
        Assert.Contains("() -> new Item(new Item.Properties().stacksTo(1)));", items);
        Assert.Contains("() -> new BlockItem(ModBlocks.RUBY_BLOCK.get(), new Item.Properties()));", items);

        string main = Read("src/main/java/com/mineengine/mods/testmod/TestMod.java");
        Assert.Contains("public TestMod(FMLJavaModLoadingContext context)", main);
        Assert.Contains("IEventBus modEventBus = context.getModEventBus();", main);

        string tabs = Read("src/main/java/com/mineengine/mods/testmod/registry/ModCreativeTabs.java");
        Assert.Contains("public static final RegistryObject<CreativeModeTab> MAIN_TAB", tabs);
    }

    [Fact]
    public async Task Mismatched_mdk_is_reported_as_an_error()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateSampleProject(temp.Path, "neoforge", "1.21.1");
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, new DiagnosticBag())!;

        GenerationResult result = await ModGenerator.CreateDefault(new FakeBackend(ModLoaderKind.Forge, "1.20.1", new ForgeJavaEmitter()))
            .GenerateAsync(ir, Path.Combine(temp.Path, "workspace"), NullLog.Instance, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public void Gradle_properties_are_updated_and_escaped()
    {
        var file = new GradlePropertiesFile("# commentaire\nmod_id=examplemod\nmod_name=Example Mod\n");
        file.Set("mod_id", "testmod");
        file.Set("mod_name", "Épée");
        file.Set("mod_group_id", "com.test");

        Assert.Equal("# commentaire\nmod_id=testmod\nmod_name=\\u00c9p\\u00e9e\nmod_group_id=com.test\n", file.ToString());
    }

    [Fact]
    public void Gradle_jvm_arguments_are_added_once()
    {
        var file = new GradlePropertiesFile("org.gradle.jvmargs=-Xmx3G\nmod_id=a\n");
        file.AddJvmArgument("-Dfile.encoding=UTF-8");
        file.AddJvmArgument("-Dfile.encoding=UTF-8");

        Assert.Equal("org.gradle.jvmargs=-Xmx3G -Dfile.encoding=UTF-8\nmod_id=a\n", file.ToString());
    }

    [Fact]
    public void Mods_toml_values_are_protected_from_gradle_expansion()
    {
        string result = new ModsTomlTemplate("#authors=\"\"\n#displayURL=\"https://change.me/\" #optional\ndescription='''\nExample mod description.\n'''\n")
            .ReplaceExampleDescription("Coûte 5$")
            .SetOptionalString("authors", "Moi")
            .SetOptionalString("displayURL", "https://example.com")
            .ToString();

        Assert.Contains("Coûte 5\\$", result);
        Assert.Contains("authors=\"Moi\"", result);
        Assert.Contains("displayURL=\"https://example.com\"", result);
        Assert.DoesNotContain("#displayURL", result);
    }

    [Fact]
    public void Java_names_avoid_collisions_with_generated_fields()
    {
        var naming = new JavaModNaming(new IRModInfo(
            ModId.Parse("int"), "Mod Blocks", "1.0", "", "", "", "", "1.21.1", "neoforge"));

        Assert.Equal("com.mineengine.mods.int_mod", naming.RootPackage);
        Assert.Equal("ModBlocksMod", naming.MainClass);
        Assert.Equal("ITEMS_ENTRY", naming.ConstantFor(ResourceId.Parse("items")));
    }

    private static async Task<string> GenerateAsync(string directory, FakeBackend backend)
    {
        ModProject project = CreateSampleProject(directory, backend.LoaderId, backend.MinecraftVersion.Id);
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, new DiagnosticBag())!;
        string workspace = Path.Combine(directory, "workspace");

        GenerationResult result = await ModGenerator.CreateDefault(backend)
            .GenerateAsync(ir, workspace, NullLog.Instance, CancellationToken.None);

        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return workspace;
    }

    private static ModProject CreateSampleProject(string directory, string loaderId, string minecraftVersion)
    {
        var repository = new ProjectRepository(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());
        var settings = new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loaderId, MinecraftVersion = minecraftVersion };
        ModProject project = repository.Create(directory, settings);
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Rubis"));
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("magic_sword"), "Épée magique") { MaxStackSize = 1 });
        project.Assets.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("ruby_block"), "Bloc de rubis") { Hardness = 3f, Resistance = 9.5f });
        return project;
    }

    /// <summary>Backend sans MDK réel, pour tester la génération seule.</summary>
    private sealed class FakeBackend : IModLoaderBackend
    {
        private readonly IFileEmitter _javaEmitter;

        public FakeBackend(ModLoaderKind loader, string minecraftVersion, IFileEmitter javaEmitter)
        {
            _javaEmitter = javaEmitter;
            Mdk = new MdkDescriptor(
                "test", loader, MineEngine.Minecraft.MinecraftVersion.Parse(minecraftVersion), "0", 21, "9.2.1",
                Path.GetTempPath(), "test", DateTime.Now);
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
