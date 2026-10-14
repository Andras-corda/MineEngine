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
}

/// <summary>
/// Entrée ou sortie d'un nœud. Une entrée dont le type a une valeur en ligne (nombre,
/// texte, item...) prend <see cref="DefaultValue"/> tant qu'elle n'est pas reliée ;
/// une entrée non reliable (<see cref="Connectable"/> faux) n'est qu'un champ du nœud.
/// </summary>
public sealed record PinInfo(string Name, string Label, PinType Type, string? DefaultValue = null, bool Connectable = true);

/// <summary>Description d'une sorte de nœud : titre, famille, entrées et sorties.</summary>
public sealed class NodeInfo
{
    /// <summary>Entrée d'exécution de tous les nœuds qui en ont une.</summary>
    public const string ExecInput = "exec";

    /// <summary>Sortie d'exécution "la suite" des événements et des actions.</summary>
    public const string ExecThen = "then";

    public const string ExecTrue = "true";

    public const string ExecFalse = "false";

    public NodeInfo(
        GraphNodeKind kind,
        NodeCategory category,
        string title,
        string description,
        IReadOnlyList<PinInfo>? inputs = null,
        IReadOnlyList<PinInfo>? outputs = null)
    {
        Kind = kind;
        Category = category;
        Title = title;
        Description = description;
        Inputs = inputs ?? [];
        Outputs = outputs ?? [];
        ExecOutputs = category switch
        {
            NodeCategory.Event or NodeCategory.Action => [new PinInfo(ExecThen, string.Empty, PinType.Exec)],
            NodeCategory.Condition => [new PinInfo(ExecTrue, "Vrai", PinType.Exec), new PinInfo(ExecFalse, "Faux", PinType.Exec)],
            _ => [],
        };
    }

    public GraphNodeKind Kind { get; }

    public NodeCategory Category { get; }

    public string Title { get; }

    public string Description { get; }

    /// <summary>Entrées de valeur (sans l'entrée d'exécution).</summary>
    public IReadOnlyList<PinInfo> Inputs { get; }

    /// <summary>Sorties de valeur (sans les sorties d'exécution).</summary>
    public IReadOnlyList<PinInfo> Outputs { get; }

    /// <summary>Sorties d'exécution : "then", ou "true" et "false" pour une condition.</summary>
    public IReadOnlyList<PinInfo> ExecOutputs { get; }

    public bool HasExecInput => Category is NodeCategory.Condition or NodeCategory.Action;

    /// <summary>Vrai pour un nœud de valeur : il est calculé là où sa sortie est utilisée.</summary>
    public bool IsPure => Category == NodeCategory.Variable;

    /// <summary>Événement que Minecraft permet d'annuler (le nœud "Annuler l'événement" y est permis).</summary>
    public bool IsCancellable { get; init; }

    /// <summary>Événement propre à un item du mod (son entrée "item" désigne l'item), sinon null.</summary>
    public ItemEventKind? ItemEvent { get; init; }

    public PinInfo? FindInput(string name) => Inputs.FirstOrDefault(p => p.Name == name);

    public PinInfo? FindOutput(string name) => Outputs.FirstOrDefault(p => p.Name == name);

    public PinInfo? FindExecOutput(string name) => ExecOutputs.FirstOrDefault(p => p.Name == name);

    public override string ToString() => Title;
}
