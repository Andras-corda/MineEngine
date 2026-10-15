using MineEngine.Core.GameData;

namespace MineEngine.Assets.Graphs;

/// <summary>Famille d'un nœud ; donne sa couleur et son dossier dans le menu d'ajout.</summary>
public enum NodeCategory
{
    /// <summary>Point de départ : réagit à un événement du jeu.</summary>
    Event,

    /// <summary>Choisit la suite selon une condition (sorties Vrai / Faux).</summary>
    Condition,

    /// <summary>Agit sur le jeu, puis continue.</summary>
    Action,

    /// <summary>Calcule une valeur, sans fil d'exécution.</summary>
    Variable,

    /// <summary>Entrée, retour et appel d'une fonction ou d'une procédure.</summary>
    Function,

    /// <summary>Objets : création, champs, valeurs d'énumération, variables du graphe.</summary>
    Object,
}

/// <summary>Sorte de nœud. Le nom est enregistré dans les fichiers : ne pas renommer.</summary>
public enum GraphNodeKind
{
    // Événements
    OnRightClickBlock,
    OnUseItem,
    OnPlayerJoin,
    OnBlockBroken,

    // Conditions
    Branch,
    HasItem,
    HoldsItem,
    IsSneaking,
    Chance,

    // Actions
    SpawnItem,
    GiveItem,
    RemoveItem,
    SendMessage,
    PlaySound,
    SetBlock,
    Explosion,
    CancelEvent,

    // Valeurs
    NumberValue,
    TextValue,
    BooleanValue,
    PlayerPosition,
    PlayerName,
    OffsetPosition,
    RandomNumber,
    JoinText,
    GreaterThan,
    NumbersEqual,
    Not,

    // Événements d'un item du mod (V0.4.1) : générés dans la classe de l'item.
    OnItemRightClick,
    OnItemUseOnBlock,
    OnItemHitEntity,
    OnItemBlockMined,
    OnItemConsumed,
    OnItemCrafted,
    OnItemHeldTick,

    // Programmation (V0.4.3) : nœuds décrits par les définitions du projet (voir ProgramModel).
    /// <summary>Début du corps d'une fonction, d'une procédure ou d'un constructeur : ses sorties sont les paramètres.</summary>
    FunctionEntry,

    /// <summary>Fin d'une fonction ou d'une procédure : ses entrées sont les valeurs renvoyées.</summary>
    FunctionReturn,

    /// <summary>Appel d'une fonction, d'une procédure ou d'une méthode (Target : sa définition ; Member : méthode d'interface).</summary>
    CallFunction,

    /// <summary>Création d'un objet d'une classe ou d'une structure (Target).</summary>
    NewObject,

    /// <summary>Lecture d'une variable du graphe (Member : son identifiant).</summary>
    GetVariable,

    /// <summary>Modification d'une variable du graphe.</summary>
    SetVariable,

    /// <summary>Lecture d'un champ (Target : la classe ou la structure ; Member : le champ).</summary>
    GetField,

    /// <summary>Modification d'un champ.</summary>
    SetField,

    /// <summary>Une valeur d'une énumération (Target), choisie sur le nœud.</summary>
    EnumLiteral,

    /// <summary>Compare deux valeurs d'une énumération (Target).</summary>
    EnumEquals,

    /// <summary>L'objet dont on exécute la méthode ou le constructeur ("this").</summary>
    Self,

    /// <summary>Appel du constructeur de la classe parente (Target), au début d'un constructeur.</summary>
    SuperConstructor,

    // Calcul (V0.4.3)
    Add,
    Subtract,
    Multiply,
    LessThan,
}

/// <summary>
/// Entrée ou sortie d'un nœud. Une entrée dont le type a une valeur en ligne (nombre,
/// texte, item...) prend <see cref="DefaultValue"/> tant qu'elle n'est pas reliée ;
/// une entrée non reliable (<see cref="Connectable"/> faux) n'est qu'un champ du nœud.
/// Une entrée facultative (<see cref="Optional"/>) peut rester vide : l'objet d'une méthode
/// appelée depuis sa propre classe vaut alors "this".
/// </summary>
public sealed record PinInfo(string Name, string Label, DataType Type, string? DefaultValue = null, bool Connectable = true)
{
    public bool Optional { get; init; }

    /// <summary>Valeurs proposées pour une entrée d'énumération (identifiant, nom), vide sinon.</summary>
    public IReadOnlyList<EnumConstant> Choices { get; init; } = [];
}

/// <summary>Description d'une sorte de nœud : titre, famille, entrées et sorties.</summary>
public sealed class NodeInfo
{
    /// <summary>Entrée d'exécution de tous les nœuds qui en ont une.</summary>
    public const string ExecInput = "exec";

