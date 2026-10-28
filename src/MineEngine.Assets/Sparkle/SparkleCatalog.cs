using System.Globalization;

namespace MineEngine.Assets.Sparkle;

/// <summary>Part of the body of an emitter, shown like a folder (Niagara's stages).</summary>
public enum SparkleSection
{
    Properties,
    EmitterSpawn,
    ParticleSpawn,
    ParticleUpdate,
    Render,
    Event,
}

/// <summary>Module of an emitter: a group of settings of one of its parts.</summary>
public enum SparkleModuleKind
{
    /// <summary>Resources taken on the processor and the graphics card, and a summary (read only).</summary>
    Performance,

    Lifecycle,

    /// <summary>Numeric identifier of the emitter, used by the events.</summary>
    Identity,

    SpawnParticles,
    Velocity,
    Gravity,
    Collision,
    Scale,
    Texture,
    GenerateDeathEvent,

    /// <summary>Event sent by a particle once after a time of its life, or every interval (V0.7.6).</summary>
    GenerateTimeEvent,

    /// <summary>Event sent by a particle when it enters a zone, a sphere or a box (V0.7.6).</summary>
    GenerateLocationEvent,

    /// <summary>Spawns on the events of another emitter: deaths, times or zones (named before V0.7.6, when only deaths existed).</summary>
    ReceiveDeathEvent,
}

/// <summary>Event of a particle that makes another emitter spawn particles.</summary>
public enum SparkleEventKind
{
    Death,
    Time,
    Location,
}

/// <summary>Kind of value of a module setting.</summary>
public enum SparkleParameterType
{
    Number,
    Integer,
    Boolean,

    /// <summary>A value among <see cref="SparkleParameterInfo.Choices"/>.</summary>
    Choice,

    /// <summary>Three numbers "x y z" (invariant culture).</summary>
    Vector,

    /// <summary>A particle texture: "asset:guid" (texture of the project) or "minecraft:glitter_7" (texture of the game).</summary>
    Texture,

    /// <summary>"#RRGGBB".</summary>
    Color,

    /// <summary>The identifier of another emitter of the system (<see cref="SparkleCatalog.EmitterId"/>).</summary>
    EmitterId,

    /// <summary>Several particle textures separated by ';' (each one like <see cref="Texture"/>).</summary>
    TextureList,
}

/// <summary>Value offered by a choice setting: stored value and label.</summary>
public sealed record SparkleChoice(string Value, string Label);

/// <summary>
/// A setting of a module. Values are stored as text: numbers in invariant culture, "true"/"false",
/// name of a choice, "x y z" for a vector.
/// </summary>
public sealed record SparkleParameterInfo
{
    public SparkleParameterInfo(string name, string label, SparkleParameterType type, string defaultValue)
    {
        Name = name;
        Label = label;
        Type = type;
        DefaultValue = defaultValue;
    }

    /// <summary>Name stored in the file ("speedMin").</summary>
    public string Name { get; }

    public string Label { get; }

    public SparkleParameterType Type { get; }

    public string DefaultValue { get; }

    public string? Hint { get; init; }

    public double Min { get; init; } = double.MinValue;

    public double Max { get; init; } = double.MaxValue;

    public IReadOnlyList<SparkleChoice> Choices { get; init; } = [];

    /// <summary>The setting is shown only when the choice <c>Name</c> of the module has one of these values.</summary>
    public (string Name, string[] Values)? VisibleWhen { get; init; }

    public bool IsVisible(Func<string, string> value) =>
        VisibleWhen is not { } condition || condition.Values.Contains(value(condition.Name));

    /// <summary>Reads a number; null when it is invalid. The value is clamped to the bounds.</summary>
    public double? ParseNumber(string? text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
        {
            return null;
        }

        value = Math.Clamp(value, Min, Max);
        return Type == SparkleParameterType.Integer ? Math.Round(value) : value;
    }
}

