namespace MineEngine.Assets.Graphs;

/// <summary>
/// Type d'une entrée ou d'une sortie de nœud. Exec porte l'ordre d'exécution (le fil
/// blanc) ; les autres types portent une valeur.
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
}

public static class PinTypeExtensions
{
    /// <summary>
    /// Vrai si une sortie de type <paramref name="from"/> peut alimenter une entrée de type
    /// <paramref name="to"/>. Un nombre se convertit en texte ; le reste doit correspondre.
    /// </summary>
    public static bool CanFeed(this PinType from, PinType to) =>
        from == to || (from == PinType.Number && to == PinType.Text);

    /// <summary>Type dont la valeur par défaut se règle directement sur le nœud (entrée non reliée).</summary>
    public static bool HasInlineValue(this PinType type) =>
        type is PinType.Boolean or PinType.Number or PinType.Text or PinType.Item or PinType.Block or PinType.Sound;

    /// <summary>Type qui désigne un élément du jeu ou du projet (item, bloc, son).</summary>
    public static bool IsContentReference(this PinType type) =>
        type is PinType.Item or PinType.Block or PinType.Sound;

    public static string Label(this PinType type) => type switch
    {
        PinType.Exec => "Exécution",
        PinType.Boolean => "Booléen",
        PinType.Number => "Nombre",
        PinType.Text => "Texte",
        PinType.Player => "Joueur",
        PinType.Position => "Position",
        PinType.Item => "Item",
        PinType.Block => "Bloc",
        PinType.Sound => "Son",
        _ => type.ToString(),
    };
}
