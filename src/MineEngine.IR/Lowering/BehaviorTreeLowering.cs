using MineEngine.Assets;
using MineEngine.Assets.Puppeteer;
using MineEngine.Core.Assets;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

/// <summary>
/// Translates a Puppeteer tree: only the part connected to the root is kept, the children
/// are sorted in execution order (from left to right) and the settings resolved.
/// </summary>
public sealed class BehaviorTreeLowering : AssetLowering<BehaviorTreeAsset>
{
    public override AssetType AssetType => AssetType.BehaviorTree;

    protected override void Lower(BehaviorTreeAsset asset, LoweringContext context)
    {
        Dictionary<Guid, BlackboardKey> keys = asset.Keys.DistinctBy(k => k.Id).ToDictionary(k => k.Id);
        BehaviorTree tree = asset.Tree;
        IRBtNode? root = tree.ChildrenOf(tree.Root.Id).FirstOrDefault() is { } first ? LowerNode(first) : null;
        context.AddBehaviorTree(new IRBehaviorTree(
            asset.Id,
            asset.ResourceId,
            asset.DisplayName,
            [.. asset.Keys.Select(k => new IRBlackboardKey(k.Name.Trim(), k.Type))],
            root));

        IRBtNode LowerNode(BtNode node) => new(node.Id, node.Kind, Resolve(node.Info, node.GetValue))
        {
            Children = [.. tree.ChildrenOf(node.Id).Select(LowerNode)],
            Decorators = [.. node.Decorators.Select(LowerAttachment)],
            Services = [.. node.Services.Select(LowerAttachment)],
        };

        IRBtNode LowerAttachment(BtAttachment attachment) => new(attachment.Id, attachment.Kind, Resolve(attachment.Info, attachment.GetValue));

        Dictionary<string, string> Resolve(BtNodeInfo info, Func<string, string> value)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (BtParameterInfo parameter in info.Parameters)
            {
                string text = value(parameter.Name);
                string? resolved = parameter.Type switch
                {
                    BtParameterType.Key => Guid.TryParse(text, out Guid id) && keys.TryGetValue(id, out BlackboardKey? key) ? key.Name.Trim() : null,
                    BtParameterType.Goal => Guid.TryParse(text, out Guid goal) && context.Project.Assets.Find(goal) is BehaviorTreeAsset tree
                        ? tree.ResourceId.Value
                        : null,
                    BtParameterType.Sound => ContentReference.TryParseStorageText(text, out ContentReference? sound)
                        ? context.ResolveSound(asset, sound, Tr.F("Node \"{0}\"", info.Title))?.ToString()
                        : null,
                    _ => text,
                };
                if (resolved is not null)
                {
                    values[parameter.Name] = resolved;
                }
            }

            return values;
        }
    }
}
