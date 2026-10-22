using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// V0.7.0 and V0.7.1: plants of every kind (with patches in the world), a biome added to the
/// overworld with its surface, tags of several kinds (of the mod, of Minecraft, common) and an event
/// graph that uses the tag nodes. Added after everything else, so that the end-to-end builds compile them.
/// </summary>
internal sealed class WorldSample
{
    private WorldSample()
    {
    }

    public IReadOnlyList<PlantAsset> Plants { get; private set; } = [];

    public BiomeAsset Biome { get; private set; } = null!;

    public IReadOnlyList<TagAsset> Tags { get; private set; } = [];

    public GraphAsset Events { get; private set; } = null!;

    public static WorldSample AddTo(ModProject project)
    {
        var sample = new WorldSample();
        Guid texture = project.Assets.OfType<TextureAsset>().First().Id;
        Guid other = project.Assets.OfType<TextureAsset>().Skip(1).FirstOrDefault()?.Id ?? texture;
        var flower = new PlantAsset(Guid.NewGuid(), ResourceId.Parse("moon_flower"), "Fleur de lune")
        {
            TextureId = texture, LightLevel = 5,
            Generation = new PlantGeneration { Enabled = true, Biomes = ["minecraft:plains", "moon_valley"], Rarity = 4, PatchSize = 24 },
        };
        var tall = new PlantAsset(Guid.NewGuid(), ResourceId.Parse("giant_reed"), "Roseau géant")
        {
            Kind = PlantKind.Tall, TextureId = texture, TopTextureId = other, Soil = PlantSoil.Sand,
            Generation = new PlantGeneration { Enabled = true, Biomes = ["#minecraft:is_beach"] },
        };
        var cane = new PlantAsset(Guid.NewGuid(), ResourceId.Parse("star_cane"), "Canne étoilée")
        {
            Kind = PlantKind.Growing, TextureId = texture, MaxHeight = 4, Soil = PlantSoil.AnySolid,
            Generation = new PlantGeneration { Enabled = true },
        };
        var crop = new PlantAsset(Guid.NewGuid(), ResourceId.Parse("moon_wheat"), "Graines de blé lunaire")
        {
            Kind = PlantKind.Crop, TextureId = texture, StageTextures = [texture, other, other],
            Produce = ContentReference.TryParseStorageText("minecraft:wheat", out ContentReference? wheat) ? wheat : null, ProduceMin = 1, ProduceMax = 2,
        };
        sample.Plants = [flower, tall, cane, crop];
        sample.Biome = new BiomeAsset(Guid.NewGuid(), ResourceId.Parse("moon_valley"), "Vallée lunaire")
        {
            BaseBiome = "minecraft:flower_forest", Temperature = 0.6, GrassColor = 0x7FB8FF, TopBlock = "minecraft:podzol", UnderBlock = "coarse_dirt",
            ClimateTemperature = new ClimateRange(0.1, 0.15), Weirdness = new ClimateRange(-0.5, 0.1),
        };
        sample.Tags =
        [
            new TagAsset(Guid.NewGuid(), ResourceId.Parse("moon_plants"), "moon_plants")
            {
                Registry = TagRegistry.Blocks, TagPath = "moon_plants", Values = ["moon_flower", "giant_reed", "#minecraft:flowers", "othermod:thing?"],
            },
            new TagAsset(Guid.NewGuid(), ResourceId.Parse("vanilla_flowers"), "vanilla_flowers")
            {
                Registry = TagRegistry.Items, Namespace = "minecraft", TagPath = "small_flowers", Values = ["moon_flower"],
            },
            new TagAsset(Guid.NewGuid(), ResourceId.Parse("seeds_common"), "seeds_common")
            {
                Registry = TagRegistry.Items, Namespace = "c", TagPath = "seeds/moon", Values = ["moon_wheat"], Replace = true,
            },
            new TagAsset(Guid.NewGuid(), ResourceId.Parse("moon_biomes"), "moon_biomes")
            {
                Registry = TagRegistry.Biomes, TagPath = "is_moon", Values = ["moon_valley", "minecraft:plains"],
            },
            new TagAsset(Guid.NewGuid(), ResourceId.Parse("moon_mobs"), "moon_mobs")
            {
                Registry = TagRegistry.EntityTypes, TagPath = "moon_mobs", Values = ["minecraft:zombie"],
            },
            new TagAsset(Guid.NewGuid(), ResourceId.Parse("moon_damage"), "moon_damage")
            {
                Registry = TagRegistry.DamageTypes, TagPath = "moon", Values = ["minecraft:magic"],
            },
        ];

        foreach (Asset asset in new Asset[] { flower, tall, cane, crop, sample.Biome }.Concat(sample.Tags))
        {
            project.Assets.Add(asset);
        }

        sample.Events = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("usage_tags"), "Usage des tags") { Kind = GraphKind.Events };
        project.Assets.Add(sample.Events);
        var graph = new ProgramGraphBuilder(sample.Events, () => ProgramModel.From(project.Assets));
        GraphNode click = graph.Add(GraphNodeKind.OnRightClickBlock, 0, 0);
        GraphNode last = click;
        double y = 0;
        foreach ((GraphNodeKind kind, string input, string from, string tag) in new[]
                 {
                     (GraphNodeKind.IsBlockInTag, "position", "position", "#testmod:moon_plants"),
                     (GraphNodeKind.IsBiomeInTag, "position", "position", "#testmod:is_moon"),
                     (GraphNodeKind.IsEntityInTag, "entity", "player", "testmod:moon_mobs"),
                 })
        {
            GraphNode test = graph.Add(kind, 300, y += 120, values: ("tag", tag));
            graph.Data(click, from, test, input);
            graph.Exec(last, test, ReferenceEquals(last, click) ? NodeInfo.ExecThen : NodeInfo.ExecFalse);
            GraphNode message = graph.Add(GraphNodeKind.SendMessage, 600, y, values: ("message", tag));
            graph.Data(click, "player", message, "player");
            graph.Exec(test, message, NodeInfo.ExecTrue);
            last = test;
        }

        GraphNode hand = graph.Add(GraphNodeKind.MainHandItem, 0, y += 120);
        graph.Data(click, "player", hand, "player");
        GraphNode item = graph.Add(GraphNodeKind.IsItemInTag, 300, y, values: ("tag", "#c:seeds/moon"));
        graph.Data(hand, "stack", item, "stack");
        graph.Exec(last, item, NodeInfo.ExecFalse);
        GraphNode done = graph.Add(GraphNodeKind.SendMessage, 600, y, values: ("message", "seeds"));
        graph.Data(click, "player", done, "player");
        graph.Exec(item, done, NodeInfo.ExecTrue);
        graph.Commit();
        return sample;
    }
}
