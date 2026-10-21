using MineEngine.Core.Assets;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Les définitions de programme d'un projet (fonctions, procédures, classes, structures,
/// interfaces, énumérations) et ce qui en découle : héritage, champs et méthodes d'une
/// classe, visibilité d'une définition depuis un graphe, et description des nœuds qui
/// les utilisent (appel, création d'objet, champ...). Instantané : on en reconstruit un
/// après une modification du projet.
/// </summary>
/// <remarks>
/// Visibilité : une définition publique est utilisable partout. Une définition intégrée
/// à une classe (méthode, type imbriqué) privée ne l'est que dans cette classe, protégée
/// aussi dans les classes qui en héritent. Une définition privée hors de toute classe ne
/// l'est que depuis les graphes de son dossier du Dashboard (et de ses sous-dossiers).
/// </remarks>
public sealed class ProgramModel
{
    private readonly Dictionary<Guid, GraphAsset> _graphs;

    public ProgramModel(IEnumerable<GraphAsset> graphs)
    {
        _graphs = (graphs ?? []).GroupBy(g => g.Id).ToDictionary(g => g.Key, g => g.First());
    }

    public static ProgramModel From(IEnumerable<Asset> assets) => new(assets.OfType<GraphAsset>());

    public IEnumerable<GraphAsset> Graphs => _graphs.Values;

    /// <summary>Définitions de programme (sans les graphes d'événements).</summary>
    public IEnumerable<GraphAsset> Definitions => _graphs.Values.Where(g => g.Kind != GraphKind.Events);

    public GraphAsset? Find(Guid? id) => id is { } value ? _graphs.GetValueOrDefault(value) : null;

    public IGraphContext ContextFor(GraphAsset graph) => new GraphContext(this, graph ?? throw new ArgumentNullException(nameof(graph)));

    // ----- Types et héritage -----

    /// <summary>Type des valeurs d'une classe, d'une structure, d'une interface ou d'une énumération.</summary>
    public static DataType TypeOf(GraphAsset definition) => DataType.UserType(definition.Id, definition.Kind == GraphKind.Enum);

    public string TypeLabel(DataType type) => type.Definition is { } id
        ? Find(id) is { } definition ? Name(definition) : Tr.T("Type deleted")
        : type.BuiltInLabel;

    /// <summary>Nom affiché d'une définition (nom du graphe, sinon son identifiant).</summary>
    public static string Name(GraphAsset definition) =>
        string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.ResourceId.Value : definition.DisplayName;

    /// <summary>Classe parente, puis ses parentes, sans boucler sur un héritage circulaire.</summary>
    public IEnumerable<GraphAsset> Ancestors(GraphAsset type)
    {
        var seen = new HashSet<Guid> { type.Id };
        for (GraphAsset? current = Find(type.BaseClassId); current is { Kind: GraphKind.Class } && seen.Add(current.Id); current = Find(current.BaseClassId))
        {
            yield return current;
        }
    }

    /// <summary>La classe et ses parentes, en commençant par elle.</summary>
    public IEnumerable<GraphAsset> SelfAndAncestors(GraphAsset type) => Ancestors(type).Prepend(type);

    /// <summary>Vrai si un objet de <paramref name="type"/> est aussi un <paramref name="target"/> (même type, parente, interface).</summary>
    public bool IsSubtypeOf(Guid type, Guid target)
    {
        if (type == target)
        {
            return true;
        }

        if (Find(type) is not { } definition)
        {
            return false;
        }

        return SelfAndAncestors(definition).Any(c => c.Id == target || c.InterfaceIds.Contains(target));
    }

    public bool CanFeed(DataType from, DataType to) =>
        TypeRules.CanFeedBuiltIn(from, to)
        || (from.Kind == PinType.Object && to.Kind == PinType.Object
            && from.Definition is { } source && to.Definition is { } target && IsSubtypeOf(source, target));

    /// <summary>Champs d'une classe ou d'une structure, hérités compris (ceux des parentes d'abord).</summary>
    public IEnumerable<(GraphAsset Declaring, VariableDefinition Field)> Fields(GraphAsset type) =>
        SelfAndAncestors(type).Reverse().SelectMany(c => c.Variables.Select(v => (c, v)));

    /// <summary>Définitions intégrées à une classe (méthodes, types imbriqués).</summary>
    public IEnumerable<GraphAsset> Members(GraphAsset owner) => _graphs.Values.Where(g => g.OwnerId == owner.Id);

