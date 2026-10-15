using System.Collections.Immutable;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Contenu d'un graphe nodal : des nœuds et les fils qui les relient. Immuable : chaque
/// modification (ajout, déplacement, branchement) produit un nouveau graphe, ce qui rend
/// l'annulation triviale (l'historique garde l'ancien graphe).
/// </summary>
/// <remarks>
/// Règles de branchement, comme dans les Blueprints d'Unreal : une entrée de valeur ne
/// reçoit qu'un fil, une sortie d'exécution n'en envoie qu'un ; une sortie de valeur et
/// une entrée d'exécution peuvent en avoir plusieurs. Un nouveau fil remplace l'ancien.
/// </remarks>
public sealed class NodeGraph
{
    public NodeGraph(IEnumerable<GraphNode> nodes, IEnumerable<GraphConnection> connections)
    {
        Nodes = [.. nodes];
        var ids = Nodes.Select(n => n.Id).ToHashSet();
        Connections = [.. connections.Distinct().Where(c => ids.Contains(c.FromNode) && ids.Contains(c.ToNode))];
    }

    public static NodeGraph Empty { get; } = new([], []);

    public ImmutableArray<GraphNode> Nodes { get; }

    public ImmutableArray<GraphConnection> Connections { get; }

    public GraphNode? FindNode(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);

    /// <summary>Fil qui arrive sur une entrée, ou null.</summary>
    public GraphConnection? InputConnection(Guid node, string pin) =>
        Connections.FirstOrDefault(c => c.ToNode == node && c.ToPin == pin);

    /// <summary>Fils qui partent d'une sortie.</summary>
    public IEnumerable<GraphConnection> OutputConnections(Guid node, string pin) =>
        Connections.Where(c => c.FromNode == node && c.FromPin == pin);

    /// <summary>Nœud qui suit une sortie d'exécution, ou null.</summary>
    public GraphNode? NextNode(Guid node, string execPin) =>
        OutputConnections(node, execPin).Select(c => FindNode(c.ToNode)).FirstOrDefault(n => n is not null);

    public NodeGraph AddNode(GraphNode node) => new(Nodes.Add(node), Connections);

    public NodeGraph ReplaceNode(GraphNode node) =>
        new(Nodes.Select(n => n.Id == node.Id ? node : n), Connections);

    /// <summary>Retire des nœuds et tous les fils qui les touchent.</summary>
    public NodeGraph RemoveNodes(IEnumerable<Guid> ids)
    {
        var removed = ids.ToHashSet();
        return new NodeGraph(
            Nodes.Where(n => !removed.Contains(n.Id)),
            Connections.Where(c => !removed.Contains(c.FromNode) && !removed.Contains(c.ToNode)));
    }

    /// <summary>Déplace plusieurs nœuds d'un coup (un seul pas d'annulation).</summary>
    public NodeGraph MoveNodes(IReadOnlyDictionary<Guid, (double X, double Y)> positions) =>
        new(Nodes.Select(n => positions.TryGetValue(n.Id, out var p) ? n.MoveTo(p.X, p.Y) : n), Connections);

    /// <summary>Ajoute (ou complète) un ensemble de nœuds et de fils, par exemple au collage.</summary>
    public NodeGraph Merge(IEnumerable<GraphNode> nodes, IEnumerable<GraphConnection> connections) =>
        new(Nodes.AddRange(nodes), Connections.AddRange(connections));

    /// <summary>
    /// Vérifie qu'un fil peut relier ces deux broches ; retourne null si c'est possible,
    /// sinon la raison du refus (affichée dans l'éditeur). Sans <paramref name="context"/>,
    /// seuls les nœuds du catalogue sont connus.
    /// </summary>
    public string? DescribeConnectionProblem(GraphConnection connection, IGraphContext? context = null)
    {
        context ??= CatalogGraphContext.Instance;
        GraphNode? from = FindNode(connection.FromNode);
        GraphNode? to = FindNode(connection.ToNode);
        if (from is null || to is null)
        {
            return Tr.T("Nœud introuvable.");
        }

        if (from.Id == to.Id)
        {
            return Tr.T("Un nœud ne peut pas être relié à lui-même.");
        }

        NodeInfo fromInfo = context.Describe(from);
        NodeInfo toInfo = context.Describe(to);
        if (fromInfo.FindExecOutput(connection.FromPin) is not null)
        {
            return connection.ToPin == NodeInfo.ExecInput && toInfo.HasExecInput
                ? null
                : Tr.T("Un fil d'exécution (blanc) doit arriver sur l'entrée d'exécution d'une condition ou d'une action.");
        }

        PinInfo? output = fromInfo.FindOutput(connection.FromPin);
        PinInfo? input = toInfo.FindInput(connection.ToPin);
        if (output is null || input is null)
        {
            return connection.ToPin == NodeInfo.ExecInput
                ? Tr.T("Une valeur ne peut pas être reliée à une entrée d'exécution.")
                : Tr.T("Broche introuvable.");
        }

        if (!input.Connectable)
        {
            return Tr.F("L'entrée « {0} » se règle directement sur le nœud.", input.Label);
        }

        if (!context.CanFeed(output.Type, input.Type))
        {
            return Tr.F("Types incompatibles : {0} vers {1}.", context.TypeLabel(output.Type), context.TypeLabel(input.Type));
        }

        return CreatesValueCycle(from.Id, to.Id) ? Tr.T("Ce fil créerait une boucle de valeurs.") : null;
    }

    /// <summary>
    /// Ajoute un fil en respectant les règles de branchement (l'ancien fil d'une entrée de
    /// valeur ou d'une sortie d'exécution est remplacé). Un fil invalide lève une exception :
    /// vérifier d'abord avec <see cref="DescribeConnectionProblem"/>.
    /// </summary>
    public NodeGraph Connect(GraphConnection connection, IGraphContext? context = null)
    {
        context ??= CatalogGraphContext.Instance;
        if (DescribeConnectionProblem(connection, context) is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        bool isExec = context.Describe(FindNode(connection.FromNode)!).FindExecOutput(connection.FromPin) is not null;
        IEnumerable<GraphConnection> kept = isExec
            ? Connections.Where(c => !(c.FromNode == connection.FromNode && c.FromPin == connection.FromPin))
            : Connections.Where(c => !(c.ToNode == connection.ToNode && c.ToPin == connection.ToPin));
        return new NodeGraph(Nodes, kept.Append(connection));
    }

    public NodeGraph Disconnect(GraphConnection connection) => new(Nodes, Connections.Remove(connection));

    /// <summary>Retire tous les fils d'une broche (Alt+clic dans l'éditeur).</summary>
    public NodeGraph DisconnectPin(Guid node, string pin) =>
        new(Nodes, Connections.Where(c => !(c.FromNode == node && c.FromPin == pin) && !(c.ToNode == node && c.ToPin == pin)));

    /// <summary>Vrai si "to" alimente déjà "from" par des fils de valeur (le nouveau fil fermerait une boucle).</summary>
    private bool CreatesValueCycle(Guid from, Guid to)
    {
        var pending = new Stack<Guid>([from]);
        var seen = new HashSet<Guid>();
        while (pending.Count > 0)
        {
            Guid current = pending.Pop();
            if (current == to)
            {
                return true;
            }

            if (!seen.Add(current))
            {
                continue;
            }

            foreach (GraphConnection incoming in Connections.Where(c => c.ToNode == current && c.ToPin != NodeInfo.ExecInput))
            {
                pending.Push(incoming.FromNode);
            }
        }

        return false;
    }
}
