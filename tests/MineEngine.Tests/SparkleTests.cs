using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Serialization;
using MineEngine.Assets.Sparkle;
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
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Sparkle particle systems (V0.7.5): emitters, simulation of the preview, saving and the code of the mod.</summary>
public sealed class SparkleTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static SparkleAsset SystemOf(params SparkleEmitter[] emitters) =>
        new(Guid.NewGuid(), ResourceId.Parse("test_sparkle"), "Test") { Emitters = emitters };

    [Fact]
    public void The_body_of_an_emitter_has_the_six_parts_of_the_spec()
    {
        Assert.Equal(Enum.GetValues<SparkleSection>(), SparkleCatalog.All.Select(m => m.Section).Distinct());
        Assert.Equal(4, SparkleCatalog.InSection(SparkleSection.Event).Count());
        Assert.All(SparkleCatalog.All.SelectMany(m => m.Parameters), p => Assert.True(p.Type != SparkleParameterType.Choice || p.Choices.Any(c => c.Value == p.DefaultValue)));

        SparkleEmitter emitter = SparkleEmitter.Create("Sparks", 3);
        Assert.Equal(SparkleCatalog.All.Count, emitter.Modules.Count);
        Assert.Equal(3, emitter.EventId);
        Assert.True(emitter.Uses(SparkleModuleKind.Gravity));
        Assert.False(emitter.Uses(SparkleModuleKind.Collision));
        Assert.False(emitter.IsEventDriven);
    }

    [Fact]
    public void A_burst_spawns_all_its_particles_and_the_system_ends()
    {
        SparkleEmitter emitter = SparkleEmitter.Create("Burst", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "25").WithValue(SparkleCatalog.LifetimeMin, "0.5").WithValue(SparkleCatalog.LifetimeMax, "0.5"));
        var simulation = new SparkleSimulation([emitter]);

        simulation.Step(0.05);
        Assert.Equal(25, simulation.Particles.Count);

        for (int i = 0; i < 60; i++)
        {
            simulation.Step(0.05);
        }

        Assert.Empty(simulation.Particles);
        Assert.True(simulation.IsFinished);
    }

    [Fact]
    public void A_continuous_spawn_spreads_the_particles_over_the_duration()
    {
        SparkleEmitter emitter = SparkleEmitter.Create("Rain", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.SpawnMode, "Continuous").WithValue(SparkleCatalog.Count, "100").WithValue(SparkleCatalog.LifetimeMin, "10").WithValue(SparkleCatalog.LifetimeMax, "10"))
            .With(SparkleModuleKind.Lifecycle, m => m.WithValue(SparkleCatalog.Duration, "2"));
        var simulation = new SparkleSimulation([emitter]);

        simulation.Step(1);
        Assert.InRange(simulation.Particles.Count, 49, 51);
        simulation.Step(1.5);
        Assert.Equal(100, simulation.Particles.Count);
    }

    [Fact]
    public void Gravity_and_collision_make_the_particles_bounce_on_the_ground()
    {
        SparkleEmitter emitter = SparkleEmitter.Create("Ball", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "1").WithValue(SparkleCatalog.Offset, "0 2 0").WithValue(SparkleCatalog.LifetimeMin, "60").WithValue(SparkleCatalog.LifetimeMax, "60"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.SpeedMin, "0").WithValue(SparkleCatalog.SpeedMax, "0"))
            .With(SparkleModuleKind.Collision, m => (m with { Enabled = true }).WithValue(SparkleCatalog.Bounce, "0.5"));
        var simulation = new SparkleSimulation([emitter]);

        double lowest = double.MaxValue;
        bool wentUp = false;
        for (int i = 0; i < 200; i++)
        {
            simulation.Step(0.01);
            SparkleParticle ball = simulation.Particles.Single();
            lowest = Math.Min(lowest, ball.Position.Y);
            wentUp |= ball.Velocity.Y > 0;
        }

        Assert.Equal(0, lowest, 3);
        Assert.True(wentUp);
    }

    [Fact]
    public void A_death_event_spawns_the_particles_of_the_receiver_where_the_particle_died()
    {
        SparkleEmitter sender = SparkleEmitter.Create("Rocket", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "3").WithValue(SparkleCatalog.LifetimeMin, "1").WithValue(SparkleCatalog.LifetimeMax, "1"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.SpeedMin, "2").WithValue(SparkleCatalog.SpeedMax, "2"))
            .With(SparkleModuleKind.Gravity, m => m with { Enabled = false })
            .With(SparkleModuleKind.GenerateDeathEvent, m => m with { Enabled = true });
        SparkleEmitter receiver = SparkleEmitter.Create("Burst", 2)
            .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.Source, "1").WithValue(SparkleCatalog.CountPerEvent, "5"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.SpeedMin, "0").WithValue(SparkleCatalog.SpeedMax, "0"));
        var simulation = new SparkleSimulation([sender, receiver], ground: null);

        simulation.Step(0.5);
        Assert.Equal(3, simulation.Particles.Count);
        Assert.All(simulation.Particles, p => Assert.Equal(0, p.Emitter));

        simulation.Step(0.6);
        Assert.Equal(3, simulation.DeathEvents);
        Assert.Equal(15, simulation.Particles.Count);
        Assert.All(simulation.Particles, p => Assert.Equal(1, p.Emitter));
        // Born where the rockets died, about 2 blocks above the origin.
        Assert.All(simulation.Particles, p => Assert.InRange(p.Position.Y, 1.9, 2.3));
    }

    [Theory]
    [InlineData("Once", 1)]
    [InlineData("Repeat", 4)]
    public void A_time_event_is_sent_once_or_at_every_interval(string mode, int events)
    {
        SparkleEmitter head = SparkleEmitter.Create("Head", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "1").WithValue(SparkleCatalog.LifetimeMin, "1.1").WithValue(SparkleCatalog.LifetimeMax, "1.1"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.VelocityMode, "Direction").WithValue(SparkleCatalog.SpeedMin, "4").WithValue(SparkleCatalog.SpeedMax, "4"))
            .With(SparkleModuleKind.Gravity, m => m with { Enabled = false })
            .With(SparkleModuleKind.GenerateTimeEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.TimeMode, mode).WithValue(SparkleCatalog.EventTime, "0.25"));
        SparkleEmitter trail = SparkleEmitter.Create("Trail", 2)
            .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.EventType, "Time").WithValue(SparkleCatalog.Source, "1").WithValue(SparkleCatalog.CountPerEvent, "1"))
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.LifetimeMin, "5").WithValue(SparkleCatalog.LifetimeMax, "5"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.SpeedMin, "0").WithValue(SparkleCatalog.SpeedMax, "0"))
            .With(SparkleModuleKind.Gravity, m => m with { Enabled = false });
        var simulation = new SparkleSimulation([head, trail], ground: null);

        for (int i = 0; i < 60; i++)
        {
            simulation.Step(1.0 / 60);
        }

        Assert.Equal(events, simulation.Events);
        Assert.Equal(0, simulation.DeathEvents);
        // The trail is left where the head was: 4 blocks per second, at 0.25 s, 0.5 s...
        double[] heights = [.. simulation.Particles.Where(p => p.Emitter == 1).Select(p => p.Position.Y).Order()];
        Assert.Equal(events, heights.Length);
        Assert.InRange(heights[0], 0.9, 1.1);
    }

    [Fact]
    public void A_location_event_is_sent_when_a_particle_enters_the_zone()
    {
        SparkleEmitter rising = SparkleEmitter.Create("Rising", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "1").WithValue(SparkleCatalog.LifetimeMin, "2").WithValue(SparkleCatalog.LifetimeMax, "2"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.VelocityMode, "Direction").WithValue(SparkleCatalog.SpeedMin, "3").WithValue(SparkleCatalog.SpeedMax, "3"))
            .With(SparkleModuleKind.Gravity, m => m with { Enabled = false })
            .With(SparkleModuleKind.GenerateLocationEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.ZoneCenter, "0 3 0").WithValue(SparkleCatalog.ZoneRadius, "0.5"));
        SparkleEmitter splash = SparkleEmitter.Create("Splash", 2)
            .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.EventType, "Location").WithValue(SparkleCatalog.Source, "1").WithValue(SparkleCatalog.CountPerEvent, "3"))
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.SpeedMin, "0").WithValue(SparkleCatalog.SpeedMax, "0"))
            .With(SparkleModuleKind.Gravity, m => m with { Enabled = false });
        var simulation = new SparkleSimulation([rising, splash], ground: null);

        for (int i = 0; i < 90; i++)
        {
            simulation.Step(1.0 / 60);
        }

        // It goes through the sphere (y from 2.5 to 3.5) once: one event, three particles at its edge.
        Assert.Equal(1, simulation.Events);
        Assert.Equal(3, simulation.Particles.Count(p => p.Emitter == 1));
        Assert.All(simulation.Particles.Where(p => p.Emitter == 1), p => Assert.InRange(p.Position.Y, 2.5, 2.6));

        // Born inside the zone, a particle has not entered it.
        var inside = new SparkleSimulation([rising.With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Offset, "0 3 0")), splash], ground: null);
        inside.Step(0.1);
        Assert.Equal(0, inside.Events);
    }

    [Fact]
    public void The_templates_are_valid_emitters()
    {
        foreach (SparkleTemplateInfo info in SparkleTemplates.All)
        {
            int next = 0;
            IReadOnlyList<SparkleEmitter> emitters = SparkleTemplates.Create(info.Template, info.Title, () => ++next, 100, 50);
            SparkleAsset system = SystemOf([.. emitters]);
            var diagnostics = new DiagnosticBag();
            system.Validate(diagnostics);
            Assert.True(diagnostics.All(d => d.Severity == DiagnosticSeverity.Info), $"{info.Template}: {string.Join('\n', diagnostics)}");
            Assert.Equal((100.0, 50.0), (emitters[0].X, emitters[0].Y));

            var simulation = new SparkleSimulation(emitters, ground: 0);
            for (int i = 0; i < 60; i++)
            {
                simulation.Step(1.0 / 60);
            }

            Assert.NotEmpty(simulation.Particles);
        }

        // The ribbon: its trail spawns on the time events of its heads.
        IReadOnlyList<SparkleEmitter> ribbon = SparkleTemplates.Create(SparkleTemplate.Ribbon, "Ribbon", new[] { 7, 8 }.AsEnumerable().GetEnumerator() is var ids ? () => { ids.MoveNext(); return ids.Current; } : null!, 0, 0);
        Assert.Equal((7, 8), (ribbon[0].EventId, ribbon[1].EventId));
        Assert.Equal((7, SparkleEventKind.Time), (ribbon[1].EventSource, ribbon[1].ReceivedEvent));
        Assert.True(ribbon[0].Sends(SparkleEventKind.Time));
        Assert.Equal(SparkleEmitter.NodeSpacing, ribbon[1].X);
    }

    [Fact]
    public void A_setting_linked_to_a_user_parameter_takes_its_value()
    {
        SparkleUserParameter count = SparkleUserParameter.Create("Count", SparkleUserParameterType.Number) with { Value = "12" };
        SparkleUserParameter tint = SparkleUserParameter.Create("Tint", SparkleUserParameterType.Color) with { Value = "#112233" };
        SparkleEmitter emitter = SparkleEmitter.Create("Linked", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, count.Binding).WithValue(SparkleCatalog.Offset, count.Binding))
            .With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Tint, tint.Binding));
        SparkleAsset system = SystemOf(emitter);
        system.UserParameters = [count, tint];

        SparkleEmitter resolved = system.ResolvedEmitters[0];
        Assert.Equal(12, resolved.Get(SparkleModuleKind.SpawnParticles).Integer(SparkleCatalog.Count));
        Assert.Equal(0x112233, resolved.Get(SparkleModuleKind.Texture).Color(SparkleCatalog.Tint));
        // A number cannot be a vector: the setting keeps its default, and the validation says so.
        Assert.Equal(SparkleVector.Zero, resolved.Get(SparkleModuleKind.SpawnParticles).Vector(SparkleCatalog.Offset));
        var diagnostics = new DiagnosticBag();
        system.Validate(diagnostics);
        Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("\"Count\"", StringComparison.Ordinal));

        // The parameter is gone: the link is an error.
        system.UserParameters = [tint];
        diagnostics = new DiagnosticBag();
        system.Validate(diagnostics);
        Assert.Equal(2, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void The_user_parameters_and_the_notes_are_saved_and_read_back()
    {
        using var temp = new TemporaryDirectory();
        SparkleSample sample = SparkleSample.AddTo(new ProjectRepository(Serializer, new ProjectSettingsSerializer())
            .Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod")));

        var read = (SparkleAsset)Serializer.Deserialize(Serializer.Serialize(sample.Templates), "magic");

        Assert.Equal(sample.Templates.UserParameters, read.UserParameters);
        Assert.Equal(sample.Templates.Notes.Select(n => (n.Id, n.Kind, n.X, n.Y, n.Width, n.Height, n.Text, n.Color, n.Tooltip, string.Join(";", n.Targets))),
            read.Notes.Select(n => (n.Id, n.Kind, n.X, n.Y, n.Width, n.Height, n.Text, n.Color, n.Tooltip, string.Join(";", n.Targets))));
        Assert.Equal(new SparkleVector(0, 2, 8), read.ResolvedEmitters[0].Get(SparkleModuleKind.SpawnParticles).Vector(SparkleCatalog.LineEnd));
    }

    [Fact]
    public void The_links_to_the_user_parameters_are_kept_for_the_graphs()
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod"));
        SparkleSample sample = SparkleSample.AddTo(project);
        var diagnostics = new DiagnosticBag();

        IRSparkle magic = ModIRBuilder.CreateDefault().Build(project, diagnostics)!.Content.Sparkles.Single(s => s.Id.Value == "magic");

        Assert.Equal(["Target", "Speed"], magic.Parameters.Select(p => p.Name));
        Assert.Equal([new IRSparkleBinding("SpawnParticles", SparkleCatalog.LineEnd, 0)], magic.Emitters[0].Bindings);
        Assert.Equal([new IRSparkleBinding("Velocity", SparkleCatalog.SpeedMax, 1)], magic.Emitters[3].Bindings);
        // The values of the parameters are the ones of the editor until a graph changes them.
        Assert.Equal(new IRVector(0, 2, 8), magic.Emitters[0].LineEnd);
        Assert.Equal(8, magic.Emitters[3].SpeedMax);
        Assert.Empty(magic.Emitters[1].Bindings);
    }

    [Fact]
    public void Several_textures_are_taken_at_random_or_over_the_life()
    {
        SparkleEmitter random = SparkleEmitter.Create("Random", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "200"))
            .With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Sprite, "minecraft:spark_0; minecraft:spark_1;minecraft:spark_2"));
        var simulation = new SparkleSimulation([random], ground: null);
        simulation.Step(0.05);

        Assert.Equal(["minecraft:spark_0", "minecraft:spark_1", "minecraft:spark_2"], SparkleCatalog.SplitTextures(random.Get(SparkleModuleKind.Texture).GetValue(SparkleCatalog.Sprite)));
        Assert.Equal([0, 1, 2], simulation.Particles.Select(p => SparkleSimulation.SpriteOf(random, p, 3)).Distinct().Order());

        SparkleEmitter animated = random.With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.SpriteMode, "OverLife"))
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "1").WithValue(SparkleCatalog.LifetimeMin, "1").WithValue(SparkleCatalog.LifetimeMax, "1"));
        var life = new SparkleSimulation([animated], ground: null);
        int[] frames = new int[3];
        for (int i = 0; i < 3; i++)
        {
            life.Step(i == 0 ? 0.1 : 0.33);
            frames[i] = SparkleSimulation.SpriteOf(animated, life.Particles[0], 3);
        }

        Assert.Equal([0, 1, 2], frames);
        Assert.Equal([SparkleCatalog.DefaultSprite], SparkleCatalog.SplitTextures(" ; "));
    }

    [Fact]
    public void A_beam_spawns_its_particles_along_its_line()
    {
        SparkleEmitter beam = SparkleTemplates.Create(SparkleTemplate.Beam, "Beam", () => 1, 0, 0)[0]
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.SpeedMax, "0"));
        var simulation = new SparkleSimulation([beam], ground: null);

        simulation.Step(0.1);

        Assert.NotEmpty(simulation.Particles);
        Assert.All(simulation.Particles, p =>
        {
            Assert.Equal(0, p.Position.X, 6);
            Assert.Equal(1, p.Position.Y, 6);
            Assert.InRange(p.Position.Z, 0, 6);
        });
    }

    [Fact]
    public void An_emitter_unchecked_in_the_preview_is_not_simulated()
    {
        SparkleEmitter shown = SparkleEmitter.Create("Shown", 1);
        SparkleEmitter hidden = SparkleEmitter.Create("Hidden", 2) with { Preview = false };
        var simulation = new SparkleSimulation([shown, hidden], e => e.Preview);

        simulation.Step(0.1);

        Assert.All(simulation.Particles, p => Assert.Equal(0, p.Emitter));
    }

    [Fact]
    public void The_scale_changes_over_the_life()
    {
        SparkleEmitter emitter = SparkleEmitter.Create("Fade", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "1").WithValue(SparkleCatalog.LifetimeMin, "1").WithValue(SparkleCatalog.LifetimeMax, "1"))
            .With(SparkleModuleKind.Scale, m => m.WithValue(SparkleCatalog.ScaleMode, "OverLife").WithValue(SparkleCatalog.Start, "1").WithValue(SparkleCatalog.End, "0"));
        var simulation = new SparkleSimulation([emitter]);

        simulation.Step(0.5);

        Assert.Equal(0.5, simulation.Particles.Single().Size, 2);
    }

    [Fact]
    public void The_validation_finds_the_mistakes_of_the_events()
    {
        SparkleEmitter a = SparkleEmitter.Create("A", 1);
        SparkleEmitter twin = SparkleEmitter.Create("Twin", 1);
        SparkleEmitter orphan = SparkleEmitter.Create("Orphan", 2)
            .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.Source, "9"));
        var diagnostics = new DiagnosticBag();

        SystemOf(a, twin, orphan).Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("A, Twin", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("Orphan", StringComparison.Ordinal));
    }

    [Fact]
    public void A_loop_of_events_is_reported()
    {
        SparkleEmitter a = SparkleEmitter.Create("A", 1)
            .With(SparkleModuleKind.GenerateDeathEvent, m => m with { Enabled = true })
            .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.Source, "2"));
        SparkleEmitter b = SparkleEmitter.Create("B", 2)
            .With(SparkleModuleKind.GenerateDeathEvent, m => m with { Enabled = true })
            .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.Source, "1"));

        Assert.True(SystemOf(a, b).HasEventLoop());
        Assert.False(SystemOf(a, SparkleEmitter.Create("B", 2)).HasEventLoop());
    }

    [Fact]
    public void The_performance_counts_the_particles_alive_at_the_same_time()
    {
        SparkleEmitter rain = SparkleEmitter.Create("Rain", 1)
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.SpawnMode, "Continuous").WithValue(SparkleCatalog.Count, "100").WithValue(SparkleCatalog.LifetimeMin, "1").WithValue(SparkleCatalog.LifetimeMax, "1"))
            .With(SparkleModuleKind.Lifecycle, m => m.WithValue(SparkleCatalog.Duration, "10"))
            .With(SparkleModuleKind.GenerateDeathEvent, m => m with { Enabled = true });
        SparkleEmitter splash = SparkleEmitter.Create("Splash", 2)
            .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.Source, "1").WithValue(SparkleCatalog.CountPerEvent, "3"))
            .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.LifetimeMin, "1").WithValue(SparkleCatalog.LifetimeMax, "1"));

        IReadOnlyList<SparklePerformance> performance = SparklePerformance.Estimate([rain, splash]);

        Assert.Equal(10, performance[0].MaxParticles);
        Assert.Equal(30, performance[1].MaxParticles);
        Assert.Equal(SparkleCost.Low, performance[0].CpuCost);
    }

    [Fact]
    public void A_system_is_saved_and_read_back()
    {
        using var temp = new TemporaryDirectory();
        SparkleSample sample = SparkleSample.AddTo(new ProjectRepository(Serializer, new ProjectSettingsSerializer())
            .Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod")));

        string json = Serializer.Serialize(sample.System);
        var read = (SparkleAsset)Serializer.Deserialize(json, "firework");

        Assert.Equal(sample.System.Emitters.Select(e => (e.Id, e.Name, e.Preview, e.EventId, e.EventSource, e.X, e.Y)),
            read.Emitters.Select(e => (e.Id, e.Name, e.Preview, e.EventId, e.EventSource, e.X, e.Y)));
        Assert.True(read.Emitters[0].Uses(SparkleModuleKind.Collision));
        Assert.Equal("#FF8800", read.Emitters[0].Get(SparkleModuleKind.Texture).GetValue(SparkleCatalog.Tint));
        Assert.False(read.Emitters[1].Uses(SparkleModuleKind.Gravity));
    }

    [Fact]
    public void A_file_without_positions_places_the_emitters_side_by_side()
    {
        SparkleAsset system = SystemOf(SparkleEmitter.Create("A", 1) with { X = 500, Y = 80 }, SparkleEmitter.Create("B", 2));
        var json = System.Text.Json.Nodes.JsonNode.Parse(Serializer.Serialize(system))!;
        foreach (System.Text.Json.Nodes.JsonNode? emitter in json["properties"]!["emitters"]!.AsArray())
        {
            emitter!.AsObject().Remove("x");
            emitter.AsObject().Remove("y");
        }

        var read = (SparkleAsset)Serializer.Deserialize(json.ToJsonString(), "test_sparkle");

        Assert.Equal([(0.0, 0.0), (SparkleEmitter.NodeSpacing, 0.0)], read.Emitters.Select(e => (e.X, e.Y)));
    }

    [Fact]
    public void The_length_of_a_system_follows_its_lifecycles_and_its_events()
    {
        using var temp = new TemporaryDirectory();
        SparkleSample sample = SparkleSample.AddTo(new ProjectRepository(Serializer, new ProjectSettingsSerializer())
            .Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod")));
        SparkleEmitter rocket = sample.System.Emitters[0];
        SparkleEmitter burst = sample.System.Emitters[1];
        double Life(SparkleEmitter e) => e.Get(SparkleModuleKind.SpawnParticles).Number(SparkleCatalog.LifetimeMax);

        // Two loops of one second, the longest rocket, then the longest burst born at its death.
        Assert.Equal(2 + Life(rocket) + Life(burst), SparkleSimulation.Length(sample.System.Emitters)!.Value, 6);
        Assert.Equal(2 + Life(rocket), SparkleSimulation.Length(sample.System.Emitters, e => e.Name != "Burst")!.Value, 6);

        SparkleEmitter forever = rocket.With(SparkleModuleKind.Lifecycle, m => m.WithValue(SparkleCatalog.LoopMode, "Infinite"));
        Assert.Null(SparkleSimulation.Length([forever, burst]));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task A_system_becomes_particle_types_of_the_mod(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        (string java, string resources) = await GenerateAsync(temp, loader, version, project => SparkleSample.AddTo(project));

        string behavior = File.ReadAllText(Path.Combine(java, "behavior/FireworksBehavior.java"));
        Assert.Contains("import com.mineengine.mods.testmod.particle.MineEngineSparkles;", behavior);
        Assert.Contains("MineEngineSparkles.play(level, \"firework\", ", behavior);
        Assert.Contains("MineEngineSparkles.playOn(player, \"testmod:firework\");", behavior);
        // V0.7.7: the graph sets a parameter, plays between two blocks and stops the systems.
        Assert.Contains("MineEngineSparkles.setNumber(\"magic\", \"Speed\", 12", behavior);
        Assert.Contains("MineEngineSparkles.playBetween(level, \"magic\", \"Target\", ", behavior);
        Assert.Contains("MineEngineSparkles.stopAt(level, ", behavior);
        Assert.Contains("MineEngineSparkles.stopOn(player);", behavior);

        string sparkles = File.ReadAllText(Path.Combine(java, "particle/MineEngineSparkles.java"));
        Assert.Contains("DeferredRegister.create(Registries.PARTICLE_TYPE, ", sparkles);
        Assert.Contains(" FIREWORK = system(\"firework\");", sparkles);
        Assert.Contains(" FIREWORK_E1 = PARTICLE_TYPES.register(\"firework_e1\", () -> new SimpleParticleType(false));", sparkles);
        Assert.Contains(loader == ModLoaderKind.NeoForge ? "DeferredHolder<ParticleType<?>, SimpleParticleType>" : "RegistryObject<SimpleParticleType>", sparkles);
        Assert.Contains(" MAGIC_P0 = parameter(\"magic\", \"Target\", 0);", sparkles);
        Assert.Contains(" MAGIC_P1 = parameter(\"magic\", \"Speed\", 1);", sparkles);
        Assert.Contains(" STOP = PARTICLE_TYPES.register(\"sparkle_stop\", ", sparkles);

        string data = File.ReadAllText(Path.Combine(java, "particle/MineEngineSparkleData.java"));
        Assert.Contains("s0e0.loops = 2;", data);
        Assert.Contains("s0e0.collides = true;", data);
        Assert.Contains("s0e0.deathChance = 100.0;", data);
        Assert.Contains("s0e1.eventSource = 1;", data);
        Assert.Contains("s0e1.countPerEvent = 4;", data);
        Assert.Contains("s0e0.speedMin = 0.2;", data); // 4 blocks per second.
        Assert.Contains("s0e1.eventType = DEATH;", data);
        // The second system: a beam, a ribbon on time events, a splash on the location events of a fountain.
        Assert.Contains("s1e0.shape = LINE;", data);
        // The end of the beam is linked to the user parameter "Target" (0 2 8).
        Assert.Contains("s1e0.lineY = 2.0;", data);
        Assert.Contains("s1e0.lineZ = 8.0;", data);
        Assert.Contains("s1e1.animated = true;", data);
        // The graphs change the linked settings of a playing copy of the emitters.
        Assert.Contains("case \"magic\" -> {", data);
        Assert.Contains("e[0].lineX = x;", data);
        Assert.Contains("e[3].speedMax = x / 20.0;", data);
        Assert.DoesNotContain("case \"firework\" -> {", data);
        string system = File.ReadAllText(Path.Combine(java, "client/SparkleSystemParticle.java"));
        Assert.Contains("MineEngineSparkleData.applyParameter(system, emitters, value.getKey(), v[0], v[1], v[2]);", system);
        Assert.DoesNotContain("s1e3.animated", data);
        Assert.Contains("setSpriteFromAge(sprites);", File.ReadAllText(Path.Combine(java, "client/SparkleParticle.java")));
        string fountainSprites = File.ReadAllText(Path.Combine(resources, "particles/magic_e3.json"));
        Assert.Contains("\"minecraft:bubble\"", fountainSprites);
        Assert.Contains("\"minecraft:glitter_3\"", fountainSprites);
        Assert.Contains("s1e1.timeChance = 100.0;", data);
        Assert.Contains("s1e1.timeTicks = 1;", data);
        Assert.Contains("s1e1.timeRepeats = true;", data);
        Assert.Contains("s1e2.eventType = TIME;", data);
        Assert.Contains("s1e3.locationChance = 100.0;", data);
        Assert.Contains("s1e3.zoneShape = BOX;", data);
        Assert.Contains("s1e3.zoneY = 3.0;", data);
        Assert.Contains("s1e4.eventType = LOCATION;", data);
        string particle = File.ReadAllText(Path.Combine(java, "client/SparkleParticle.java"));
        Assert.Contains("system.onEvent(index, MineEngineSparkleData.TIME, x, y, z, xd, yd, zd);", particle);
        Assert.Contains("system.onEvent(index, MineEngineSparkleData.LOCATION, x, y, z, xd, yd, zd);", particle);

        string client = File.ReadAllText(Path.Combine(java, "client/MineEngineSparkleClient.java"));
        Assert.Contains(loader == ModLoaderKind.NeoForge ? "import net.neoforged.neoforge.client.event.RegisterParticleProvidersEvent;" : "import net.minecraftforge.client.event.RegisterParticleProvidersEvent;", client);
        Assert.Contains("SparkleSystemParticle.start(level, \"firework\", x, y, z, (int) Math.round(xd) - 1));", client);
        Assert.Contains("SparkleSystemParticle.parameter(\"magic\", 1, xd, yd, zd));", client);
        Assert.Contains("SparkleSystemParticle.stop(x, y, z, (int) Math.round(xd) - 1, yd));", client);
        Assert.True(File.Exists(Path.Combine(java, "client/SparkleParticle.java")));
        Assert.Contains("Entity.collideBoundingBox(null,", File.ReadAllText(Path.Combine(java, "client/SparkleParticle.java")));

        string main = File.ReadAllText(Directory.GetFiles(java, "*.java").Single(f => File.ReadAllText(f).Contains("@Mod(", StringComparison.Ordinal)));
        Assert.Contains("MineEngineSparkles.PARTICLE_TYPES.register(modEventBus);", main);
        Assert.Contains("modEventBus.addListener(MineEngineSparkleClient::registerProviders);", main);

        Assert.Contains("\"minecraft:glitter_7\"", File.ReadAllText(Path.Combine(resources, "particles/firework_e0.json")));
        Assert.True(File.Exists(Path.Combine(resources, "particles/firework_e1.json")));
    }

    [Fact]
    public async Task A_mod_without_particle_system_has_no_sparkle_class()
    {
        using var temp = new TemporaryDirectory();
        (string java, _) = await GenerateAsync(temp, ModLoaderKind.NeoForge, "1.21.1", _ => { });

        Assert.False(Directory.Exists(Path.Combine(java, "particle")));
    }

    /// <summary>Generates a project and gives the folder of its Java package and of its assets.</summary>
    private static async Task<(string Java, string Resources)> GenerateAsync(TemporaryDirectory temp, ModLoaderKind loader, string version, Action<ModProject> addContent)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        addContent(project);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return (Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod"),
            Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources/assets/testmod"));
    }
}
