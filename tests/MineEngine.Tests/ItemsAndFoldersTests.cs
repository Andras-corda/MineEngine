using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
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
using MineEngine.Project.Commands;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>V0.4.1 : outils, armures, nourriture à effets, combustibles, tags, événements d'items et dossiers.</summary>
public sealed class ItemsAndFoldersTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static ProjectRepository Repository => new(Serializer, new ProjectSettingsSerializer());

    // ----- Génération des items avancés -----

    [Fact]
    public async Task Minecraft_1_20_1_passes_tool_damage_to_the_constructor()
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateSampleAsync(temp.Path, ModLoaderKind.Forge, "1.20.1");
        string items = GeneratedItems.Read(Path.Combine(workspace, "src/main/java/com/mineengine/mods/testmod"));

        Assert.Contains("() -> new RubySwordItem(new Item.Properties()));", items);
        Assert.Contains("public class RubySwordItem extends SwordItem {", items);
        Assert.Contains("super(Tiers.DIAMOND, 3, -2.4f, properties);", items);
        Assert.Contains("public boolean isValidRepairItem(ItemStack stack, ItemStack repairCandidate) {", items);
        Assert.Contains("super(Tiers.STONE, 6.0f, -3.1f, properties);", items);
        Assert.Contains("super(Tiers.NETHERITE, -2, -1.0f, properties);", items);
        Assert.Contains("super(ArmorMaterials.CHAIN, ArmorItem.Type.CHESTPLATE, properties);", items);
        int sword = items.IndexOf("RUBY_SWORD =", StringComparison.Ordinal);
        Assert.DoesNotContain(".stacksTo(", items[sword..items.IndexOf("public static final", sword, StringComparison.Ordinal)]);
        Assert.Contains(".effect(new MobEffectInstance(MobEffects.REGENERATION, 100, 1), 1.0f)", items);
        Assert.Contains("return 16;", items);
        Assert.Contains(".effect(new MobEffectInstance(MobEffects.MOVEMENT_SPEED, 600, 0), 0.5f)", items);
        Assert.Contains("public int getBurnTime(ItemStack stack, RecipeType<?> recipeType) {", items);
        Assert.Contains("return new ItemStack(BuiltInRegistries.ITEM.get(new ResourceLocation(\"minecraft:bowl\")));", items);
        Assert.True(File.Exists(Path.Combine(workspace, "src/main/resources/data/c/tags/items/tools/swords.json")));
    }

    [Fact]
    public async Task Minecraft_1_21_1_passes_tool_attributes_through_the_properties()
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateSampleAsync(temp.Path, ModLoaderKind.NeoForge, "1.21.1");
        string items = GeneratedItems.Read(Path.Combine(workspace, "src/main/java/com/mineengine/mods/testmod"));

        Assert.Contains("super(Tiers.DIAMOND, properties);", items);
        Assert.Contains(".attributes(SwordItem.createAttributes(Tiers.DIAMOND, 3, -2.4f))", items);
        Assert.Contains(".attributes(AxeItem.createAttributes(Tiers.STONE, 6.0f, -3.1f))", items);
        Assert.Contains(".durability(ArmorItem.Type.BOOTS.getDurability(37))", items);
        Assert.DoesNotContain("public int getBurnTime(ItemStack stack", items);

        string fuels = File.ReadAllText(Path.Combine(workspace, "src/main/resources/data/neoforge/data_maps/item/furnace_fuels.json"));
        Assert.Contains("\"testmod:star_stew\"", fuels);
        Assert.Contains("\"burn_time\": 400", fuels);
        Assert.True(File.Exists(Path.Combine(workspace, "src/main/resources/data/c/tags/item/tools/swords.json")));

        string model = File.ReadAllText(Path.Combine(workspace, "src/main/resources/assets/testmod/models/item/ruby_pickaxe.json"));
        Assert.Contains("minecraft:item/handheld", model);
    }

    [Fact]
    public async Task Item_events_are_called_from_the_item_class()
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateSampleAsync(temp.Path, ModLoaderKind.NeoForge, "1.21.1");
        string items = GeneratedItems.Read(Path.Combine(workspace, "src/main/java/com/mineengine/mods/testmod"));
        string behavior = ReadJava(workspace, "behavior/ItemEventsBehavior.java");

        Assert.Contains("import com.mineengine.mods.testmod.behavior.ItemEventsBehavior;", items);
        Assert.Contains("public boolean hurtEnemy(ItemStack stack, LivingEntity target, LivingEntity attacker) {", items);
        Assert.Contains("ItemEventsBehavior.onItemHitEntity1(player, attacker.level(), target.blockPosition(), target, 0.0);", items);
        Assert.Contains("public void onCraftedBy(ItemStack stack, Level level, Player player) {", items);
        Assert.Contains("public void inventoryTick(ItemStack stack, Level level, Entity entity, int slot, boolean selected) {", items);
        Assert.Contains("ItemEventsBehavior.onItemUseOnBlock1(player, context.getLevel(), context.getClickedPos(), null, 0.0);", items);
        Assert.Contains("public ItemStack finishUsingItem(ItemStack stack, Level level, LivingEntity entity) {", items);
        Assert.Contains("public InteractionResultHolder<ItemStack> use(Level level, Player player, InteractionHand hand) {", items);

        Assert.Contains("public static void onItemHitEntity1(Player player, Level level, BlockPos position, Entity other, double amount) {", behavior);
        Assert.DoesNotContain("addListener", behavior);
    }

    [Fact]
    public void An_item_event_must_target_an_item_of_the_mod()
    {
        var builder = new GraphBuilder();
        GraphNode click = builder.Add(GraphNodeKind.OnItemRightClick, 0, 0, ("item", "minecraft:stick"));
        GraphNode cancel = builder.Add(GraphNodeKind.CancelEvent, 300, 0);
        builder.Exec(click, cancel);

        IReadOnlyList<GraphIssue> issues = GraphValidator.Validate(builder.Build());

        Assert.Contains(issues, i => i.NodeId == click.Id && i.Message.Contains("item from the mod"));
        Assert.Contains(issues, i => i.NodeId == cancel.Id && i.Message.Contains("cannot be canceled"));
    }

    [Fact]
    public void New_item_properties_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        SampleContent.AddTo(project);
        Repository.Save(project);

        ModProject reopened = Repository.Open(project.Layout.RootDirectory);
        var stew = (ItemAsset)reopened.Assets.FindByResourceId(ResourceId.Parse("star_stew"), AssetType.Item)!;
        var sword = (ItemAsset)reopened.Assets.FindByResourceId(ResourceId.Parse("ruby_sword"), AssetType.Item)!;

        Assert.Equal(0.8f, stew.EatSeconds);
        Assert.Equal([new FoodEffect(FoodEffectKind.Regeneration, 5, 1, 1f), new FoodEffect(FoodEffectKind.Speed, 30, 0, 0.5f)], stew.FoodEffects);
        Assert.Equal(400, stew.BurnTime);
        Assert.Equal("minecraft:bowl", stew.CraftRemainder!.ToStorageText());
        Assert.Equal(ItemKind.Sword, sword.Kind);
        Assert.Equal(ItemTier.Diamond, sword.Tier);
        Assert.True(sword.Handheld && sword.NoRepair);
        Assert.Equal(["c:tools/swords", "minecraft:piglin_loved"], sword.ItemTags.Select(t => t.ToString()));
        Assert.Equal("Équipement/Outils", sword.Folder);
        Assert.Contains("Vide", reopened.Settings.Folders);
    }

    // ----- Dossiers -----

    [Fact]
    public void Folders_include_declared_ones_and_those_holding_assets()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        SampleContent.AddTo(project);

        Assert.Equal(["Équipement", "Équipement/Armure", "Équipement/Outils", "Programme", "Vide"], FolderOperations.AllFolders(project));
    }

    [Fact]
    public void Renaming_a_folder_moves_its_assets_and_can_be_undone()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        SampleContent.AddTo(project);
        Asset sword = project.Assets.FindByResourceId(ResourceId.Parse("ruby_sword"), AssetType.Item)!;

        project.History.Execute(FolderOperations.Move(project, "Équipement", "Armes", "Renommer"));

        Assert.Equal("Armes/Outils", sword.Folder);
        Assert.DoesNotContain(FolderOperations.AllFolders(project), f => f.StartsWith("Équipement", StringComparison.Ordinal));

        project.History.Undo();

        Assert.Equal("Équipement/Outils", sword.Folder);
        Assert.Contains("Équipement", project.Settings.Folders);
    }

    [Fact]
    public void Deleting_a_folder_moves_its_content_up_one_level()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        SampleContent.AddTo(project);
        Asset sword = project.Assets.FindByResourceId(ResourceId.Parse("ruby_sword"), AssetType.Item)!;
        Asset graph = project.Assets.FindByResourceId(ResourceId.Parse("item_events"), AssetType.Graph)!;

        project.History.Execute(FolderOperations.Dissolve(project, "Équipement"));

        Assert.Equal("Outils", sword.Folder);
        Assert.Equal(string.Empty, graph.Folder);
        Assert.Contains("Outils", FolderOperations.AllFolders(project));
    }

    [Theory]
    [InlineData(" Armes / Épées ", "Armes/Épées")]
    [InlineData("", "")]
    [InlineData("a//b", "a/b")]
    [InlineData("bad:name", null)]
    public void Folder_paths_are_normalized(string input, string? expected) => Assert.Equal(expected, AssetFolders.Normalize(input));

    // ----- Outils -----

    private static ModProject CreateProject(string directory, ModLoaderKind loader = ModLoaderKind.NeoForge, string version = "1.21.1") =>
        Repository.Create(directory, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });

    private static string ReadJava(string workspace, string relative) =>
        File.ReadAllText(Path.Combine(workspace, "src/main/java/com/mineengine/mods/testmod", relative));

    private static async Task<string> GenerateSampleAsync(string directory, ModLoaderKind loader, string version)
    {
        ModProject project = CreateProject(directory, loader, version);
        SampleContent.AddTo(project);
        var diagnostics = new DiagnosticBag();
        ModIR? ir = ModIRBuilder.CreateDefault().Build(project, diagnostics);
        Assert.True(ir is not null, string.Join('\n', diagnostics));

        string workspace = Path.Combine(directory, "workspace");
        IFileEmitter[] emitters = loader == ModLoaderKind.Forge
            ? [new ForgeJavaEmitter(), new Minecraft.Resources.TagsEmitter()]
            : [new NeoForgeJavaEmitter(), new Minecraft.Resources.TagsEmitter(), new FuelDataMapEmitter(),
               new Minecraft.Resources.ItemResourcesEmitter(new Minecraft.Resources.Imaging.PlaceholderTexture(new Minecraft.Resources.Imaging.PngEncoder()))];
        GenerationResult result = await ModGenerator.CreateDefault(new Backend(loader, version, emitters))
            .GenerateAsync(ir!, workspace, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return workspace;
    }

    private sealed class Backend : IModLoaderBackend
    {
        private readonly IReadOnlyList<IFileEmitter> _emitters;

        public Backend(ModLoaderKind loader, string version, IReadOnlyList<IFileEmitter> emitters)
        {
            _emitters = emitters;
            Mdk = new MdkDescriptor("test", loader, MinecraftVersion.Parse(version), "0", 21, null, Path.GetTempPath(), "test", DateTime.Now);
        }

        public string LoaderId => Mdk.Loader.ToId();

        public string DisplayName => Mdk.DisplayName;

        public MdkDescriptor Mdk { get; }

        public MinecraftVersion MinecraftVersion => Mdk.MinecraftVersion;

        public IReadOnlyList<string> BuildTasks => ["build"];

        public IReadOnlyList<string> RunClientTasks => ["runClient"];

        public Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<IFileEmitter> CreateEmitters() => _emitters;

        public string GetOutputJarPath(string workspaceDirectory, IRModInfo mod) => string.Empty;
    }
}
