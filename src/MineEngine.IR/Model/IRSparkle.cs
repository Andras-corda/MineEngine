using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>Sparkle particle system (V0.7.5): its emitters, played by the graphs.</summary>
public sealed record IRSparkle(Guid SourceAssetId, ResourceId Id, string DisplayName)
{
    public IReadOnlyList<IRSparkleEmitter> Emitters { get; init; } = [];

    /// <summary>User parameters, in their order (their index travels with the particles that set them).</summary>
    public IReadOnlyList<IRSparkleParameter> Parameters { get; init; } = [];
}

/// <summary>User parameter of a system that the graphs can set before playing it (V0.7.7).</summary>
public sealed record IRSparkleParameter(string Name);

/// <summary>
/// A setting of an emitter linked to a user parameter: <see cref="Module"/> and <see cref="Setting"/>
/// name it as in the files ("SpawnParticles", "lineEnd"), <see cref="Parameter"/> is the index of the parameter.
/// </summary>
public sealed record IRSparkleBinding(string Module, string Setting, int Parameter);

/// <summary>Lifecycle of an emitter.</summary>
public enum IRSparkleLoop
{
    Once,
    Loop,
    Infinite,
}

public enum IRSparkleShape
{
    Point,
    Sphere,
    Box,

    /// <summary>Between the offset and <see cref="IRSparkleEmitter.LineEnd"/> (a beam).</summary>
    Line,
}

/// <summary>Event that makes an emitter spawn: a particle dies, reaches a time of its life, enters a zone.</summary>
public enum IRSparkleEvent
{
    Death,
    Time,
    Location,
}

public enum IRSparkleDirection
{
    Direction,
    Cone,
    Radial,
}

public enum IRSparkleScale
{
    Fixed,
    OverLife,
    Random,
}

/// <summary>Three numbers (y goes up).</summary>
public readonly record struct IRVector(double X, double Y, double Z);

/// <summary>
/// Emitter of a system, with its settings already read (seconds, blocks, blocks per second). Its
/// texture is either one of the game ("minecraft:glitter_7") or a file of the project copied into
/// the particle textures of the mod.
/// </summary>
public sealed record IRSparkleEmitter(string Name, int EventId)
{
    public IRSparkleLoop Loop { get; init; }

    public double Duration { get; init; } = 2;

    public int Loops { get; init; } = 1;

    public double Delay { get; init; }

    /// <summary>All at once at the start of each loop, or spread over its duration.</summary>
    public bool Burst { get; init; } = true;

    public int Count { get; init; } = 1;

    public IRSparkleShape Shape { get; init; }

    public IRVector Offset { get; init; }

    public double Radius { get; init; }

    public IRVector BoxSize { get; init; }

    public IRVector LineEnd { get; init; }

    public double LifetimeMin { get; init; } = 1;

    public double LifetimeMax { get; init; } = 1;

    public IRSparkleDirection DirectionMode { get; init; }

    public IRVector Direction { get; init; } = new(0, 1, 0);

    public double ConeAngle { get; init; }

    public double SpeedMin { get; init; }

    public double SpeedMax { get; init; }

    /// <summary>Acceleration, or null without gravity.</summary>
    public IRVector? Gravity { get; init; }

    public bool Collides { get; init; }

    public double Bounce { get; init; }

    public bool DiesOnCollision { get; init; }

    public IRSparkleScale ScaleMode { get; init; }

    /// <summary>Fixed: A. Over the life: from A to B. Random: between A and B.</summary>
    public double ScaleA { get; init; } = 0.2;

    public double ScaleB { get; init; } = 0.2;

    /// <summary>Textures of the particles, in their order (at least one).</summary>
    public IReadOnlyList<IRSparkleSprite> Sprites { get; init; } = [new("minecraft:glitter_7", null)];

    /// <summary>With several textures: the particle goes through them over its life, instead of taking one at random.</summary>
    public bool AnimatedSprites { get; init; }

    /// <summary>0xRRGGBB.</summary>
    public int Tint { get; init; } = 0xFFFFFF;

    public bool Glow { get; init; }

    public bool Translucent { get; init; } = true;

    /// <summary>Chance (0-100) that a dying particle sends a death event; null: no event.</summary>
    public double? DeathEventChance { get; init; }

    /// <summary>Chance (0-100) that a particle sends its time event; null: no time event.</summary>
    public double? TimeEventChance { get; init; }

    /// <summary>Time of the life of a particle when it sends its time event, or the interval between two.</summary>
    public double TimeEventTime { get; init; } = 0.5;

    /// <summary>The time event is sent at every interval, not once.</summary>
    public bool TimeEventRepeats { get; init; }

    /// <summary>Chance (0-100) that a particle entering the zone sends its location event; null: no location event.</summary>
    public double? LocationEventChance { get; init; }

    /// <summary>Sphere or box.</summary>
    public IRSparkleShape ZoneShape { get; init; } = IRSparkleShape.Sphere;

    /// <summary>Center of the zone, relative to the place where the system plays.</summary>
    public IRVector ZoneCenter { get; init; }

    public double ZoneRadius { get; init; } = 1;

    public IRVector ZoneSize { get; init; } = new(2, 2, 2);

    /// <summary>Emitter whose events spawn the particles of this one; null: its own lifecycle.</summary>
    public int? EventSource { get; init; }

    /// <summary>Kind of the events received from <see cref="EventSource"/>.</summary>
    public IRSparkleEvent ReceivedEvent { get; init; }

    public int CountPerEvent { get; init; } = 1;

    public bool AtDeathLocation { get; init; } = true;

    /// <summary>Part of the velocity of the dead particle given to the new ones (0-1).</summary>
    public double InheritVelocity { get; init; }

    /// <summary>Settings linked to user parameters: the graphs can change them when they play the system.</summary>
    public IReadOnlyList<IRSparkleBinding> Bindings { get; init; } = [];
}

/// <summary>
/// A texture of an emitter: one of the game ("minecraft:glitter_7"), or an image of the project
/// (<see cref="Texture"/>) copied into textures/particle/ of the mod.
/// </summary>
public sealed record IRSparkleSprite(string Sprite, IRTexture? Texture);
