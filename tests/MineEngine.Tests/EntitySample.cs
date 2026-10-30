using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// V0.7.9 (Entity.md): two mobs that use the new sections, and the small functions of the project
/// they are bound to. The storm wolf is an animal that can be tamed and ridden with a saddle, with
/// variations, layers (glowing eyes, the saddle), a scale given by a function, a bigger collision
/// box, immunities, weaknesses, water settings, a despawn condition and groups given by functions.
/// The stone guardian is a raider, an illager, a boss and a golem, limited to pillager outposts.
/// </summary>
internal sealed class EntitySample
{
    private EntitySample()
    {
    }

    public MobAsset Wolf { get; private set; } = null!;

    public MobAsset Guardian { get; private set; } = null!;

    public GraphAsset Size { get; private set; } = null!;

    public GraphAsset Glows { get; private set; } = null!;

    public GraphAsset Pack { get; private set; } = null!;

    public GraphAsset GolemHere { get; private set; } = null!;

    /// <summary>A boat with its own item texture (V0.7.9, step 3).</summary>
    public MobAsset Raft { get; private set; } = null!;

    /// <summary>A minecart with the item of the game; it would spawn and have an egg if it were not a vehicle.</summary>
    public MobAsset Trolley { get; private set; } = null!;

    /// <summary>Events graph (V0.7.9, step 2): every event of the wolf, and the guardian hurt.</summary>
    public GraphAsset Events { get; private set; } = null!;

