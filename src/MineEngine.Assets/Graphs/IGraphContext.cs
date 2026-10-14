namespace MineEngine.Assets.Graphs;

/// <summary>
/// Ce qu'il faut savoir d'un graphe pour décrire et vérifier ses nœuds : les broches de
/// chaque nœud (celles d'un appel dépendent de la fonction appelée), la compatibilité des
/// types (une classe alimente une entrée de sa classe parente) et leur nom.
/// </summary>
public interface IGraphContext
{
    /// <summary>Ce que définit le graphe (événements, fonction, classe...).</summary>
    GraphKind Kind { get; }

    NodeInfo Describe(GraphNode node);

    /// <summary>Vrai si une valeur de type <paramref name="from"/> peut alimenter une entrée de type <paramref name="to"/>.</summary>
    bool CanFeed(DataType from, DataType to);

    string TypeLabel(DataType type);
}

/// <summary>
/// Contexte d'un graphe d'événements sans définitions du projet : seuls les nœuds du
/// catalogue sont connus. Sert aux tests et au code qui ne connaît pas le projet.
/// </summary>
public sealed class CatalogGraphContext : IGraphContext
{
    public static CatalogGraphContext Instance { get; } = new();

    private CatalogGraphContext()
    {
    }

    public GraphKind Kind => GraphKind.Events;

    public NodeInfo Describe(GraphNode node) => NodeCatalog.Find(node.Kind) ?? NodeCatalog.Missing(node.Kind, "Définition introuvable");

    public bool CanFeed(DataType from, DataType to) => TypeRules.CanFeedBuiltIn(from, to);

    public string TypeLabel(DataType type) => type.BuiltInLabel;
}

/// <summary>Règles de compatibilité communes à tous les contextes.</summary>
public static class TypeRules
{
    /// <summary>Même type, ou nombre vers texte (converti).</summary>
    public static bool CanFeedBuiltIn(DataType from, DataType to) =>
        from == to || (from.Kind == PinType.Number && to.Kind == PinType.Text);
}