/// <summary>Description of a module: part of the body, title, help and settings.</summary>
public sealed class SparkleModuleInfo
{
    public required SparkleModuleKind Kind { get; init; }

    public required SparkleSection Section { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<SparkleParameterInfo> Parameters { get; init; } = [];

    /// <summary>The module can be switched off (gravity, collision...); the others always apply.</summary>
    public bool IsOptional { get; init; }

    /// <summary>State of an optional module in a new emitter.</summary>
    public bool EnabledByDefault { get; init; } = true;

    public SparkleParameterInfo? FindParameter(string name) => Parameters.FirstOrDefault(p => p.Name == name);

    public override string ToString() => Title;
}

/// <summary>
/// Catalog of the modules of a Sparkle emitter, sorted by part of its body: properties, emitter
/// spawn, particle spawn, particle update, render and events (Sparkle.md, inspired by Niagara).
/// Units: seconds, blocks, blocks per second, blocks per second squared.
/// </summary>
public static class SparkleCatalog
{
    // Lifecycle.
    public const string LoopMode = "mode";
    public const string Duration = "duration";
    public const string Loops = "loops";
    public const string Delay = "delay";

    // Identity.
    public const string EmitterId = "id";

    // Particle spawn.
    public const string SpawnMode = "mode";
    public const string Count = "count";
    public const string Shape = "shape";
    public const string Offset = "offset";
    public const string Radius = "radius";
    public const string BoxSize = "size";
    public const string LifetimeMin = "lifetimeMin";
    public const string LifetimeMax = "lifetimeMax";
    public const string LineEnd = "lineEnd";

    // Velocity.
    public const string VelocityMode = "mode";
    public const string Direction = "direction";
    public const string ConeAngle = "angle";
    public const string SpeedMin = "speedMin";
    public const string SpeedMax = "speedMax";

    // Gravity.
    public const string Acceleration = "acceleration";

    // Collision.
    public const string Bounce = "bounce";
    public const string DieOnCollision = "die";

    // Scale.
    public const string ScaleMode = "mode";
    public const string Value = "value";
    public const string Start = "start";
    public const string End = "end";
    public const string RangeMin = "min";
    public const string RangeMax = "max";

    // Texture.
    public const string Sprite = "texture";
    public const string SpriteMode = "spriteMode";
    public const string Tint = "tint";
    public const string Glow = "glow";
    public const string Translucent = "translucent";

    // Events.
    public const string Chance = "chance";
    public const string TimeMode = "mode";
    public const string EventTime = "time";
    public const string ZoneShape = "shape";
    public const string ZoneCenter = "center";
    public const string ZoneRadius = "radius";
    public const string ZoneSize = "size";
    public const string Source = "source";
    public const string EventType = "event";
    public const string CountPerEvent = "count";
    public const string AtDeathLocation = "atDeath";
    public const string InheritVelocity = "inheritVelocity";

    /// <summary>Texture of a new emitter: a soft star of the game.</summary>
    public const string DefaultSprite = "minecraft:glitter_7";

    /// <summary>Textures of a texture list setting ("a;b"), in their order; never empty.</summary>
    public static IReadOnlyList<string> SplitTextures(string? value)
    {
        string[] textures = (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return textures.Length > 0 ? textures : [DefaultSprite];
    }

    public static string JoinTextures(IEnumerable<string> textures) => string.Join(';', textures.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));

    private static readonly Lazy<Dictionary<SparkleModuleKind, SparkleModuleInfo>> LazyInfos = new(() => Build().ToDictionary(i => i.Kind));

    private static Dictionary<SparkleModuleKind, SparkleModuleInfo> Infos => LazyInfos.Value;

    /// <summary>Every module, in the order of the body.</summary>
    public static IReadOnlyList<SparkleModuleInfo> All { get; } = [.. Enum.GetValues<SparkleModuleKind>().Select(Get)];

