using System.Collections.Immutable;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Un nœud placé dans un graphe : sa sorte, sa position sur le canevas et les valeurs
/// saisies sur ses entrées non reliées (texte brut : "3", "true", "minecraft:diamond",
/// "asset:guid"). Les nœuds de programmation désignent en plus une définition du projet
/// (<see cref="Target"/> : fonction, classe...) et un de ses membres (<see cref="Member"/> :
/// champ, variable, méthode d'interface). Ses broches sont décrites par un
/// <see cref="IGraphContext"/>. Immuable : chaque modification produit un nouveau nœud.
/// </summary>
public sealed record GraphNode(Guid Id, GraphNodeKind Kind, double X, double Y, ImmutableDictionary<string, string> Values)
{
    /// <summary>Définition visée (fonction appelée, classe créée...), ou null.</summary>
    public Guid? Target { get; init; }

    /// <summary>Membre visé (champ, variable, méthode d'interface), ou null.</summary>
    public Guid? Member { get; init; }

    /// <summary>Nouveau nœud avec les valeurs par défaut des entrées décrites par <paramref name="info"/>.</summary>
    public static GraphNode Create(NodeInfo info, double x, double y, Guid? target = null, Guid? member = null)
    {
        ArgumentNullException.ThrowIfNull(info);
        ImmutableDictionary<string, string> values = info.Inputs
            .Where(p => p.DefaultValue is not null)
            .ToImmutableDictionary(p => p.Name, p => p.DefaultValue!);
        return new GraphNode(Guid.NewGuid(), info.Kind, x, y, values) { Target = target, Member = member };
    }

    /// <summary>Nouveau nœud du catalogue (événement, action, valeur...).</summary>
    public static GraphNode Create(GraphNodeKind kind, double x, double y) => Create(NodeCatalog.Get(kind), x, y);

    /// <summary>Valeur saisie sur une entrée, sinon la valeur par défaut du catalogue.</summary>
    public string? GetValue(string pin) =>
        Values.TryGetValue(pin, out string? value) ? value : NodeCatalog.Find(Kind)?.FindInput(pin)?.DefaultValue;

    public GraphNode WithValue(string pin, string value) => this with { Values = Values.SetItem(pin, value ?? string.Empty) };

    public GraphNode MoveTo(double x, double y) => this with { X = x, Y = y };

    public override string ToString() => NodeCatalog.Find(Kind)?.Title ?? Kind.ToString();
}

/// <summary>
/// Fil entre la sortie <paramref name="FromPin"/> d'un nœud et l'entrée <paramref name="ToPin"/>
/// d'un autre. Un fil d'exécution relie une sortie "then"/"true"/"false" à l'entrée "exec".
/// </summary>
public sealed record GraphConnection(Guid FromNode, string FromPin, Guid ToNode, string ToPin);