    /// <summary>Méthodes d'une classe, héritées comprises (une méthode redéfinie n'apparaît qu'une fois).</summary>
    public IEnumerable<GraphAsset> Methods(GraphAsset type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (GraphAsset declaring in SelfAndAncestors(type))
        {
            foreach (GraphAsset method in Members(declaring).Where(m => m.Kind.IsCallable() && names.Add(MemberName(m))))
            {
                yield return method;
            }
        }
    }

    /// <summary>
    /// Nom de programme d'une méthode ("Attaquer la cible" donne "attaquerLaCible") : deux méthodes
    /// de même nom dans une classe et sa parente sont une redéfinition.
    /// </summary>
    public static string MemberName(GraphAsset method) => ProgramNames.Identifier(Name(method));

    /// <summary>Classe dans laquelle s'exécute le graphe : la classe elle-même (constructeur), ou la classe d'une méthode.</summary>
    public GraphAsset? EnclosingClass(GraphAsset graph)
    {
        if (graph.Kind == GraphKind.Class)
        {
            return graph;
        }

        return Find(graph.OwnerId) is { Kind: GraphKind.Class } owner ? owner : null;
    }

    /// <summary>Type de "Moi" dans le graphe : la classe d'un constructeur ou d'une méthode non statique, sinon null.</summary>
    public GraphAsset? SelfClass(GraphAsset graph) =>
        graph.Kind == GraphKind.Class || graph.IsInstanceMember ? EnclosingClass(graph) : null;

    // ----- Visibilité -----