    public static SparkleModuleInfo Get(SparkleModuleKind kind) =>
        Infos.TryGetValue(kind, out SparkleModuleInfo? info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, Tr.T("Unknown module."));

    public static IEnumerable<SparkleModuleInfo> InSection(SparkleSection section) => All.Where(i => i.Section == section);

    /// <summary>Title of a part of the body.</summary>
    public static string SectionTitle(SparkleSection section) => section switch
    {
        SparkleSection.Properties => Tr.T("Properties"),
        SparkleSection.EmitterSpawn => Tr.T("Emitter spawn"),
        SparkleSection.ParticleSpawn => Tr.T("Particle spawn"),
        SparkleSection.ParticleUpdate => Tr.T("Particle update"),
        SparkleSection.Render => Tr.T("Render"),
        _ => Tr.T("Event"),
    };

    private static SparkleParameterInfo Number(string name, string label, double value, double min = 0, double max = 10000) =>
        new(name, label, SparkleParameterType.Number, Text(value)) { Min = min, Max = max };

    private static SparkleParameterInfo Integer(string name, string label, int value, int min = 0, int max = 100000) =>
        new(name, label, SparkleParameterType.Integer, Text(value)) { Min = min, Max = max };

    private static SparkleParameterInfo Boolean(string name, string label, bool value) =>
        new(name, label, SparkleParameterType.Boolean, value ? "true" : "false");

    private static SparkleParameterInfo Vector(string name, string label, double x, double y, double z) =>
        new(name, label, SparkleParameterType.Vector, SparkleVector.Format(new SparkleVector(x, y, z))) { Min = -10000, Max = 10000 };

    private static SparkleParameterInfo Choice(string name, string label, params SparkleChoice[] choices) =>
        new(name, label, SparkleParameterType.Choice, choices[0].Value) { Choices = choices };

