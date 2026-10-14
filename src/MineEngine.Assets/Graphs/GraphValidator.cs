using System.Globalization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;

namespace MineEngine.Assets.Graphs;

/// <summary>Problème d'un graphe, rattaché au nœud concerné (null : le graphe entier).</summary>
public sealed record GraphIssue(DiagnosticSeverity Severity, string Message, Guid? NodeId);

/// <summary>
/// Vérifie qu'un graphe peut être traduit en Java : entrées obligatoires reliées, valeurs
/// saisies valides, nœuds atteints par un événement (ou par l'entrée d'une fonction), pas
/// de boucle d'exécution, valeurs prises dans le bon événement, nœuds permis dans ce genre
/// de graphe. Utilisé par les Diagnostics, le compilateur et l'éditeur (qui entoure les
/// nœuds en erreur).
/// </summary>
public static class GraphValidator
{
    /// <param name="context">Décrit les nœuds ; sans lui, graphe d'événements avec les seuls nœuds du catalogue.</param>
    public static IReadOnlyList<GraphIssue> Validate(NodeGraph graph, IGraphContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        context ??= CatalogGraphContext.Instance;
        var issues = new List<GraphIssue>();
        Dictionary<Guid, NodeInfo> infos = graph.Nodes.ToDictionary(n => n.Id, context.Describe);
        NodeInfo Info(GraphNode node) => infos[node.Id];

        List<GraphNode> starts = [.. graph.Nodes.Where(n => Info(n).IsEntryPoint)];
        ValidateKind(graph, context.Kind, Info, starts, issues);

        foreach (GraphNode node in graph.Nodes)
        {
            if (Info(node).IsMissing)
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"« {Info(node).Title} » : {Info(node).Description}", node.Id));
                continue;
            }

            ValidateInputs(graph, node, Info(node), context, issues);
        }

        Dictionary<Guid, HashSet<GraphNode>> reachedBy = FindExecReach(graph, starts, Info, issues);
        foreach (GraphNode node in graph.Nodes.Where(n => Info(n).HasExecInput))
        {
            if (!reachedBy.TryGetValue(node.Id, out HashSet<GraphNode>? sources))
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Warning,
                    context.Kind == GraphKind.Events
                        ? $"« {Info(node).Title} » n'est relié à aucun événement : il ne sera jamais exécuté."
                        : $"« {Info(node).Title} » n'est pas relié à l'entrée : il ne sera jamais exécuté.",
                    node.Id));
                continue;
            }

            foreach (GraphNode source in sources)
            {
                if (node.Kind == GraphNodeKind.CancelEvent && !Info(source).IsCancellable)
                {
                    issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"L'événement « {Info(source).Title} » ne peut pas être annulé.", node.Id));
                }

                foreach (GraphNode other in DataSources(graph, node, Info).Where(s => Info(s).IsEntryPoint && s.Id != source.Id))
                {
                    issues.Add(new GraphIssue(
                        DiagnosticSeverity.Error,
                        $"« {Info(node).Title} » utilise une valeur de l'événement « {Info(other).Title} » alors qu'il est exécuté par « {Info(source).Title} ».",
                        node.Id));
                }
            }
        }

        foreach (GraphNode node in starts.Where(e => e.Kind != GraphNodeKind.FunctionEntry && graph.NextNode(e.Id, NodeInfo.ExecThen) is null))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Info, $"L'événement « {Info(node).Title} » ne déclenche encore rien.", node.Id));
        }

        return issues;
    }

    /// <summary>Le nombre saisi sur une entrée, s'il est valide.</summary>
    public static bool TryParseNumber(string? text, out int value) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
        && Math.Abs((long)value) <= NodeCatalog.MaxNumber;

    /// <summary>Problème d'une valeur saisie sur une entrée (ou d'une valeur par défaut de variable), ou null.</summary>
    public static string? DescribeValueProblem(PinInfo pin, string? value)
    {
        switch (pin.Type.Kind)
        {
            case PinType.Number:
                return TryParseNumber(value, out _) ? null : $"nombre entier attendu (entre -{NodeCatalog.MaxNumber} et {NodeCatalog.MaxNumber}).";
            case PinType.Boolean:
                return bool.TryParse(value, out _) ? null : "vrai ou faux attendu.";
            case PinType.Item or PinType.Block or PinType.Sound:
                if (!ContentReference.TryParseStorageText(value, out ContentReference? reference))
                {
                    return $"choisissez un {pin.Type.BuiltInLabel.ToLowerInvariant()}.";
                }

                return reference!.Kind == ContentReferenceKind.Tag ? "un tag n'est pas accepté ici." : null;
            case PinType.Enum:
                return Guid.TryParse(value, out Guid constant) && pin.Choices.Any(c => c.Id == constant)
                    ? null
                    : "choisissez une valeur de l'énumération.";
            default:
                return null;
        }
    }

    /// <summary>
    /// Nœuds permis selon ce que définit le graphe : événements dans un graphe d'événements,
    /// une seule entrée dans une fonction, une procédure ou un constructeur.
    /// </summary>
    private static void ValidateKind(NodeGraph graph, GraphKind kind, Func<GraphNode, NodeInfo> info, List<GraphNode> starts, List<GraphIssue> issues)
    {
        if (kind == GraphKind.Events)
        {
            if (starts.Count == 0)
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Warning, "Le graphe ne contient aucun événement : il ne fera rien en jeu.", null));
            }

            foreach (GraphNode node in graph.Nodes.Where(n => n.Kind is GraphNodeKind.FunctionEntry or GraphNodeKind.FunctionReturn or GraphNodeKind.SuperConstructor))
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error,
                    $"« {info(node).Title} » n'a sa place que dans une fonction, une procédure ou un constructeur.", node.Id));
            }

            return;
        }

        List<GraphNode> entries = [.. graph.Nodes.Where(n => n.Kind == GraphNodeKind.FunctionEntry)];
        if (entries.Count == 0)
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error, "Le nœud d'entrée a été supprimé : ajoutez-en un (clic droit, « Entrée »).", null));
        }

        foreach (GraphNode extra in entries.Skip(1))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error, "Un seul nœud d'entrée par graphe.", extra.Id));
        }

        foreach (GraphNode node in starts.Where(n => n.Kind != GraphNodeKind.FunctionEntry))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error,
                $"« {info(node).Title} » : un événement n'a sa place que dans un graphe d'événements ; appelez cette {kind.Label().ToLowerInvariant()} depuis l'un d'eux.",
                node.Id));
        }

        foreach (GraphNode node in graph.Nodes.Where(n => n.Kind == GraphNodeKind.CancelEvent))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error, "« Annuler l'événement » n'a sa place que dans un graphe d'événements.", node.Id));
        }
    }

    private static void ValidateInputs(NodeGraph graph, GraphNode node, NodeInfo info, IGraphContext context, List<GraphIssue> issues)
    {
        foreach (PinInfo pin in info.Inputs)
        {
            if (graph.InputConnection(node.Id, pin.Name) is { } connection)
            {
                if (graph.DescribeConnectionProblem(connection, context) is { } problem)
                {
                    issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"« {info.Title} », entrée « {pin.Label} » : {problem}", node.Id));
                }

                continue;
            }

            if (pin.Optional)
            {
                continue;
            }

            if (!pin.Type.HasInlineValue)
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"« {info.Title} » : l'entrée « {pin.Label} » doit être reliée.", node.Id));
                continue;
            }

            string? value = node.Values.TryGetValue(pin.Name, out string? typed) ? typed : pin.DefaultValue;
            if (DescribeValueProblem(pin, value) is { } valueProblem)
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"« {info.Title} », « {pin.Label} » : {valueProblem}", node.Id));
            }
            else if (info.ItemEvent is not null
                     && ContentReference.TryParseStorageText(value, out ContentReference? item)
                     && item!.Kind != ContentReferenceKind.Asset)
            {
                // Le code de l'événement est ajouté à la classe de l'item : il doit être un item du mod.
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"« {info.Title} » : choisissez un item du mod, pas un item de Minecraft.", node.Id));
            }
        }
    }

    /// <summary>Pour chaque nœud exécutable, les points de départ qui y mènent ; signale les boucles d'exécution.</summary>
    private static Dictionary<Guid, HashSet<GraphNode>> FindExecReach(
        NodeGraph graph, List<GraphNode> starts, Func<GraphNode, NodeInfo> info, List<GraphIssue> issues)
    {
        var reachedBy = new Dictionary<Guid, HashSet<GraphNode>>();
        var reportedLoops = new HashSet<Guid>();
        foreach (GraphNode start in starts)
        {
            Visit(start, []);

            void Visit(GraphNode node, HashSet<Guid> path)
            {
                if (!path.Add(node.Id))
                {
                    if (reportedLoops.Add(node.Id))
                    {
                        issues.Add(new GraphIssue(DiagnosticSeverity.Error, $"Boucle d'exécution : « {info(node).Title} » se relance lui-même.", node.Id));
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

                foreach (PinInfo exec in info(node).ExecOutputs)
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
    private static IEnumerable<GraphNode> DataSources(NodeGraph graph, GraphNode node, Func<GraphNode, NodeInfo> info)
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
                    if (info(source).IsPure)
                    {
                        pending.Push(source);
                    }
                }
            }
        }
    }
}
