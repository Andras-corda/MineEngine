using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Global variables (V0.6.7): one variable per scope and many types (number, decimal, block state,
/// entity, direction, vectors, list, set, dictionary, array, a list of structures, a dictionary of
/// enumerations), and an event graph that reads and changes them with the collection, vector,
/// decimal, direction, block state and entity nodes. Added after everything else, so that the
/// end-to-end builds compile them.
/// </summary>
internal sealed class GlobalsSample
{
    private readonly ModProject _project;
    private readonly List<GraphNode> _chain = [];
    private ProgramGraphBuilder _graph = null!;

    private GlobalsSample(ModProject project) => _project = project;

    public GraphAsset Events { get; private set; } = null!;

    public GlobalVariableAsset Score { get; private set; } = null!;

    public GlobalVariableAsset Seen { get; private set; } = null!;

    public GlobalVariableAsset Counters { get; private set; } = null!;

    public IReadOnlyList<GlobalVariableAsset> Globals { get; private set; } = [];

    private ProgramModel Program => ProgramModel.From(_project.Assets);

    public static GlobalsSample AddTo(ModProject project)
    {
        var sample = new GlobalsSample(project);
        sample.Build();
        return sample;
    }

    private GlobalVariableAsset Global(string name, DataType type, GlobalScope scope, string defaultValue = "")
    {
        var global = new GlobalVariableAsset(Guid.NewGuid(), ResourceId.Parse(name), name) { ValueType = type, Scope = scope, DefaultValue = defaultValue };
        _project.Assets.Add(global);
        return global;
    }

    private GraphAsset Definition(string id) => _project.Assets.OfType<GraphAsset>().First(g => g.ResourceId.Value == id);

    private void Build()
    {
        DataType loot = ProgramModel.TypeOf(Definition("butin"));
        DataType rarity = ProgramModel.TypeOf(Definition("rarete"));
        Score = Global("score", DataType.Number, GlobalScope.PlayerPersistent, "5");
        GlobalVariableAsset lives = Global("vies", DataType.Decimal, GlobalScope.PlayerLifetime, "3.5");
        Seen = Global("joueurs_vus", DataType.ListOf(DataType.Text), GlobalScope.Map);
        Counters = Global("compteurs", DataType.Collection(PinType.Map, DataType.Number, DataType.Text), GlobalScope.World);
        GlobalVariableAsset lastBlock = Global("dernier_bloc", DataType.BlockState, GlobalScope.Session);
        GlobalVariableAsset target = Global("cible", DataType.Entity, GlobalScope.Session);
        GlobalVariableAsset wind = Global("vent", DataType.Direction, GlobalScope.Map, "east");
        GlobalVariableAsset point = Global("point", DataType.Vector3f, GlobalScope.World);
        GlobalVariableAsset grid = Global("grille", DataType.Collection(PinType.Array, DataType.Number), GlobalScope.Session);
        GlobalVariableAsset marks = Global("marques", DataType.Collection(PinType.Set, DataType.Position), GlobalScope.Map);
        GlobalVariableAsset cell = Global("case", DataType.Vector2i, GlobalScope.PlayerPersistent);
        GlobalVariableAsset loots = Global("butins", DataType.ListOf(loot), GlobalScope.Session);
        GlobalVariableAsset rarities = Global("raretes", DataType.Collection(PinType.Map, rarity, DataType.Text), GlobalScope.Map);
        GlobalVariableAsset flag = Global("drapeau", DataType.Boolean, GlobalScope.Session, "true");
        Globals = [Score, lives, Seen, Counters, lastBlock, target, wind, point, grid, marks, cell, loots, rarities, flag];

        Events = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("usage_globales"), "Usage des globales") { Kind = GraphKind.Events };
        _project.Assets.Add(Events);
        _graph = new ProgramGraphBuilder(Events, () => Program);

        GraphNode join = Add(GraphNodeKind.OnPlayerJoin);
        _chain.Add(join);
        GraphNode Player() => join;
        GraphNode position = Add(GraphNodeKind.PlayerPosition);
        Data(join, "player", position, "player");
        GraphNode name = Add(GraphNodeKind.PlayerName);
        Data(join, "player", name, "player");

        // score += 1 (per player).
        GraphNode score = Get(Score, Player());
        GraphNode plus = Add(GraphNodeKind.Add, ("b", "1"));
        Data(score, "value", plus, "a");
        Step(Set(Score, Player()), (plus, "result", "value"));

        // The name joins the list; the dictionary counts the players seen.
        Step(Typed(GraphNodeKind.ListAdd, Seen.ValueType), (Get(Seen), "value", "collection"), (name, "name", "value"));
        GraphNode size = Typed(GraphNodeKind.CollectionSize, Seen.ValueType);
        Data(Get(Seen), "value", size, "collection");
        Step(Typed(GraphNodeKind.MapPut, Counters.ValueType), (Get(Counters), "value", "collection"), (name, "name", "key"), (size, "result", "value"));

        // Block state under the player, nearest entity.
        GraphNode state = Add(GraphNodeKind.BlockStateAt);
        Data(position, "position", state, "position");
        Step(Set(lastBlock), (state, "state", "value"));
        GraphNode nearest = Add(GraphNodeKind.NearestEntity, ("radius", "16"));
        Data(position, "position", nearest, "position");
        Step(Set(target), (nearest, "entity", "value"));

        // For each name seen: a message; then the vector of the position is added to "point".
        GraphNode each = Typed(GraphNodeKind.ForEachInCollection, Seen.ValueType);
        Data(Get(Seen), "value", each, "collection");
        Link(each);
        GraphNode bodyMessage = Add(GraphNodeKind.SendMessage);
        _graph.Exec(each, bodyMessage, NodeInfo.ExecBody);
        Data(join, "player", bodyMessage, "player");
        Data(each, "value", bodyMessage, "message");
        _chain.Add(each);

        GraphNode toVector = Add(GraphNodeKind.PositionToVector);
        Data(position, "position", toVector, "position");
        GraphNode sum = Typed(GraphNodeKind.AddVectors, DataType.Vector3f);
        Data(Get(point), "value", sum, "vector");
        Data(toVector, "vector", sum, "other");
        Step(Set(point), (sum, "vector", "value"));

        // A block placed three blocks in front of the player, with the state seen.
        GraphNode facing = Add(GraphNodeKind.PlayerFacing);
        Data(join, "player", facing, "player");
        GraphNode front = Add(GraphNodeKind.OffsetByDirection, ("distance", "3"));
        Data(position, "position", front, "position");
        Data(facing, "direction", front, "direction");
        GraphNode turned = Add(GraphNodeKind.StateWithProperty, ("property", "facing"), ("value", "south"));
        Data(Get(lastBlock), "value", turned, "state");
        Step(Add(GraphNodeKind.SetBlockState), (front, "position", "position"), (turned, "state", "state"));

        // Array of 10 numbers, the first one is the rounded X of "point".
        Step(Typed(GraphNodeKind.ArrayResize, grid.ValueType, ("size", "10")), (Get(grid), "value", "collection"));
        GraphNode x = Typed(GraphNodeKind.VectorX, DataType.Vector3f);
        Data(Get(point), "value", x, "vector");
        GraphNode rounded = Add(GraphNodeKind.RoundDecimal);
        Data(x, "value", rounded, "a");
        Step(Typed(GraphNodeKind.ListSet, grid.ValueType, ("index", "0")), (Get(grid), "value", "collection"), (rounded, "value", "value"));

        // The set of positions: a mark at the block of a vector, then cleared if it holds the player's position.
        GraphNode made = Typed(GraphNodeKind.MakeVector, DataType.Vector3f, ("x", "1.5"), ("y", "64"), ("z", "-2.5"));
        GraphNode block = Typed(GraphNodeKind.VectorToPosition, DataType.Vector3f);
        Data(made, "vector", block, "vector");
        Step(Typed(GraphNodeKind.ListAdd, marks.ValueType), (Get(marks), "value", "collection"), (block, "position", "value"));
        GraphNode contains = Typed(GraphNodeKind.CollectionContains, marks.ValueType);
        Data(Get(marks), "value", contains, "collection");
        Data(position, "position", contains, "value");
        Step(Set(flag), (contains, "result", "value"));
        Step(Typed(GraphNodeKind.CollectionClear, marks.ValueType), (Get(marks), "value", "collection"));

        // Whole vector per player: (score, length of "point" rounded down).
        GraphNode length = Typed(GraphNodeKind.VectorLength, DataType.Vector3f);
        Data(Get(point), "value", length, "vector");
        GraphNode floor = Add(GraphNodeKind.FloorDecimal);
        Data(length, "value", floor, "a");
        GraphNode cellVector = Typed(GraphNodeKind.MakeVector, DataType.Vector2i);
        Data(score, "value", cellVector, "x");
        Data(floor, "value", cellVector, "y");
        Step(Set(cell, Player()), (cellVector, "vector", "value"));

        // Lives: a decimal computation.
        GraphNode random = Add(GraphNodeKind.RandomDecimal);
        GraphNode product = Add(GraphNodeKind.DecimalMultiply, ("b", "2.0"));
        Data(random, "result", product, "a");
        GraphNode divided = Add(GraphNodeKind.DecimalDivide, ("b", "4.0"));
        Data(Get(lives, Player()), "value", divided, "a");
        GraphNode added = Add(GraphNodeKind.DecimalAdd);
        Data(divided, "result", added, "a");
        Data(product, "result", added, "b");
        GraphNode minus = Add(GraphNodeKind.DecimalSubtract, ("b", "0.5"));
        Data(added, "result", minus, "a");
        Step(Set(lives, Player()), (minus, "result", "value"));
        GraphNode greater = Add(GraphNodeKind.DecimalGreater, ("b", "1.0"));
        Data(minus, "result", greater, "a");
        Step(Set(flag), (greater, "result", "value"));
        GraphNode less = Add(GraphNodeKind.DecimalLess, ("b", "10.0"));
        Data(minus, "result", less, "a");
        Step(Set(flag), (less, "result", "value"));

        // Messages: wind direction reversed, a property of the state, the counter of the player, the entity.
        GraphNode opposite = Add(GraphNodeKind.OppositeDirection);
        Data(Get(wind), "value", opposite, "direction");
        GraphNode windName = Add(GraphNodeKind.DirectionName);
        Data(opposite, "direction", windName, "direction");
        Message(join, windName, "text");
        GraphNode property = Add(GraphNodeKind.StateProperty, ("property", "axis"));
        Data(Get(lastBlock), "value", property, "state");
        Message(join, property, "text");
        GraphNode counter = Typed(GraphNodeKind.MapGet, Counters.ValueType);
        Data(Get(Counters), "value", counter, "collection");
        Data(name, "name", counter, "key");
        Message(join, counter, "value");
        GraphNode typeId = Add(GraphNodeKind.EntityTypeId);
        Data(Get(target), "value", typeId, "entity");
        Message(join, typeId, "text");
        GraphNode health = Add(GraphNodeKind.EntityHealth);
        Data(Get(target), "value", health, "entity");
        Message(join, health, "value");
        GraphNode first = Typed(GraphNodeKind.ListGet, grid.ValueType, ("index", "0"));
        Data(Get(grid), "value", first, "collection");
        Message(join, first, "value");
        GraphNode lootCount = Typed(GraphNodeKind.CollectionSize, loots.ValueType);
        Data(Get(loots), "value", lootCount, "collection");
        Message(join, lootCount, "result");
        GraphNode z = Typed(GraphNodeKind.VectorZ, DataType.Vector3f);
        Data(Get(point), "value", z, "vector");
        Message(join, z, "value");
        GraphNode cellY = Typed(GraphNodeKind.VectorY, DataType.Vector2i);
        Data(Get(cell, Player()), "value", cellY, "vector");
        Message(join, cellY, "value");

        // Entities: alive, player, looked at, teleported, hurt, removed.
        GraphNode alive = Add(GraphNodeKind.IsEntityAlive);
        Data(Get(target), "value", alive, "entity");
        Step(Set(flag), (alive, "result", "value"));
        GraphNode isPlayer = Add(GraphNodeKind.EntityIsPlayer);
        Data(join, "player", isPlayer, "entity");
        Step(Set(flag), (isPlayer, "result", "value"));
        GraphNode lookedAt = Add(GraphNodeKind.EntityLookedAt);
        Data(join, "player", lookedAt, "player");
        Step(Set(target), (lookedAt, "entity", "value"));
        GraphNode where = Add(GraphNodeKind.EntityPosition);
        Data(Get(target), "value", where, "entity");
        GraphNode above = Add(GraphNodeKind.OffsetPosition, ("y", "1"));
        Data(where, "position", above, "position");
        Step(Add(GraphNodeKind.TeleportEntity), (Get(target), "value", "entity"), (above, "position", "position"));
        Step(Add(GraphNodeKind.DamageEntity, ("amount", "1")), (Get(target), "value", "entity"));
        Step(Add(GraphNodeKind.RemoveEntity), (Get(target), "value", "entity"));

        // Block of a default state, then the other collection nodes.
        GraphNode defaultState = Add(GraphNodeKind.DefaultBlockState, ("block", "minecraft:oak_log"));
        GraphNode blockOf = Add(GraphNodeKind.BlockOfState);
        Data(defaultState, "state", blockOf, "state");
        Step(Add(GraphNodeKind.SetBlock), (position, "position", "position"), (blockOf, "block", "block"));
        Step(Typed(GraphNodeKind.MapPut, rarities.ValueType, ("key", "epee"), ("value", Definition("rarete").Constants[1].Id.ToString("D"))), (Get(rarities), "value", "collection"));
        Step(Typed(GraphNodeKind.CollectionRemove, Seen.ValueType), (Get(Seen), "value", "collection"), (name, "name", "value"));
        Step(Typed(GraphNodeKind.ListRemoveAt, Seen.ValueType, ("index", "0")), (Get(Seen), "value", "collection"));
        Step(Typed(GraphNodeKind.CollectionRemove, Counters.ValueType, ("value", "personne")), (Get(Counters), "value", "collection"));
        GraphNode eachCounter = Typed(GraphNodeKind.ForEachInCollection, Counters.ValueType);
        Data(Get(Counters), "value", eachCounter, "collection");
        Link(eachCounter);
        GraphNode counterMessage = Add(GraphNodeKind.SendMessage);
        _graph.Exec(eachCounter, counterMessage, NodeInfo.ExecBody);
        Data(join, "player", counterMessage, "player");
        Data(eachCounter, "key", counterMessage, "message");
        _chain.Add(eachCounter);
        Step(Set(wind), (opposite, "direction", "value"));

        _graph.Commit();
    }

    private double _y;

    private GraphNode Add(GraphNodeKind kind, params (string Pin, string Value)[] values) => _graph.Add(kind, 0, _y += 40, values: values);

    private GraphNode Typed(GraphNodeKind kind, DataType type, params (string Pin, string Value)[] values) =>
        Add(kind, [(GenericNodes.TypeKey, type.ToStorageText()), .. values]);

    private GraphNode Get(GlobalVariableAsset global, GraphNode? player = null)
    {
        GraphNode node = _graph.Add(GraphNodeKind.GetGlobal, 0, _y += 40, global.Id);
        if (player is not null)
        {
            Data(player, "player", node, "player");
        }

        return node;
    }

    private GraphNode Set(GlobalVariableAsset global, GraphNode? player = null)
    {
        GraphNode node = _graph.Add(GraphNodeKind.SetGlobal, 0, _y += 40, global.Id);
        if (player is not null)
        {
            Data(player, "player", node, "player");
        }

        return node;
    }

    private void Data(GraphNode from, string fromPin, GraphNode to, string toPin) => _graph.Data(from, fromPin, to, toPin);

    /// <summary>Next action of the chain, with its inputs (output of the source, input of the action).</summary>
    private void Step(GraphNode action, params (GraphNode From, string Pin, string To)[] inputs)
    {
        foreach ((GraphNode from, string pin, string to) in inputs)
        {
            Data(from, pin, action, to);
        }

        Link(action);
        _chain.Add(action);
    }

    /// <summary>Exec wire from the last action ("Completed" of a loop: the same pin as "then").</summary>
    private void Link(GraphNode action) => _graph.Exec(_chain[^1], action);

    private void Message(GraphNode join, GraphNode from, string pin)
    {
        GraphNode message = Add(GraphNodeKind.SendMessage);
        Data(join, "player", message, "player");
        Step(message, (from, pin, "message"));
    }
}