    private static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<SparkleModuleInfo> Build()
    {
        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.Performance,
            Section = SparkleSection.Properties,
            Title = Tr.T("Performance"),
            Description = Tr.T("Resources taken on the processor and the graphics card by the emitter, and a summary."),
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.Lifecycle,
            Section = SparkleSection.EmitterSpawn,
            Title = Tr.T("Lifecycle"),
            Description = Tr.T("How long the emitter spawns particles: once, a number of times, or until the system is stopped."),
            Parameters =
            [
                Choice(LoopMode, Tr.T("Mode"), new("Once", Tr.T("Once")), new("Loop", Tr.T("Loops")), new("Infinite", Tr.T("Infinite"))),
                Number(Duration, Tr.T("Duration (s)"), 2, 0.05, 3600),
                Integer(Loops, Tr.T("Loops"), 3, 1, 1000) with { VisibleWhen = (LoopMode, ["Loop"]) },
                Number(Delay, Tr.T("Delay (s)"), 0, 0, 3600),
            ],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.Identity,
            Section = SparkleSection.EmitterSpawn,
            Title = Tr.T("ID"),
            Description = Tr.T("Number that identifies the emitter in the system: the events are sent and received with it."),
            Parameters = [Integer(EmitterId, Tr.T("ID"), 1, 0, 9999)],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.SpawnParticles,
            Section = SparkleSection.ParticleSpawn,
            Title = Tr.T("Spawn particle"),
            Description = Tr.T("Where the particles appear, how many in all and how long each one lives."),
            Parameters =
            [
                Choice(SpawnMode, Tr.T("Spawn"), new("Burst", Tr.T("All at once")), new("Continuous", Tr.T("Spread over the duration"))),
                Integer(Count, Tr.T("Number of particles"), 30, 1, 10000) with { Hint = Tr.T("In all, for each loop of the emitter.") },
                Choice(Shape, Tr.T("Shape"), new("Point", Tr.T("Point")), new("Sphere", Tr.T("Sphere")), new("Box", Tr.T("Box")), new("Line", Tr.T("Line"))),
                Vector(Offset, Tr.T("Coordinates"), 0, 0, 0) with { Hint = Tr.T("Relative to the place where the system plays, in blocks.") },
                Number(Radius, Tr.T("Radius"), 0.5, 0, 64) with { VisibleWhen = (Shape, ["Sphere"]) },
                Vector(BoxSize, Tr.T("Size"), 1, 1, 1) with { VisibleWhen = (Shape, ["Box"]) },
                Vector(LineEnd, Tr.T("End of the line"), 0, 0, 5) with
                {
                    VisibleWhen = (Shape, ["Line"]),
                    Hint = Tr.T("The particles appear between the coordinates and this point (a beam), relative to the place where the system plays."),
                },
                Number(LifetimeMin, Tr.T("Lifetime min (s)"), 1, 0.05, 600),
                Number(LifetimeMax, Tr.T("Lifetime max (s)"), 1.5, 0.05, 600),
            ],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.Velocity,
            Section = SparkleSection.ParticleSpawn,
            Title = Tr.T("Velocity"),
            Description = Tr.T("Speed and direction of the particles when they appear."),
            Parameters =
            [
                Choice(VelocityMode, Tr.T("Direction"), new("Direction", Tr.T("Fixed direction")), new("Cone", Tr.T("Cone")), new("Radial", Tr.T("From the center"))),
                Vector(Direction, Tr.T("Vector"), 0, 1, 0) with { VisibleWhen = (VelocityMode, ["Direction", "Cone"]) },
                Number(ConeAngle, Tr.T("Cone angle (°)"), 30, 0, 180) with { VisibleWhen = (VelocityMode, ["Cone"]) },
                Number(SpeedMin, Tr.T("Speed min (blocks/s)"), 1, 0, 1000),
                Number(SpeedMax, Tr.T("Speed max (blocks/s)"), 2, 0, 1000),
            ],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.Gravity,
            Section = SparkleSection.ParticleUpdate,
            Title = Tr.T("Gravity"),
            Description = Tr.T("Constant acceleration applied to the particles."),
            IsOptional = true,
            Parameters = [Vector(Acceleration, Tr.T("Acceleration (blocks/s²)"), 0, -9.8, 0)],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.Collision,
            Section = SparkleSection.ParticleUpdate,
            Title = Tr.T("Collision"),
            Description = Tr.T("The particles stop on the blocks of the world: they bounce or die."),
            IsOptional = true,
            EnabledByDefault = false,
            Parameters =
            [
                Number(Bounce, Tr.T("Bounce"), 0.4, 0, 1) with { Hint = Tr.T("0: stops, 1: keeps all its speed.") },
                Boolean(DieOnCollision, Tr.T("Dies on collision"), false),
            ],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.Scale,
            Section = SparkleSection.ParticleUpdate,
            Title = Tr.T("Scale"),
            Description = Tr.T("Size of the particles: fixed, changing over their life, or random in a range."),
            Parameters =
            [
                Choice(ScaleMode, Tr.T("Mode"), new("Fixed", Tr.T("Fixed")), new("OverLife", Tr.T("Over the life")), new("Random", Tr.T("Random"))),
                Number(Value, Tr.T("Size (blocks)"), 0.2, 0, 64) with { VisibleWhen = (ScaleMode, ["Fixed"]) },
                Number(Start, Tr.T("Size at birth"), 0.3, 0, 64) with { VisibleWhen = (ScaleMode, ["OverLife"]) },
                Number(End, Tr.T("Size at death"), 0, 0, 64) with { VisibleWhen = (ScaleMode, ["OverLife"]) },
                Number(RangeMin, Tr.T("Size min"), 0.1, 0, 64) with { VisibleWhen = (ScaleMode, ["Random"]) },
                Number(RangeMax, Tr.T("Size max"), 0.3, 0, 64) with { VisibleWhen = (ScaleMode, ["Random"]) },
            ],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.Texture,
            Section = SparkleSection.Render,
            Title = Tr.T("Texture"),
            Description = Tr.T("Images of the particles, their tint and their light. With several images, each particle takes one at random, or goes through them over its life."),
            Parameters =
            [
                new SparkleParameterInfo(Sprite, Tr.T("Textures"), SparkleParameterType.TextureList, DefaultSprite),
                Choice(SpriteMode, Tr.T("With several textures"), new("Random", Tr.T("One at random for each particle")), new("OverLife", Tr.T("Animated over the life"))),
                new SparkleParameterInfo(Tint, Tr.T("Tint"), SparkleParameterType.Color, "#FFFFFF"),
                Boolean(Glow, Tr.T("Glows in the dark"), true),
                Boolean(Translucent, Tr.T("Translucent"), true),
            ],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.GenerateDeathEvent,
            Section = SparkleSection.Event,
            Title = Tr.T("Generate death event"),
            Description = Tr.T("When a particle dies, the emitters that receive the events of this one spawn particles (where it died)."),
            IsOptional = true,
            EnabledByDefault = false,
            Parameters = [Number(Chance, Tr.T("Chance (%)"), 100, 0, 100)],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.GenerateTimeEvent,
            Section = SparkleSection.Event,
            Title = Tr.T("Generate time event"),
            Description = Tr.T("Each particle sends an event once after a time of its life, or at every interval: the emitters that receive it spawn particles where it is (a trail, sparks...)."),
            IsOptional = true,
            EnabledByDefault = false,
            Parameters =
            [
                Choice(TimeMode, Tr.T("When"), new("Once", Tr.T("Once, after the time")), new("Repeat", Tr.T("At every interval"))),
                Number(EventTime, Tr.T("Time (s)"), 0.5, 0.05, 600) with { Hint = Tr.T("Counted from the birth of the particle.") },
                Number(Chance, Tr.T("Chance (%)"), 100, 0, 100),
            ],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.GenerateLocationEvent,
            Section = SparkleSection.Event,
            Title = Tr.T("Generate location event"),
            Description = Tr.T("A particle sends an event when it enters a zone, a sphere or a box: the emitters that receive it spawn particles where it is."),
            IsOptional = true,
            EnabledByDefault = false,
            Parameters =
            [
                Choice(ZoneShape, Tr.T("Zone"), new("Sphere", Tr.T("Sphere")), new("Box", Tr.T("Box"))),
                Vector(ZoneCenter, Tr.T("Center of the zone"), 0, 2, 0) with { Hint = Tr.T("Relative to the place where the system plays, in blocks.") },
                Number(ZoneRadius, Tr.T("Radius"), 1, 0, 64) with { VisibleWhen = (ZoneShape, ["Sphere"]) },
                Vector(ZoneSize, Tr.T("Size"), 2, 2, 2) with { VisibleWhen = (ZoneShape, ["Box"]) },
                Number(Chance, Tr.T("Chance (%)"), 100, 0, 100),
            ],
        };

        yield return new SparkleModuleInfo
        {
            Kind = SparkleModuleKind.ReceiveDeathEvent,
            Section = SparkleSection.Event,
            Title = Tr.T("Receive event"),
            Description = Tr.T("The emitter spawns particles on the events of another emitter (deaths, times or zones), instead of following its lifecycle."),
            IsOptional = true,
            EnabledByDefault = false,
            Parameters =
            [
                Choice(EventType, Tr.T("Event"), new("Death", Tr.T("Death")), new("Time", Tr.T("Time")), new("Location", Tr.T("Location (zone)"))),
                new SparkleParameterInfo(Source, Tr.T("Emitter ID"), SparkleParameterType.EmitterId, "1"),
                Integer(CountPerEvent, Tr.T("Particles per event"), 5, 1, 1000),
                Boolean(AtDeathLocation, Tr.T("At the place of the event"), true),
                Number(InheritVelocity, Tr.T("Inherited velocity (%)"), 0, 0, 100),
            ],
        };
    }
}
