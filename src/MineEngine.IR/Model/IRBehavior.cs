using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>
/// Comportements d'un graphe nodal, indépendants du loader : pour chaque nœud événement,
/// la suite d'instructions qu'il déclenche. Les backends en font une classe Java.
/// </summary>
public sealed class IRBehavior
{
    public IRBehavior(Guid sourceAssetId, ResourceId id, string displayName, IReadOnlyList<IREventHandler> handlers)
    {
        SourceAssetId = sourceAssetId;
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Value : displayName;
        Handlers = handlers ?? [];
    }

    public Guid SourceAssetId { get; }

    public ResourceId Id { get; }

    public string DisplayName { get; }

    public IReadOnlyList<IREventHandler> Handlers { get; }
}

/// <summary>
/// Réaction à un événement du jeu : le nœud événement et les instructions qui le suivent.
/// <paramref name="Item"/> est l'item du mod d'un événement d'item (son code va dans la classe de l'item).
/// </summary>
public sealed record IREventHandler(GraphNodeKind Event, Guid NodeId, IReadOnlyList<IRStatement> Body, NamespacedId? Item = null)
{
    public bool IsItemEvent => Item is not null;
}

/// <summary>Instruction d'un comportement.</summary>
public abstract record IRStatement(Guid NodeId);

/// <summary>Action sur le jeu (donner un item, placer un bloc...), avec ses arguments dans l'ordre des entrées du nœud.</summary>
public sealed record IRActionStatement(Guid NodeId, GraphNodeKind Action, IReadOnlyList<IRExpression> Arguments) : IRStatement(NodeId);

/// <summary>Choix entre deux suites d'instructions (nœud condition).</summary>
public sealed record IRIfStatement(Guid NodeId, IRExpression Condition, IReadOnlyList<IRStatement> Then, IReadOnlyList<IRStatement> Else)
    : IRStatement(NodeId);

/// <summary>Valeur calculée : constante, valeur fournie par l'événement, ou nœud de calcul.</summary>
public abstract record IRExpression(PinType Type);

public sealed record IRNumberLiteral(int Value) : IRExpression(PinType.Number);

public sealed record IRTextLiteral(string Value) : IRExpression(PinType.Text);

public sealed record IRBooleanLiteral(bool Value) : IRExpression(PinType.Boolean);

/// <summary>Item, bloc ou son désigné par son identifiant en jeu.</summary>
public sealed record IRContentLiteral(PinType ContentType, NamespacedId Id) : IRExpression(ContentType);

/// <summary>Valeur donnée par l'événement en cours ("player", "position").</summary>
public sealed record IREventValue(PinType ValueType, string Name) : IRExpression(ValueType);

/// <summary>Nœud de calcul (ou condition) appliqué à ses arguments, dans l'ordre des entrées du nœud.</summary>
public sealed record IRCallExpression(PinType ResultType, GraphNodeKind Kind, IReadOnlyList<IRExpression> Arguments) : IRExpression(ResultType);

/// <summary>Conversion d'un nombre en texte (fil vert branché sur une entrée de texte).</summary>
public sealed record IRToText(IRExpression Value) : IRExpression(PinType.Text);
