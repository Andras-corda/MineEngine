using System.Text.Json.Nodes;
using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
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
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Drop tables of V0.7.10: the element, its checks and its loot table on each version.</summary>
public sealed class LootTableTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static ModProject CreateProject(TemporaryDirectory temp, ModLoaderKind loader, string version)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        SampleContent.AddTo(project);
        return project;
    }

    private static LootTableAsset Table(ModProject project, string id) => project.Assets.OfType<LootTableAsset>().Single(t => t.ResourceId.Value == id);

    [Fact]
    public void A_drop_table_survives_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        LootTableAsset cache = Table(project, "rune_cache");

        var reloaded = (LootTableAsset)Serializer.Deserialize(Serializer.Serialize(cache), "rune_cache");

        Assert.Equal(LootTableKind.Chest, reloaded.Kind);
        Assert.Equal(cache.Pools, reloaded.Pools);
        Assert.Equal(LootEntryKind.Empty, reloaded.Pools[0].Entries[2].Kind);
        Assert.Equal("#c:gems", reloaded.Pools[0].Entries[1].Item!.ToStorageText());
    }

    [Fact]
    public void Entries_without_item_and_conditions_of_another_kind_are_reported()
    {
        var table = new LootTableAsset(Guid.NewGuid(), ResourceId.Parse("odd"), "Odd")
        {
            Kind = LootTableKind.Chest,
            Pools = [new LootPool { Entries = [new LootEntry(), new LootEntry { Kind = LootEntryKind.Empty, KilledByPlayer = true }] }],
        };
        var diagnostics = new DiagnosticBag();

        table.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("no item", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Message.Contains("drop table of a mob", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task A_drop_table_becomes_a_loot_table_of_the_game(ModLoaderKind loader, string version)
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
        string folder = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources/data/testmod", modern ? "loot_table" : "loot_tables");
        JsonObject Read(string id) => JsonNode.Parse(File.ReadAllText(Path.Combine(folder, id + ".json")))!.AsObject();

        JsonObject cache = Read("rune_cache");
        Assert.Equal("minecraft:chest", cache["type"]!.GetValue<string>());
        JsonObject pool = cache["pools"]![0]!.AsObject();
        Assert.Equal(3, pool["rolls"]!["max"]!.GetValue<int>());
        Assert.Equal(1f, pool["bonus_rolls"]!.GetValue<float>());
        JsonArray entries = pool["entries"]!.AsArray();
        Assert.Equal("testmod:ruby", entries[0]!["name"]!.GetValue<string>());
        Assert.Equal(3, entries[0]!["weight"]!.GetValue<int>());
        Assert.Equal("minecraft:set_count", entries[0]!["functions"]![0]!["function"]!.GetValue<string>());
        Assert.Equal("minecraft:tag", entries[1]!["type"]!.GetValue<string>());
        Assert.Equal("c:gems", entries[1]!["name"]!.GetValue<string>());
        Assert.Equal("minecraft:empty", entries[2]!["type"]!.GetValue<string>());
        Assert.Equal(0.25, cache["pools"]![1]!["entries"]![0]!["conditions"]![0]!["chance"]!.GetValue<double>());

        string spoils = Read("goblin_spoils").ToJsonString();
        Assert.Contains(modern ? "\"minecraft:enchanted_count_increase\"" : "\"minecraft:looting_enchant\"", spoils);
        Assert.Contains("\"minecraft:furnace_smelt\"", spoils);
        Assert.Contains("\"minecraft:killed_by_player\"", spoils);

        string ore = Read("ore_drops").ToJsonString();
        Assert.Contains(modern ? "\"minecraft:enchantments\":[{\"enchantments\":\"minecraft:silk_touch\"" : "\"enchantments\":[{\"enchantment\":\"minecraft:silk_touch\"", ore);
        Assert.Contains("\"minecraft:inverted\"", ore);
        Assert.Contains("\"minecraft:survives_explosion\"", ore);
    }

    [Fact]
    public void A_block_whose_drop_table_was_deleted_is_reported()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        project.Assets.Remove(Table(project, "ore_drops"));
        var diagnostics = new DiagnosticBag();

        project.Assets.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Source.Contains("rune_vault", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Blocks_mobs_and_graphs_use_the_drop_tables(ModLoaderKind loader, string version)
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
        string folder = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources/data/testmod", modern ? "loot_table" : "loot_tables");
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");

        // The block drops the pools of its table, under the conditions of the block (it survives the explosion).
        JsonObject vault = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "blocks/rune_vault.json")))!.AsObject();
        Assert.Equal("minecraft:block", vault["type"]!.GetValue<string>());
        Assert.Contains("\"minecraft:inverted\"", vault.ToJsonString());
        Assert.Contains("\"minecraft:survives_explosion\"", vault["pools"]![0]!["conditions"]!.ToJsonString());

        // The mob drops its table instead of its list.
        string guardian = File.ReadAllText(Path.Combine(folder, "entities/stone_guardian.json"));
        Assert.Contains("\"minecraft:entity\"", guardian);
        Assert.Contains("\"minecraft:killed_by_player\"", guardian);

        // The graph draws the cache where the vault is placed.
        string behavior = File.ReadAllText(Path.Combine(java, "behavior/CacheEventsBehavior.java"));
        Assert.Contains("dropLootTable(level, \"rune_cache\", position, \"testmod\");", behavior);
        Assert.Contains(modern
            ? "server.getServer().reloadableRegistries().getLootTable(ResourceKey.create(Registries.LOOT_TABLE, ResourceLocation.parse(id)))"
            : "server.getServer().getLootData().getLootTable(new ResourceLocation(id))", behavior);
    }
}
