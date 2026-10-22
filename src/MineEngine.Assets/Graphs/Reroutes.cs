using System.Collections.Immutable;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Nœuds de redirection (« reroute » des Blueprints d'Unreal) : un point posé sur un fil pour
/// le faire passer ailleurs et garder le graphe lisible. Une redirection porte le type du fil
/// sur lequel elle a été créée (exécution ou valeur, enregistré dans la valeur "type") ; elle
/// ne change rien au programme : la validation et la génération du Java travaillent sur le
/// graphe sans redirections (<see cref="Collapse"/>).
/// </summary>
public static class Reroutes
{
    /// <summary>Valeur du nœud qui garde le type du fil : "exec" ou un type de valeur ("number", "object:guid").</summary>
    public const string TypeKey = "type";

    /// <summary>Entrée d'une redirection de valeur.</summary>
    public const string ValueInput = "in";

    /// <summary>Sortie d'une redirection de valeur.</summary>
    public const string ValueOutput = "out";

    /// <summary>
    /// Value of a reference: the id of the reroute it repeats. A reference has no input: it gives the
    /// value of its reroute anywhere in the graph, without a wire across it (named reroutes of Unreal).
    /// </summary>
    public const string ReferenceKey = "ref";

    private const string ExecType = "exec";

    /// <summary>A reference to a value reroute, placed at (x, y); null for an execution reroute or a reference.</summary>
    public static GraphNode? CreateReference(GraphNode reroute, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(reroute);
        if (!IsReroute(reroute) || IsReference(reroute) || TypeOf(reroute) is not { IsExec: false })
        {
            return null;
        }

        return new GraphNode(Guid.NewGuid(), GraphNodeKind.Reroute, x, y,
            reroute.Values.SetItem(ReferenceKey, reroute.Id.ToString("D")));
    }

    /// <summary>True for a reference to another reroute.</summary>
    public static bool IsReference(GraphNode node) => IsReroute(node) && node.Values.ContainsKey(ReferenceKey);

    /// <summary>The reroute a reference repeats, or null.</summary>
    public static Guid? ReferenceOf(GraphNode node) =>
        IsReroute(node) && Guid.TryParse(node.Values.GetValueOrDefault(ReferenceKey), out Guid id) ? id : null;

    /// <summary>The references of these reroutes (deleted with them: their wires then start from the real source).</summary>
    public static IEnumerable<Guid> ReferencesTo(NodeGraph graph, IEnumerable<Guid> reroutes)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var targets = reroutes.ToHashSet();
        return graph.Nodes.Where(n => ReferenceOf(n) is { } target && targets.Contains(target)).Select(n => n.Id);
    }

    /// <summary>Nouvelle redirection pour un fil de ce type.</summary>
    public static GraphNode Create(DataType type, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(type);
        string text = type.IsExec ? ExecType : type.ToStorageText();
        return new GraphNode(Guid.NewGuid(), GraphNodeKind.Reroute, x, y,
            ImmutableDictionary<string, string>.Empty.Add(TypeKey, text));
    }

    /// <summary>Type du fil que porte la redirection ; null si la valeur enregistrée est illisible.</summary>
    public static DataType? TypeOf(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        string? text = node.Values.GetValueOrDefault(TypeKey);
        return text == ExecType ? DataType.Exec : DataType.Parse(text);
    }

    /// <summary>
    /// Broches d'une redirection : entrée et sortie d'exécution pour un fil blanc, sinon une entrée
    /// et une sortie du type du fil. Le nom du type est donné par <paramref name="typeLabel"/>.
    /// </summary>
    public static NodeInfo Describe(GraphNode node, Func<DataType, string> typeLabel)
    {
        ArgumentNullException.ThrowIfNull(typeLabel);
        string title = Tr.T("Reroute");
        DataType? type = TypeOf(node);
        if (type is null)
        {
            return NodeCatalog.Missing(GraphNodeKind.Reroute, title);
        }

        if (type.IsExec)
        {
            return new NodeInfo(GraphNodeKind.Reroute, NodeCategory.Action, title,
                Tr.T("Waypoint of an execution wire: it does not change the program."), flow: NodeFlow.Step);
        }

        string label = typeLabel(type);
        if (IsReference(node))
        {
            return new NodeInfo(GraphNodeKind.Reroute, NodeCategory.Variable, Tr.T("Reroute reference"),
                Tr.F("Gives the {0} value of its reroute here, without a wire across the graph.", label),
                outputs: [new PinInfo(ValueOutput, string.Empty, type)],
                flow: NodeFlow.Pure);
        }

        return new NodeInfo(GraphNodeKind.Reroute, NodeCategory.Variable, title,
            Tr.F("Waypoint of a {0} wire: it does not change the program.", label),
            inputs: [new PinInfo(ValueInput, string.Empty, type)],
            outputs: [new PinInfo(ValueOutput, string.Empty, type)],
            flow: NodeFlow.Pure);
    }

    public static bool IsReroute(GraphNode node) => node.Kind == GraphNodeKind.Reroute;

    /// <summary>
    /// Le graphe tel que le programme le voit : sans redirection (chaque fil qui en traversait relie
    /// directement ses deux bouts), ni commentaire, ni info-bulle.
    /// </summary>
    public static NodeGraph Collapse(NodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        graph = SplitFannedOut(GraphAnnotations.Strip(graph));
        return graph.Nodes.Any(IsReroute) ? Bypass(graph, graph.Nodes.Where(IsReroute).Select(n => n.Id)) : graph;
    }

    /// <summary>
    /// An execution reroute that sends several wires becomes a Sequence: its wires run one after
    /// the other, from the highest node to the lowest (like the outputs of a Sequence).
    /// </summary>
    private static NodeGraph SplitFannedOut(NodeGraph graph)
    {
        List<GraphNode> fanned =
        [
            .. graph.Nodes.Where(n => IsReroute(n) && TypeOf(n) is { IsExec: true }
                                      && graph.OutputConnections(n.Id, NodeInfo.ExecThen).Skip(1).Any()),
        ];
        if (fanned.Count == 0)
        {
            return graph;
        }

        var connections = graph.Connections.ToList();
        var nodes = graph.Nodes.ToList();
        foreach (GraphNode reroute in fanned)
        {
            List<GraphConnection> outgoing =
            [
                .. graph.OutputConnections(reroute.Id, NodeInfo.ExecThen)
                    .OrderBy(c => graph.FindNode(c.ToNode)?.Y ?? 0).ThenBy(c => graph.FindNode(c.ToNode)?.X ?? 0),
            ];
            connections.RemoveAll(c => c.FromNode == reroute.Id && c.FromPin == NodeInfo.ExecThen);
            connections.AddRange(outgoing.Select((c, i) => c with { FromPin = FlowNodes.SequencePin(i) }));
            nodes[nodes.IndexOf(reroute)] = new GraphNode(reroute.Id, GraphNodeKind.Sequence, reroute.X, reroute.Y,
                ImmutableDictionary<string, string>.Empty.Add(FlowNodes.CountKey, outgoing.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return new NodeGraph(nodes, connections);
    }

    /// <summary>
    /// Retire des redirections en reliant directement ce qu'elles reliaient (suppression d'une
    /// redirection dans l'éditeur : le fil reste).
    /// </summary>
    public static NodeGraph Bypass(NodeGraph graph, IEnumerable<Guid> reroutes)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var removed = reroutes.Where(id => graph.FindNode(id) is { } node && IsReroute(node)).ToHashSet();
        if (removed.Count == 0)
        {
            return graph;
        }

        // Un fil de valeur est réécrit à la sortie de la redirection (il part de ce qui l'alimente) ;
        // un fil d'exécution est réécrit à son entrée (il va là où elle mène). Les fils qui
        // touchent l'autre côté disparaissent avec elle. Les chaînes de redirections sont suivies.
        var connections = new List<GraphConnection>();
        foreach (GraphConnection connection in graph.Connections)
        {
            bool fromRemoved = removed.Contains(connection.FromNode);
            bool toRemoved = removed.Contains(connection.ToNode);
            if (!fromRemoved && !toRemoved)
            {
                connections.Add(connection);
            }
            else if (fromRemoved && !toRemoved && connection.FromPin == ValueOutput)
            {
                if (Source(graph, connection.FromNode, removed, []) is { } source)
                {
                    connections.Add(connection with { FromNode = source.Node, FromPin = source.Pin });
                }
            }
            else if (!fromRemoved && toRemoved && connection.ToPin == NodeInfo.ExecInput)
            {
                if (Destination(graph, connection.ToNode, removed, []) is { } destination)
                {
                    connections.Add(connection with { ToNode = destination.Node, ToPin = destination.Pin });
                }
            }
        }

        return new NodeGraph(graph.Nodes.Where(n => !removed.Contains(n.Id)), connections.Distinct());
    }

    /// <summary>Broche réelle qui alimente l'entrée d'une redirection retirée (en remontant la chaîne).</summary>
    private static (Guid Node, string Pin)? Source(NodeGraph graph, Guid reroute, HashSet<Guid> removed, HashSet<Guid> seen)
    {
        if (!seen.Add(reroute))
        {
            return null;
        }

        // A reference takes the value of its reroute (removed with it, or still in the graph).
        if (graph.FindNode(reroute) is { } node && ReferenceOf(node) is { } target)
        {
            return graph.FindNode(target) is null ? null
                : removed.Contains(target) ? Source(graph, target, removed, seen)
                : (target, ValueOutput);
        }

        if (graph.InputConnection(reroute, ValueInput) is not { } incoming)
        {
            return null;
        }

        return removed.Contains(incoming.FromNode)
            ? Source(graph, incoming.FromNode, removed, seen)
            : (incoming.FromNode, incoming.FromPin);
    }

    /// <summary>Entrée réelle vers laquelle mène la sortie d'exécution d'une redirection retirée.</summary>
    private static (Guid Node, string Pin)? Destination(NodeGraph graph, Guid reroute, HashSet<Guid> removed, HashSet<Guid> seen)
    {
        if (!seen.Add(reroute) || graph.OutputConnections(reroute, NodeInfo.ExecThen).FirstOrDefault() is not { } outgoing)
        {
            return null;
        }

        return removed.Contains(outgoing.ToNode)
            ? Destination(graph, outgoing.ToNode, removed, seen)
            : (outgoing.ToNode, outgoing.ToPin);
    }
}