    public static EntitySample AddTo(ModProject project, TextureAsset texture)
    {
        var sample = new EntitySample
        {
            Size = Function(project, "beast_size", DataType.Entity, DataType.Decimal, "1.2"),
            Glows = Function(project, "beast_glows", DataType.Entity, DataType.Boolean, "true"),
            Pack = Function(project, "pack_size", DataType.Entity, DataType.Number, "3"),
            GolemHere = Function(project, "golem_here", DataType.Position, DataType.Boolean, "true"),
        };

        sample.Wolf = new MobAsset(Guid.NewGuid(), ResourceId.Parse("storm_wolf"), "Loup d'orage")
        {
            Model = MobModel.Cow,
            Kind = MobKind.Animal,
            TextureId = texture.Id,
            BreedingItem = Id("minecraft:beef"),
            VisualScale = new MobScale(MobValueMode.Dynamic, 1, sample.Size.Id),
            TextureTransparent = true,
            ShadowRadius = 0.8,
            PassengerOffset = 1.1,
            HitboxScale = new MobScale(MobValueMode.Fixed, 1.2),
            Variations = [texture.Id],
            Layers =
            [
                new MobLayer(texture.Id) { Glow = true, NoHurtOverlay = true, GlowFunction = sample.Glows.Id },
                new MobLayer(texture.Id),
            ],
            StepHeight = 1,
            TrackingRange = 10,
            Knockback = 0.5,
            Taming = new MobTaming(true, [new MobTamingItem(Id("minecraft:bone"), 33), new MobTamingItem(Id("minecraft:beef"), 50)]),
            Riding = new MobRiding(true) { NeedsSaddle = true, SaddleLayer = 1 },
            Water = new MobWater(true) { BreathesFunction = sample.Glows.Id, PushedByFluid = false },
            Immunities = new HashSet<MobImmunity> { MobImmunity.Fall, MobImmunity.Drowning, MobImmunity.Potions },
            Vulnerabilities = new HashSet<MobVulnerability> { MobVulnerability.Moon, MobVulnerability.Water },
            DespawnFunction = sample.Glows.Id,
            SpawnsNaturally = true,
            Dimensions = new HashSet<MobDimension> { MobDimension.Overworld, MobDimension.Nether },
            SpawnBiomes = new HashSet<SpawnBiome> { SpawnBiome.Forest },
            GroupMode = MobGroupMode.Dynamic,
            GroupFunction = sample.Pack.Id,
            EggTextureId = texture.Id,
        };

        sample.Guardian = new MobAsset(Guid.NewGuid(), ResourceId.Parse("stone_guardian"), "Gardien de pierre")
        {
            Model = MobModel.Zombie,
            Kind = MobKind.Raider,
            CreatureType = MobCreatureType.Illager,
            HitboxScale = new MobScale(MobValueMode.Dynamic, 1, sample.Size.Id),
            NoCollision = true,
            Immunities = new HashSet<MobImmunity> { MobImmunity.Fire, MobImmunity.Explosion },
            Vulnerabilities = new HashSet<MobVulnerability> { MobVulnerability.Potions },
            Boss = new MobBoss(true) { Color = MobBossColor.Red, Overlay = MobBossOverlay.Notched10 },
            Golem = new MobGolem(true) { Function = sample.GolemHere.Id },
            SpawnsNaturally = true,
            InStructures = true,
            Structures = new HashSet<string> { "minecraft:pillager_outpost" },
            GroupMode = MobGroupMode.Fixed,
            SpawnMinGroup = 2,
        };

        sample.Raft = new MobAsset(Guid.NewGuid(), ResourceId.Parse("log_raft"), "Radeau")
        {
            Model = MobModel.Pig,
            Kind = MobKind.Creature,
            Vehicle = new MobVehicle(MobVehicleKind.Boat) { Rarity = ItemRarity.Rare, ItemTextureId = texture.Id },
        };

        sample.Trolley = new MobAsset(Guid.NewGuid(), ResourceId.Parse("mine_trolley"), "Chariot de mine")
        {
            Model = MobModel.Cow,
            Kind = MobKind.Creature,
            Vehicle = new MobVehicle(MobVehicleKind.Minecart),
            SpawnsNaturally = true,
            HasSpawnEgg = true,
        };

        project.Assets.Add(sample.Wolf);
        project.Assets.Add(sample.Guardian);
        project.Assets.Add(sample.Raft);
        project.Assets.Add(sample.Trolley);
        sample.Events = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("beast_events"), "Beast events") { Graph = EventsGraph() };
        project.Assets.Add(sample.Events);
        return sample;
    }

    /// <summary>
    /// Each event of the wolf sends a message to its player, or makes an explosion at the mob when it
    /// gives no player; the guardian (named with the namespace of the mod) sends a message when hurt.
    /// </summary>
    private static NodeGraph EventsGraph()
    {
        var builder = new GraphBuilder();
        int row = 0;
        foreach (MobEventKind kind in Enum.GetValues<MobEventKind>())
        {
            NodeInfo info = NodeCatalog.ForMobEvent(kind);
            GraphNode node = builder.Add(info.Kind, 0, row++ * 200, ("mob", "storm_wolf"));
            if (info.Outputs.Any(o => o.Name == "player"))
            {
                GraphNode message = builder.Add(GraphNodeKind.SendMessage, 300, row * 200);
                builder.Exec(node, message).Data(node, "player", message, "player");
            }
            else
            {
                GraphNode boom = builder.Add(GraphNodeKind.Explosion, 300, row * 200, ("power", "1"));
                builder.Exec(node, boom).Data(node, "position", boom, "position");
            }
        }

        GraphNode hurt = builder.Add(GraphNodeKind.OnMobHurt, 0, row * 200, ("mob", "testmod:stone_guardian"));
        GraphNode warn = builder.Add(GraphNodeKind.SendMessage, 300, row * 200);
        builder.Exec(hurt, warn).Data(hurt, "player", warn, "player");
        return builder.Build();
    }

    /// <summary>A function of one argument that returns a constant.</summary>
    internal static GraphAsset Function(ModProject project, string id, DataType argument, DataType result, string value)
    {
        var graph = new GraphAsset(Guid.NewGuid(), ResourceId.Parse(id), id) { Kind = GraphKind.Function };
        graph.Parameters = [VariableDefinition.Create("argument", argument)];
        graph.Returns = [VariableDefinition.Create("result", result)];
        project.Assets.Add(graph);
        var builder = new ProgramGraphBuilder(graph, () => ProgramModel.From(project.Assets));
        GraphNode entry = builder.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode done = builder.Add(GraphNodeKind.FunctionReturn, 300, 0, values: (ProgramSample.Pin(graph.Returns[0]), value));
        builder.Exec(entry, done).Commit();
        return graph;
    }

    private static ContentReference Id(string id) =>
        ContentReference.ToId(NamespacedId.TryParse(id, out NamespacedId? parsed) ? parsed! : throw new ArgumentException(id));
}
