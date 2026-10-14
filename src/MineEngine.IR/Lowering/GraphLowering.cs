using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

/// <summary>Traduit un graphe nodal en comportement (voir <see cref="GraphCompiler"/>).</summary>
public sealed class GraphLowering : AssetLowering<GraphAsset>
{
    public override AssetType AssetType => AssetType.Graph;

    protected override void Lower(GraphAsset asset, LoweringContext context)
    {
        var compiler = new GraphCompiler(asset.Graph, (reference, type) =>
            context.ResolveItem(asset, reference, $"Nœud {type.Label().ToLowerInvariant()}"));
        try
        {
            // Un événement d'item ajoute du code à la classe de l'item : il faut un item (pas un bloc).
            foreach (GraphNode node in asset.Graph.Nodes.Where(n => n.Info.ItemEvent is not null))
            {
                if (ContentReference.TryParseStorageText(node.GetValue("item"), out ContentReference? reference)
                    && reference!.Kind == ContentReferenceKind.Asset
                    && context.Project.Assets.Find(reference.AssetId) is not null and not ItemAsset)
                {
                    context.Diagnostics.Error($"« {node.Info.Title} » : seul un item (pas un bloc) peut avoir des événements d'item.", asset.ToString(), asset.Id);
                    return;
                }
            }

            IReadOnlyList<IREventHandler> handlers = compiler.Compile();
            if (handlers.Count > 0)
            {
                context.AddBehavior(new IRBehavior(asset.Id, asset.ResourceId, asset.DisplayName, handlers));
            }
        }
        catch (InvalidOperationException exception)
        {
            context.Diagnostics.Error(exception.Message, asset.ToString(), asset.Id);
        }
    }
}
