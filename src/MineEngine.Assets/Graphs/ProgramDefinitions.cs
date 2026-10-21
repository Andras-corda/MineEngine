namespace MineEngine.Assets.Graphs;

/// <summary>Ce qu'un graphe définit. Le nom est enregistré dans les fichiers : ne pas renommer.</summary>
public enum GraphKind
{
    /// <summary>Réactions à des événements du jeu (le graphe des versions précédentes).</summary>
    Events,

    /// <summary>Suite d'instructions qui renvoie une ou plusieurs valeurs.</summary>
    Function,

    /// <summary>Suite d'instructions qui ne renvoie rien.</summary>
    Procedure,

    /// <summary>Type avec des champs, un constructeur (le graphe) et des méthodes ; peut hériter d'une classe et implémenter des interfaces.</summary>
    Class,

    /// <summary>Type qui regroupe des champs, créé avec la valeur de chacun.</summary>
    Struct,

    /// <summary>Liste de méthodes que les classes qui l'implémentent doivent fournir.</summary>
    Interface,

    /// <summary>Type dont les valeurs possibles sont une liste de noms.</summary>
    Enum,

    /// <summary>
    /// Function library: functions and procedures always public and usable in every graph; they
    /// are its members (like the static methods of a class) and are grouped under its name.
    /// </summary>
    Library,
}

/// <summary>Qui peut utiliser une définition, un champ ou une méthode.</summary>
public enum AccessLevel
{
    /// <summary>Partout dans le projet (et depuis le code Java).</summary>
    Public,

    /// <summary>Dans la classe et dans les classes qui en héritent.</summary>
    Protected,

    /// <summary>
    /// Membre d'une classe : seulement dans cette classe. Définition hors de toute classe :
    /// seulement depuis les graphes du même dossier du Dashboard.
    /// </summary>
    Private,
}

/// <summary>
/// Paramètre, valeur renvoyée, variable ou champ. L'identifiant reste le même quand on
/// renomme : les nœuds qui l'utilisent suivent. <see cref="DefaultValue"/> est au format
/// des valeurs saisies sur les nœuds ("3", "true", "texte", identifiant de constante).
/// </summary>
/// <param name="IsGlobal">Variable d'une fonction ou d'une procédure conservée d'un appel à l'autre.</param>
public sealed record VariableDefinition(
    Guid Id,
    string Name,
    DataType Type,
    string DefaultValue = "",
    AccessLevel Access = AccessLevel.Public,
    bool IsGlobal = false)
{
    public static VariableDefinition Create(string name, DataType type) => new(Guid.NewGuid(), name, type);
}

/// <summary>Valeur possible d'une énumération.</summary>
public sealed record EnumConstant(Guid Id, string Name)
{
    public static EnumConstant Create(string name) => new(Guid.NewGuid(), name);
}

/// <summary>Méthode déclarée par une interface : nom, paramètres et valeurs renvoyées (aucune pour une procédure).</summary>
public sealed record MethodSignature(Guid Id, string Name, IReadOnlyList<VariableDefinition> Parameters, IReadOnlyList<VariableDefinition> Returns)
{
    public static MethodSignature Create(string name) => new(Guid.NewGuid(), name, [], []);

    /// <summary>Même nom de programme, mêmes types de paramètres et de valeurs renvoyées (les noms des paramètres peuvent différer).</summary>
    public bool Matches(string name, IReadOnlyList<VariableDefinition> parameters, IReadOnlyList<VariableDefinition> returns) =>
        ProgramNames.Identifier(Name) == ProgramNames.Identifier(name)
        && Parameters.Select(p => p.Type).SequenceEqual(parameters.Select(p => p.Type))
        && Returns.Select(r => r.Type).SequenceEqual(returns.Select(r => r.Type));
}

public static class GraphKindExtensions
{
    public static string Label(this GraphKind kind) => kind switch
    {
        GraphKind.Events => Tr.T("Event graph"),
        GraphKind.Function => Tr.T("Function"),
        GraphKind.Procedure => Tr.T("Procedure"),
        GraphKind.Class => Tr.T("Class"),
        GraphKind.Struct => Tr.T("Struct"),
        GraphKind.Interface => Tr.T("Interface"),
        GraphKind.Library => Tr.T("Function library"),
        _ => Tr.T("Enumeration"),
    };

    /// <summary>Le graphe a un canevas de nœuds (corps d'une fonction, constructeur d'une classe, événements).</summary>
    public static bool HasBody(this GraphKind kind) => kind is GraphKind.Events or GraphKind.Function or GraphKind.Procedure or GraphKind.Class;

    /// <summary>Fonction ou procédure : s'appelle depuis un autre graphe.</summary>
    public static bool IsCallable(this GraphKind kind) => kind is GraphKind.Function or GraphKind.Procedure;

    /// <summary>Définit un type utilisable pour une variable ou un paramètre.</summary>
    public static bool IsType(this GraphKind kind) => kind is GraphKind.Class or GraphKind.Struct or GraphKind.Interface or GraphKind.Enum;

    public static string Label(this AccessLevel access) => access switch
    {
        AccessLevel.Public => Tr.T("Public"),
        AccessLevel.Protected => Tr.T("Protected"),
        _ => Tr.T("Private"),
    };
}
