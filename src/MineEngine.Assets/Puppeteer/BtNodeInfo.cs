using System.Globalization;

namespace MineEngine.Assets.Puppeteer;

/// <summary>Kind of value of a node setting.</summary>
public enum BtParameterType
{
    Number,
    Integer,
    Boolean,

    /// <summary>A value among <see cref="BtParameterInfo.Choices"/>.</summary>
    Choice,

    /// <summary>A blackboard key (identifier of the key), of one of the types of <see cref="BtParameterInfo.KeyTypes"/>.</summary>
    Key,

    /// <summary>A sound of the project or of the game (reference text: "asset:guid" or "namespace:path").</summary>
    Sound,

    /// <summary>A behavior tree of the project marked as public goal (identifier of the asset).</summary>
    Goal,
}

/// <summary>Value offered by a choice setting: stored value and label.</summary>
public sealed record BtChoice(string Value, string Label);

/// <summary>
/// A setting of a kind of node. Values are stored as text, like those of graph nodes:
/// numbers in invariant culture, "true"/"false", name of an enumeration value,
/// identifier of a blackboard key or reference of a sound.
/// </summary>
public sealed class BtParameterInfo
{
    public BtParameterInfo(string name, string label, BtParameterType type, string defaultValue)
    {
        Name = name;
        Label = label;
        Type = type;
        DefaultValue = defaultValue;
    }

    /// <summary>Name stored in the file ("speed").</summary>
    public string Name { get; }

    public string Label { get; }

    public BtParameterType Type { get; }

    public string DefaultValue { get; }

    public string? Hint { get; init; }

    public double Min { get; init; } = double.MinValue;

    public double Max { get; init; } = double.MaxValue;

    public IReadOnlyList<BtChoice> Choices { get; init; } = [];

    /// <summary>Key types accepted by a <see cref="BtParameterType.Key"/> setting (empty: all).</summary>
    public IReadOnlyList<BlackboardKeyType> KeyTypes { get; init; } = [];

    /// <summary>An optional setting can stay empty (no key chosen).</summary>
    public bool Optional { get; init; }

    public bool AcceptsKey(BlackboardKeyType type) => KeyTypes.Count == 0 || KeyTypes.Contains(type);

    /// <summary>Reads a number; null when it is invalid. The value is clamped to the bounds.</summary>
    public double? ParseNumber(string? text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
        {
            return null;
        }

        value = Math.Clamp(value, Min, Max);
        return Type == BtParameterType.Integer ? Math.Round(value) : value;
    }
}

/// <summary>Description of a kind of node: title, help, role and settings.</summary>
public sealed class BtNodeInfo
{
    public required BtNodeKind Kind { get; init; }

    public required BtCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<BtParameterInfo> Parameters { get; init; } = [];

    /// <summary>Decorator that tests a condition (can be inverted, can abort the tree).</summary>
    public bool IsCondition { get; init; }

    /// <summary>Composite or root: the node has children.</summary>
    public bool HasChildren => Category is BtCategory.Root or BtCategory.Composite;

    /// <summary>Comment or tooltip: drawn on the canvas, never linked, ignored by the AI.</summary>
    public bool IsAnnotation => Category == BtCategory.Annotation;

    /// <summary>What is attached to a node rather than placed in the tree.</summary>
    public bool IsAttachment => Category is BtCategory.Decorator or BtCategory.Service;

    public BtParameterInfo? FindParameter(string name) => Parameters.FirstOrDefault(p => p.Name == name);

    public override string ToString() => Title;
}
