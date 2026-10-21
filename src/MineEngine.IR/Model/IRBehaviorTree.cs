using System.Globalization;
using MineEngine.Assets.Puppeteer;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>
/// Puppeteer behavior tree ready to be generated: its keys and its main branch (the child
/// of the root), with the children already sorted in execution order.
/// </summary>
public sealed class IRBehaviorTree
{
    public IRBehaviorTree(Guid sourceAssetId, ResourceId id, string displayName, IReadOnlyList<IRBlackboardKey> keys, IRBtNode? root)
    {
        SourceAssetId = sourceAssetId;
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Value : displayName;
        Keys = keys ?? [];
        Root = root;
    }

    public Guid SourceAssetId { get; }

    public ResourceId Id { get; }

    public string DisplayName { get; }

    public IReadOnlyList<IRBlackboardKey> Keys { get; }

    /// <summary>First node under the root, or null when the tree is empty (the mob does nothing).</summary>
    public IRBtNode? Root { get; }
}

/// <summary>Blackboard key: its name is the one used by the Java code.</summary>
public sealed record IRBlackboardKey(string Name, BlackboardKeyType Type);

/// <summary>
/// Node, decorator or service of a tree. The settings are already resolved: a key is
/// replaced by its name, a sound by its "namespace:path" identifier, an empty optional
/// setting is absent.
/// </summary>
public sealed class IRBtNode
{
    public IRBtNode(Guid id, BtNodeKind kind, IReadOnlyDictionary<string, string> values)
    {
        Id = id;
        Kind = kind;
        Values = values;
    }

    public Guid Id { get; }

    public BtNodeKind Kind { get; }

    public IReadOnlyDictionary<string, string> Values { get; }

    public IReadOnlyList<IRBtNode> Children { get; init; } = [];

    public IReadOnlyList<IRBtNode> Decorators { get; init; } = [];

    public IReadOnlyList<IRBtNode> Services { get; init; } = [];

    public BtNodeInfo Info => BtCatalog.Get(Kind);

    public string? Text(string name) => Values.TryGetValue(name, out string? value) ? value : null;

    /// <summary>Number of a setting (bounded by the catalog); the default value when it is invalid.</summary>
    public double Number(string name)
    {
        BtParameterInfo? parameter = Info.FindParameter(name);
        return parameter?.ParseNumber(Text(name)) ?? parameter?.ParseNumber(parameter.DefaultValue) ?? 0;
    }

    public bool Flag(string name) => string.Equals(Text(name) ?? Info.FindParameter(name)?.DefaultValue, "true", StringComparison.OrdinalIgnoreCase);

    public TEnum Choice<TEnum>(string name, TEnum fallback)
        where TEnum : struct, Enum =>
        Enum.TryParse(Text(name), out TEnum value) && Enum.IsDefined(value) ? value : fallback;

    public override string ToString() => Kind.ToString() + string.Concat(Values.Select(v => string.Create(CultureInfo.InvariantCulture, $" {v.Key}={v.Value}")));
}
