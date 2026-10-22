using System.Collections.Immutable;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Nodes whose pins follow a type kept in the node (value "type"), like the wildcard pins of
/// Unreal: the collection nodes (size, add, get at index, dictionary...) and the vector nodes. A
/// wire connected to their main input gives them its type; the add menu offers them already
/// typed when a wire is dropped in empty space.
/// </summary>
public static class GenericNodes
{
    public const string TypeKey = "type";

    /// <summary>Main input: the collection, or the vector.</summary>
    public const string CollectionInput = "collection";

    public const string VectorInput = "vector";

    public static bool IsGeneric(GraphNodeKind kind) => IsCollectionNode(kind) || IsVectorNode(kind) || IsCast(kind);

    /// <summary>Input of a cast: the value to convert.</summary>
    public const string CastInput = "value";

    public static bool IsCast(GraphNodeKind kind) => kind is >= GraphNodeKind.CastToNumber and <= GraphNodeKind.CastToPlayer;

    /// <summary>Type a cast gives.</summary>
    public static DataType CastTarget(GraphNodeKind kind) => kind switch
    {
        GraphNodeKind.CastToNumber => DataType.Number,
        GraphNodeKind.CastToLong => DataType.Long,
        GraphNodeKind.CastToFloat => DataType.Float,
        GraphNodeKind.CastToDecimal => DataType.Decimal,
        GraphNodeKind.CastToByte => DataType.Byte,
        GraphNodeKind.CastToChar => DataType.Char,
        GraphNodeKind.CastToText => DataType.Text,
        GraphNodeKind.CastToComponent => DataType.TextComponent,
        GraphNodeKind.CastToIdentifier => DataType.Identifier,
        GraphNodeKind.CastToUuid => DataType.Uuid,
        GraphNodeKind.CastToLiving => DataType.LivingEntity,
        _ => DataType.Player,
    };

    /// <summary>
    /// The cast that turns a value of <paramref name="from"/> into one that fits <paramref name="to"/>
    /// (inserted by the editor between two pins of different types, like Unreal), or null.
    /// </summary>
    public static GraphNodeKind? CastBetween(DataType from, DataType to, Func<DataType, DataType, bool> canFeed)
    {
        ArgumentNullException.ThrowIfNull(canFeed);
        for (GraphNodeKind kind = GraphNodeKind.CastToNumber; kind <= GraphNodeKind.CastToPlayer; kind++)
        {
            if (Accepts(kind, from) && canFeed(CastTarget(kind), to))
            {
                return kind;
            }
        }

        return null;
    }

    public static bool IsCollectionNode(GraphNodeKind kind) => kind is >= GraphNodeKind.CollectionSize and <= GraphNodeKind.ForEachInCollection;

    public static bool IsVectorNode(GraphNodeKind kind) => kind is >= GraphNodeKind.MakeVector and <= GraphNodeKind.VectorToPosition;

    /// <summary>Name of the input that gives its type to the node (null for "Make vector", typed by the add menu).</summary>
    public static string? MainInput(GraphNodeKind kind) =>
        IsCollectionNode(kind) ? CollectionInput : kind == GraphNodeKind.MakeVector ? null : IsVectorNode(kind) ? VectorInput : IsCast(kind) ? CastInput : null;

