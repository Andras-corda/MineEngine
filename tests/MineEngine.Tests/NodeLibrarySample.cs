using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Graph that uses every block and item node of V0.6.2 and every node of V0.6.5, so that the
/// generated Java of each one is compiled by the end-to-end builds. Built from the catalog: a node
/// added to these groups is covered without changing the sample. Players, positions and item stacks
/// come from the event and from the item in the main hand; each value is consumed by an action of
/// its type. The nodes after a loop go in its body; each event of V0.6.5 gets its own chain.
/// </summary>
internal static class NodeLibrarySample
{
    public static readonly ResourceId Id = ResourceId.Parse("node_library");

    public static GraphAsset AddTo(ModProject project)
    {
        var graph = new GraphAsset(Guid.NewGuid(), Id, "Node library") { Graph = BuildGraph() };
        project.Assets.Add(graph);
        return graph;
    }

    public static NodeGraph BuildGraph()
    {
        var builder = new Builder();
        foreach (NodeInfo info in Nodes)
        {
            builder.Use(info);
        }

        // An empty branch is left out of the code: the last condition is given an action to run.
        builder.Use(NodeCatalog.Get(GraphNodeKind.UpdateBlock));
        foreach (NodeInfo info in Events)
        {
            builder.UseEvent(info);
        }

        return builder.Build();
    }

    /// <summary>Block and item nodes of the catalog, and the nodes of V0.6.5 that are not events.</summary>
    public static IReadOnlyList<NodeInfo> Nodes =>
    [
        .. NodeCatalog.All
            .Where(n => NodeCatalog.GroupOf(n) is NodeGroup.Block or NodeGroup.Item || IsV065(n) && n.Category != NodeCategory.Event
                        || n.Flow == NodeFlow.Multi || n.Kind is >= GraphNodeKind.RunCommandAsPlayer and <= GraphNodeKind.CreatureAppearance)
            .Where(n => n.Kind < GraphNodeKind.GetGlobal) // V0.6.7 nodes: see GlobalsSample.
            .OrderBy(n => n.Kind),
    ];

    /// <summary>Events of V0.6.5.</summary>
    public static IReadOnlyList<NodeInfo> Events => [.. NodeCatalog.All.Where(n => IsV065(n) && n.Category == NodeCategory.Event)];

    private static bool IsV065(NodeInfo info) => info.Kind is >= GraphNodeKind.And and <= GraphNodeKind.OnEntityDeath;

    private sealed class Builder
    {
        private readonly GraphBuilder _graph = new();
        private readonly GraphNode _event;
        private readonly GraphNode _hand;
        private GraphNode _last;
        private string _lastPin = NodeInfo.ExecThen;
        private double _x;

        public Builder()
        {
            _event = _graph.Add(GraphNodeKind.OnRightClickBlock, 0, 0);
            _hand = _graph.Add(GraphNodeKind.MainHandItem, 0, 300);
            _graph.Data(_event, "player", _hand, "player");
            _last = _event;
        }

        public NodeGraph Build() => _graph.Build();

        /// <summary>An event of its own, whose values are given to actions: the player is healed, the creature spawned.</summary>
        public void UseEvent(NodeInfo info)
        {
            _x += 300;
            GraphNode start = _graph.Add(info.Kind, _x, 600);
            GraphNode action = info.Outputs.Any(o => o.Type.Kind == PinType.Player)
                ? _graph.Add(GraphNodeKind.HealPlayer, _x + 300, 600)
                : _graph.Add(GraphNodeKind.SpawnEntity, _x + 300, 600);
            if (action.Kind == GraphNodeKind.HealPlayer)
            {
                _graph.Data(start, "player", action, "player");
                if (info.Outputs.Any(o => o.Name == "damage"))
                {
                    _graph.Data(start, "damage", action, "amount");
                }
            }
            else
            {
                _graph.Data(start, "entity", action, "entity").Data(start, "position", action, "position");
            }

            _graph.Exec(start, action);
        }

        public void Use(NodeInfo info)
        {
            if (info.Kind == GraphNodeKind.MainHandItem)
            {
                Consume(_hand, info.Outputs[0]);
                return;
            }

            _x += 300;
            GraphNode node = _graph.Add(info.Kind, _x, info.HasExecInput ? 0 : 300);
            Feed(node, info);
            if (info.HasExecInput)
            {
                Chain(node);
            }

            foreach (PinInfo output in info.Outputs)
            {
                Consume(node, output);
            }
        }

        /// <summary>Connects the inputs that have no inline value to the event or to the item in hand.</summary>
        private void Feed(GraphNode node, NodeInfo info, string? except = null)
        {
            foreach (PinInfo input in info.Inputs.Where(i => i.Name != except))
            {
                switch (input.Type.Kind)
                {
                    case PinType.Player:
                        _graph.Data(_event, "player", node, input.Name);
                        break;
                    case PinType.Position:
                        _graph.Data(_event, "position", node, input.Name);
                        break;
                    case PinType.ItemStack:
                        _graph.Data(_hand, "stack", node, input.Name);
                        break;
                }
            }
        }

        /// <summary>Gives a value to an action of its type, so that it is part of the generated code.</summary>
        private void Consume(GraphNode node, PinInfo output)
        {
            (GraphNodeKind kind, string pin) = output.Type.Kind switch
            {
                PinType.Text or PinType.Number => (GraphNodeKind.SendMessage, "message"),
                PinType.Boolean => (GraphNodeKind.Branch, "condition"),
                PinType.Block => (GraphNodeKind.SetBlock, "block"),
                PinType.Item => (GraphNodeKind.SetItemCooldown, "item"),
                PinType.ItemStack => (GraphNodeKind.RemoveAllEnchantments, "stack"),
                PinType.Player => (GraphNodeKind.HealPlayer, "player"),
                PinType.Position => (GraphNodeKind.TeleportPlayer, "position"),
                _ => throw new InvalidOperationException($"No consumer for {output.Type.Kind}."),
            };
            _x += 300;
            GraphNode consumer = _graph.Add(kind, _x, 0);
            Feed(consumer, NodeCatalog.Get(kind), except: pin);
            _graph.Data(node, output.Name, consumer, pin);
            Chain(consumer);
        }

        private void Chain(GraphNode node)
        {
            _graph.Exec(_last, node, _lastPin);
            _last = node;
            _lastPin = NodeCatalog.Get(node.Kind).Flow switch
            {
                NodeFlow.Branch => NodeInfo.ExecTrue,
                NodeFlow.Loop => NodeInfo.ExecBody,
                NodeFlow.Multi => NodeCatalog.Get(node.Kind).ExecOutputs[0].Name,
                _ => NodeInfo.ExecThen,
            };
        }
    }
}
