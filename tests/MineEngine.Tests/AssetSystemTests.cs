using System.Text.Json.Nodes;
using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Mobs;
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
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Resources;
using MineEngine.Minecraft.Resources.Imaging;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>V0.3 : textures et sons en assets, références par guid, migration, recettes et mobs.</summary>
public sealed class AssetSystemTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static ProjectRepository Repository => new(Serializer, new ProjectSettingsSerializer());

    // ----- Critère de réussite de la V0.3 -----

    [Fact]
    public async Task Renaming_a_texture_used_by_three_items_keeps_the_three_items_working()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        TextureAsset texture = new AssetFileImporter().ImportTexture(project, WritePng(temp.Path, "gem.png"));
        project.Assets.Add(texture);
        foreach (string id in new[] { "red_gem", "blue_gem", "green_gem" })
        {
            project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse(id), id) { TextureId = texture.Id });
        }

        texture.ResourceId = ResourceId.Parse("shiny_gem");
        Repository.Save(project);
        ModProject reopened = Repository.Open(project.Layout.RootDirectory);

        var reopenedTexture = (TextureAsset)reopened.Assets.FindByResourceId(ResourceId.Parse("shiny_gem"), AssetType.Texture)!;
        Assert.Equal(3, reopened.Assets.FindReferencesTo(reopenedTexture.Id).Count);

        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(reopened, diagnostics)!;
        Assert.NotNull(ir);
        Assert.False(diagnostics.HasErrors);
        Assert.All(ir.Content.Items, i => Assert.Equal(
            Path.GetFullPath(reopened.Layout.ToAbsolutePath(reopenedTexture.FilePath)), i.Texture.SourceFile));

        string workspace = await GenerateAsync(reopened, temp.Path, ModLoaderKind.NeoForge, "1.21.1");
        Assert.True(File.Exists(Path.Combine(workspace, "src/main/resources/assets/testmod/textures/item/red_gem.png")));
    }

    // ----- Migration -----

    [Fact]
    public void Old_projects_are_converted_textures_become_assets_and_drops_become_references()
    {
        using var temp = new TemporaryDirectory();
        ModProject created = CreateProject(temp.Path);
        string root = created.Layout.RootDirectory;
        WritePng(created.Layout.TexturesDirectory, "ruby-0a1b2c3d.png");

        string ruby = Guid.NewGuid().ToString("D");
        WriteOldAsset(root, "ruby", $$$"""
            {"formatVersion":1,"guid":"{{{ruby}}}","type":"Item","resourceId":"ruby","displayName":"Rubis",
             "properties":{"texture":"Textures/ruby-0a1b2c3d.png","maxStackSize":64}}
            """);
        WriteOldAsset(root, "ruby_dust", $$$"""
            {"formatVersion":1,"guid":"{{{Guid.NewGuid()}}}","type":"Item","resourceId":"ruby_dust","displayName":"Poudre",
             "properties":{"texture":"Textures/ruby-0a1b2c3d.png"}}
            """);
        WriteOldAsset(root, "ruby_ore", $$$"""
            {"formatVersion":1,"guid":"{{{Guid.NewGuid()}}}","type":"Block","resourceId":"ruby_ore","displayName":"Minerai",
             "properties":{"texture":null,"drop":{"kind":"OtherItem","item":"ruby","min":1,"max":2} } }
            """);
        WriteOldAsset(root, "coal_block", $$$"""
            {"formatVersion":1,"guid":"{{{Guid.NewGuid()}}}","type":"Block","resourceId":"coal_block","displayName":"Charbon",
             "properties":{"drop":{"kind":"OtherItem","item":"coal","min":1,"max":1} } }
            """);

        ModProject project = Repository.Open(root);

        Assert.NotEmpty(project.LoadNotes);
        Assert.True(project.IsDirty);
        TextureAsset texture = Assert.Single(project.Assets.OfType<TextureAsset>());
        Assert.Equal("ruby", texture.ResourceId.Value);
        Assert.All(project.Assets.OfType<ItemAsset>(), i => Assert.Equal(texture.Id, i.TextureId));

        var ore = (BlockAsset)project.Assets.FindByResourceId(ResourceId.Parse("ruby_ore"), AssetType.Block)!;
        Assert.Equal(ContentReference.ToAsset(Guid.Parse(ruby)), ore.DropItem);
        var coal = (BlockAsset)project.Assets.FindByResourceId(ResourceId.Parse("coal_block"), AssetType.Block)!;
        Assert.Equal("minecraft:coal", coal.DropItem!.ToStorageText());

        Repository.Save(project);
        Assert.Empty(Directory.GetFiles(project.Layout.ContentDirectory, "*.json"));
        Assert.True(File.Exists(Path.Combine(project.Layout.ContentDirectory, "Textures", "ruby.asset.json")));
        Assert.Empty(Repository.Open(root).LoadNotes);
    }

    [Fact]
    public void Content_references_round_trip_through_their_storage_text()
    {
        var asset = ContentReference.ToAsset(Guid.NewGuid());
        Assert.True(ContentReference.TryParseStorageText(asset.ToStorageText(), out ContentReference? parsedAsset));
        Assert.Equal(asset, parsedAsset);

        Assert.True(ContentReference.TryParseStorageText("#minecraft:planks", out ContentReference? tag));
        Assert.Equal(ContentReferenceKind.Tag, tag!.Kind);

        Assert.True(ContentReference.TryParseStorageText("diamond", out ContentReference? id));
        Assert.Equal("minecraft:diamond", id!.ToStorageText());

        Assert.False(ContentReference.TryParseStorageText("Not Valid", out _));
    }

    // ----- Références et identifiants -----

    [Fact]
    public void A_texture_may_share_its_identifier_with_an_item_but_two_items_may_not()
    {
        var registry = new AssetRegistry();
        registry.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Rubis"));
        registry.Add(new TextureAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "ruby", "Textures/ruby.png"));

        Assert.Throws<InvalidOperationException>(() => registry.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Mob")));
        Assert.Equal("ruby_2", registry.CreateUniqueResourceId("ruby", AssetType.Block).Value);
        Assert.Equal("ruby_2", registry.CreateUniqueResourceId("ruby", AssetType.Texture).Value);
        Assert.Equal("ruby", registry.CreateUniqueResourceId("ruby", AssetType.Sound).Value);
    }

    [Fact]
    public void Deleting_a_used_asset_is_reported_as_a_broken_reference()
    {
        var registry = new AssetRegistry();
        var ruby = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Rubis");
        var texture = new TextureAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "ruby", "Textures/ruby.png");
        var recipe = new RecipeAsset(Guid.NewGuid(), ResourceId.Parse("ruby_twice"), "x")
        {
            Kind = RecipeKind.Shapeless,
            Grid = [ContentReference.ToAsset(ruby.Id), null, null, null, null, null, null, null, null],
            Result = ContentReference.ToAsset(ruby.Id),
            ResultCount = 2,
        };
        ruby.TextureId = texture.Id;
        registry.Add(ruby);
        registry.Add(texture);
        registry.Add(recipe);

        Assert.Equal(2, registry.FindReferencesTo(ruby.Id).Count);
        var before = new DiagnosticBag();
        registry.Validate(before);
        Assert.False(before.HasErrors);

        registry.Remove(texture);
        registry.Remove(ruby);
        var after = new DiagnosticBag();
        registry.Validate(after);
        Assert.Contains(after, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("supprimé") && d.AssetId == recipe.Id);
    }

    [Fact]
    public void A_block_without_item_form_cannot_be_used_as_an_item()
    {
        var registry = new AssetRegistry();
        var portal = new BlockAsset(Guid.NewGuid(), ResourceId.Parse("portal"), "Portail") { HasItemFormEnabled = false };
        registry.Add(portal);
        registry.Add(new RecipeAsset(Guid.NewGuid(), ResourceId.Parse("make_portal"), "x")
        {
            Grid = [Id("minecraft:obsidian"), null, null, null, null, null, null, null, null],
            Result = ContentReference.ToAsset(portal.Id),
        });

        var diagnostics = new DiagnosticBag();
        registry.Validate(diagnostics);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("forme item"));
    }

    // ----- Recettes -----

    [Fact]
    public async Task Recipes_follow_the_format_of_each_minecraft_version()
    {
        using var temp = new TemporaryDirectory();
        string modern = await GenerateSampleAsync(Path.Combine(temp.Path, "modern"), ModLoaderKind.NeoForge, "1.21.1");
        string legacy = await GenerateSampleAsync(Path.Combine(temp.Path, "legacy"), ModLoaderKind.Forge, "1.20.1");

        JsonObject shaped = ReadJson(modern, "src/main/resources/data/testmod/recipe/magic_sword.json");
        Assert.Equal(["A", "A", "B"], shaped["pattern"]!.AsArray().Select(l => l!.GetValue<string>()));
        Assert.Equal("testmod:ruby", shaped["key"]!["A"]!["item"]!.GetValue<string>());
        Assert.Equal("testmod:magic_sword", shaped["result"]!["id"]!.GetValue<string>());

        JsonObject shapeless = ReadJson(modern, "src/main/resources/data/testmod/recipe/cooked_beast_from_beef.json");
        Assert.Contains(shapeless["ingredients"]!.AsArray(), i => i!["tag"]?.GetValue<string>() == "minecraft:coals");
        Assert.Equal(2, shapeless["result"]!["count"]!.GetValue<int>());

        JsonObject smeltingModern = ReadJson(modern, "src/main/resources/data/testmod/recipe/ruby_from_ore.json");
        Assert.Equal("testmod:ruby", smeltingModern["result"]!["id"]!.GetValue<string>());

        JsonObject smeltingLegacy = ReadJson(legacy, "src/main/resources/data/testmod/recipes/ruby_from_ore.json");
        Assert.Equal("testmod:ruby", smeltingLegacy["result"]!.GetValue<string>());
        Assert.Equal("testmod:magic_sword", ReadJson(legacy, "src/main/resources/data/testmod/recipes/magic_sword.json")["result"]!["item"]!.GetValue<string>());
    }

    // ----- Mobs -----

    [Fact]
    public async Task Mobs_generate_entity_classes_with_their_ai_registration_and_resources()
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateSampleAsync(temp.Path, ModLoaderKind.NeoForge, "1.21.1");
        string Read(string relative) => File.ReadAllText(Path.Combine(workspace, relative));
        const string Java = "src/main/java/com/mineengine/mods/testmod/";

        string main = Read(Java + "TestMod.java");
        Assert.Contains("ModEntities.ENTITY_TYPES.register(modEventBus);", main);
        Assert.Contains("ModSounds.SOUND_EVENTS.register(modEventBus);", main);
        Assert.Contains("modEventBus.addListener(ModEntities::registerSpawnPlacements);", main);
        Assert.Contains("if (FMLEnvironment.dist == Dist.CLIENT)", main);

        string goblin = Read(Java + "entity/GoblinEntity.java");
        Assert.Contains("public class GoblinEntity extends Monster", goblin);
        Assert.Contains("this.goalSelector.addGoal(1, new FloatGoal(this));", goblin);
        Assert.Contains("this.targetSelector.addGoal(1, new HurtByTargetGoal(this));", goblin);
        Assert.Contains("new NearestAttackableTargetGoal<>(this, AbstractVillager.class, false)", goblin);
        Assert.Contains("this.igniteForSeconds(8);", goblin);
        Assert.Contains("return sound(\"testmod:goblin_hurt\");", goblin);

        string cow = Read(Java + "entity/StarCowEntity.java");
        Assert.Contains("extends Animal", cow);
        Assert.Contains("return stack.is(item(\"minecraft:wheat\"));", cow);
        Assert.Contains("ModEntities.STAR_COW.get().create(level)", cow);

        string guard = Read(Java + "entity/SnowGuardEntity.java");
        Assert.Contains("new HurtByTargetGoal(this).setAlertOthers()", guard);

        string items = Read(Java + "registry/ModItems.java");
        Assert.Contains("new DeferredSpawnEggItem(ModEntities.GOBLIN, 0x3A6B1F, 0xC8A24B, new Item.Properties())", items);
        Assert.DoesNotContain("SNOW_GUARD_SPAWN_EGG", items);

        string renderers = Read(Java + "client/ModEntityRenderers.java");
        Assert.Contains("ResourceLocation.parse(\"testmod:textures/entity/goblin.png\")", renderers);
        Assert.Contains("ResourceLocation.parse(\"minecraft:textures/entity/cow/cow.png\")", renderers);

        const string Resources = "src/main/resources/";
        Assert.True(File.Exists(Path.Combine(workspace, Resources + "data/testmod/neoforge/biome_modifier/spawn_goblin_forest.json")));
        JsonObject spawn = ReadJson(workspace, Resources + "data/testmod/neoforge/biome_modifier/spawn_star_cow_plains.json");
        Assert.Equal("neoforge:add_spawns", spawn["type"]!.GetValue<string>());
        Assert.Equal("minecraft:plains", spawn["biomes"]!.GetValue<string>());

        JsonObject loot = ReadJson(workspace, Resources + "data/testmod/loot_table/entities/goblin.json");
        Assert.Equal(2, loot["pools"]!.AsArray().Count);

        JsonObject sounds = ReadJson(workspace, Resources + "assets/testmod/sounds.json");
        Assert.Equal("subtitles.testmod.goblin_hurt", sounds["goblin_hurt"]!["subtitle"]!.GetValue<string>());

        JsonObject french = ReadJson(workspace, Resources + "assets/testmod/lang/fr_fr.json");
        Assert.Equal("Gobelin", french["entity.testmod.goblin"]!.GetValue<string>());
        Assert.Equal("Œuf d'apparition de Gobelin", french["item.testmod.goblin_spawn_egg"]!.GetValue<string>());
        Assert.Equal("Gobelin Spawn Egg", ReadJson(workspace, Resources + "assets/testmod/lang/en_us.json")["item.testmod.goblin_spawn_egg"]!.GetValue<string>());
    }

    [Fact]
    public async Task Forge_1_20_uses_the_legacy_entity_api()
    {
        using var temp = new TemporaryDirectory();
        string workspace = await GenerateSampleAsync(temp.Path, ModLoaderKind.Forge, "1.20.1");
        const string Java = "src/main/java/com/mineengine/mods/testmod/";

        string goblin = File.ReadAllText(Path.Combine(workspace, Java + "entity/GoblinEntity.java"));
        Assert.Contains("this.setSecondsOnFire(8);", goblin);
        Assert.Contains("new ResourceLocation(id)", goblin);

        string entities = File.ReadAllText(Path.Combine(workspace, Java + "registry/ModEntities.java"));
        Assert.Contains("SpawnPlacements.Type.ON_GROUND", entities);
        Assert.Contains("Animal::checkAnimalSpawnRules, SpawnPlacementRegisterEvent.Operation.REPLACE", entities);
        Assert.Contains("RegistryObject<EntityType<GoblinEntity>> GOBLIN", entities);
        Assert.True(File.Exists(Path.Combine(workspace, "src/main/resources/data/testmod/forge/biome_modifier/spawn_goblin_overworld.json")));
    }

    [Fact]
    public void Mob_validation_catches_impossible_behaviours()
    {
        var mob = new MobAsset(Guid.NewGuid(), ResourceId.Parse("brute"), "Brute")
        {
            Kind = MobKind.Monster,
            Goals = [MobGoal.Create(MobGoalKind.Breed), MobGoal.Create(MobGoalKind.Tempt), MobGoal.Create(MobGoalKind.MeleeAttack)],
        };

        var diagnostics = new DiagnosticBag();
        mob.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("Animal"));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("n'a pas d'item"));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("cible"));
    }

    [Fact]
    public void Mobs_and_recipes_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        SampleContent.AddTo(project);
        Repository.Save(project);

        ModProject reopened = Repository.Open(project.Layout.RootDirectory);
        var cow = (MobAsset)reopened.Assets.FindByResourceId(ResourceId.Parse("star_cow"), AssetType.Mob)!;
        var original = (MobAsset)project.Assets.FindByResourceId(ResourceId.Parse("star_cow"), AssetType.Mob)!;
        Assert.Equal(original.Goals, cow.Goals);
        Assert.Equal(original.Drops, cow.Drops);
        Assert.Equal(original.BreedingItem, cow.BreedingItem);
        Assert.True(cow.UseCustomHitbox);
        Assert.Equal(1.1f, cow.Width);

        var shaped = (RecipeAsset)reopened.Assets.FindByResourceId(ResourceId.Parse("magic_sword"), AssetType.Recipe)!;
        Assert.Equal(((RecipeAsset)project.Assets.FindByResourceId(ResourceId.Parse("magic_sword"), AssetType.Recipe)!).Grid, shaped.Grid);
        Assert.Empty(reopened.LoadNotes);
    }

    // ----- Outils -----

    private static ModProject CreateProject(string directory)
    {
        var settings = new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = "neoforge", MinecraftVersion = "1.21.1" };
        return Repository.Create(directory, settings);
    }

    private static async Task<string> GenerateSampleAsync(string directory, ModLoaderKind loader, string version)
    {
        var settings = new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version };
        ModProject project = Repository.Create(directory, settings);
        SampleContent.AddTo(project);
        return await GenerateAsync(project, directory, loader, version);
    }

    private static async Task<string> GenerateAsync(ModProject project, string directory, ModLoaderKind loader, string version)
    {
        var diagnostics = new DiagnosticBag();
        ModIR? ir = ModIRBuilder.CreateDefault().Build(project, diagnostics);
        Assert.True(ir is not null, string.Join('\n', diagnostics));

        string workspace = Path.Combine(directory, "workspace");
        IFileEmitter[] emitters = loader == ModLoaderKind.Forge
            ? [new ForgeJavaEmitter(), new SpawnBiomeModifierEmitter("forge")]
            : [new NeoForgeJavaEmitter(), new SpawnBiomeModifierEmitter("neoforge")];
        GenerationResult result = await ModGenerator.CreateDefault(new Backend(loader, version, emitters))
            .GenerateAsync(ir!, workspace, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return workspace;
    }

    private static JsonObject ReadJson(string workspace, string relative) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, relative)))!.AsObject();

    private static string WritePng(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, name);
        File.WriteAllBytes(file, new PlaceholderTexture(new PngEncoder()).GetPngBytes());
        return file;
    }

    /// <summary>Écrit un fichier d'asset à l'ancien emplacement (Content/ sans sous-dossier).</summary>
    private static void WriteOldAsset(string root, string id, string json) =>
        File.WriteAllText(Path.Combine(root, "Content", id + AssetSerializer.FileExtension), json);

    private static ContentReference Id(string id) =>
        ContentReference.TryParseStorageText(id, out ContentReference? reference) ? reference! : throw new ArgumentException(id);

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

        public IReadOnlyList<string> GeneratedSourceRoots => ["src/main/java", "src/main/resources"];

        public IReadOnlyList<string> BuildTasks => ["build"];

        public IReadOnlyList<string> RunClientTasks => ["runClient"];

        public Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<IFileEmitter> CreateEmitters() => _emitters;

        public string GetOutputJarPath(string workspaceDirectory, IRModInfo mod) => string.Empty;
    }
}