    /// <summary>True if the node accepts this type (a list for "Add", a dictionary for "Get by key"...).</summary>
    public static bool Accepts(GraphNodeKind kind, DataType type) => kind switch
    {
        GraphNodeKind.CollectionSize or GraphNodeKind.CollectionClear or GraphNodeKind.CollectionContains or GraphNodeKind.CollectionRemove
            or GraphNodeKind.ForEachInCollection => type.IsCollection && type.Element is not null,
        GraphNodeKind.ListGet or GraphNodeKind.ListSet => type.Kind is PinType.Array or PinType.List && type.Element is not null,
        GraphNodeKind.ListAdd => type.Kind is PinType.List or PinType.Set && type.Element is not null,
        GraphNodeKind.ListRemoveAt => type.Kind == PinType.List && type.Element is not null,
        GraphNodeKind.MapGet or GraphNodeKind.MapPut => type.Kind == PinType.Map && type.Element is not null && type.Key is not null,
        GraphNodeKind.ArrayResize => type.Kind == PinType.Array && type.Element is not null,
        GraphNodeKind.VectorZ or GraphNodeKind.VectorToPosition => type.Kind is PinType.Vector3i or PinType.Vector3f,
        _ when IsVectorNode(kind) => IsVector(type.Kind),
        GraphNodeKind.CastToNumber => type.Kind is PinType.Decimal or PinType.Float or PinType.Long or PinType.Byte or PinType.Char or PinType.Text or PinType.Boolean,
        GraphNodeKind.CastToLong => type.Kind is PinType.Number or PinType.Decimal or PinType.Float or PinType.Byte or PinType.Char or PinType.Text,
        GraphNodeKind.CastToFloat => type.Kind is PinType.Number or PinType.Long or PinType.Decimal or PinType.Byte or PinType.Text,
        GraphNodeKind.CastToDecimal => type.Kind is PinType.Number or PinType.Long or PinType.Float or PinType.Byte or PinType.Text,
        GraphNodeKind.CastToByte => type.Kind is PinType.Number or PinType.Long or PinType.Decimal or PinType.Float or PinType.Char,
        GraphNodeKind.CastToChar => type.Kind is PinType.Number or PinType.Byte or PinType.Text,
        GraphNodeKind.CastToText => type.Kind is not (PinType.Exec or PinType.Text),
        GraphNodeKind.CastToComponent => type.Kind is not (PinType.Exec or PinType.TextComponent),
        GraphNodeKind.CastToIdentifier => type.Kind == PinType.Text,
        GraphNodeKind.CastToUuid => type.Kind is PinType.Text or PinType.Entity or PinType.LivingEntity or PinType.Player,
        GraphNodeKind.CastToLiving => type.Kind == PinType.Entity,
        GraphNodeKind.CastToPlayer => type.Kind is PinType.Entity or PinType.LivingEntity,
        _ => false,
    };

    public static bool IsVector(PinType kind) => kind is PinType.Vector2i or PinType.Vector3i or PinType.Vector2f or PinType.Vector3f;

    public static DataType DefaultType(GraphNodeKind kind) => kind switch
    {
        GraphNodeKind.MapGet or GraphNodeKind.MapPut => DataType.Collection(PinType.Map, DataType.Number, DataType.Text),
        GraphNodeKind.ArrayResize => DataType.Collection(PinType.Array, DataType.Number),
        _ when IsVectorNode(kind) => DataType.Vector3f,
        GraphNodeKind.CastToNumber => DataType.Decimal,
        GraphNodeKind.CastToComponent or GraphNodeKind.CastToIdentifier => DataType.Text,
        GraphNodeKind.CastToUuid or GraphNodeKind.CastToLiving or GraphNodeKind.CastToPlayer => DataType.Entity,
        _ when IsCast(kind) => DataType.Number,
        _ => DataType.ListOf(DataType.Number),
    };

    /// <summary>Type kept in the node (or the default one).</summary>
    public static DataType TypeOf(GraphNode node) =>
        DataType.Parse(node.Values.GetValueOrDefault(TypeKey)) is { } type && Accepts(node.Kind, type) ? type : DefaultType(node.Kind);

    /// <summary>The node with another type; its values of inputs that changed type are dropped.</summary>
    public static GraphNode Retype(GraphNode node, DataType type) =>
        node with { Values = ImmutableDictionary<string, string>.Empty.Add(TypeKey, type.ToStorageText()) };

    /// <summary>Default values of a new node of the catalog.</summary>
    public static ImmutableDictionary<string, string> DefaultValues(GraphNodeKind kind) =>
        IsGeneric(kind) ? ImmutableDictionary<string, string>.Empty.Add(TypeKey, DefaultType(kind).ToStorageText()) : ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Pins of the node for its type; <paramref name="typeLabel"/> names the types and
    /// <paramref name="valuePin"/> (the program) gives an enumeration value its constants.
    /// </summary>
    public static NodeInfo Describe(GraphNode node, Func<DataType, string> typeLabel, Func<string, string, DataType, PinInfo>? valuePin = null) =>
        Describe(node.Kind, TypeOf(node), typeLabel, valuePin);