    /// <summary>Sortie d'exécution "la suite" des événements et des actions.</summary>
    public const string ExecThen = "then";

    public const string ExecTrue = "true";

    public const string ExecFalse = "false";

    /// <summary>Entrée "objet" d'une méthode ou d'un champ.</summary>
    public const string TargetInput = "target";

    private static readonly PinInfo ThenPin = new(ExecThen, string.Empty, DataType.Exec);

    /// <param name="flow">
    /// Fil d'exécution du nœud ; par défaut celui de sa famille : sortie seule pour un événement,
    /// entrée et sortie pour une action, Vrai / Faux pour une condition, aucun pour une valeur.
    /// </param>
    public NodeInfo(
        GraphNodeKind kind,
        NodeCategory category,
        string title,
        string description,
        IReadOnlyList<PinInfo>? inputs = null,
        IReadOnlyList<PinInfo>? outputs = null,
        NodeFlow? flow = null)
    {
        Kind = kind;
        Category = category;
        Title = title;
        Description = description;
        Inputs = inputs ?? [];
        Outputs = outputs ?? [];
        Flow = flow ?? category switch
        {
            NodeCategory.Event => NodeFlow.Start,
            NodeCategory.Condition => NodeFlow.Branch,
            NodeCategory.Action => NodeFlow.Step,
            _ => NodeFlow.Pure,
        };
        ExecOutputs = Flow switch
        {
            NodeFlow.Start or NodeFlow.Step => [ThenPin],
            NodeFlow.Branch => [new PinInfo(ExecTrue, Tr.T("Vrai"), DataType.Exec), new PinInfo(ExecFalse, Tr.T("Faux"), DataType.Exec)],
            _ => [],
        };
    }

    public GraphNodeKind Kind { get; }

    public NodeCategory Category { get; }

    public string Title { get; }

    public string Description { get; }

    public NodeFlow Flow { get; }

    /// <summary>Entrées de valeur (sans l'entrée d'exécution).</summary>
    public IReadOnlyList<PinInfo> Inputs { get; }

    /// <summary>Sorties de valeur (sans les sorties d'exécution).</summary>
    public IReadOnlyList<PinInfo> Outputs { get; }

    /// <summary>Sorties d'exécution : "then", ou "true" et "false" pour une condition.</summary>
    public IReadOnlyList<PinInfo> ExecOutputs { get; }

    public bool HasExecInput => Flow is NodeFlow.Branch or NodeFlow.Step or NodeFlow.End;

    /// <summary>Point de départ d'une exécution : événement, ou entrée d'une fonction.</summary>
    public bool IsEntryPoint => Flow == NodeFlow.Start;

    /// <summary>Vrai pour un nœud de valeur : il est calculé là où sa sortie est utilisée.</summary>
    public bool IsPure => Flow == NodeFlow.Pure;

    /// <summary>Événement que Minecraft permet d'annuler (le nœud "Annuler l'événement" y est permis).</summary>
    public bool IsCancellable { get; init; }

    /// <summary>Événement propre à un item du mod (son entrée "item" désigne l'item), sinon null.</summary>
    public ItemEventKind? ItemEvent { get; init; }

    /// <summary>Vrai si la définition visée par le nœud n'existe plus (le nœud est alors en erreur).</summary>
    public bool IsMissing { get; init; }

    public PinInfo? FindInput(string name) => Inputs.FirstOrDefault(p => p.Name == name);

    public PinInfo? FindOutput(string name) => Outputs.FirstOrDefault(p => p.Name == name);

    public PinInfo? FindExecOutput(string name) => ExecOutputs.FirstOrDefault(p => p.Name == name);

    /// <summary>Décrit les broches : deux descriptions égales donnent le même nœud à l'écran.</summary>
    public string Signature => string.Join("|",
        new[] { Title, Flow.ToString() }
            .Concat(Inputs.Select(p => $"i:{p.Name}:{p.Label}:{p.Type}:{p.Optional}:{string.Join(',', p.Choices.Select(c => c.Name))}"))
            .Concat(Outputs.Select(p => $"o:{p.Name}:{p.Label}:{p.Type}")));

    public override string ToString() => Title;
}

/// <summary>Place d'un nœud dans le fil d'exécution.</summary>
public enum NodeFlow
{
    /// <summary>Point de départ (événement, entrée d'une fonction) : une sortie, pas d'entrée.</summary>
    Start,

    /// <summary>Instruction : une entrée, une sortie "la suite".</summary>
    Step,

    /// <summary>Condition : une entrée, sorties Vrai et Faux.</summary>
    Branch,

    /// <summary>Fin (retour d'une fonction) : une entrée, pas de suite.</summary>
    End,

    /// <summary>Valeur calculée à la demande, sans fil d'exécution.</summary>
    Pure,
}
