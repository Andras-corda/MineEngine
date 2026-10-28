namespace MineEngine.Assets.Sparkle;

/// <summary>Pre-configured emitters offered when an emitter is added (V0.7.6), like the templates of Niagara.</summary>
public enum SparkleTemplate
{
    /// <summary>The default settings.</summary>
    Empty,

    /// <summary>A ray of light from a point to another (Line shape).</summary>
    Beam,

    /// <summary>Drops thrown up in a narrow cone that fall back.</summary>
    Fountain,

    /// <summary>Balls that bounce on the ground until they stop.</summary>
    Bounce,

    /// <summary>Heads that leave a trail behind them (a time event repeated on a second emitter).</summary>
    Ribbon,
}

/// <summary>Description of a template, for the add menu.</summary>
public sealed record SparkleTemplateInfo(SparkleTemplate Template, string Title, string Description);

/// <summary>
/// Builds the emitters of a template. A template is one emitter, or several linked by their events
/// (the ribbon: the heads send a time event at every interval, the trail spawns where they are).
/// </summary>
public static class SparkleTemplates
{
    public static IReadOnlyList<SparkleTemplateInfo> All { get; } =
    [
        new(SparkleTemplate.Empty, Tr.T("Empty emitter"), Tr.T("An emitter with the default settings.")),
        new(SparkleTemplate.Beam, Tr.T("Beam"), Tr.T("A ray of light that goes from a point to another (End of the line).")),
        new(SparkleTemplate.Fountain, Tr.T("Fountain"), Tr.T("Drops thrown up without end, that fall back.")),
        new(SparkleTemplate.Bounce, Tr.T("Bounce"), Tr.T("Balls that burst out and bounce on the ground until they stop.")),
        new(SparkleTemplate.Ribbon, Tr.T("Ribbon"), Tr.T("Heads that leave a ribbon behind them: two emitters linked by a time event.")),
    ];

    public static SparkleTemplateInfo Info(SparkleTemplate template) => All.First(t => t.Template == template);