    public static NodeInfo Describe(GraphNodeKind kind, DataType type, Func<DataType, string> typeLabel, Func<string, string, DataType, PinInfo>? valuePin = null)
    {
        ArgumentNullException.ThrowIfNull(typeLabel);
        string label = typeLabel(type);
        PinInfo collection = new(CollectionInput, label, type);
        DataType element = type.Element ?? DataType.Number;
        DataType key = type.Key ?? DataType.Text;
        PinInfo Value(string name, string text, DataType valueType) =>
            valuePin?.Invoke(name, text, valueType) ?? new(name, text, valueType, valueType.HasInlineValue ? DefaultText(valueType) : null);
        PinInfo index = new("index", Tr.T("Index"), PinType.Number, "0");
        PinInfo vector = new(VectorInput, label, type);
        bool whole = type.Kind is PinType.Vector2i or PinType.Vector3i;
        DataType component = whole ? DataType.Number : DataType.Decimal;
        PinInfo Output(string name, string text, DataType valueType) => new(name, text, valueType);

        return kind switch
        {
            GraphNodeKind.CollectionSize => new NodeInfo(kind, NodeCategory.Variable, Tr.T("Size"),
                Tr.T("Number of values of the collection (entries of a dictionary)."), inputs: [collection], outputs: [Output("result", Tr.T("Size"), DataType.Number)]),
            GraphNodeKind.CollectionClear => new NodeInfo(kind, NodeCategory.Action, Tr.T("Clear"),
                Tr.T("Removes every value of the collection."), inputs: [collection]),
            GraphNodeKind.CollectionContains => new NodeInfo(kind, NodeCategory.Variable, type.Kind == PinType.Map ? Tr.T("Has the key") : Tr.T("Contains"),
                type.Kind == PinType.Map ? Tr.T("True if the dictionary has this key.") : Tr.T("True if the collection contains the value."),
                inputs: [collection, type.Kind == PinType.Map ? Value("value", Tr.T("Key"), key) : Value("value", Tr.T("Value"), element)],
                outputs: [Output("result", Tr.T("Result"), DataType.Boolean)]),
            GraphNodeKind.CollectionRemove => new NodeInfo(kind, NodeCategory.Action, type.Kind == PinType.Map ? Tr.T("Remove the key") : Tr.T("Remove the value"),
                type.Kind == PinType.Map ? Tr.T("Removes the key and its value from the dictionary.") : Tr.T("Removes the value from the collection (the first one in a list)."),
                inputs: [collection, type.Kind == PinType.Map ? Value("value", Tr.T("Key"), key) : Value("value", Tr.T("Value"), element)]),
            GraphNodeKind.ListGet => new NodeInfo(kind, NodeCategory.Variable, Tr.T("Get at index"),
                Tr.T("The value at the index (0: the first one); the default value of the type outside the list."),
                inputs: [collection, index], outputs: [Output("value", Tr.T("Value"), element)]),
            GraphNodeKind.ListSet => new NodeInfo(kind, NodeCategory.Action, Tr.T("Set at index"),
                Tr.T("Replaces the value at the index (nothing happens outside the list)."), inputs: [collection, index, Value("value", Tr.T("Value"), element)]),
            GraphNodeKind.ListAdd => new NodeInfo(kind, NodeCategory.Action, Tr.T("Add"),
                Tr.T("Adds the value at the end of the list (or to the set, if it is not there yet)."), inputs: [collection, Value("value", Tr.T("Value"), element)]),
            GraphNodeKind.ListRemoveAt => new NodeInfo(kind, NodeCategory.Action, Tr.T("Remove at index"),
                Tr.T("Removes the value at the index; the next ones move back."), inputs: [collection, index]),
            GraphNodeKind.MapGet => new NodeInfo(kind, NodeCategory.Variable, Tr.T("Get by key"),
                Tr.T("The value of the key; the default value of the type if the key is missing."),
                inputs: [collection, Value("key", Tr.T("Key"), key)], outputs: [Output("value", Tr.T("Value"), element)]),
            GraphNodeKind.MapPut => new NodeInfo(kind, NodeCategory.Action, Tr.T("Set the key"),
                Tr.T("Gives a value to the key (replaces the old one)."), inputs: [collection, Value("key", Tr.T("Key"), key), Value("value", Tr.T("Value"), element)]),
            GraphNodeKind.ArrayResize => new NodeInfo(kind, NodeCategory.Action, Tr.T("Set the size"),
                Tr.T("Gives the array this size: new cells take the default value, extra cells are removed."),
                inputs: [collection, new PinInfo("size", Tr.T("Size"), PinType.Number, "10")]),
            GraphNodeKind.ForEachInCollection => new NodeInfo(kind, NodeCategory.Action, Tr.T("For each value"),
                type.Kind == PinType.Map
                    ? Tr.T("Runs the Loop body for each entry of the dictionary (its key and its value), then continues with Completed.")
                    : Tr.T("Runs the Loop body for each value of the collection, then continues with Completed."),
                inputs: [collection],
                outputs: type.Kind == PinType.Map ? [Output("key", Tr.T("Key"), key), Output("value", Tr.T("Value"), element)] : [Output("value", Tr.T("Value"), element)],
                flow: NodeFlow.Loop),
            GraphNodeKind.MakeVector => new NodeInfo(kind, NodeCategory.Variable, Tr.F("Make {0}", label),
                Tr.T("A vector from its coordinates."),
                inputs:
                [
                    new PinInfo("x", "X", component, "0"), new PinInfo("y", "Y", component, "0"),
                    .. type.Kind is PinType.Vector3i or PinType.Vector3f ? [new PinInfo("z", "Z", component, "0")] : Array.Empty<PinInfo>(),
                ],
                outputs: [Output("vector", label, type)]),
            GraphNodeKind.VectorX => new NodeInfo(kind, NodeCategory.Variable, Tr.T("Vector X"), Tr.T("The X coordinate of the vector."),
                inputs: [vector], outputs: [Output("value", "X", component)]),
            GraphNodeKind.VectorY => new NodeInfo(kind, NodeCategory.Variable, Tr.T("Vector Y"), Tr.T("The Y coordinate of the vector."),
                inputs: [vector], outputs: [Output("value", "Y", component)]),
            GraphNodeKind.VectorZ => new NodeInfo(kind, NodeCategory.Variable, Tr.T("Vector Z"), Tr.T("The Z coordinate of a 3D vector."),
                inputs: [vector], outputs: [Output("value", "Z", component)]),
            GraphNodeKind.VectorLength => new NodeInfo(kind, NodeCategory.Variable, Tr.T("Vector length"), Tr.T("Length of the vector (distance from zero)."),
                inputs: [vector], outputs: [Output("value", Tr.T("Length"), DataType.Decimal)]),
            GraphNodeKind.AddVectors => new NodeInfo(kind, NodeCategory.Variable, Tr.T("Add vectors"), Tr.T("The sum of the two vectors."),
                inputs: [vector, new PinInfo("other", Tr.T("Other"), type)], outputs: [Output("vector", label, type)]),
            GraphNodeKind.VectorToPosition => new NodeInfo(kind, NodeCategory.Variable, Tr.T("Vector to position"), Tr.T("The block position of a 3D vector (rounded down)."),
                inputs: [vector], outputs: [Output("position", Tr.T("Position"), DataType.Position)]),
            _ => new NodeInfo(kind, NodeCategory.Variable, CastTitle(kind), CastDescription(kind, label, typeLabel(CastTarget(kind))),
                inputs: [Value(CastInput, label, type)], outputs: [Output("result", typeLabel(CastTarget(kind)), CastTarget(kind))]),
        };
    }

