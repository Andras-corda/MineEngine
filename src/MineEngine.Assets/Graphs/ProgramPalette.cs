using System.Collections.Immutable;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Graphs;

/// <summary>Nœud proposé dans le menu d'ajout : sa description et la définition (et le membre) qu'il vise.</summary>
public sealed record NodeTemplate(NodeInfo Info, Guid? Target = null, Guid? Member = null)
{
    public GraphNode Create(double x, double y) => GraphNode.Create(Info, x, y, Target, Member);
}

/// <summary>
/// Ce qu'on peut ajouter dans un graphe, et la création des nouveaux graphes. Les nœuds du
/// catalogue dépendent de ce que définit le graphe (pas d'événement dans une fonction) ;
/// les nœuds de programmation viennent des définitions visibles depuis le graphe.
/// </summary>
public static class ProgramPalette
{
    /// <summary>Tous les nœuds qu'on peut ajouter dans <paramref name="graph"/>.</summary>
    public static IEnumerable<NodeTemplate> AvailableNodes(ProgramModel program, GraphAsset graph)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(graph);
        if (!graph.Kind.HasBody())
        {
            yield break;
        }

        foreach (NodeInfo info in NodeCatalog.All)
        {
            bool eventOnly = info.Category == NodeCategory.Event || info.Kind == GraphNodeKind.CancelEvent;
            if (graph.Kind == GraphKind.Events || !eventOnly)
            {
                yield return new NodeTemplate(info);
            }
        }

        foreach (NodeTemplate template in ProgramNodes(program, graph))
        {
            if (!template.Info.IsMissing)
            {
                yield return template;
            }
        }
    }

    private static IEnumerable<NodeTemplate> ProgramNodes(ProgramModel program, GraphAsset graph)
    {
        NodeTemplate Make(GraphNodeKind kind, Guid? target = null, Guid? member = null)
        {
            var probe = new GraphNode(Guid.Empty, kind, 0, 0, ImmutableDictionary<string, string>.Empty) { Target = target, Member = member };
            return new NodeTemplate(program.Describe(probe, graph), target, member);
        }

        if (graph.Kind != GraphKind.Events)
        {
            if (!graph.Graph.Nodes.Any(n => n.Kind == GraphNodeKind.FunctionEntry))
            {
                yield return Make(GraphNodeKind.FunctionEntry);
            }

            yield return Make(GraphNodeKind.FunctionReturn);
            if (graph.Kind == GraphKind.Class && graph.BaseClassId is { } parent)
            {
                yield return Make(GraphNodeKind.SuperConstructor, parent);
            }
        }

        if (program.SelfClass(graph) is not null)
        {
            yield return Make(GraphNodeKind.Self);
        }

        foreach (VariableDefinition variable in graph.Kind is GraphKind.Events or GraphKind.Function or GraphKind.Procedure ? graph.Variables : [])
        {
            yield return Make(GraphNodeKind.GetVariable, member: variable.Id);
            yield return Make(GraphNodeKind.SetVariable, member: variable.Id);
        }

        foreach (GraphAsset definition in program.Definitions.Where(d => program.IsVisible(d, graph)).OrderBy(ProgramModel.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            switch (definition.Kind)
            {
                case GraphKind.Function or GraphKind.Procedure:
                    yield return Make(GraphNodeKind.CallFunction, definition.Id);
                    break;
                case GraphKind.Interface:
                    foreach (MethodSignature method in definition.Methods)
                    {
                        yield return Make(GraphNodeKind.CallFunction, definition.Id, method.Id);
                    }

                    break;
                case GraphKind.Class or GraphKind.Struct:
                    yield return Make(GraphNodeKind.NewObject, definition.Id);
                    foreach ((GraphAsset declaring, VariableDefinition field) in program.Fields(definition))
                    {
                        if (program.CanAccess(declaring, field.Access, graph))
                        {
                            yield return Make(GraphNodeKind.GetField, definition.Id, field.Id);
                            yield return Make(GraphNodeKind.SetField, definition.Id, field.Id);
                        }
                    }

                    break;
                case GraphKind.Enum:
                    yield return Make(GraphNodeKind.EnumLiteral, definition.Id);
                    yield return Make(GraphNodeKind.EnumEquals, definition.Id);
                    break;
            }
        }
    }

    /// <summary>Base de l'identifiant d'un nouveau graphe ("new_function").</summary>
    public static string ResourceIdBase(GraphKind kind) => kind switch
    {
        GraphKind.Events => "new_graph",
        _ => "new_" + kind.ToString().ToLowerInvariant(),
    };

    /// <summary>Nom proposé pour un nouveau graphe ("Nouvelle fonction").</summary>
    public static string DefaultName(GraphKind kind) => kind switch
    {
        GraphKind.Events => "Nouveau graphe",
        GraphKind.Function => "Nouvelle fonction",
        GraphKind.Procedure => "Nouvelle procédure",
        GraphKind.Class => "Nouvelle classe",
        GraphKind.Struct => "Nouvelle structure",
        GraphKind.Interface => "Nouvelle interface",
        _ => "Nouvelle énumération",
    };

    /// <summary>
    /// Nouveau graphe prêt à l'emploi : nœud d'entrée pour une fonction, une procédure ou un
    /// constructeur (relié au retour pour une fonction), une valeur pour une énumération.
    /// </summary>
    public static GraphAsset CreateGraph(GraphKind kind, ResourceId resourceId, string name, Guid? owner = null)
    {
        var graph = new GraphAsset(Guid.NewGuid(), resourceId, name) { Kind = kind, OwnerId = owner };
        ProgramModel program = new([graph]);
        NodeGraph body = NodeGraph.Empty;
        if (kind is GraphKind.Function or GraphKind.Procedure or GraphKind.Class)
        {
            var entryProbe = new GraphNode(Guid.Empty, GraphNodeKind.FunctionEntry, 0, 0, ImmutableDictionary<string, string>.Empty);
            GraphNode entry = GraphNode.Create(program.Describe(entryProbe, graph), 0, 0);
            body = body.AddNode(entry);
            if (kind == GraphKind.Function)
            {
                graph.Returns = [VariableDefinition.Create("résultat", DataType.Number)];
                var returnProbe = entryProbe with { Kind = GraphNodeKind.FunctionReturn };
                GraphNode result = GraphNode.Create(program.Describe(returnProbe, graph), 420, 0);
                body = body.AddNode(result).Connect(new GraphConnection(entry.Id, NodeInfo.ExecThen, result.Id, NodeInfo.ExecInput), program.ContextFor(graph));
            }
        }

        if (kind == GraphKind.Enum)
        {
            graph.Constants = [EnumConstant.Create("Valeur")];
        }

        graph.Graph = body;
        return graph;
    }
}
