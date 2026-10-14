using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

/// <summary>
/// Traduit un graphe : un graphe d'événements devient un comportement, une fonction, une
/// procédure, une classe, une structure, une interface ou une énumération devient une
/// définition de programme (voir <see cref="GraphCompiler"/> pour les corps).
/// </summary>
public sealed class GraphLowering : AssetLowering<GraphAsset>
{
    public override AssetType AssetType => AssetType.Graph;

    protected override void Lower(GraphAsset asset, LoweringContext context)
    {
        var compiler = new GraphCompiler(asset, context.Program, (reference, type) =>
            context.ResolveItem(asset, reference, $"Nœud {type.BuiltInLabel.ToLowerInvariant()}"));
        try
        {
            switch (asset.Kind)
            {
                case GraphKind.Events:
                    LowerEvents(asset, context, compiler);
                    break;
                case GraphKind.Function or GraphKind.Procedure:
                    context.AddDefinition(new IRFunction(asset.Id, asset.ResourceId, asset.DisplayName, asset.Access, asset.OwnerId, compiler.CompileBody())
                    {
                        IsProcedure = asset.Kind == GraphKind.Procedure,
                        IsStatic = asset.IsStatic,
                        IsPure = asset.IsPure,
                        Parameters = asset.Parameters,
                        Returns = asset.Kind == GraphKind.Function ? asset.Returns : [],
                        Locals = [.. asset.Variables.Where(v => !v.IsGlobal).Select(v => new IRVariable(v, compiler.Initial(v)))],
                        Globals = [.. asset.Variables.Where(v => v.IsGlobal).Select(v => new IRVariable(v, compiler.Initial(v)))],
                    });
                    break;
                case GraphKind.Class:
                    context.AddDefinition(new IRClass(asset.Id, asset.ResourceId, asset.DisplayName, asset.Access, asset.OwnerId, compiler.CompileBody())
                    {
                        BaseClassId = asset.BaseClassId,
                        InterfaceIds = [.. asset.InterfaceIds.Distinct()],
                        Fields = [.. asset.Variables.Select(v => new IRVariable(v, compiler.Initial(v)))],
                        ConstructorParameters = asset.Parameters,
                    });
                    break;
                case GraphKind.Struct:
                    context.AddDefinition(new IRStruct(asset.Id, asset.ResourceId, asset.DisplayName, asset.Access, asset.OwnerId,
                        [.. asset.Variables.Select(v => new IRVariable(v, compiler.Initial(v)))]));
                    break;
                case GraphKind.Interface:
                    context.AddDefinition(new IRInterface(asset.Id, asset.ResourceId, asset.DisplayName, asset.Access, asset.OwnerId, asset.Methods));
                    break;
                case GraphKind.Enum:
                    context.AddDefinition(new IREnum(asset.Id, asset.ResourceId, asset.DisplayName, asset.Access, asset.OwnerId, asset.Constants));
                    break;
            }
        }
        catch (InvalidOperationException exception)
        {
            context.Diagnostics.Error(exception.Message, asset.ToString(), asset.Id);
        }
    }

    private static void LowerEvents(GraphAsset asset, LoweringContext context, GraphCompiler compiler)
    {
        // Un événement d'item ajoute du code à la classe de l'item : il faut un item (pas un bloc).
        foreach (GraphNode node in asset.Graph.Nodes.Where(n => NodeCatalog.Find(n.Kind)?.ItemEvent is not null))
        {
            if (ContentReference.TryParseStorageText(node.GetValue("item"), out ContentReference? reference)
                && reference!.Kind == ContentReferenceKind.Asset
                && context.Project.Assets.Find(reference.AssetId) is not null and not ItemAsset)
            {
                context.Diagnostics.Error($"« {node} » : seul un item (pas un bloc) peut avoir des événements d'item.", asset.ToString(), asset.Id);
                return;
            }
        }

        IReadOnlyList<IREventHandler> handlers = compiler.CompileEvents();
        if (handlers.Count > 0)
        {
            context.AddBehavior(new IRBehavior(asset.Id, asset.ResourceId, asset.DisplayName, handlers)
            {
                Variables = [.. asset.Variables.Select(v => new IRVariable(v, compiler.Initial(v)))],
            });
        }
    }
}
