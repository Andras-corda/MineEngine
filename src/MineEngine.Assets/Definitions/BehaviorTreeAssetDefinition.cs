using System.Collections.Immutable;
using System.Text.Json.Nodes;
using MineEngine.Assets.Puppeteer;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>
/// Reading and writing of a Puppeteer behavior tree: blackboard keys, nodes (with their
/// decorators and services) and parent → child links. A node, decorator or service of an
/// unknown kind (newer file) is skipped.
/// </summary>
public sealed class BehaviorTreeAssetDefinition : AssetDefinition<BehaviorTreeAsset>
{
    public override AssetType Type => AssetType.BehaviorTree;

    public override string Label => Tr.T("Behavior tree");

    public override string DefaultResourceIdBase => "new_ai";

    public override string DefaultDisplayName => Tr.T("New behavior tree");

    /// <summary>A new tree starts from an example (spot, attack, otherwise wander); reading replaces it.</summary>
    protected override BehaviorTreeAsset Create(Guid id, ResourceId resourceId, string displayName)
    {
        var asset = new BehaviorTreeAsset(id, resourceId, displayName);
        asset.ApplyStarterTree();
        return asset;
    }

    protected override void ReadProperties(BehaviorTreeAsset asset, JsonPropertyReader reader)
    {
        asset.IsPublicGoal = reader.GetBoolean("publicGoal", false);
        asset.Keys = [.. reader.GetObjectList("keys")
            .Select(k => (Id: k.GetGuid("id"), Name: k.GetString("name"), Type: k.GetEnum("type", BlackboardKeyType.Entity)))
            .Where(k => k.Id is not null && k.Name is not null)
            .Select(k => new BlackboardKey(k.Id!.Value, k.Name!, k.Type))];

        var nodes = new List<BtNode>();
        foreach (JsonPropertyReader node in reader.GetObjectList("nodes"))
        {
            if (node.GetGuid("id") is not { } id || ReadKind(node) is not { } kind || BtCatalog.Get(kind).IsAttachment)
            {
                continue;
            }

            nodes.Add(new BtNode(id, kind, node.GetDouble("x", 0), node.GetDouble("y", 0), node.GetStringMap("values").ToImmutableDictionary())
            {
                Decorators = ReadAttachments(node, "decorators", BtCategory.Decorator),
                Services = ReadAttachments(node, "services", BtCategory.Service),
            });
        }

        IEnumerable<BtLink> links = reader.GetObjectList("links")
            .Select(l => (Parent: l.GetGuid("parent"), Child: l.GetGuid("child")))
            .Where(l => l.Parent is not null && l.Child is not null)
            .Select(l => new BtLink(l.Parent!.Value, l.Child!.Value));

        asset.Tree = new BehaviorTree(nodes, links);
    }

    protected override void WriteProperties(BehaviorTreeAsset asset, JsonObject properties)
    {
        if (asset.IsPublicGoal)
        {
            properties["publicGoal"] = true;
        }

        properties["keys"] = new JsonArray([.. asset.Keys.Select(k => (JsonNode)new JsonObject
        {
            ["id"] = k.Id.ToString("D"),
            ["name"] = k.Name,
            ["type"] = k.Type.ToString(),
        })]);
        properties["nodes"] = new JsonArray([.. asset.Tree.Nodes.Select(WriteNode)]);
        properties["links"] = new JsonArray([.. asset.Tree.Links
            .OrderBy(l => l.Parent).ThenBy(l => l.Child)
            .Select(l => (JsonNode)new JsonObject { ["parent"] = l.Parent.ToString("D"), ["child"] = l.Child.ToString("D") })]);
    }

    private static BtNodeKind? ReadKind(JsonPropertyReader reader) =>
        Enum.TryParse(reader.GetString("kind"), ignoreCase: false, out BtNodeKind kind) && Enum.IsDefined(kind) ? kind : null;

    private static ImmutableList<BtAttachment> ReadAttachments(JsonPropertyReader node, string name, BtCategory category) =>
        [.. node.GetObjectList(name)
            .Select(a => (Id: a.GetGuid("id"), Kind: ReadKind(a), Values: a.GetStringMap("values")))
            .Where(a => a.Id is not null && a.Kind is { } kind && BtCatalog.Get(kind).Category == category)
            .Select(a => new BtAttachment(a.Id!.Value, a.Kind!.Value, a.Values.ToImmutableDictionary()))];

    private static JsonNode WriteNode(BtNode node)
    {
        var json = new JsonObject
        {
            ["id"] = node.Id.ToString("D"),
            ["kind"] = node.Kind.ToString(),
            ["x"] = Math.Round(node.X, 1, MidpointRounding.AwayFromZero),
            ["y"] = Math.Round(node.Y, 1, MidpointRounding.AwayFromZero),
            ["values"] = WriteValues(node.Values),
        };
        if (node.Decorators.Count > 0)
        {
            json["decorators"] = new JsonArray([.. node.Decorators.Select(WriteAttachment)]);
        }

        if (node.Services.Count > 0)
        {
            json["services"] = new JsonArray([.. node.Services.Select(WriteAttachment)]);
        }

        return json;
    }

    private static JsonNode WriteAttachment(BtAttachment attachment) => new JsonObject
    {
        ["id"] = attachment.Id.ToString("D"),
        ["kind"] = attachment.Kind.ToString(),
        ["values"] = WriteValues(attachment.Values),
    };

    private static JsonObject WriteValues(ImmutableDictionary<string, string> values)
    {
        var json = new JsonObject();
        foreach ((string name, string value) in values.OrderBy(v => v.Key, StringComparer.Ordinal))
        {
            json[name] = value;
        }

        return json;
    }
}