    private static string CastTitle(GraphNodeKind kind) => kind switch
    {
        GraphNodeKind.CastToNumber => Tr.T("To whole number (int)"),
        GraphNodeKind.CastToLong => Tr.T("To long"),
        GraphNodeKind.CastToFloat => Tr.T("To float"),
        GraphNodeKind.CastToDecimal => Tr.T("To decimal (double)"),
        GraphNodeKind.CastToByte => Tr.T("To byte"),
        GraphNodeKind.CastToChar => Tr.T("To character"),
        GraphNodeKind.CastToText => Tr.T("To text"),
        GraphNodeKind.CastToComponent => Tr.T("To rich text"),
        GraphNodeKind.CastToIdentifier => Tr.T("To identifier"),
        GraphNodeKind.CastToUuid => Tr.T("To UUID"),
        GraphNodeKind.CastToLiving => Tr.T("To living entity"),
        _ => Tr.T("To player"),
    };

    private static string CastDescription(GraphNodeKind kind, string from, string to) => kind switch
    {
        GraphNodeKind.CastToNumber or GraphNodeKind.CastToByte => Tr.F("Converts a {0} to a {1} (decimals are cut off, a text is read).", from, to),
        GraphNodeKind.CastToChar => Tr.F("Converts a {0} to a {1} (the character of a code, or the first character of a text).", from, to),
        GraphNodeKind.CastToLiving or GraphNodeKind.CastToPlayer => Tr.F("The {0} as a {1}, or none if it is not one.", from, to),
        _ => Tr.F("Converts a {0} to a {1}.", from, to),
    };

    /// <summary>Text of the default value of an inline input.</summary>
    private static string DefaultText(DataType type) => type.Kind switch
    {
        PinType.Number => "0",
        PinType.Decimal => "0.0",
        PinType.Boolean => "false",
        PinType.Direction => "north",
        PinType.Long or PinType.Byte => "0",
        PinType.Float => "0.0",
        PinType.Char => "a",
        PinType.Identifier => "minecraft:stone",
        PinType.Fluid => "minecraft:water",
        PinType.EquipmentSlot or PinType.Hand or PinType.Rarity => DataType.NamedValues(type.Kind)![0],
        _ => string.Empty,
    };
}