    /// <summary>Vrai si <paramref name="graph"/> est la définition <paramref name="container"/> ou lui est intégré (à tout niveau).</summary>
    public bool IsInside(GraphAsset graph, GraphAsset container)
    {
        var seen = new HashSet<Guid>();
        for (GraphAsset? current = graph; current is not null && seen.Add(current.Id); current = Find(current.OwnerId))
        {
            if (current.Id == container.Id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Vrai si la définition peut être utilisée (appelée, créée, comme type) depuis le graphe.</summary>
    public bool IsVisible(GraphAsset definition, GraphAsset from) => IsVisible(definition, from, depth: 0);

    /// <summary>Vrai si un membre (champ ou méthode) déclaré dans <paramref name="declaring"/> est accessible depuis le graphe.</summary>
    public bool CanAccess(GraphAsset declaring, AccessLevel access, GraphAsset from) => access switch
    {
        AccessLevel.Public => true,
        AccessLevel.Private => IsInside(from, declaring),
        _ => IsInside(from, declaring) || (EnclosingClass(from) is { } enclosing && IsSubtypeOf(enclosing.Id, declaring.Id)),
    };

    /// <summary>Types (classes, structures, interfaces, énumérations) utilisables depuis le graphe.</summary>
    public IEnumerable<GraphAsset> VisibleTypes(GraphAsset from) =>
        Definitions.Where(d => d.Kind.IsType() && IsVisible(d, from)).OrderBy(Name, StringComparer.CurrentCultureIgnoreCase);

    private bool IsVisible(GraphAsset definition, GraphAsset from, int depth)
    {
        if (definition.Id == from.Id || IsInside(from, definition))
        {
            return true;
        }

        if (depth > 32)
        {
            return false;
        }

        if (Find(definition.OwnerId) is { } owner)
        {
            return IsVisible(owner, from, depth + 1) && CanAccess(owner, definition.Access, from);
        }

        return definition.Access == AccessLevel.Public || AssetFolders.IsWithin(from.Folder, definition.Folder);
    }

    // ----- Description des nœuds -----

    /// <summary>Broches d'un nœud placé dans <paramref name="graph"/>.</summary>
    public NodeInfo Describe(GraphNode node, GraphAsset graph)
    {
        if (NodeCatalog.Find(node.Kind) is { } catalog)
        {
            return catalog;
        }

        return node.Kind switch
        {
            GraphNodeKind.FunctionEntry => Entry(graph),
            GraphNodeKind.FunctionReturn => Return(graph),
            GraphNodeKind.CallFunction => Call(node, graph),
            GraphNodeKind.NewObject => New(node, graph),
            GraphNodeKind.GetVariable or GraphNodeKind.SetVariable => Variable(node, graph),
            GraphNodeKind.GetField or GraphNodeKind.SetField => Field(node, graph),
            GraphNodeKind.EnumLiteral or GraphNodeKind.EnumEquals => EnumNode(node, graph),
            GraphNodeKind.Self => Self(graph),
            GraphNodeKind.SuperConstructor => Super(node, graph),
            GraphNodeKind.Reroute => Reroutes.Describe(node, TypeLabel),
            GraphNodeKind.Comment or GraphNodeKind.Tooltip => GraphAnnotations.Describe(node),
            _ => NodeCatalog.Missing(node.Kind, node.Kind.ToString()),
        };
    }

    /// <summary>Entrée facultative "Objet" : vide, elle désigne "Moi" quand le graphe s'exécute dans la classe (ou une héritière).</summary>
    public bool CanUseImplicitSelf(GraphAsset declaring, GraphAsset graph) =>
        SelfClass(graph) is { } self && IsSubtypeOf(self.Id, declaring.Id);

    /// <summary>Valeur par défaut d'une entrée de ce type : celle de la variable, sinon zéro, faux, vide ou la première constante.</summary>
    public string? DefaultValueFor(DataType type, string? declared = null)
    {
        if (!string.IsNullOrEmpty(declared))
        {
            return declared;
        }

        return type.Kind switch
        {
            PinType.Number => "0",
            PinType.Boolean => "false",
            PinType.Text or PinType.Item or PinType.Block or PinType.Sound => string.Empty,
            PinType.Enum => Find(type.Definition)?.Constants.FirstOrDefault()?.Id.ToString("D") ?? string.Empty,
            _ => null,
        };
    }

    /// <summary>Broche d'une valeur : valeur par défaut et, pour une énumération, ses constantes.</summary>
    public PinInfo Pin(string name, string label, DataType type, string? declaredDefault = null, bool connectable = true) =>
        new(name, label, type, type.HasInlineValue ? DefaultValueFor(type, declaredDefault) : null, connectable)
        {
            Choices = type.Kind == PinType.Enum ? Find(type.Definition)?.Constants ?? [] : [],
        };

    /// <summary>Entrée "Objet" d'une méthode ou d'un champ ; facultative (vide : "Moi") dans la classe elle-même.</summary>
    private PinInfo ObjectPin(GraphAsset type, GraphAsset graph)
    {
        bool implicitSelf = CanUseImplicitSelf(type, graph);
        return new PinInfo(NodeInfo.TargetInput, implicitSelf ? Tr.T("Object (empty: Self)") : Tr.T("Object"), TypeOf(type)) { Optional = implicitSelf };
    }

    private PinInfo VariablePin(VariableDefinition variable) =>
        Pin(variable.Id.ToString("D"), variable.Name, variable.Type, variable.DefaultValue);

    private NodeInfo Entry(GraphAsset graph) => graph.Kind switch
    {
        GraphKind.Function or GraphKind.Procedure => new NodeInfo(GraphNodeKind.FunctionEntry, NodeCategory.Function, Name(graph),
            graph.Kind == GraphKind.Function
                ? Tr.T("Start of the function: its outputs are the parameters.")
                : Tr.T("Start of the procedure: its outputs are the parameters."),
            outputs: [.. graph.Parameters.Select(VariablePin)], flow: NodeFlow.Start),
        GraphKind.Class => new NodeInfo(GraphNodeKind.FunctionEntry, NodeCategory.Function, Tr.F("{0} constructor", Name(graph)),
            Tr.T("Runs when an object is created: its outputs are the constructor parameters."),
            outputs: [.. graph.Parameters.Select(VariablePin)], flow: NodeFlow.Start),
        _ => NodeCatalog.Missing(GraphNodeKind.FunctionEntry, Tr.T("Entry")),
    };

    private NodeInfo Return(GraphAsset graph) => graph.Kind switch
    {
        GraphKind.Function => new NodeInfo(GraphNodeKind.FunctionReturn, NodeCategory.Function, Tr.T("Return"),
            Tr.T("Ends the function and returns these values."), inputs: [.. graph.Returns.Select(VariablePin)], flow: NodeFlow.End),
        GraphKind.Procedure or GraphKind.Class => new NodeInfo(GraphNodeKind.FunctionReturn, NodeCategory.Function, Tr.T("Return"),
            Tr.T("Ends the execution here."), flow: NodeFlow.End),
        _ => NodeCatalog.Missing(GraphNodeKind.FunctionReturn, Tr.T("Return")),
    };

    private NodeInfo Call(GraphNode node, GraphAsset graph)
    {
        if (Find(node.Target) is not { } target || !IsVisible(target, graph))
        {
            return NodeCatalog.Missing(GraphNodeKind.CallFunction, Tr.T("Call not found"));
        }

        // Méthode d'une interface : appelée sur un objet qui l'implémente.
        if (target.Kind == GraphKind.Interface)
        {
            if (target.Methods.FirstOrDefault(m => m.Id == node.Member) is not { } signature)
            {
                return NodeCatalog.Missing(GraphNodeKind.CallFunction, Tr.T("Method not found"));
            }

            var objectPin = ObjectPin(target, graph);
            return new NodeInfo(GraphNodeKind.CallFunction, NodeCategory.Function, $"{Name(target)}.{signature.Name}",
                Tr.F("Calls the method \"{0}\" of interface {1} on the object.", signature.Name, Name(target)),
                inputs: [objectPin, .. signature.Parameters.Select(VariablePin)], outputs: [.. signature.Returns.Select(VariablePin)],
                flow: NodeFlow.Step);
        }

        if (!target.Kind.IsCallable())
        {
            return NodeCatalog.Missing(GraphNodeKind.CallFunction, Tr.T("Call not found"));
        }

        var inputs = new List<PinInfo>();
        string title = Name(target);
        string kind = target.Kind == GraphKind.Function ? Tr.T("the function") : Tr.T("the procedure");
        NodeFlow flow = target.IsPure && target.Kind == GraphKind.Function ? NodeFlow.Pure : NodeFlow.Step;
        if (Find(target.OwnerId) is { Kind: GraphKind.Library } library)
        {
            // Function of a library: gray, grouped under the name of the library in the Nodes window.
            return new NodeInfo(GraphNodeKind.CallFunction, NodeCategory.Library, title,
                Tr.F("Calls {0} \"{1}\" of the library {2}.", kind, Name(target), Name(library)),
                inputs: [.. target.Parameters.Select(VariablePin)], outputs: [.. target.Returns.Select(VariablePin)], flow: flow)
            {
                Library = Name(library),
            };
        }

        if (Find(target.OwnerId) is { Kind: GraphKind.Class } owner)
        {
            title = $"{Name(owner)}.{Name(target)}";
            if (!target.IsStatic)
            {
                inputs.Add(ObjectPin(owner, graph));
            }
        }

        inputs.AddRange(target.Parameters.Select(VariablePin));
        return new NodeInfo(GraphNodeKind.CallFunction, NodeCategory.Function, title, Tr.F("Calls {0} \"{1}\".", kind, Name(target)),
            inputs: inputs, outputs: [.. target.Returns.Select(VariablePin)], flow: flow);
    }

    private NodeInfo New(GraphNode node, GraphAsset graph)
    {
        if (Find(node.Target) is not { Kind: GraphKind.Class or GraphKind.Struct } type || !IsVisible(type, graph))
        {
            return NodeCatalog.Missing(GraphNodeKind.NewObject, Tr.T("Type not found"));
        }

        IEnumerable<VariableDefinition> arguments = type.Kind == GraphKind.Class ? type.Parameters : type.Variables;
        return new NodeInfo(GraphNodeKind.NewObject, NodeCategory.Object, Tr.F("Create {0}", Name(type)),
            type.Kind == GraphKind.Class
                ? Tr.F("Creates an object of class {0} (its constructor runs).", Name(type))
                : Tr.F("Creates a {0} struct with the value of each field.", Name(type)),
            inputs: [.. arguments.Select(VariablePin)],
            outputs: [new PinInfo("object", Name(type), TypeOf(type))],
            flow: NodeFlow.Step);
    }

    private NodeInfo Variable(GraphNode node, GraphAsset graph)
    {
        if (graph.Variables.FirstOrDefault(v => v.Id == node.Member) is not { } variable)
        {
            return NodeCatalog.Missing(node.Kind, Tr.T("Variable not found"));
        }

        return node.Kind == GraphNodeKind.GetVariable
            ? new NodeInfo(GraphNodeKind.GetVariable, NodeCategory.Object, variable.Name, Tr.F("Value of the variable \"{0}\".", variable.Name),
                outputs: [new PinInfo("value", variable.Name, variable.Type)])
            : new NodeInfo(GraphNodeKind.SetVariable, NodeCategory.Object, Tr.F("Set {0}", variable.Name),
                Tr.F("Changes the value of the variable \"{0}\".", variable.Name),
                inputs: [Pin("value", variable.Name, variable.Type, variable.DefaultValue)], flow: NodeFlow.Step);
    }

    private NodeInfo Field(GraphNode node, GraphAsset graph)
    {
        if (Find(node.Target) is not { Kind: GraphKind.Class or GraphKind.Struct } type || !IsVisible(type, graph)
            || Fields(type).FirstOrDefault(f => f.Field.Id == node.Member) is not ({ } declaring, { } field)
            || !CanAccess(declaring, field.Access, graph))
        {
            return NodeCatalog.Missing(node.Kind, Tr.T("Field not found"));
        }

        var objectPin = ObjectPin(type, graph);
        string title = $"{Name(type)}.{field.Name}";
        return node.Kind == GraphNodeKind.GetField
            ? new NodeInfo(GraphNodeKind.GetField, NodeCategory.Object, title, Tr.F("Value of the object's field \"{0}\".", field.Name),
                inputs: [objectPin], outputs: [new PinInfo("value", field.Name, field.Type)])
            : new NodeInfo(GraphNodeKind.SetField, NodeCategory.Object, Tr.F("Set {0}", title), Tr.F("Changes the field \"{0}\" of the object.", field.Name),
                inputs: [objectPin, Pin("value", field.Name, field.Type, field.DefaultValue)], flow: NodeFlow.Step);
    }

    private NodeInfo EnumNode(GraphNode node, GraphAsset graph)
    {
        if (Find(node.Target) is not { Kind: GraphKind.Enum } type || !IsVisible(type, graph))
        {
            return NodeCatalog.Missing(node.Kind, Tr.T("Enumeration not found"));
        }

        DataType enumType = TypeOf(type);
        return node.Kind == GraphNodeKind.EnumLiteral
            ? new NodeInfo(GraphNodeKind.EnumLiteral, NodeCategory.Object, Name(type), Tr.F("A value of the enumeration {0}.", Name(type)),
                inputs: [Pin("value", Tr.T("Value"), enumType, connectable: false)], outputs: [new PinInfo("value", Name(type), enumType)])
            : new NodeInfo(GraphNodeKind.EnumEquals, NodeCategory.Object, Tr.F("{0} equal to", Name(type)),
                Tr.F("True if both {0} values are the same.", Name(type)),
                inputs: [Pin("a", "A", enumType), Pin("b", "B", enumType)], outputs: [new PinInfo("result", Tr.T("Result"), DataType.Boolean)]);
    }

    private NodeInfo Self(GraphAsset graph) => SelfClass(graph) is { } self
        ? new NodeInfo(GraphNodeKind.Self, NodeCategory.Object, Tr.T("Self"), Tr.F("The {0} object whose code is running.", Name(self)),
            outputs: [new PinInfo("self", Tr.T("Self"), TypeOf(self))])
        : NodeCatalog.Missing(GraphNodeKind.Self, Tr.T("Self (outside a class)"));

    private NodeInfo Super(GraphNode node, GraphAsset graph)
    {
        if (graph.Kind != GraphKind.Class || Find(graph.BaseClassId) is not { Kind: GraphKind.Class } parent || parent.Id != node.Target)
        {
            return NodeCatalog.Missing(GraphNodeKind.SuperConstructor, Tr.T("Parent constructor not found"));
        }

        return new NodeInfo(GraphNodeKind.SuperConstructor, NodeCategory.Function, Tr.F("Parent constructor: {0}", Name(parent)),
            Tr.T("Builds the inherited part of the object; must directly follow the constructor entry."),
            inputs: [.. parent.Parameters.Select(VariablePin)], flow: NodeFlow.Step);
    }

    /// <summary>Contexte d'un graphe : ses nœuds sont décrits avec les définitions visibles depuis lui.</summary>
    private sealed class GraphContext : IGraphContext
    {
        private readonly ProgramModel _program;
        private readonly GraphAsset _graph;

        public GraphContext(ProgramModel program, GraphAsset graph)
        {
            _program = program;
            _graph = graph;
        }

        public GraphKind Kind => _graph.Kind;

        public NodeInfo Describe(GraphNode node) => _program.Describe(node, _graph);

        public bool CanFeed(DataType from, DataType to) => _program.CanFeed(from, to);

        public string TypeLabel(DataType type) => _program.TypeLabel(type);
    }
}
