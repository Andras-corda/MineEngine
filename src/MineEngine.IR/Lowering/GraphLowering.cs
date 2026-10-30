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
            context.ResolveItem(asset, reference, Tr.F("{0} node", type.BuiltInLabel.ToLowerInvariant())));
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
                case GraphKind.Library:
                    // A Java class that only holds the static methods of its functions.
                    context.AddDefinition(new IRClass(asset.Id, asset.ResourceId, asset.DisplayName, AccessLevel.Public, null, IRBody.Empty));
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
                context.Diagnostics.Error(Tr.F("\"{0}\": only an item (not a block) can have item events.", node), asset.ToString(), asset.Id);
                return;
            }
        }

        // A mob event adds code to the class of the mob: it must name a mob of the project (V0.7.9).
        foreach (GraphNode node in asset.Graph.Nodes.Where(n => NodeCatalog.Find(n.Kind)?.MobEvent is not null))
        {
            string name = node.GetValue("mob")?.Trim() ?? NodeCatalog.Get(node.Kind).Inputs[0].DefaultValue ?? string.Empty;
            name = name[(name.IndexOf(':', StringComparison.Ordinal) + 1)..];
            if (!context.Project.Assets.Any(a => a.Type == AssetType.Mob && a.ResourceId.Value == name))
            {
                context.Diagnostics.Error(Tr.F("\"{0}\": \"{1}\" is not a mob of the project.", NodeCatalog.Get(node.Kind).Title, name), asset.ToString(), asset.Id);
                return;
            }
        }

        // A block event adds code to the class of the block: it must name a block of the project (V0.7.11).
        foreach (GraphNode node in asset.Graph.Nodes.Where(n => NodeCatalog.Find(n.Kind)?.BlockEvent is not null))
        {
            string name = node.GetValue("block")?.Trim() ?? NodeCatalog.Get(node.Kind).Inputs[0].DefaultValue ?? string.Empty;
            name = name[(name.IndexOf(':', StringComparison.Ordinal) + 1)..];
            if (context.Project.Assets.FirstOrDefault(a => a.Type == AssetType.Block && a.ResourceId.Value == name) is not BlockAsset block)
            {
                context.Diagnostics.Error(Tr.F("\"{0}\": \"{1}\" is not a block of the project.", NodeCatalog.Get(node.Kind).Title, name), asset.ToString(), asset.Id);
                return;
            }

            if (node.Kind == GraphNodeKind.OnBlockTick && !block.Options.TickEnabled)
            {
                context.Diagnostics.Warning(Tr.F("\"{0}\": the block \"{1}\" does not tick (Ticks, in Block properties): the event never runs.", NodeCatalog.Get(node.Kind).Title, name), asset.ToString(), asset.Id);
            }
        }

        // An "On command" or "On key pressed" event names a command or a keybind of the mod.
        foreach (GraphNode node in asset.Graph.Nodes.Where(n => NodeCatalog.Find(n.Kind)?.DirectEvent is not null))
        {
            NodeInfo info = NodeCatalog.Get(node.Kind);
            string name = node.GetValue(info.Inputs[0].Name)?.Trim() ?? string.Empty;
            AssetType type = info.DirectEvent == Core.GameData.DirectEventKind.Command ? AssetType.Command : AssetType.Keybind;
            if (!context.Project.Assets.Any(a => a.Type == type && a.ResourceId.Value == name))
            {
                string[] known = [.. context.Project.Assets.Where(a => a.Type == type).Select(a => a.ResourceId.Value).Order(StringComparer.Ordinal)];
                context.Diagnostics.Error(
                    known.Length == 0
                        ? Tr.F("\"{0}\": the project has no {1} yet: add one, then type its identifier.", info.Title, type == AssetType.Command ? Tr.T("command") : Tr.T("keybind"))
                        : Tr.F("\"{0}\": \"{1}\" is not an identifier of the project ({2}).", info.Title, name, string.Join(", ", known)),
                    asset.ToString(),
                    asset.Id);
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
