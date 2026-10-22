using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>
/// Comportements d'un graphe d'événements, indépendants du loader : pour chaque nœud
/// événement, la suite d'instructions qu'il déclenche, et les variables du graphe
/// (conservées entre deux événements). Les backends en font une classe Java.
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

    /// <summary>Variables du graphe, communes à toutes ses réactions.</summary>
    public IReadOnlyList<IRVariable> Variables { get; init; } = [];
}

/// <summary>
/// Réaction à un événement du jeu : le nœud événement et les instructions qui le suivent.
/// <paramref name="Item"/> est l'item du mod d'un événement d'item (son code va dans la classe de l'item).
/// </summary>
public sealed record IREventHandler(GraphNodeKind Event, Guid NodeId, IReadOnlyList<IRStatement> Body, NamespacedId? Item = null)
{
    public bool IsItemEvent => Item is not null;

    /// <summary>Command or keybind (its identifier) that runs an "On command" or "On key pressed" event, or null.</summary>
    public string? Trigger { get; init; }

    /// <summary>Called directly by the generated code (item, command, keybind) instead of the event bus.</summary>
    public bool IsDirectCall => Item is not null || Trigger is not null;

    /// <summary>Valeurs produites par les nœuds exécutés (appel, création d'objet), déclarées en tête de méthode.</summary>
    public IReadOnlyList<IRLocal> Locals { get; init; } = [];
}

/// <summary>Variable déclarée en tête de méthode, initialisée à la valeur par défaut de son type.</summary>
public sealed record IRLocal(string Name, DataType Type);

/// <summary>Variable, champ ou paramètre, avec sa valeur initiale.</summary>
public sealed record IRVariable(VariableDefinition Definition, IRExpression Initial)
{
    public Guid Id => Definition.Id;

    public DataType Type => Definition.Type;
}

/// <summary>Ce qu'on appelle : une fonction, une procédure, une méthode (<see cref="Definition"/>), ou une méthode d'interface.</summary>
public sealed record IRCallee(Guid Definition, Guid? InterfaceMethod = null);

/// <summary>Instruction d'un comportement.</summary>
public abstract record IRStatement(Guid NodeId);

/// <summary>
/// Loop (V0.6.5): Repeat, While or For each player. <paramref name="Outputs"/> are the locals that
/// receive the values of each turn (number of the turn, player); <paramref name="Body"/> runs at each turn.
/// </summary>
public sealed record IRLoopStatement(
    Guid NodeId, GraphNodeKind Kind, IReadOnlyList<IRExpression> Arguments, IReadOnlyList<string> Outputs, IReadOnlyList<IRStatement> Body)
    : IRStatement(NodeId);

/// <summary>Action sur le jeu (donner un item, placer un bloc...), avec ses arguments dans l'ordre des entrées du nœud.</summary>
public sealed record IRActionStatement(Guid NodeId, GraphNodeKind Action, IReadOnlyList<IRExpression> Arguments) : IRStatement(NodeId);

/// <summary>
/// Switch on a number or a text: the value is computed once, then the body of the first equal
/// case runs (<paramref name="Cases"/> and <paramref name="Bodies"/> go together), otherwise
/// <paramref name="Default"/>.
/// </summary>
public sealed record IRSwitchStatement(
    Guid NodeId, DataType Type, IRExpression Value, IReadOnlyList<IRExpression> Cases,
    IReadOnlyList<IReadOnlyList<IRStatement>> Bodies, IReadOnlyList<IRStatement> Default)
    : IRStatement(NodeId);

/// <summary>Flip Flop: <paramref name="A"/> one time, <paramref name="B"/> the next; <paramref name="Output"/> receives "is A".</summary>
public sealed record IRFlipFlopStatement(Guid NodeId, string Output, IReadOnlyList<IRStatement> A, IReadOnlyList<IRStatement> B)
    : IRStatement(NodeId);

/// <summary>Change of a global variable; <paramref name="Key"/>: the player, the entity or the position its scope needs, or null.</summary>
public sealed record IRSetGlobalStatement(Guid NodeId, Guid GlobalId, IRExpression? Key, IRExpression Value) : IRStatement(NodeId);

/// <summary>Choix entre deux suites d'instructions (nœud condition).</summary>
public sealed record IRIfStatement(Guid NodeId, IRExpression Condition, IReadOnlyList<IRStatement> Then, IReadOnlyList<IRStatement> Else)
    : IRStatement(NodeId);

/// <summary>
/// Appel d'une fonction ou d'une procédure. <paramref name="Target"/> : l'objet d'une méthode
/// (null : "Moi", ou méthode statique). Les valeurs renvoyées vont dans <paramref name="Results"/>
/// (une variable locale par valeur, dans l'ordre ; vide si elles ne servent pas).
/// </summary>
public sealed record IRCallStatement(Guid NodeId, IRCallee Callee, IRExpression? Target, IReadOnlyList<IRExpression> Arguments, IReadOnlyList<string> Results)
    : IRStatement(NodeId);

/// <summary>Création d'un objet ; il est rangé dans la variable locale <paramref name="Result"/>.</summary>
public sealed record IRNewStatement(Guid NodeId, Guid TypeId, IReadOnlyList<IRExpression> Arguments, string Result) : IRStatement(NodeId);

