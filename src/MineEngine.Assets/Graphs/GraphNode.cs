using System.Collections.Immutable;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Un nœud placé dans un graphe : sa sorte, sa position sur le canevas et les valeurs
/// saisies sur ses entrées non reliées (texte brut : "3", "true", "minecraft:diamond",
/// "asset:guid"). Immuable : chaque modification produit un nouveau nœud.
/// </summary>
public sealed record GraphNode(Guid Id, GraphNodeKind Kind, double X, double Y, ImmutableDictionary<string, string> Values)
{
    public NodeInfo Info => NodeCatalog.Get(Kind);

    /// <summary>Nouveau nœud avec les valeurs par défaut de ses entrées.</summary>
    public static GraphNode Create(GraphNodeKind kind, double x, double y)
    {
        ImmutableDictionary<string, string> values = NodeCatalog.Get(kind).Inputs
            .Where(p => p.DefaultValue is not null)
            .ToImmutableDictionary(p => p.Name, p => p.DefaultValue!);
        return new GraphNode(Guid.NewGuid(), kind, x, y, values);
    }

    /// <summary>Valeur saisie sur une entrée, sinon sa valeur par défaut.</summary>
    public string? GetValue(string pin) =>
        Values.TryGetValue(pin, out string? value) ? value : Info.FindInput(pin)?.DefaultValue;

    public GraphNode WithValue(string pin, string value) => this with { Values = Values.SetItem(pin, value ?? string.Empty) };

    public GraphNode MoveTo(double x, double y) => this with { X = x, Y = y };

    public override string ToString() => Info.Title;
}

/// <summary>
/// Fil entre la sortie <paramref name="FromPin"/> d'un nœud et l'entrée <paramref name="ToPin"/>
/// d'un autre. Un fil d'exécution relie une sortie "then"/"true"/"false" à l'entrée "exec".
/// </summary>
public sealed record GraphConnection(Guid FromNode, string FromPin, Guid ToNode, string ToPin);
