using System.Collections.Immutable;
using System.Text.Json.Nodes;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>
/// Lecture et écriture d'un graphe : ce qu'il définit (événements, fonction, classe...),
/// sa signature, ses variables, son héritage, puis ses nœuds et ses fils. Les propriétés
/// absentes (graphes des versions précédentes) donnent un graphe d'événements.
/// </summary>
public sealed class GraphAssetDefinition : AssetDefinition<GraphAsset>
{
    public override AssetType Type => AssetType.Graph;

    public override string Label => Tr.T("Graphe");

    public override string DefaultResourceIdBase => "new_graph";

    public override string DefaultDisplayName => Tr.T("Nouveau graphe");

    protected override GraphAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    /// <summary>Un nœud de sorte inconnue (fichier plus récent) est ignoré, avec ses fils.</summary>
    protected override void ReadProperties(GraphAsset asset, JsonPropertyReader reader)
    {
        asset.Kind = reader.GetEnum("kind", GraphKind.Events);
        asset.Access = reader.GetEnum("access", AccessLevel.Public);
        asset.OwnerId = reader.GetGuid("owner");
        asset.IsStatic = reader.GetBoolean("static", false);
        asset.IsPure = reader.GetBoolean("pure", false);
        asset.Parameters = ReadVariables(reader, "parameters");
        asset.Returns = ReadVariables(reader, "returns");
        asset.Variables = ReadVariables(reader, "variables");
        asset.BaseClassId = reader.GetGuid("baseClass");
        asset.InterfaceIds = [.. reader.GetStringList("interfaces").Select(t => Guid.TryParse(t, out Guid id) ? id : Guid.Empty).Where(id => id != Guid.Empty)];
        asset.Constants = [.. reader.GetObjectList("constants")
            .Select(c => (Id: c.GetGuid("id"), Name: c.GetString("name")))
            .Where(c => c.Id is not null && c.Name is not null)
            .Select(c => new EnumConstant(c.Id!.Value, c.Name!))];
        asset.Methods = [.. reader.GetObjectList("methods")
            .Select(m => (Id: m.GetGuid("id"), Name: m.GetString("name"), Reader: m))
            .Where(m => m.Id is not null && m.Name is not null)
            .Select(m => new MethodSignature(m.Id!.Value, m.Name!, ReadVariables(m.Reader, "parameters"), ReadVariables(m.Reader, "returns")))];

        var nodes = new List<GraphNode>();
        foreach (JsonPropertyReader node in reader.GetObjectList("nodes"))
        {
            if (node.GetGuid("id") is not { } id
                || !Enum.TryParse(node.GetString("kind"), ignoreCase: false, out GraphNodeKind kind)
                || !Enum.IsDefined(kind))
            {
                continue;
            }

            ImmutableDictionary<string, string> values = node.GetStringMap("values").ToImmutableDictionary();
            nodes.Add(new GraphNode(id, kind, node.GetDouble("x", 0), node.GetDouble("y", 0), values)
            {
                Target = node.GetGuid("target"),
                Member = node.GetGuid("member"),
            });
        }

        var connections = reader.GetObjectList("connections")
            .Select(c => (From: c.GetGuid("from"), FromPin: c.GetString("fromPin"), To: c.GetGuid("to"), ToPin: c.GetString("toPin")))
            .Where(c => c.From is not null && c.To is not null && c.FromPin is not null && c.ToPin is not null)
            .Select(c => new GraphConnection(c.From!.Value, c.FromPin!, c.To!.Value, c.ToPin!));

        asset.Graph = new NodeGraph(nodes, connections);
    }

