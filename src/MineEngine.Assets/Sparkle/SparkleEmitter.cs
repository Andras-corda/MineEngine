using System.Collections.Immutable;
using System.Globalization;

namespace MineEngine.Assets.Sparkle;

/// <summary>Three numbers: a position, a size, a direction or an acceleration (y goes up).</summary>
public readonly record struct SparkleVector(double X, double Y, double Z)
{
    public static SparkleVector Zero => default;

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public SparkleVector Normalized() => Length is > 1e-9 and var length ? this / length : Zero;

    public static SparkleVector operator +(SparkleVector a, SparkleVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static SparkleVector operator -(SparkleVector a, SparkleVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static SparkleVector operator *(SparkleVector a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    public static SparkleVector operator /(SparkleVector a, double k) => new(a.X / k, a.Y / k, a.Z / k);

    /// <summary>"0 1.5 0".</summary>
    public static string Format(SparkleVector vector) =>
        string.Join(' ', new[] { vector.X, vector.Y, vector.Z }.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Reads "x y z" (or "x, y, z"); null when it is invalid.</summary>
    public static SparkleVector? Parse(string? text)
    {
        string[] parts = (text ?? string.Empty).Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        double[] values = [.. parts.Select(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v) ? v : double.NaN)];
        return values.Length == 3 && values.All(double.IsFinite) ? new SparkleVector(values[0], values[1], values[2]) : null;
    }
}

/// <summary>A module of an emitter: switched on or off, and its settings (missing ones take their default).</summary>
public sealed record SparkleModule(SparkleModuleKind Kind, bool Enabled, ImmutableDictionary<string, string> Values)
{
    public SparkleModuleInfo Info => SparkleCatalog.Get(Kind);

    public static SparkleModule Create(SparkleModuleKind kind) =>
        new(kind, !SparkleCatalog.Get(kind).IsOptional || SparkleCatalog.Get(kind).EnabledByDefault, ImmutableDictionary<string, string>.Empty);

    public string GetValue(string name) =>
        Values.TryGetValue(name, out string? value) ? value : Info.FindParameter(name)?.DefaultValue ?? string.Empty;

    public SparkleModule WithValue(string name, string value) => this with { Values = Values.SetItem(name, value ?? string.Empty) };

    /// <summary>Number of a setting, clamped to its bounds (its default when the text is invalid).</summary>
    public double Number(string name)
    {
        SparkleParameterInfo? parameter = Info.FindParameter(name);
        return parameter?.ParseNumber(GetValue(name)) ?? parameter?.ParseNumber(parameter.DefaultValue) ?? 0;
    }

    public int Integer(string name) => (int)Number(name);

    public bool Boolean(string name) => string.Equals(GetValue(name), "true", StringComparison.OrdinalIgnoreCase);

    public SparkleVector Vector(string name) =>
        SparkleVector.Parse(GetValue(name)) ?? SparkleVector.Parse(Info.FindParameter(name)?.DefaultValue) ?? SparkleVector.Zero;

    /// <summary>Value of a choice setting, or its default when the text is not one of its values.</summary>
    public string Choice(string name)
    {
        string value = GetValue(name);
        SparkleParameterInfo? parameter = Info.FindParameter(name);
        return parameter is null || parameter.Choices.Any(c => c.Value == value) ? value : parameter.DefaultValue;
    }

    /// <summary>"#RRGGBB" as 0xRRGGBB (white when invalid).</summary>
    public int Color(string name)
    {
        string text = GetValue(name).TrimStart('#');
        return text.Length == 6 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb) ? rgb : 0xFFFFFF;
    }
}

/// <summary>
/// Emitter of a Sparkle system: its head (shown in the preview or not, name) and its body (the
/// modules of every part, one of each kind). Immutable: a change replaces the emitter in the system.
/// </summary>
public sealed record SparkleEmitter(Guid Id, string Name, bool Preview, ImmutableList<SparkleModule> Modules)
{
    /// <summary>Horizontal space between two emitters placed side by side in the editor.</summary>
    public const double NodeSpacing = 320;

    /// <summary>Position of its node in the editor (top left corner); it has no effect on the particles.</summary>
    public double X { get; init; }

    public double Y { get; init; }

    /// <summary>A new emitter with the default settings and the given identifier for the events.</summary>
    public static SparkleEmitter Create(string name, int eventId) =>
        new SparkleEmitter(Guid.NewGuid(), name, true, [.. SparkleCatalog.All.Select(i => SparkleModule.Create(i.Kind))])
            .With(SparkleModuleKind.Identity, m => m.WithValue(SparkleCatalog.EmitterId, eventId.ToString(CultureInfo.InvariantCulture)));

    /// <summary>The module of a kind (its default when the file does not have it).</summary>
    public SparkleModule Get(SparkleModuleKind kind) => Modules.FirstOrDefault(m => m.Kind == kind) ?? SparkleModule.Create(kind);

    /// <summary>True if the module applies (always for a module that cannot be switched off).</summary>
    public bool Uses(SparkleModuleKind kind) => !SparkleCatalog.Get(kind).IsOptional || Get(kind).Enabled;

    public SparkleEmitter With(SparkleModuleKind kind, Func<SparkleModule, SparkleModule> change)
    {
        SparkleModule updated = change(Get(kind));
        int index = Modules.FindIndex(m => m.Kind == kind);
        return this with { Modules = index < 0 ? Modules.Add(updated) : Modules.SetItem(index, updated) };
    }

    /// <summary>Number used by the events (ID module).</summary>
    public int EventId => Get(SparkleModuleKind.Identity).Integer(SparkleCatalog.EmitterId);

    /// <summary>Spawns only when it receives a death event of another emitter.</summary>
    public bool IsEventDriven => Uses(SparkleModuleKind.ReceiveDeathEvent);

    /// <summary>Emitter whose events it receives, when it is driven by events.</summary>
    public int? EventSource => IsEventDriven ? Get(SparkleModuleKind.ReceiveDeathEvent).Integer(SparkleCatalog.Source) : null;

    /// <summary>Kind of the events it receives, when it is driven by events.</summary>
    public SparkleEventKind? ReceivedEvent => IsEventDriven
        ? Enum.Parse<SparkleEventKind>(Get(SparkleModuleKind.ReceiveDeathEvent).Choice(SparkleCatalog.EventType))
        : null;

    /// <summary>True if its particles send this kind of event.</summary>
    public bool Sends(SparkleEventKind kind) => Uses(GeneratorOf(kind));

    /// <summary>Module that sends a kind of event.</summary>
    public static SparkleModuleKind GeneratorOf(SparkleEventKind kind) => kind switch
    {
        SparkleEventKind.Time => SparkleModuleKind.GenerateTimeEvent,
        SparkleEventKind.Location => SparkleModuleKind.GenerateLocationEvent,
        _ => SparkleModuleKind.GenerateDeathEvent,
    };

    /// <summary>Copy with new identifiers (head and body unchanged).</summary>
    public SparkleEmitter Duplicate(string name, int eventId) =>
        (this with { Id = Guid.NewGuid(), Name = name })
            .With(SparkleModuleKind.Identity, m => m.WithValue(SparkleCatalog.EmitterId, eventId.ToString(CultureInfo.InvariantCulture)));

    public override string ToString() => Name;
}
