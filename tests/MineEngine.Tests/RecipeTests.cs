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

/// <summary>Recipes of every station, their place in the recipe book and the advancements that unlock them.</summary>
public sealed class RecipeTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    [Fact]
    public void The_book_settings_survive_a_save()
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod"));
        SampleContent.AddTo(project);
        repository.Save(project);

        ModProject reopened = repository.Open(project.Layout.RootDirectory);

        RecipeAsset blasting = reopened.Assets.OfType<RecipeAsset>().Single(r => r.ResourceId.Value == "ruby_from_ore_blasting");
        Assert.Equal(Core.GameData.RecipeKind.Blasting, blasting.Kind);
        Assert.Equal("ruby", blasting.Group);
        Assert.Equal("blocks", blasting.Category);
        Assert.Equal(2, blasting.UnlockItems.Count);
        Assert.Equal("minecraft", reopened.Assets.OfType<RecipeAsset>().Single(r => r.ResourceId.Value == "stick").Namespace);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Every_station_writes_the_format_of_the_game(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        SampleContent.AddTo(project);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter, new RecipesEmitter()))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        bool modern = version == "1.21.1";
        string data = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources/data");
        string recipes = modern ? "recipe" : "recipes";
        JsonNode Json(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(data, path)))!;

        JsonNode blasting = Json($"testmod/{recipes}/ruby_from_ore_blasting.json");
        Assert.Equal("minecraft:blasting", blasting["type"]!.GetValue<string>());
        Assert.Equal("ruby", blasting["group"]!.GetValue<string>());
        Assert.Equal("blocks", blasting["category"]!.GetValue<string>());
        Assert.Equal("minecraft:campfire_cooking", Json($"testmod/{recipes}/cooked_beast_campfire.json")["type"]!.GetValue<string>());
        Assert.Equal("minecraft:smoking", Json($"testmod/{recipes}/cooked_beast_smoking.json")["type"]!.GetValue<string>());

        JsonNode cut = Json($"testmod/{recipes}/stone_bricks_cut.json");
        Assert.Equal("minecraft:stonecutting", cut["type"]!.GetValue<string>());
        Assert.Equal(2, modern ? cut["result"]!["count"]!.GetValue<int>() : cut["count"]!.GetValue<int>());

        JsonNode smithing = Json($"testmod/{recipes}/ruby_sword_upgrade.json");
        Assert.Equal("minecraft:smithing_transform", smithing["type"]!.GetValue<string>());
        Assert.Equal("minecraft:diamond_sword", smithing["base"]!["item"]!.GetValue<string>());
        Assert.Equal("testmod:magic_sword", smithing["result"]![modern ? "id" : "item"]!.GetValue<string>());

        // "minecraft" as namespace replaces the recipe of the game.
        Assert.Equal("sticks", Json($"minecraft/{recipes}/stick.json")["group"]!.GetValue<string>());

        // The advancement that unlocks a recipe: the recipe itself, then one criterion per item or tag.
        JsonNode advancement = Json($"testmod/{(modern ? "advancement" : "advancements")}/recipes/ruby_from_ore_blasting.json");
        Assert.Equal("testmod:ruby_from_ore_blasting", advancement["rewards"]!["recipes"]![0]!.GetValue<string>());
        Assert.Equal(3, advancement["requirements"]![0]!.AsArray().Count);
        JsonNode tag = advancement["criteria"]!["has_item_1"]!["conditions"]!["items"]![0]!;
        Assert.Equal(modern ? "#minecraft:logs" : "minecraft:logs", (modern ? tag["items"] : tag["tag"])!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(data, $"testmod/{(modern ? "advancement" : "advancements")}/recipes/cooked_beast_smoking.json")));
    }
}
