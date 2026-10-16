using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

/// <summary>
/// Traduit le corps d'un graphe en instructions de l'IR. On suit le fil d'exécution depuis
/// chaque point de départ (un événement, ou l'entrée d'une fonction) : une action, un
/// appel, une création d'objet ou une affectation devient une instruction, une condition
/// un "si / sinon" dont chaque branche suit son propre fil, un retour termine le fil. Les
/// entrées de valeur deviennent des expressions : valeur saisie, paramètre, valeur de
/// l'événement, résultat d'un nœud déjà exécuté (rangé dans une variable locale), ou calcul
/// d'un nœud de valeur (recalculé à chaque utilisation, comme dans les Blueprints).
/// </summary>
/// <remarks>Le graphe doit avoir été validé (<see cref="ProgramValidator"/>) : pas de boucle, entrées obligatoires reliées.</remarks>
public sealed class GraphCompiler
{
    private readonly GraphAsset _asset;
    private readonly NodeGraph _graph;
    private readonly ProgramModel _program;
    private readonly Func<ContentReference, DataType, NamespacedId?> _resolveContent;
    private readonly Dictionary<Guid, NodeInfo> _infos;
    private Dictionary<(Guid Node, string Pin), IRLocal> _locals = [];

    /// <param name="resolveContent">Identifiant en jeu d'un item, d'un bloc ou d'un son ; null s'il n'existe plus.</param>
    public GraphCompiler(GraphAsset asset, ProgramModel program, Func<ContentReference, DataType, NamespacedId?> resolveContent)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _program = program ?? throw new ArgumentNullException(nameof(program));
        _resolveContent = resolveContent ?? throw new ArgumentNullException(nameof(resolveContent));
        _graph = Reroutes.Collapse(asset.Graph);
        _infos = _graph.Nodes.ToDictionary(n => n.Id, n => program.Describe(n, asset));
    }

    /// <summary>Un gestionnaire par événement du graphe qui déclenche au moins une instruction.</summary>
    public IReadOnlyList<IREventHandler> CompileEvents() =>
        [.. _graph.Nodes
            .Where(n => Info(n).IsEntryPoint && n.Kind != GraphNodeKind.FunctionEntry)
            .OrderBy(n => n.Y).ThenBy(n => n.X)
            .Select(CompileHandler)
            .Where(h => h.Body.Count > 0)];

    /// <summary>Corps d'une fonction, d'une procédure ou d'un constructeur, depuis son nœud d'entrée.</summary>
    public IRBody CompileBody()
    {
        _locals = [];
        GraphNode? entry = _graph.Nodes.FirstOrDefault(n => n.Kind == GraphNodeKind.FunctionEntry);
        if (entry is null)
        {
            return IRBody.Empty;
        }

        List<IRStatement> statements = CompileChain(_graph.NextNode(entry.Id, NodeInfo.ExecThen), entry, []);
        return new IRBody([.. _locals.Values], statements);
    }

    /// <summary>Valeur initiale d'une variable, d'un champ ou d'un paramètre, d'après sa valeur par défaut.</summary>
    public IRExpression Initial(VariableDefinition variable)
    {
        PinInfo pin = _program.Pin(variable.Id.ToString("D"), variable.Name, variable.Type, variable.DefaultValue);
        return variable.Type.HasInlineValue && !(variable.Type.IsContentReference && string.IsNullOrEmpty(pin.DefaultValue))
            ? Literal(pin, pin.DefaultValue)
            : new IRDefaultValue(variable.Type);
    }

    private NodeInfo Info(GraphNode node) => _infos[node.Id];

    private IREventHandler CompileHandler(GraphNode eventNode)
    {
        _locals = [];
        List<IRStatement> body = CompileChain(_graph.NextNode(eventNode.Id, NodeInfo.ExecThen), eventNode, []);
        return new IREventHandler(eventNode.Kind, eventNode.Id, body, BoundItem(eventNode)) { Locals = [.. _locals.Values] };
    }

    /// <summary>Item du mod visé par un événement d'item, sinon null.</summary>
    private NamespacedId? BoundItem(GraphNode eventNode) =>
        Info(eventNode).ItemEvent is null
            ? null
            : ((IRContentLiteral)Literal(Info(eventNode).Inputs[0], eventNode.GetValue("item"))).Id;

    private List<IRStatement> CompileChain(GraphNode? node, GraphNode start, HashSet<Guid> path)
    {
        var statements = new List<IRStatement>();
        while (node is not null)
        {
            if (!path.Add(node.Id))
            {
                throw new InvalidOperationException(Tr.F("Boucle d'exécution sur « {0} ».", Info(node).Title));
            }

            NodeInfo info = Info(node);
            if (info.Flow == NodeFlow.Branch)
            {
                statements.Add(new IRIfStatement(
                    node.Id,
                    Condition(node, start),
                    CompileChain(_graph.NextNode(node.Id, NodeInfo.ExecTrue), start, [.. path]),
                    CompileChain(_graph.NextNode(node.Id, NodeInfo.ExecFalse), start, [.. path])));
                break;
            }

            statements.Add(Statement(node, info, start));
            if (info.Flow == NodeFlow.End)
            {
                break;
            }

            node = _graph.NextNode(node.Id, NodeInfo.ExecThen);
        }

        return statements;
    }

    private IRStatement Statement(GraphNode node, NodeInfo info, GraphNode start) => node.Kind switch
    {
        GraphNodeKind.CallFunction => new IRCallStatement(node.Id, Callee(node), Target(node, info, start),
            Arguments(node, info, start, skipTarget: true), [.. info.Outputs.Select(o => Local(node, o).Name)]),
        GraphNodeKind.NewObject => new IRNewStatement(node.Id, node.Target!.Value, Arguments(node, info, start, skipTarget: false),
            Local(node, info.Outputs[0]).Name),
        GraphNodeKind.SetVariable => new IRSetVariableStatement(node.Id, node.Member!.Value, Input(node, info.Inputs[0], start)),
        GraphNodeKind.SetField => new IRSetFieldStatement(node.Id, Target(node, info, start), node.Target!.Value, node.Member!.Value,
            Input(node, info.FindInput("value")!, start)),
        GraphNodeKind.FunctionReturn => new IRReturnStatement(node.Id, Arguments(node, info, start, skipTarget: false)),
        GraphNodeKind.SuperConstructor => new IRSuperStatement(node.Id, Arguments(node, info, start, skipTarget: false)),
        _ => new IRActionStatement(node.Id, node.Kind, Arguments(node, info, start, skipTarget: false)),
    };

    private IRCallee Callee(GraphNode node) =>
        _program.Find(node.Target) is { Kind: GraphKind.Interface }
            ? new IRCallee(node.Target!.Value, node.Member)
            : new IRCallee(node.Target!.Value);

    /// <summary>Objet d'une méthode ou d'un champ ; null quand l'entrée est vide ("Moi") ou absente (méthode statique).</summary>
    private IRExpression? Target(GraphNode node, NodeInfo info, GraphNode start) =>
        info.FindInput(NodeInfo.TargetInput) is { } pin && _graph.InputConnection(node.Id, pin.Name) is not null
            ? Input(node, pin, start)
            : null;

    private IRExpression Condition(GraphNode node, GraphNode start) =>
        node.Kind == GraphNodeKind.Branch
            ? Input(node, Info(node).Inputs[0], start)
            : new IRCallExpression(DataType.Boolean, node.Kind, Arguments(node, Info(node), start, skipTarget: false));

    private List<IRExpression> Arguments(GraphNode node, NodeInfo info, GraphNode start, bool skipTarget) =>
        [.. info.Inputs.Where(p => !skipTarget || p.Name != NodeInfo.TargetInput).Select(pin => Input(node, pin, start))];

    /// <summary>Expression qui alimente une entrée : le fil branché dessus, sinon la valeur saisie.</summary>
    private IRExpression Input(GraphNode node, PinInfo pin, GraphNode start)
    {
        if (_graph.InputConnection(node.Id, pin.Name) is not { } connection || _graph.FindNode(connection.FromNode) is not { } source)
        {
            return Literal(pin, node.Values.TryGetValue(pin.Name, out string? typed) ? typed : pin.DefaultValue);
        }

        NodeInfo sourceInfo = Info(source);
        PinInfo output = sourceInfo.FindOutput(connection.FromPin)
                         ?? throw new InvalidOperationException(Tr.F("Sortie « {0} » inconnue sur « {1} ».", connection.FromPin, sourceInfo.Title));
        IRExpression value = sourceInfo switch
        {
            { IsEntryPoint: true } when source.Kind == GraphNodeKind.FunctionEntry => new IRParameterValue(output.Type, Guid.Parse(output.Name)),
            { IsEntryPoint: true } => new IREventValue(output.Type, output.Name),
            { IsPure: true } => Value(source, sourceInfo, output, start),
            _ => new IRLocalValue(output.Type, Local(source, output).Name),
        };
        return output.Type.Kind == PinType.Number && pin.Type.Kind == PinType.Text ? new IRToText(value) : value;
    }

    /// <summary>Résultat d'un nœud de valeur.</summary>
    private IRExpression Value(GraphNode node, NodeInfo info, PinInfo output, GraphNode start) => node.Kind switch
    {
        GraphNodeKind.NumberValue or GraphNodeKind.TextValue or GraphNodeKind.BooleanValue =>
            Literal(info.Inputs[0], node.GetValue("value")),
        GraphNodeKind.GetVariable => new IRVariableValue(output.Type, node.Member!.Value),
        GraphNodeKind.GetField => new IRFieldValue(output.Type, Target(node, info, start), node.Target!.Value, node.Member!.Value),
        GraphNodeKind.EnumLiteral => Literal(info.Inputs[0], node.Values.GetValueOrDefault("value") ?? info.Inputs[0].DefaultValue),
        GraphNodeKind.Self => new IRSelfValue(output.Type),
        GraphNodeKind.CallFunction => new IRPureCall(output.Type, Callee(node), Target(node, info, start),
            Arguments(node, info, start, skipTarget: true), info.Outputs.Count > 1 ? Guid.Parse(output.Name) : null),
        _ => new IRCallExpression(output.Type, node.Kind, Arguments(node, info, start, skipTarget: false)),
    };

    /// <summary>Variable locale qui reçoit une sortie d'un nœud exécuté ("_v1"...) ; créée à la première demande.</summary>
    private IRLocal Local(GraphNode node, PinInfo output)
    {
        if (!_locals.TryGetValue((node.Id, output.Name), out IRLocal? local))
        {
            local = new IRLocal($"_v{_locals.Count + 1}", output.Type);
            _locals[(node.Id, output.Name)] = local;
        }

        return local;
    }

    private IRExpression Literal(PinInfo pin, string? text) => pin.Type.Kind switch
    {
        PinType.Number => new IRNumberLiteral(GraphValidator.TryParseNumber(text, out int number) ? number : 0),
        PinType.Text => new IRTextLiteral(text ?? string.Empty),
        PinType.Boolean => new IRBooleanLiteral(bool.TryParse(text, out bool flag) && flag),
        PinType.Enum => Guid.TryParse(text, out Guid constant)
            ? new IREnumValue(pin.Type, constant)
            : throw new InvalidOperationException(Tr.F("« {0} » : choisissez une valeur.", pin.Label)),
        PinType.Item or PinType.Block or PinType.Sound =>
            ContentReference.TryParseStorageText(text, out ContentReference? reference) && _resolveContent(reference!, pin.Type) is { } id
                ? new IRContentLiteral(pin.Type, id)
                : throw new InvalidOperationException(Tr.F("« {0} » ne désigne aucun {1}.", pin.Label, pin.Type.BuiltInLabel.ToLowerInvariant())),
        _ => pin.Optional ? new IRDefaultValue(pin.Type) : throw new InvalidOperationException(Tr.F("L'entrée « {0} » doit être reliée.", pin.Label)),
    };
}