    /// <summary>
    /// Emitters of the template, the first one with its node at (<paramref name="x"/>, <paramref name="y"/>).
    /// <paramref name="nextId"/> gives a free event ID at each call.
    /// </summary>
    public static IReadOnlyList<SparkleEmitter> Create(SparkleTemplate template, string name, Func<int> nextId, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(nextId);
        SparkleEmitter Place(SparkleEmitter emitter, int column = 0) => emitter with { X = x + column * SparkleEmitter.NodeSpacing, Y = y };

        switch (template)
        {
            case SparkleTemplate.Beam:
                return [Place(SparkleEmitter.Create(name, nextId())
                    .With(SparkleModuleKind.Lifecycle, m => m.WithValue(SparkleCatalog.LoopMode, "Infinite").WithValue(SparkleCatalog.Duration, "1"))
                    .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.SpawnMode, "Continuous").WithValue(SparkleCatalog.Count, "120")
                        .WithValue(SparkleCatalog.Shape, "Line").WithValue(SparkleCatalog.Offset, "0 1 0").WithValue(SparkleCatalog.LineEnd, "0 1 6")
                        .WithValue(SparkleCatalog.LifetimeMin, "0.2").WithValue(SparkleCatalog.LifetimeMax, "0.35"))
                    .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.VelocityMode, "Radial").WithValue(SparkleCatalog.SpeedMin, "0").WithValue(SparkleCatalog.SpeedMax, "0.2"))
                    .With(SparkleModuleKind.Gravity, m => m with { Enabled = false })
                    .With(SparkleModuleKind.Scale, m => m.WithValue(SparkleCatalog.ScaleMode, "OverLife").WithValue(SparkleCatalog.Start, "0.25").WithValue(SparkleCatalog.End, "0.05"))
                    .With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Tint, "#7FE7FF").WithValue(SparkleCatalog.Glow, "true")))];

            case SparkleTemplate.Fountain:
                return [Place(SparkleEmitter.Create(name, nextId())
                    .With(SparkleModuleKind.Lifecycle, m => m.WithValue(SparkleCatalog.LoopMode, "Infinite").WithValue(SparkleCatalog.Duration, "1"))
                    .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.SpawnMode, "Continuous").WithValue(SparkleCatalog.Count, "60")
                        .WithValue(SparkleCatalog.Shape, "Sphere").WithValue(SparkleCatalog.Radius, "0.1")
                        .WithValue(SparkleCatalog.LifetimeMin, "1.2").WithValue(SparkleCatalog.LifetimeMax, "1.6"))
                    .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.VelocityMode, "Cone").WithValue(SparkleCatalog.ConeAngle, "12")
                        .WithValue(SparkleCatalog.SpeedMin, "6").WithValue(SparkleCatalog.SpeedMax, "8"))
                    .With(SparkleModuleKind.Collision, m => (m with { Enabled = true }).WithValue(SparkleCatalog.Bounce, "0.1"))
                    .With(SparkleModuleKind.Scale, m => m.WithValue(SparkleCatalog.ScaleMode, "Random").WithValue(SparkleCatalog.RangeMin, "0.12").WithValue(SparkleCatalog.RangeMax, "0.22"))
                    .With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Tint, "#4FA3FF").WithValue(SparkleCatalog.Glow, "false")))];

            case SparkleTemplate.Bounce:
                return [Place(SparkleEmitter.Create(name, nextId())
                    .With(SparkleModuleKind.Lifecycle, m => m.WithValue(SparkleCatalog.LoopMode, "Loop").WithValue(SparkleCatalog.Loops, "3").WithValue(SparkleCatalog.Duration, "2.5"))
                    .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "20").WithValue(SparkleCatalog.Offset, "0 1 0")
                        .WithValue(SparkleCatalog.LifetimeMin, "2").WithValue(SparkleCatalog.LifetimeMax, "2.4"))
                    .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.VelocityMode, "Cone").WithValue(SparkleCatalog.ConeAngle, "60")
                        .WithValue(SparkleCatalog.SpeedMin, "3").WithValue(SparkleCatalog.SpeedMax, "6"))
                    .With(SparkleModuleKind.Collision, m => (m with { Enabled = true }).WithValue(SparkleCatalog.Bounce, "0.7"))
                    .With(SparkleModuleKind.Scale, m => m.WithValue(SparkleCatalog.Value, "0.25"))
                    .With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Tint, "#FFB347")))];

            case SparkleTemplate.Ribbon:
                int headId = nextId();
                SparkleEmitter head = SparkleEmitter.Create(name, headId)
                    .With(SparkleModuleKind.Lifecycle, m => m.WithValue(SparkleCatalog.LoopMode, "Infinite").WithValue(SparkleCatalog.Duration, "1.5"))
                    .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.Count, "3").WithValue(SparkleCatalog.Offset, "0 0.5 0")
                        .WithValue(SparkleCatalog.LifetimeMin, "1.2").WithValue(SparkleCatalog.LifetimeMax, "1.5"))
                    .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.VelocityMode, "Cone").WithValue(SparkleCatalog.ConeAngle, "50")
                        .WithValue(SparkleCatalog.SpeedMin, "4").WithValue(SparkleCatalog.SpeedMax, "6"))
                    .With(SparkleModuleKind.Gravity, m => m.WithValue(SparkleCatalog.Acceleration, "0 -4 0"))
                    .With(SparkleModuleKind.Scale, m => m.WithValue(SparkleCatalog.Value, "0.2"))
                    .With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Tint, "#FF6FD8"))
                    .With(SparkleModuleKind.GenerateTimeEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.TimeMode, "Repeat").WithValue(SparkleCatalog.EventTime, "0.05"));
                SparkleEmitter trail = SparkleEmitter.Create(Tr.F("{0} trail", name), nextId())
                    .With(SparkleModuleKind.SpawnParticles, m => m.WithValue(SparkleCatalog.LifetimeMin, "0.5").WithValue(SparkleCatalog.LifetimeMax, "0.6"))
                    .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.SpeedMin, "0").WithValue(SparkleCatalog.SpeedMax, "0"))
                    .With(SparkleModuleKind.Gravity, m => m with { Enabled = false })
                    .With(SparkleModuleKind.Scale, m => m.WithValue(SparkleCatalog.ScaleMode, "OverLife").WithValue(SparkleCatalog.Start, "0.18").WithValue(SparkleCatalog.End, "0"))
                    .With(SparkleModuleKind.Texture, m => m.WithValue(SparkleCatalog.Tint, "#C86BFF"))
                    .With(SparkleModuleKind.ReceiveDeathEvent, m => (m with { Enabled = true }).WithValue(SparkleCatalog.EventType, "Time")
                        .WithValue(SparkleCatalog.Source, headId.ToString(System.Globalization.CultureInfo.InvariantCulture)).WithValue(SparkleCatalog.CountPerEvent, "1"));
                return [Place(head), Place(trail, 1)];

            default:
                return [Place(SparkleEmitter.Create(name, nextId()))];
        }
    }
}
