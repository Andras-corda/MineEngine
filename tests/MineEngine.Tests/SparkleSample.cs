using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Sparkle;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// V0.7.5: a Sparkle system of two emitters (a rocket whose sparks die on the ground and burst
/// into smaller ones where they die) and an event graph that plays it on the block and on the
/// player. V0.7.6: a second system made of templates (beam, ribbon, fountain) whose fountain
/// sends location events to a splash. Added after everything else, so that the end-to-end builds
/// compile the particle classes on each loader.
/// </summary>
internal sealed class SparkleSample
{
    private SparkleSample()
    {
    }

    public SparkleAsset System { get; private set; } = null!;

    public GraphAsset Events { get; private set; } = null!;

    /// <summary>
    /// Beam (ID 1, its end linked to the user parameter "Target"), ribbon (heads 2 animated through
    /// three textures, trail 3 on their time events), fountain (4, two textures at random) and
    /// splash (5, on the location events of 4), with a comment and a tooltip. The maximum speed of
    /// the fountain is linked to the user parameter "Speed". V0.7.7: the event graph sets "Speed",
    /// plays the system from the block to itself, then stops the systems around it and on the player.
    /// </summary>
    public SparkleAsset Templates { get; private set; } = null!;

    public static SparkleSample AddTo(ModProject project)
    {
        SparkleEmitter rocket = SparkleEmitter.Create("Rocket", 1)
            .With(SparkleModuleKind.Lifecycle, m => m.WithValue(SparkleCatalog.LoopMode, "Loop").WithValue(SparkleCatalog.Loops, "2").WithValue(SparkleCatalog.Duration, "1"))
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "10").WithValue(SparkleCatalog.Shape, "Sphere"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.VelocityMode, "Cone").WithValue(SparkleCatalog.SpeedMin, "4").WithValue(SparkleCatalog.SpeedMax, "6"))
            .With(SparkleModuleKind.Collision, m => (m with { Enabled = true }).WithValue(SparkleCatalog.DieOnCollision, "true"))
            .With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Tint, "#FF8800"))
            .With(SparkleModuleKind.GenerateDeathEvent, m => m with { Enabled = true });
        SparkleEmitter burst = SparkleEmitter.Create("Burst", 2)
            .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.Source, "1").WithValue(SparkleCatalog.CountPerEvent, "4"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.VelocityMode, "Radial"))
            .With(SparkleModuleKind.Scale, m => m.WithValue(SparkleCatalog.ScaleMode, "Random"))
            .With(SparkleModuleKind.Gravity, m => m with { Enabled = false })
            with { X = 320, Y = 60 };

        int next = 0;
        int NextId() => ++next;
        SparkleUserParameter target = SparkleUserParameter.Create("Target", SparkleUserParameterType.Vector) with { Value = "0 2 8" };
        SparkleUserParameter speed = SparkleUserParameter.Create("Speed", SparkleUserParameterType.Number) with { Value = "8" };
        IReadOnlyList<SparkleEmitter> beam = SparkleTemplates.Create(SparkleTemplate.Beam, "Beam", NextId, 0, 0);
        IReadOnlyList<SparkleEmitter> ribbon = SparkleTemplates.Create(SparkleTemplate.Ribbon, "Ribbon", NextId, 320, 0);
        SparkleEmitter fountain = SparkleTemplates.Create(SparkleTemplate.Fountain, "Fountain", () => 4, 0, 400)[0]
            .With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Sprite, "minecraft:bubble;minecraft:glitter_3"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.SpeedMax, speed.Binding))
            .With(SparkleModuleKind.GenerateLocationEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.ZoneShape, "Box")
                .WithValue(SparkleCatalog.ZoneCenter, "0 3 0").WithValue(SparkleCatalog.ZoneSize, "2 0.5 2"));
        SparkleEmitter splash = SparkleEmitter.Create("Splash", 5)
            .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.EventType, "Location")
                .WithValue(SparkleCatalog.Source, "4").WithValue(SparkleCatalog.CountPerEvent, "2"));

        var sample = new SparkleSample
        {
            System = new SparkleAsset(Guid.NewGuid(), ResourceId.Parse("firework"), "Feu d'artifice") { Emitters = [rocket, burst] },
            Events = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("fireworks"), "Feux d'artifice") { Kind = GraphKind.Events },
            Templates = new SparkleAsset(Guid.NewGuid(), ResourceId.Parse("magic"), "Magie")
            {
                UserParameters = [target, speed],
                Emitters =
                [
                    beam[0].With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.LineEnd, target.Binding)),
                    ribbon[0].With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Sprite, "minecraft:spark_0;minecraft:spark_1;minecraft:spark_2")
                        .WithValue(SparkleCatalog.SpriteMode, "OverLife")),
                    ribbon[1],
                    fountain,
                    splash,
                ],
                Notes =
                [
                    new SparkleNote(Guid.NewGuid(), SparkleNoteKind.Comment) { X = -30, Y = -60, Text = "Beam", Tooltip = "Goes to Target." },
                    new SparkleNote(Guid.NewGuid(), SparkleNoteKind.Tooltip) { X = 600, Y = 380, Text = "Splashes in the zone.", Targets = [splash.Id] },
                ],
            },
        };
        project.Assets.Add(sample.System);
        project.Assets.Add(sample.Events);
        project.Assets.Add(sample.Templates);

        var graph = new ProgramGraphBuilder(sample.Events, () => ProgramModel.From(project.Assets));
        GraphNode use = graph.Add(GraphNodeKind.OnUseItem, 0, 0);
        GraphNode atBlock = graph.Add(GraphNodeKind.PlaySparkle, 300, 0, values: ("system", "firework"));
        GraphNode onPlayer = graph.Add(GraphNodeKind.PlaySparkleOnEntity, 600, 0, values: ("system", "testmod:firework"));
        GraphNode setSpeed = graph.Add(GraphNodeKind.SetSparkleNumber, 900, 0, values: [("system", "magic"), ("parameter", "Speed"), ("value", "12")]);
        GraphNode between = graph.Add(GraphNodeKind.PlaySparkleBetween, 1200, 0, values: [("system", "magic"), ("parameter", "Target")]);
        GraphNode stopAround = graph.Add(GraphNodeKind.StopSparklesAt, 1500, 0, values: ("radius", "6"));
        GraphNode stopOnPlayer = graph.Add(GraphNodeKind.StopSparkleOnEntity, 1800, 0);
        graph.Data(use, "position", atBlock, "position").Data(use, "player", onPlayer, "entity")
            .Data(use, "position", between, "from").Data(use, "position", between, "to")
            .Data(use, "position", stopAround, "position").Data(use, "player", stopOnPlayer, "entity")
            .Exec(use, atBlock).Exec(atBlock, onPlayer).Exec(onPlayer, setSpeed).Exec(setSpeed, between).Exec(between, stopAround).Exec(stopAround, stopOnPlayer);
        graph.Commit();
        return sample;
    }
}
