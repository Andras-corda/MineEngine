using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

/// <summary>
/// Traduit un graphe nodal en instructions de l'IR. Chaque événement devient un
/// gestionnaire : on suit son fil d'exécution, une action devient une instruction, une
/// condition un "si / sinon" dont chaque branche suit son propre fil. Les entrées de
/// valeur deviennent des expressions : valeur saisie, valeur de l'événement, ou calcul
/// d'un nœud de valeur (recalculé à chaque utilisation, comme dans les Blueprints).
/// </summary>
/// <remarks>Le graphe doit avoir été validé (<see cref="GraphValidator"/>) : pas de boucle, entrées obligatoires reliées.</remarks>
public sealed class GraphCompiler
{
    private readonly NodeGraph _graph;
    private readonly Func<ContentReference, PinType, NamespacedId?> _resolveContent;

    /// <param name="resolveContent">Identifiant en jeu d'un item, d'un bloc ou d'un son ; null s'il n'existe plus.</param>
    public GraphCompiler(NodeGraph graph, Func<ContentReference, PinType, NamespacedId?> resolveContent)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _resolveContent = resolveContent ?? throw new ArgumentNullException(nameof(resolveContent));
    }

    /// <summary>Un gestionnaire par événement du graphe qui déclenche au moins une instruction.</summary>
    public IReadOnlyList<IREventHandler> Compile() =>
        [.. _graph.Nodes
            .Where(n => n.Info.Category == NodeCategory.Event)
            .OrderBy(n => n.Y).ThenBy(n => n.X)
            .Select(e => new IREventHandler(e.Kind, e.Id, CompileChain(_graph.NextNode(e.Id, NodeInfo.ExecThen), e, []), BoundItem(e)))
            .Where(h => h.Body.Count > 0)];

    /// <summary>Item du mod visé par un événement d'item, sinon null.</summary>
    private NamespacedId? BoundItem(GraphNode eventNode) =>
        eventNode.Info.ItemEvent is null
            ? null
            : ((IRContentLiteral)Literal(eventNode.Info.Inputs[0], eventNode.GetValue("item"))).Id;

    private List<IRStatement> CompileChain(GraphNode? node, GraphNode eventNode, HashSet<Guid> path)
    {
        var statements = new List<IRStatement>();
        while (node is not null)
        {
            if (!path.Add(node.Id))
            {
                throw new InvalidOperationException($"Boucle d'exécution sur « {node.Info.Title} ».");
            }

            if (node.Info.Category == NodeCategory.Condition)
            {
                statements.Add(new IRIfStatement(
                    node.Id,
                    Condition(node, eventNode),
                    CompileChain(_graph.NextNode(node.Id, NodeInfo.ExecTrue), eventNode, [.. path]),
                    CompileChain(_graph.NextNode(node.Id, NodeInfo.ExecFalse), eventNode, [.. path])));
                break;
            }

            statements.Add(new IRActionStatement(node.Id, node.Kind, Arguments(node, eventNode)));
            node = _graph.NextNode(node.Id, NodeInfo.ExecThen);
        }

        return statements;
    }

    private IRExpression Condition(GraphNode node, GraphNode eventNode) =>
        node.Kind == GraphNodeKind.Branch
            ? Input(node, node.Info.Inputs[0], eventNode)
            : new IRCallExpression(PinType.Boolean, node.Kind, Arguments(node, eventNode));

    private List<IRExpression> Arguments(GraphNode node, GraphNode eventNode) =>
        [.. node.Info.Inputs.Select(pin => Input(node, pin, eventNode))];

    /// <summary>Expression qui alimente une entrée : le fil branché dessus, sinon la valeur saisie.</summary>
    private IRExpression Input(GraphNode node, PinInfo pin, GraphNode eventNode)
    {
        if (_graph.InputConnection(node.Id, pin.Name) is not { } connection || _graph.FindNode(connection.FromNode) is not { } source)
        {
            return Literal(pin, node.GetValue(pin.Name));
        }

        PinInfo output = source.Info.FindOutput(connection.FromPin)
                         ?? throw new InvalidOperationException($"Sortie « {connection.FromPin} » inconnue sur « {source.Info.Title} ».");
        IRExpression value = source.Info.Category == NodeCategory.Event
            ? new IREventValue(output.Type, output.Name)
            : Value(source, output, eventNode);
        return output.Type == PinType.Number && pin.Type == PinType.Text ? new IRToText(value) : value;
    }

    /// <summary>Résultat d'un nœud de valeur.</summary>
    private IRExpression Value(GraphNode node, PinInfo output, GraphNode eventNode) => node.Kind switch
    {
        GraphNodeKind.NumberValue or GraphNodeKind.TextValue or GraphNodeKind.BooleanValue =>
            Literal(node.Info.Inputs[0], node.GetValue("value")),
        _ => new IRCallExpression(output.Type, node.Kind, Arguments(node, eventNode)),
    };

    private IRExpression Literal(PinInfo pin, string? text) => pin.Type switch
    {
        PinType.Number => new IRNumberLiteral(GraphValidator.TryParseNumber(text, out int number) ? number : 0),
        PinType.Text => new IRTextLiteral(text ?? string.Empty),
        PinType.Boolean => new IRBooleanLiteral(bool.TryParse(text, out bool flag) && flag),
        PinType.Item or PinType.Block or PinType.Sound =>
            ContentReference.TryParseStorageText(text, out ContentReference? reference) && _resolveContent(reference!, pin.Type) is { } id
                ? new IRContentLiteral(pin.Type, id)
                : throw new InvalidOperationException($"« {pin.Label} » ne désigne aucun {pin.Type.Label().ToLowerInvariant()}."),
        _ => throw new InvalidOperationException($"L'entrée « {pin.Label} » doit être reliée."),
    };
}