    protected override void WriteProperties(GraphAsset asset, JsonObject properties)
    {
        // Un graphe d'événements s'écrit comme avant la V0.4.3 : seules ses variables s'ajoutent.
        if (asset.Kind != GraphKind.Events)
        {
            properties["kind"] = asset.Kind.ToString();
            properties["access"] = asset.Access.ToString();
        }

        if (asset.OwnerId is { } owner)
        {
            properties["owner"] = owner.ToString("D");
        }

        if (asset.IsStatic)
        {
            properties["static"] = true;
        }

        if (asset.IsPure)
        {
            properties["pure"] = true;
        }

        WriteVariables(properties, "parameters", asset.Parameters);
        WriteVariables(properties, "returns", asset.Returns);
        WriteVariables(properties, "variables", asset.Variables);
        if (asset.BaseClassId is { } baseClass)
        {
            properties["baseClass"] = baseClass.ToString("D");
        }

        if (asset.InterfaceIds.Count > 0)
        {
            properties["interfaces"] = new JsonArray([.. asset.InterfaceIds.Select(i => (JsonNode?)JsonValue.Create(i.ToString("D")))]);
        }

        if (asset.Constants.Count > 0)
        {
            properties["constants"] = new JsonArray([.. asset.Constants.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id.ToString("D"),
                ["name"] = c.Name,
            })]);
        }

        if (asset.Methods.Count > 0)
        {
            properties["methods"] = new JsonArray([.. asset.Methods.Select(m =>
            {
                var method = new JsonObject { ["id"] = m.Id.ToString("D"), ["name"] = m.Name };
                WriteVariables(method, "parameters", m.Parameters);
                WriteVariables(method, "returns", m.Returns);
                return (JsonNode)method;
            })]);
        }

        properties["nodes"] = new JsonArray([.. asset.Graph.Nodes.Select(WriteNode)]);
        properties["connections"] = new JsonArray([.. asset.Graph.Connections.Select(c => (JsonNode)new JsonObject
        {
            ["from"] = c.FromNode.ToString("D"),
            ["fromPin"] = c.FromPin,
            ["to"] = c.ToNode.ToString("D"),
            ["toPin"] = c.ToPin,
        })]);
    }

    private static IReadOnlyList<VariableDefinition> ReadVariables(JsonPropertyReader reader, string name) =>
        [.. reader.GetObjectList(name)
            .Select(v => (Id: v.GetGuid("id"), Name: v.GetString("name"), Type: DataType.Parse(v.GetString("type")), Reader: v))
            .Where(v => v.Id is not null && v.Name is not null && v.Type is not null)
            .Select(v => new VariableDefinition(
                v.Id!.Value,
                v.Name!,
                v.Type!,
                v.Reader.GetString("default") ?? string.Empty,
                v.Reader.GetEnum("access", AccessLevel.Public),
                v.Reader.GetBoolean("global", false)))];

    private static void WriteVariables(JsonObject target, string name, IReadOnlyList<VariableDefinition> variables)
    {
        if (variables.Count == 0)
        {
            return;
        }

        target[name] = new JsonArray([.. variables.Select(v =>
        {
            var variable = new JsonObject
            {
                ["id"] = v.Id.ToString("D"),
                ["name"] = v.Name,
                ["type"] = v.Type.ToStorageText(),
            };
            if (v.DefaultValue.Length > 0)
            {
                variable["default"] = v.DefaultValue;
            }

            if (v.Access != AccessLevel.Public)
            {
                variable["access"] = v.Access.ToString();
            }

            if (v.IsGlobal)
            {
                variable["global"] = true;
            }

            return (JsonNode)variable;
        })]);
    }

    private static JsonNode WriteNode(GraphNode node)
    {
        var values = new JsonObject();
        foreach ((string pin, string value) in node.Values.OrderBy(v => v.Key, StringComparer.Ordinal))
        {
            values[pin] = value;
        }

        var json = new JsonObject
        {
            ["id"] = node.Id.ToString("D"),
            ["kind"] = node.Kind.ToString(),
            ["x"] = Math.Round(node.X, 1, MidpointRounding.AwayFromZero),
            ["y"] = Math.Round(node.Y, 1, MidpointRounding.AwayFromZero),
        };
        if (node.Target is { } target)
        {
            json["target"] = target.ToString("D");
        }

        if (node.Member is { } member)
        {
            json["member"] = member.ToString("D");
        }

        json["values"] = values;
        return json;
    }
}
