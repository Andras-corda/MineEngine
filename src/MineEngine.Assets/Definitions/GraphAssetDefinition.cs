using System.Collections.Immutable;
using System.Text.Json.Nodes;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

public sealed class GraphAssetDefinition : AssetDefinition<GraphAsset>
{
    public override AssetType Type => AssetType.Graph;

    public override string Label => "Graphe";

    public override string DefaultResourceIdBase => "new_graph";

    public override string DefaultDisplayName => "Nouveau graphe";

    protected override GraphAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    /// <summary>Un nœud de sorte inconnue (fichier plus récent) est ignoré, avec ses fils.</summary>
    protected override void ReadProperties(GraphAsset asset, JsonPropertyReader reader)
    {
        var nodes = new List<GraphNode>();
        foreach (JsonPropertyReader node in reader.GetObjectList("nodes"))
        {
            if (node.GetGuid("id") is not { } id
                || !Enum.TryParse(node.GetString("kind"), ignoreCase: false, out GraphNodeKind kind)
                || !Enum.IsDefined(kind))
            {
                continue;
            }

            ImmutableDictionary<string, string> values = node.GetObject("values") is { } valueReader
                ? NodeCatalog.Get(kind).Inputs
                    .Select(p => (p.Name, Value: valueReader.GetString(p.Name)))
                    .Where(v => v.Value is not null)
                    .ToImmutableDictionary(v => v.Name, v => v.Value!)
                : ImmutableDictionary<string, string>.Empty;
            nodes.Add(new GraphNode(id, kind, node.GetDouble("x", 0), node.GetDouble("y", 0), values));
        }

        var connections = reader.GetObjectList("connections")
            .Select(c => (From: c.GetGuid("from"), FromPin: c.GetString("fromPin"), To: c.GetGuid("to"), ToPin: c.GetString("toPin")))
            .Where(c => c.From is not null && c.To is not null && c.FromPin is not null && c.ToPin is not null)
            .Select(c => new GraphConnection(c.From!.Value, c.FromPin!, c.To!.Value, c.ToPin!));

        asset.Graph = new NodeGraph(nodes, connections);
    }

    protected override void WriteProperties(GraphAsset asset, JsonObject properties)
    {
        properties["nodes"] = new JsonArray([.. asset.Graph.Nodes.Select(WriteNode)]);
        properties["connections"] = new JsonArray([.. asset.Graph.Connections.Select(c => (JsonNode)new JsonObject
        {
            ["from"] = c.FromNode.ToString("D"),
            ["fromPin"] = c.FromPin,
            ["to"] = c.ToNode.ToString("D"),
            ["toPin"] = c.ToPin,
        })]);
    }

    private static JsonNode WriteNode(GraphNode node)
    {
        var values = new JsonObject();
        foreach (PinInfo pin in node.Info.Inputs)
        {
            if (node.Values.TryGetValue(pin.Name, out string? value))
            {
                values[pin.Name] = value;
            }
        }

        return new JsonObject
        {
            ["id"] = node.Id.ToString("D"),
            ["kind"] = node.Kind.ToString(),
            ["x"] = Math.Round(node.X, 1, MidpointRounding.AwayFromZero),
            ["y"] = Math.Round(node.Y, 1, MidpointRounding.AwayFromZero),
            ["values"] = values,
        };
    }
}
