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

        // Les redirections ne sont que de la mise en page : on vérifie le graphe tel qu'il sera compilé.
        graph = Reroutes.Collapse(graph);
        var issues = new List<GraphIssue>();
        Dictionary<Guid, NodeInfo> infos = graph.Nodes.ToDictionary(n => n.Id, context.Describe);
        NodeInfo Info(GraphNode node) => infos[node.Id];

        List<GraphNode> starts = [.. graph.Nodes.Where(n => Info(n).IsEntryPoint)];
        ValidateKind(graph, context.Kind, Info, starts, issues);

        foreach (GraphNode node in graph.Nodes)
        {
            if (Info(node).IsMissing)
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\": {1}", Info(node).Title, Info(node).Description), node.Id));
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
                        ? Tr.F("\"{0}\" is not connected to any event: it will never run.", Info(node).Title)
                        : Tr.F("\"{0}\" is not connected to the entry: it will never run.", Info(node).Title),
                    node.Id));
                continue;
            }

            foreach (GraphNode source in sources)
            {
                if (node.Kind == GraphNodeKind.CancelEvent && !Info(source).IsCancellable)
                {
                    issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.F("The event \"{0}\" cannot be canceled.", Info(source).Title), node.Id));
                }

                foreach (GraphNode other in DataSources(graph, node, Info).Where(s => Info(s).IsEntryPoint && s.Id != source.Id))
                {
                    issues.Add(new GraphIssue(
                        DiagnosticSeverity.Error,
                        Tr.F("\"{0}\" uses a value from the event \"{1}\" while it is run by \"{2}\".", Info(node).Title, Info(other).Title, Info(source).Title),
                        node.Id));
                }
            }
        }

        foreach (GraphNode node in starts.Where(e => e.Kind != GraphNodeKind.FunctionEntry && graph.NextNode(e.Id, NodeInfo.ExecThen) is null))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Info, Tr.F("The event \"{0}\" does not trigger anything yet.", Info(node).Title), node.Id));
        }

        return issues;
    }

    /// <summary>Names of the six directions, as Minecraft writes them.</summary>
    public static IReadOnlyList<string> Directions { get; } = ["down", "up", "north", "south", "west", "east"];

    /// <summary>"minecraft:stone" or "stone": a valid Minecraft identifier.</summary>
    public static bool IsIdentifier(string? text) =>
        text is { Length: > 0 } && System.Text.RegularExpressions.Regex.IsMatch(text.Trim(), "^([a-z0-9_.-]+:)?[a-z0-9_./-]+$");

    /// <summary>A decimal number typed on an input (dot or comma), if it is valid.</summary>
    public static bool TryParseDecimal(string? text, out double value) =>
        double.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

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
                return TryParseNumber(value, out _) ? null : Tr.F("whole number expected (between -{0} and {1}).", NodeCatalog.MaxNumber, NodeCatalog.MaxNumber);
            case PinType.Boolean:
                return bool.TryParse(value, out _) ? null : Tr.T("true or false expected.");
            case PinType.Decimal:
                return TryParseDecimal(value, out _) ? null : Tr.T("number expected (with a dot for decimals: 0.5).");
            case PinType.Direction:
                return Directions.Contains(value?.Trim().ToLowerInvariant()) ? null : Tr.T("up, down, north, south, east or west expected.");
            case PinType.Char:
                return value is { Length: 1 } ? null : Tr.T("exactly one character expected.");
            case PinType.Long:
                return long.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : Tr.T("whole number expected.");
            case PinType.Byte:
                return sbyte.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : Tr.T("whole number from -128 to 127 expected.");
            case PinType.Float:
                return TryParseDecimal(value, out double single) && Math.Abs(single) <= float.MaxValue ? null : Tr.T("number expected (with a dot for decimals: 0.5).");
            case PinType.Identifier or PinType.Fluid:
                return IsIdentifier(value) ? null : Tr.T("identifier expected (minecraft:stone): lowercase letters, digits, _ - . / and one ':'.");
            case PinType.EquipmentSlot or PinType.Hand or PinType.Rarity:
                IReadOnlyList<string> names = DataType.NamedValues(pin.Type.Kind)!;
                return names.Contains(value?.Trim().ToLowerInvariant()) ? null : Tr.F("one of these values expected: {0}.", string.Join(", ", names));
            case PinType.Item or PinType.Block or PinType.Sound:
                if (!ContentReference.TryParseStorageText(value, out ContentReference? reference))
                {
                    return Tr.F("choose a {0}.", pin.Type.BuiltInLabel.ToLowerInvariant());
                }

                return reference!.Kind == ContentReferenceKind.Tag ? Tr.T("a tag is not accepted here.") : null;
            case PinType.Enum:
                return Guid.TryParse(value, out Guid constant) && pin.Choices.Any(c => c.Id == constant)
                    ? null
                    : Tr.T("choose a value of the enumeration.");
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
                issues.Add(new GraphIssue(DiagnosticSeverity.Warning, Tr.T("The graph has no event: it will do nothing in game."), null));
            }

            foreach (GraphNode node in graph.Nodes.Where(n => n.Kind is GraphNodeKind.FunctionEntry or GraphNodeKind.FunctionReturn or GraphNodeKind.SuperConstructor))
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error,
                    Tr.F("\"{0}\" only belongs in a function, a procedure or a constructor.", info(node).Title), node.Id));
            }

            return;
        }

        List<GraphNode> entries = [.. graph.Nodes.Where(n => n.Kind == GraphNodeKind.FunctionEntry)];
        if (entries.Count == 0)
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.T("The entry node was deleted: add one (right-click, \"Entry\")."), null));
        }

        foreach (GraphNode extra in entries.Skip(1))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.T("Only one entry node per graph."), extra.Id));
        }

        foreach (GraphNode node in starts.Where(n => n.Kind != GraphNodeKind.FunctionEntry))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error,
                Tr.F("\"{0}\": an event only belongs in an event graph; call this {1} from one of them.", info(node).Title, kind.Label().ToLowerInvariant()),
                node.Id));
        }

        foreach (GraphNode node in graph.Nodes.Where(n => n.Kind == GraphNodeKind.CancelEvent))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.T("\"Cancel event\" only belongs in an event graph."), node.Id));
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
                    issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\", input \"{1}\": {2}", info.Title, pin.Label, problem), node.Id));
                }

                continue;
            }

            if (pin.Optional)
            {
                continue;
            }

            if (!pin.Type.HasInlineValue)
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\": the input \"{1}\" must be connected.", info.Title, pin.Label), node.Id));
                continue;
            }

            string? value = node.Values.TryGetValue(pin.Name, out string? typed) ? typed : pin.DefaultValue;
            if (DescribeValueProblem(pin, value) is { } valueProblem)
            {
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\", \"{1}\": {2}", info.Title, pin.Label, valueProblem), node.Id));
            }
            else if (info.ItemEvent is not null
                     && ContentReference.TryParseStorageText(value, out ContentReference? item)
                     && item!.Kind != ContentReferenceKind.Asset)
            {
                // Le code de l'événement est ajouté à la classe de l'item : il doit être un item du mod.
                issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\": choose an item from the mod, not a Minecraft item.", info.Title), node.Id));
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
                        issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.F("Execution loop: \"{0}\" triggers itself.", info(node).Title), node.Id));
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
