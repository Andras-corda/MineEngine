using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Every scope of the global variables (scopeGlobalVariable.md): one number per scope, a list kept
/// per team, and an event graph that sets the guild of the player and the group of the player
/// entity, then reads and changes each variable with the key its scope needs.
/// </summary>
internal sealed class ScopesSample
{
    private readonly ModProject _project;

    private ScopesSample(ModProject project) => _project = project;

    public GraphAsset Events { get; private set; } = null!;

    public IReadOnlyList<GlobalVariableAsset> Globals { get; private set; } = [];

    private ProgramModel Program => ProgramModel.From(_project.Assets);

    public static ScopesSample AddTo(ModProject project)
    {
        var sample = new ScopesSample(project);
        sample.Build();
        return sample;
    }

    private void Build()
    {
        var globals = new List<GlobalVariableAsset>();
        foreach (GlobalScope scope in GlobalVariableAsset.OrderedScopes)
        {
            string name = "portee_" + GlobalVariableAsset.ScopeLabel(scope);
            var global = new GlobalVariableAsset(Guid.NewGuid(), ResourceId.Parse(name), name) { ValueType = DataType.Number, Scope = scope, DefaultValue = "1" };
            _project.Assets.Add(global);
            globals.Add(global);
        }

        var list = new GlobalVariableAsset(Guid.NewGuid(), ResourceId.Parse("membres_equipe"), "membres_equipe")
        {
            ValueType = DataType.ListOf(DataType.Text), Scope = GlobalScope.PlayerTeam,
        };
        _project.Assets.Add(list);
        globals.Add(list);
        Globals = globals;

        Events = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("usage_portees"), "Usage des portées") { Kind = GraphKind.Events };
        _project.Assets.Add(Events);
        var graph = new ProgramGraphBuilder(Events, () => Program);
        double y = 0;
        GraphNode join = graph.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode position = graph.Add(GraphNodeKind.PlayerPosition, 0, y += 40);
        graph.Data(join, "player", position, "player");
        GraphNode last = join;
        void Chain(GraphNode node)
        {
            graph.Exec(last, node);
            last = node;
        }

        GraphNode guild = graph.Add(GraphNodeKind.SetPlayerGuild, 0, y += 40, values: ("guild", "chevaliers"));
        graph.Data(join, "player", guild, "player");
        Chain(guild);
        GraphNode group = graph.Add(GraphNodeKind.SetEntityGroup, 0, y += 40, values: ("group", "meute"));
        graph.Data(join, "player", group, "entity");
        Chain(group);
        foreach ((GraphNodeKind kind, string input) in new[] { (GraphNodeKind.PlayerGuild, "player"), (GraphNodeKind.EntityGroup, "entity") })
        {
            GraphNode name = graph.Add(kind, 0, y += 40);
            graph.Data(join, "player", name, input);
            GraphNode message = graph.Add(GraphNodeKind.SendMessage, 0, y += 40);
            graph.Data(join, "player", message, "player");
            graph.Data(name, "text", message, "message");
            Chain(message);
        }

        void Key(GlobalVariableAsset global, GraphNode node)
        {
            switch (GlobalVariableAsset.KeyOf(global.Scope))
            {
                case GlobalScopeKey.Player:
                    graph.Data(join, "player", node, "player");
                    break;
                case GlobalScopeKey.Entity:
                    graph.Data(join, "player", node, "entity");
                    break;
                case GlobalScopeKey.Position:
                    graph.Data(position, "position", node, "position");
                    break;
            }
        }

        // value = value + 1, for every scope.
        foreach (GlobalVariableAsset global in globals.Where(g => g.ValueType == DataType.Number))
        {
            GraphNode get = graph.Add(GraphNodeKind.GetGlobal, 0, y += 40, global.Id);
            Key(global, get);
            GraphNode plus = graph.Add(GraphNodeKind.Add, 0, y += 40, values: ("b", "1"));
            graph.Data(get, "value", plus, "a");
            GraphNode set = graph.Add(GraphNodeKind.SetGlobal, 0, y += 40, global.Id);
            Key(global, set);
            graph.Data(plus, "result", set, "value");
            Chain(set);
        }

        // The list of the team receives the name of the player.
        GraphNode members = graph.Add(GraphNodeKind.GetGlobal, 0, y += 40, list.Id);
        Key(list, members);
        GraphNode playerName = graph.Add(GraphNodeKind.PlayerName, 0, y += 40);
        graph.Data(join, "player", playerName, "player");
        GraphNode add = graph.Add(GraphNodeKind.ListAdd, 0, y += 40, values: (GenericNodes.TypeKey, list.ValueType.ToStorageText()));
        graph.Data(members, "value", add, "collection");
        graph.Data(playerName, "name", add, "value");
        Chain(add);
        graph.Commit();
    }
}