/// <summary>Nouvelle valeur d'une variable du graphe.</summary>
public sealed record IRSetVariableStatement(Guid NodeId, Guid VariableId, IRExpression Value) : IRStatement(NodeId);

/// <summary>Nouvelle valeur d'un champ (<paramref name="Target"/> null : champ de "Moi").</summary>
public sealed record IRSetFieldStatement(Guid NodeId, IRExpression? Target, Guid TypeId, Guid FieldId, IRExpression Value) : IRStatement(NodeId);

/// <summary>Fin de la fonction, avec ses valeurs renvoyées (aucune pour une procédure).</summary>
public sealed record IRReturnStatement(Guid NodeId, IReadOnlyList<IRExpression> Values) : IRStatement(NodeId);

/// <summary>Appel du constructeur de la classe parente, première instruction d'un constructeur.</summary>
public sealed record IRSuperStatement(Guid NodeId, IReadOnlyList<IRExpression> Arguments) : IRStatement(NodeId);

/// <summary>Valeur calculée : constante, valeur fournie par l'événement, ou nœud de calcul.</summary>
public abstract record IRExpression(DataType Type);

public sealed record IRNumberLiteral(int Value) : IRExpression(DataType.Number);

public sealed record IRTextLiteral(string Value) : IRExpression(DataType.Text);

public sealed record IRBooleanLiteral(bool Value) : IRExpression(DataType.Boolean);

/// <summary>Number with decimals typed on an input.</summary>
public sealed record IRDecimalLiteral(double Value) : IRExpression(DataType.Decimal);

/// <summary>One of the six directions ("north"), typed on an input.</summary>
public sealed record IRDirectionLiteral(string Name) : IRExpression(DataType.Direction);

/// <summary>
/// Value typed on an input for the types of TypesVar.md (character, long, float, byte, identifier,
/// fluid, equipment slot, hand, rarity); <paramref name="Text"/> is the text typed on the node.
/// </summary>
public sealed record IRValueLiteral(DataType ValueType, string Text) : IRExpression(ValueType);

/// <summary>Value of a global variable; <paramref name="Key"/>: the player, the entity or the position its scope needs, or null.</summary>
public sealed record IRGlobalValue(DataType ValueType, Guid GlobalId, IRExpression? Key) : IRExpression(ValueType);

/// <summary>Item, bloc ou son désigné par son identifiant en jeu.</summary>
public sealed record IRContentLiteral(DataType ContentType, NamespacedId Id) : IRExpression(ContentType);

/// <summary>Valeur donnée par l'événement en cours ("player", "position").</summary>
public sealed record IREventValue(DataType ValueType, string Name) : IRExpression(ValueType);

/// <summary>Nœud de calcul (ou condition) du catalogue appliqué à ses arguments, dans l'ordre des entrées du nœud.</summary>
public sealed record IRCallExpression(DataType ResultType, GraphNodeKind Kind, IReadOnlyList<IRExpression> Arguments) : IRExpression(ResultType);

/// <summary>Conversion d'un nombre (entier ou décimal) en texte (fil branché sur une entrée de texte).</summary>
public sealed record IRToText(IRExpression Value) : IRExpression(DataType.Text);

/// <summary>Paramètre de la fonction, de la procédure ou du constructeur en cours.</summary>
public sealed record IRParameterValue(DataType ValueType, Guid ParameterId) : IRExpression(ValueType);

/// <summary>Variable locale déclarée en tête de méthode (résultat d'un appel, objet créé).</summary>
public sealed record IRLocalValue(DataType ValueType, string Name) : IRExpression(ValueType);

/// <summary>Variable du graphe (locale, globale, ou variable d'un graphe d'événements).</summary>
public sealed record IRVariableValue(DataType ValueType, Guid VariableId) : IRExpression(ValueType);

/// <summary>Champ d'un objet (<paramref name="Target"/> null : champ de "Moi").</summary>
public sealed record IRFieldValue(DataType ValueType, IRExpression? Target, Guid TypeId, Guid FieldId) : IRExpression(ValueType);

/// <summary>"Moi" : l'objet dont la méthode ou le constructeur s'exécute.</summary>
public sealed record IRSelfValue(DataType SelfType) : IRExpression(SelfType);

/// <summary>Une valeur d'énumération.</summary>
public sealed record IREnumValue(DataType EnumType, Guid ConstantId) : IRExpression(EnumType);

/// <summary>
/// Appel d'une fonction pure, recalculé là où sa valeur est utilisée. <paramref name="Component"/> :
/// la valeur renvoyée voulue quand la fonction en renvoie plusieurs.
/// </summary>
public sealed record IRPureCall(DataType ResultType, IRCallee Callee, IRExpression? Target, IReadOnlyList<IRExpression> Arguments, Guid? Component)
    : IRExpression(ResultType);

/// <summary>Valeur par défaut d'un type (0, faux, texte vide, première constante, aucun objet).</summary>
public sealed record IRDefaultValue(DataType ValueType) : IRExpression(ValueType);
