namespace MineEngine.Assets.Graphs;

/// <summary>
/// Famille d'une entrée ou d'une sortie de nœud. Exec porte l'ordre d'exécution (le fil
/// blanc) ; les autres portent une valeur. Object et Enum désignent un type défini dans
/// le projet (classe, structure, interface ou énumération) : voir <see cref="DataType"/>.
/// </summary>
public enum PinType
{
    Exec,
    Boolean,
    Number,
    Text,
    Player,
    Position,
    Item,
    Block,
    Sound,

    /// <summary>Instance d'une classe, d'une structure ou d'une interface du projet.</summary>
    Object,

    /// <summary>Valeur d'une énumération du projet.</summary>
    Enum,
}

/// <summary>
/// Type d'une valeur : un type intégré (nombre, texte, joueur...) ou un type défini dans le
/// projet (<see cref="Definition"/> est alors l'asset qui le définit). Immuable, comparable.
/// </summary>
public sealed record DataType(PinType Kind, Guid? Definition = null)
{
    public static readonly DataType Exec = new(PinType.Exec);
    public static readonly DataType Boolean = new(PinType.Boolean);
    public static readonly DataType Number = new(PinType.Number);
    public static readonly DataType Text = new(PinType.Text);
    public static readonly DataType Player = new(PinType.Player);
    public static readonly DataType Position = new(PinType.Position);
    public static readonly DataType Item = new(PinType.Item);
    public static readonly DataType Block = new(PinType.Block);
    public static readonly DataType Sound = new(PinType.Sound);

    /// <summary>Types intégrés proposés pour une variable ou un paramètre (sans l'exécution).</summary>
    public static IReadOnlyList<DataType> BuiltIn { get; } = [Boolean, Number, Text, Player, Position, Item, Block, Sound];

    /// <summary>Type défini dans le projet : classe, structure ou interface (Object), ou énumération (Enum).</summary>
    public static DataType UserType(Guid definition, bool isEnum) => new(isEnum ? PinType.Enum : PinType.Object, definition);

    public static implicit operator DataType(PinType kind) => kind is PinType.Object or PinType.Enum
        ? throw new ArgumentException(Tr.T("Un type du projet doit désigner sa définition."), nameof(kind))
        : new DataType(kind);

    public bool IsExec => Kind == PinType.Exec;

    public bool IsUserType => Definition is not null;

    /// <summary>Valeur qui se règle directement sur le nœud quand l'entrée n'est pas reliée.</summary>
    public bool HasInlineValue => Kind is PinType.Boolean or PinType.Number or PinType.Text or PinType.Item or PinType.Block
        or PinType.Sound or PinType.Enum;

    /// <summary>Type qui désigne un élément du jeu ou du projet (item, bloc, son).</summary>
    public bool IsContentReference => Kind is PinType.Item or PinType.Block or PinType.Sound;

    /// <summary>Libellé d'un type intégré ; un type du projet est nommé par <see cref="IGraphContext.TypeLabel"/>.</summary>
    public string BuiltInLabel => Kind switch
    {
        PinType.Exec => Tr.T("Exécution"),
        PinType.Boolean => Tr.T("Booléen"),
        PinType.Number => Tr.T("Nombre"),
        PinType.Text => Tr.T("Texte"),
        PinType.Player => Tr.T("Joueur"),
        PinType.Position => Tr.T("Position"),
        PinType.Item => Tr.T("Item"),
        PinType.Block => Tr.T("Bloc"),
        PinType.Sound => Tr.T("Son"),
        PinType.Object => Tr.T("Objet"),
        _ => Tr.T("Énumération"),
    };

    /// <summary>Texte enregistré dans les fichiers : "number", "object:guid", "enum:guid".</summary>
    public string ToStorageText() => Definition is { } id
        ? $"{Kind.ToString().ToLowerInvariant()}:{id:D}"
        : Kind.ToString().ToLowerInvariant();

    public static DataType? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string[] parts = text.Trim().Split(':', 2);
        if (!Enum.TryParse(parts[0], ignoreCase: true, out PinType kind) || !Enum.IsDefined(kind) || kind == PinType.Exec)
        {
            return null;
        }

        if (kind is PinType.Object or PinType.Enum)
        {
            return parts.Length == 2 && Guid.TryParse(parts[1], out Guid id) ? new DataType(kind, id) : null;
        }

        return parts.Length == 1 ? new DataType(kind) : null;
    }

    public override string ToString() => ToStorageText();
}

public static class PinTypeExtensions
{
    public static string Label(this PinType type) => new DataType(type, type is PinType.Object or PinType.Enum ? Guid.Empty : null).BuiltInLabel;
}
