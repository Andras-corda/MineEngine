using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Types of TypesVar.md: global variables of these types (char, long, float, byte, UUID, rich text,
/// identifier, equipment slot, hand, rarity, fluid, box, NBT, living entity, and collections of
/// them) and an event graph that uses every cast and every node of these types. Added after
/// everything else, so that the end-to-end builds compile them.
/// </summary>
internal sealed class TypesSample
{
    private readonly ModProject _project;
    private readonly List<GraphNode> _chain = [];
    private ProgramGraphBuilder _graph = null!;
    private double _y;

    private TypesSample(ModProject project) => _project = project;

    public GraphAsset Events { get; private set; } = null!;

    public IReadOnlyList<GlobalVariableAsset> Globals { get; private set; } = [];

    private ProgramModel Program => ProgramModel.From(_project.Assets);

    public static TypesSample AddTo(ModProject project)
    {
        var sample = new TypesSample(project);
        sample.Build();
        return sample;
    }

    private GlobalVariableAsset Global(string name, DataType type, GlobalScope scope, string defaultValue = "")
    {
        var global = new GlobalVariableAsset(Guid.NewGuid(), ResourceId.Parse(name), name) { ValueType = type, Scope = scope, DefaultValue = defaultValue };
        _project.Assets.Add(global);
        return global;
    }

    private void Build()
    {
        GlobalVariableAsset letter = Global("lettre", DataType.Char, GlobalScope.Map, "z");
        GlobalVariableAsset ticks = Global("ticks", DataType.Long, GlobalScope.World, "-12");
        GlobalVariableAsset speed = Global("vitesse", DataType.Float, GlobalScope.PlayerPersistent, "1.5");
        GlobalVariableAsset small = Global("petit", DataType.Byte, GlobalScope.Session, "-3");
        GlobalVariableAsset owner = Global("proprietaire", DataType.Uuid, GlobalScope.Map);
        GlobalVariableAsset title = Global("titre", DataType.TextComponent, GlobalScope.Map);
        GlobalVariableAsset id = Global("identifiant", DataType.Identifier, GlobalScope.Map, "minecraft:diamond");
        GlobalVariableAsset slot = Global("emplacement", DataType.EquipmentSlot, GlobalScope.PlayerLifetime, "head");
        GlobalVariableAsset hand = Global("main", DataType.Hand, GlobalScope.Session, "off_hand");
        GlobalVariableAsset rarity = Global("rarete_vue", DataType.Rarity, GlobalScope.Map, "epic");
        GlobalVariableAsset fluid = Global("fluide", DataType.Fluid, GlobalScope.World, "minecraft:lava");
        GlobalVariableAsset box = Global("zone", DataType.BoundingBox, GlobalScope.Map);
        GlobalVariableAsset nbt = Global("donnees", DataType.NbtTag, GlobalScope.PlayerPersistent);
        GlobalVariableAsset living = Global("vivant", DataType.LivingEntity, GlobalScope.Session);
        GlobalVariableAsset letters = Global("lettres", DataType.Collection(PinType.Set, DataType.Char), GlobalScope.Map);
        GlobalVariableAsset times = Global("temps_par_joueur", DataType.Collection(PinType.Map, DataType.Long, DataType.Uuid), GlobalScope.Map);
        GlobalVariableAsset inside = Global("dedans", DataType.Boolean, GlobalScope.Session);
        Globals = [letter, ticks, speed, small, owner, title, id, slot, hand, rarity, fluid, box, nbt, living, letters, times, inside];

        Events = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("usage_types"), "Usage des types") { Kind = GraphKind.Events };
        _project.Assets.Add(Events);
        _graph = new ProgramGraphBuilder(Events, () => Program);

        GraphNode join = Add(GraphNodeKind.OnPlayerJoin);
        _chain.Add(join);
        GraphNode position = Add(GraphNodeKind.PlayerPosition);
        Data(join, "player", position, "player");
        GraphNode name = Add(GraphNodeKind.PlayerName);
        Data(join, "player", name, "player");

        // Character, long, float, byte.
        GraphNode first = Add(GraphNodeKind.CharAt, ("index", "0"));
        Data(name, "name", first, "text");
        Step(Set(letter), (first, "char", "value"));
        Step(Set(ticks), (Add(GraphNodeKind.GameTime), "time", "value"));
        Step(Set(speed, join), (Cast(GraphNodeKind.CastToFloat, DataType.Number, "5"), "result", "value"));
        Step(Set(small), (Cast(GraphNodeKind.CastToByte, DataType.Number, "7"), "result", "value"));
        Step(Set(letter), (Cast(GraphNodeKind.CastToChar, DataType.Number, "65"), "result", "value"));
        GraphNode parsedLong = Cast(GraphNodeKind.CastToLong, DataType.Text, "123");
        Step(Set(ticks), (parsedLong, "result", "value"));
        Message(join, Cast(GraphNodeKind.CastToNumber, DataType.Decimal, "2.5"), "result");
        GraphNode fromFloat = Cast(GraphNodeKind.CastToDecimal, DataType.Float);
        Data(Get(speed, join), "value", fromFloat, "value");
        Message(join, fromFloat, "result");
        GraphNode letterText = Cast(GraphNodeKind.CastToText, DataType.Char);
        Data(Get(letter), "value", letterText, "value");
        Message(join, letterText, "result");
        Step(Typed(GraphNodeKind.ListAdd, letters.ValueType), (Get(letters), "value", "collection"), (Get(letter), "value", "value"));

        // UUID.
        GraphNode uuid = Add(GraphNodeKind.EntityUuid);
        Data(join, "player", uuid, "entity");
        Step(Set(owner), (uuid, "uuid", "value"));
        GraphNode random = Cast(GraphNodeKind.CastToText, DataType.Uuid);
        Data(Add(GraphNodeKind.RandomUuid), "uuid", random, "value");
        Message(join, random, "result");
        GraphNode parsed = Cast(GraphNodeKind.CastToUuid, DataType.Text, "00000000-0000-0000-0000-000000000001");
        Step(Set(owner), (parsed, "result", "value"));
        GraphNode ofEntity = Cast(GraphNodeKind.CastToUuid, DataType.Entity);
        GraphNode found = Add(GraphNodeKind.EntityByUuid);
        Data(Get(owner), "value", found, "uuid");
        Data(found, "entity", ofEntity, "value");
        Step(Typed(GraphNodeKind.MapPut, times.ValueType), (Get(times), "value", "collection"), (ofEntity, "result", "key"), (Get(ticks), "value", "value"));

        // Living entity and player casts.
        GraphNode asLiving = Cast(GraphNodeKind.CastToLiving, DataType.Entity);
        Data(found, "entity", asLiving, "value");
        Step(Set(living), (asLiving, "result", "value"));
        GraphNode asPlayer = Cast(GraphNodeKind.CastToPlayer, DataType.LivingEntity);
        Data(Get(living), "value", asPlayer, "value");
        GraphNode hello = Add(GraphNodeKind.SendMessage, ("message", "Bonjour"));
        Step(hello, (asPlayer, "result", "player"));

        // Rich text.
        GraphNode styled = Add(GraphNodeKind.StyledText, ("text", "Salut"), ("color", "gold"), ("bold", "true"));
        Step(Set(title), (styled, "component", "value"));
        Step(Add(GraphNodeKind.SendRichMessage), (join, "player", "player"), (Get(title), "value", "component"));
        Step(Add(GraphNodeKind.SendRichMessage), (join, "player", "player"), (Cast(GraphNodeKind.CastToComponent, DataType.Number, "42"), "result", "component"));
        GraphNode plain = Cast(GraphNodeKind.CastToText, DataType.TextComponent);
        Data(Get(title), "value", plain, "value");
        Message(join, plain, "result");

        // Identifiers.
        Step(Set(id), (Cast(GraphNodeKind.CastToIdentifier, DataType.Text, "minecraft:emerald"), "result", "value"));
        GraphNode path = Add(GraphNodeKind.IdentifierPath);
        Data(Get(id), "value", path, "id");
        Message(join, path, "text");
        Message(join, Add(GraphNodeKind.IdentifierNamespace, ("id", "mymod:ruby")), "text");

        // Equipment, hands, rarity.
        GraphNode equipment = Add(GraphNodeKind.EquipmentItem);
        Data(Get(living), "value", equipment, "entity");
        Data(Get(slot, join), "value", equipment, "slot");
        GraphNode equipmentText = Cast(GraphNodeKind.CastToText, DataType.ItemStack);
        Data(equipment, "stack", equipmentText, "value");
        Message(join, equipmentText, "result");
        GraphNode held = Add(GraphNodeKind.ItemInHand);
        Data(join, "player", held, "player");
        Data(Get(hand), "value", held, "hand");
        GraphNode heldRarity = Add(GraphNodeKind.StackRarity);
        Data(held, "stack", heldRarity, "stack");
        Step(Set(rarity), (heldRarity, "rarity", "value"));
        Step(Add(GraphNodeKind.SwingHand, ("hand", "main_hand")), (join, "player", "player"));

        // Fluids and boxes.
        Step(Set(fluid), (Add(GraphNodeKind.FluidAt), "fluid", "value"));
        Data(position, "position", Find(GraphNodeKind.FluidAt), "position");
        GraphNode above = Add(GraphNodeKind.OffsetPosition, ("y", "4"));
        Data(position, "position", above, "position");
        Step(Add(GraphNodeKind.PlaceFluid), (above, "position", "position"), (Get(fluid), "value", "fluid"));
        GraphNode area = Add(GraphNodeKind.MakeBox);
        Data(position, "position", area, "from");
        Data(above, "position", area, "to");
        Step(Set(box), (area, "box", "value"));
        GraphNode entities = Add(GraphNodeKind.EntitiesInBox);
        Data(Get(box), "value", entities, "box");
        GraphNode count = Typed(GraphNodeKind.CollectionSize, DataType.ListOf(DataType.Entity));
        Data(entities, "entities", count, "collection");
        Message(join, count, "result");
        GraphNode playerBox = Add(GraphNodeKind.EntityBox);
        Data(join, "player", playerBox, "entity");
        GraphNode center = Add(GraphNodeKind.PositionToVector);
        Data(position, "position", center, "position");
        GraphNode contains = Add(GraphNodeKind.BoxContains);
        Data(playerBox, "box", contains, "box");
        Data(center, "vector", contains, "point");
        Step(Set(inside), (contains, "result", "value"));

        // Damage and effects.
        GraphNode damage = Add(GraphNodeKind.GenericDamage);
        Data(join, "player", damage, "entity");
        Step(Add(GraphNodeKind.DamageWithSource, ("amount", "1")), (join, "player", "entity"), (damage, "source", "source"));
        GraphNode effect = Add(GraphNodeKind.MakeEffect, ("effect", "minecraft:speed"), ("seconds", "5"), ("level", "2"));
        Step(Add(GraphNodeKind.ApplyEffect), (Get(living), "value", "entity"), (effect, "effect", "effect"));

        // NBT data.
        Step(Set(nbt, join), (Add(GraphNodeKind.NewNbt), "nbt", "value"));
        Step(Add(GraphNodeKind.NbtPutNumber, ("name", "score"), ("value", "3")), (Get(nbt, join), "value", "nbt"));
        Step(Add(GraphNodeKind.NbtPutText, ("name", "nom"), ("value", "Steve")), (Get(nbt, join), "value", "nbt"));
        GraphNode nbtNumber = Add(GraphNodeKind.NbtGetNumber, ("name", "score"));
        Data(Get(nbt, join), "value", nbtNumber, "nbt");
        Message(join, nbtNumber, "value");
        GraphNode nbtText = Add(GraphNodeKind.NbtGetText, ("name", "nom"));
        Data(Get(nbt, join), "value", nbtText, "nbt");
        Message(join, nbtText, "text");

        // Worlds.
        GraphNode worldOf = Add(GraphNodeKind.WorldOf);
        Data(join, "player", worldOf, "entity");
        GraphNode dimension = Add(GraphNodeKind.WorldDimension);
        Data(worldOf, "world", dimension, "world");
        Message(join, dimension, "text");
        GraphNode here = Add(GraphNodeKind.WorldDimension);
        Data(Add(GraphNodeKind.CurrentWorld), "world", here, "world");
        Message(join, here, "text");

        _graph.Commit();
    }

    private readonly Dictionary<GraphNodeKind, GraphNode> _last = [];

    private GraphNode Find(GraphNodeKind kind) => _last[kind];

    private GraphNode Add(GraphNodeKind kind, params (string Pin, string Value)[] values)
    {
        GraphNode node = _graph.Add(kind, 0, _y += 40, values: values);
        _last[kind] = node;
        return node;
    }

    private GraphNode Typed(GraphNodeKind kind, DataType type, params (string Pin, string Value)[] values) =>
        Add(kind, [(GenericNodes.TypeKey, type.ToStorageText()), .. values]);

    /// <summary>A cast typed by its input; <paramref name="value"/> is typed on it when it is not connected.</summary>
    private GraphNode Cast(GraphNodeKind kind, DataType from, string? value = null) =>
        value is null ? Typed(kind, from) : Typed(kind, from, (GenericNodes.CastInput, value));

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

    private void Step(GraphNode action, params (GraphNode From, string Pin, string To)[] inputs)
    {
        foreach ((GraphNode from, string pin, string to) in inputs)
        {
            Data(from, pin, action, to);
        }

        _graph.Exec(_chain[^1], action);
        _chain.Add(action);
    }

    private void Message(GraphNode join, GraphNode from, string pin) =>
        Step(Add(GraphNodeKind.SendMessage), (join, "player", "player"), (from, pin, "message"));
}
