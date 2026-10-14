using System.Globalization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;

namespace MineEngine.Assets.Graphs;

/// <summary>Problème d'un graphe, rattaché au nœud concerné (null : le graphe entier).</summary>
public sealed record GraphIssue(DiagnosticSeverity Severity, string Message, Guid? NodeId);

/// <summary>
/// Vérifie qu'un graphe peut être traduit en Java : entrées obligatoires reliées, valeurs
/// saisies valides, nœuds atteints par un événement, pas de boucle d'exécution, valeurs
/// prises dans le bon événement. Utilisé par les Diagnostics, le compilateur et l'éditeur
/// (qui entoure les nœuds en erreur).
/// </summary>
public static class GraphValidator
{
    public static IReadOnlyList<GraphIssue> Validate(NodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var issues = new List<GraphIssue>();
        List<GraphNode> events = [.. graph.Nodes.Where(n => n.Info.Category == NodeCategory.Event)];
        if (events.Count == 0)
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Warning, "Le graphe ne contient aucun événement : il ne fera rien en jeu.", null));
        }

        foreach (GraphNode node in graph.Nodes)
        {
            ValidateInputs(graph, node, issues);
        }

        Dictionary<Guid, HashSet<GraphNode>> reachedBy = FindExecReach(graph, events, issues);

        foreach (GraphNode node in graph.Nodes.Where(n => n.Info.HasExecInput))
        {
            if (!reachedBy.TryGetValue(node.Id, out HashSet<GraphNode>? sources))
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Warning, $"« {node.Info.Title} » n'est relié à aucun événement : il ne sera jamais exécuté.", node.Id));
                continue;
            }

            foreach (GraphNode source in sources)
            {
                if (node.Kind == GraphNodeKind.CancelEvent && !source.Info.IsCancellable)
                {
                    issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"L'événement « {source.Info.Title} » ne peut pas être annulé.", node.Id));
                }

                foreach (GraphNode other in DataSources(graph, node).Where(s => s.Info.Category == NodeCategory.Event && s.Id != source.Id))
                {
                    issues.Add(new GraphIssue(
                        DiagnosticSeverity.Error,
                        $"« {node.Info.Title} » utilise une valeur de l'événement « {other.Info.Title} » alors qu'il est exécuté par « {source.Info.Title} ».",
                        node.Id));
                }
            }
        }

        foreach (GraphNode node in events.Where(e => graph.NextNode(e.Id, NodeInfo.ExecThen) is null))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Info, $"L'événement « {node.Info.Title} » ne déclenche encore rien.", node.Id));
        }

        return issues;
    }

    /// <summary>Le nombre saisi sur une entrée, s'il est valide.</summary>
    public static bool TryParseNumber(string? text, out int value) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
        && Math.Abs((long)value) <= NodeCatalog.MaxNumber;

    private static void ValidateInputs(NodeGraph graph, GraphNode node, List<GraphIssue> issues)
    {
        foreach (PinInfo pin in node.Info.Inputs)
        {
            if (graph.InputConnection(node.Id, pin.Name) is { } connection)
            {
                if (graph.DescribeConnectionProblem(connection) is { } problem)
                {
                    issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"« {node.Info.Title} », entrée « {pin.Label} » : {problem}", node.Id));
                }

                continue;
            }

            if (!pin.Type.HasInlineValue())
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"« {node.Info.Title} » : l'entrée « {pin.Label} » doit être reliée.", node.Id));
                continue;
            }

            if (DescribeValueProblem(pin, node.GetValue(pin.Name)) is { } valueProblem)
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"« {node.Info.Title} », « {pin.Label} » : {valueProblem}", node.Id));
            }
            else if (node.Info.ItemEvent is not null
                     && ContentReference.TryParseStorageText(node.GetValue(pin.Name), out ContentReference? item)
                     && item!.Kind != ContentReferenceKind.Asset)
            {
                // Le code de l'événement est ajouté à la classe de l'item : il doit être un item du mod.
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"« {node.Info.Title} » : choisissez un item du mod, pas un item de Minecraft.", node.Id));
            }
        }
    }

    private static string? DescribeValueProblem(PinInfo pin, string? value)
    {
        switch (pin.Type)
        {
            case PinType.Number:
                return TryParseNumber(value, out _) ? null : $"nombre entier attendu (entre -{NodeCatalog.MaxNumber} et {NodeCatalog.MaxNumber}).";
            case PinType.Boolean:
                return bool.TryParse(value, out _) ? null : "vrai ou faux attendu.";
            case PinType.Item or PinType.Block or PinType.Sound:
                if (!ContentReference.TryParseStorageText(value, out ContentReference? reference))
                {
                    return $"choisissez un {pin.Type.Label().ToLowerInvariant()}.";
                }

                return reference!.Kind == ContentReferenceKind.Tag ? "un tag n'est pas accepté ici." : null;
            default:
                return null;
        }
    }

    /// <summary>Pour chaque nœud exécutable, les événements qui y mènent ; signale les boucles d'exécution.</summary>
    private static Dictionary<Guid, HashSet<GraphNode>> FindExecReach(NodeGraph graph, List<GraphNode> events, List<GraphIssue> issues)
    {
        var reachedBy = new Dictionary<Guid, HashSet<GraphNode>>();
        var reportedLoops = new HashSet<Guid>();
        foreach (GraphNode start in events)
        {
            Visit(start, []);

            void Visit(GraphNode node, HashSet<Guid> path)
            {
                if (!path.Add(node.Id))
                {
                    if (reportedLoops.Add(node.Id))
                    {
                        issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"Boucle d'exécution : « {node.Info.Title} » se relance lui-même.", node.Id));
                    }

                    return;
                }

                if (node.Id != start.Id)
                {
                    if (!reachedBy.TryGetValue(node.Id, out HashSet<GraphNode>? set))
                    {
                        reachedBy[node.Id] = set = [];
                    }

                    set.Add(start);
                }

                foreach (PinInfo exec in node.Info.ExecOutputs)
                {
                    if (graph.NextNode(node.Id, exec.Name) is { } next)
                    {
                        Visit(next, path);
                    }
                }

                path.Remove(node.Id);
            }
        }

        return reachedBy;
    }

    /// <summary>Nœuds dont dépendent les entrées de valeur d'un nœud (directement ou par des nœuds de valeur).</summary>
    private static IEnumerable<GraphNode> DataSources(NodeGraph graph, GraphNode node)
    {
        var seen = new HashSet<Guid>();
        var pending = new Stack<GraphNode>([node]);
        while (pending.Count > 0)
        {
            GraphNode current = pending.Pop();
            foreach (GraphConnection incoming in graph.Connections.Where(c => c.ToNode == current.Id && c.ToPin != NodeInfo.ExecInput))
            {
                if (graph.FindNode(incoming.FromNode) is { } source && seen.Add(source.Id))
                {
                    yield return source;
                    if (source.Info.IsPure)
                    {
                        pending.Push(source);
                    }
                }
            }
        }
    }
}
