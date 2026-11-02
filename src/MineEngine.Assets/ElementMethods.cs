using System.Collections.Immutable;
using MineEngine.Assets.Graphs;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>Language of a method of an element: Java code kept in the element, or a node graph that belongs to it.</summary>
public enum MethodLanguage
{
    Java,
    Nodal,
}

/// <summary>What a method of an element gives back: nothing (an event), a yes / no, or a number.</summary>
public enum MethodReturn
{
    Void,
    Boolean,
    Decimal,
}

/// <summary>
/// A method of the class of an element (V0.7.13): a binding (glint condition, burn time...) or an
/// event (right click, shot...). It does not exist until it is created; its body is Java code, or a
/// node graph of the element (a function for a value, a procedure for an event).
/// </summary>
public sealed record ElementMethod(string Slot, MethodLanguage Language, Guid? GraphId, string JavaBody)
{
    public static ElementMethod Java(string slot, string body) => new(slot, MethodLanguage.Java, null, body ?? string.Empty);

    public static ElementMethod Nodal(string slot, Guid graphId) => new(slot, MethodLanguage.Nodal, graphId, string.Empty);
}

/// <summary>An element whose class has methods in Java or as graphs (an item V0.7.13, a GUI V0.8.0).</summary>
public interface IHasMethods
{
    Guid Id { get; }

    string DisplayName { get; }

    IReadOnlyList<ElementMethod> Methods { get; set; }

    ElementMethod? FindMethod(string slot);
}

/// <summary>
/// A place of the class where a method can be created: its Java name, what it gives back, and the
/// parameters of its graph (the world is always given first, like the functions of the project).
/// </summary>
public sealed record MethodSlot(string Key, string JavaName, MethodReturn Return, IReadOnlyList<(string Name, DataType Type)> Parameters, string Title)
{
    public bool IsEvent => Return == MethodReturn.Void;

    /// <summary>Kind of the node graph of the method: a procedure for an event, a function otherwise.</summary>
    public GraphKind GraphKind => IsEvent ? GraphKind.Procedure : GraphKind.Function;

    /// <summary>Signature of a method that does not follow the ones of the items (the events of a GUI, V0.8.0).</summary>
    public string? CustomSignature { get; init; }

    /// <summary>Java signature of the method, as the generator writes it in the class of the element.</summary>
    public string JavaSignature => CustomSignature ?? Return switch
    {
        MethodReturn.Boolean => $"public static boolean {JavaName}(Level level, Entity entity)",
        MethodReturn.Decimal => $"public static double {JavaName}(Level level, Entity entity)",
        _ => $"public static void {JavaName}(Player player, Level level, BlockPos position, Entity other, double amount)",
    };

    /// <summary>Body of a new Java method: the value of the game.</summary>
    public string DefaultJavaBody => Return switch
    {
        MethodReturn.Boolean => "return false;",
        MethodReturn.Decimal => "return 0.0;",
        _ => string.Empty,
    };

    /// <summary>
    /// New graph of the method, owned by the element: its parameters in the order the generator passes
    /// them, its result (a function), the entry node and, for a function, the return node.
    /// </summary>
    public GraphAsset CreateGraph(ResourceId resourceId, Guid elementId)
    {
        var graph = new GraphAsset(Guid.NewGuid(), resourceId, JavaName)
        {
            Kind = GraphKind,
            ElementId = elementId,
            Parameters = [.. Parameters.Select(p => VariableDefinition.Create(p.Name, p.Type))],
        };
        if (!IsEvent)
        {
            graph.Returns = [VariableDefinition.Create(Tr.T("result"), Return == MethodReturn.Boolean ? DataType.Boolean : DataType.Decimal)];
        }

        ProgramModel program = new([graph]);
        var entryProbe = new GraphNode(Guid.Empty, GraphNodeKind.FunctionEntry, 0, 0, ImmutableDictionary<string, string>.Empty);
        GraphNode entry = GraphNode.Create(program.Describe(entryProbe, graph), 0, 0);
        NodeGraph body = NodeGraph.Empty.AddNode(entry);
        if (!IsEvent)
        {
            GraphNode result = GraphNode.Create(program.Describe(entryProbe with { Kind = GraphNodeKind.FunctionReturn }, graph), 420, 0);
            body = body.AddNode(result).Connect(new GraphConnection(entry.Id, NodeInfo.ExecThen, result.Id, NodeInfo.ExecInput), program.ContextFor(graph));
        }

        graph.Graph = body;
        return graph;
    }
}

/// <summary>The places of methods of an item: its bindings, then one per event.</summary>
public static class ItemMethodSlots
{
    public const string GlintCondition = "glintCondition";
    public const string BurnTime = "burnTime";
    public const string BurnCondition = "burnCondition";
    public const string CompostCondition = "compostCondition";

    private static readonly (string, DataType)[] Holder = [("entity", DataType.Entity)];

    private static readonly (string, DataType)[] EventParameters =
        [("player", DataType.Player), ("position", DataType.Position), ("other", DataType.Entity), ("amount", DataType.Decimal)];

    public static IReadOnlyList<MethodSlot> All { get; } =
    [
        new(GlintCondition, "getGlintCondition", MethodReturn.Boolean, Holder, Tr.T("Glint condition")),
        new(BurnTime, "getBurnTime", MethodReturn.Decimal, Holder, Tr.T("Burn time (s)")),
        new(BurnCondition, "getBurnCondition", MethodReturn.Boolean, Holder, Tr.T("Burning condition")),
        new(CompostCondition, "getCompostCondition", MethodReturn.Boolean, Holder, Tr.T("Composting condition")),
        .. Enum.GetValues<ItemEventKind>().Select(k => new MethodSlot(EventKey(k), "on" + k, MethodReturn.Void, EventParameters, Tr.F("Event \"{0}\"", NodeCatalog.ForItemEvent(k).Title))),
    ];

    /// <summary>Key of the method of an event ("event.RightClick").</summary>
    public static string EventKey(ItemEventKind kind) => "event." + kind;

    public static MethodSlot? Find(string key) => All.FirstOrDefault(s => s.Key == key);
}

/// <summary>
/// The places of methods of a GUI (V0.8.0): opened, every tick, closed, then one per button (clicked), per
/// checkbox (ticked or not), per text input (text changed) and per slot (item changed). The world is
/// given first, then the player, then the value of the component.
/// </summary>
public static class GuiMethodSlots
{
    public const string Opened = "opened";
    public const string Tick = "tick";
    public const string Closed = "closed";

    private static readonly (string, DataType)[] PlayerOnly = [("player", DataType.Player)];

    /// <summary>Key of the method of a component ("click.0f8f...").</summary>
    public static string ComponentKey(GuiComponent component) => component.Kind switch
    {
        GuiComponentKind.Button => "click.",
        GuiComponentKind.Checkbox => "check.",
        GuiComponentKind.TextInput => "text.",
        GuiComponentKind.Slot => "slot.",
        _ => "none.",
    } + component.Id.ToString("N");

    /// <summary>The value given with the event of a component: its Java type, its name and its type in a graph.</summary>
    public static (string JavaType, string Name, DataType Type)? ValueOf(GuiComponentKind kind) => kind switch
    {
        GuiComponentKind.Checkbox => ("boolean", "ticked", DataType.Boolean),
        GuiComponentKind.TextInput => ("String", "text", DataType.Text),
        GuiComponentKind.Slot => ("ItemStack", "stack", DataType.ItemStack),
        _ => null,
    };

    /// <summary>The places of the GUI: its 3 events, then the ones of its components that have one.</summary>
    public static IReadOnlyList<MethodSlot> For(GuiAsset gui)
    {
        ArgumentNullException.ThrowIfNull(gui);
        List<MethodSlot> slots =
        [
            Event(Opened, "onOpened", Tr.T("When the GUI opens"), null),
            Event(Tick, "onTick", Tr.T("Every tick while it is open"), null),
            Event(Closed, "onClosed", Tr.T("When the GUI closes"), null),
        ];
        foreach (GuiComponent component in gui.Components)
        {
            string name = char.ToUpperInvariant(component.Name.FirstOrDefault('x')) + component.Name[Math.Min(1, component.Name.Length)..];
            (string java, string title) = component.Kind switch
            {
                GuiComponentKind.Button => ($"on{name}Clicked", Tr.F("Button \"{0}\" clicked", component.Name)),
                GuiComponentKind.Checkbox => ($"on{name}Ticked", Tr.F("Checkbox \"{0}\" ticked or unticked", component.Name)),
                GuiComponentKind.TextInput => ($"on{name}Changed", Tr.F("Text of \"{0}\" changed", component.Name)),
                GuiComponentKind.Slot => ($"on{name}Changed", Tr.F("Item of the slot \"{0}\" changed", component.Name)),
                _ => (string.Empty, string.Empty),
            };
            if (java.Length > 0)
            {
                slots.Add(Event(ComponentKey(component), java, title, ValueOf(component.Kind)));
            }
        }

        return slots;
    }

    public static MethodSlot? Find(GuiAsset gui, string key) => For(gui).FirstOrDefault(s => s.Key == key);

    private static MethodSlot Event(string key, string java, string title, (string JavaType, string Name, DataType Type)? value) =>
        new(key, java, MethodReturn.Void, value is { } v ? [.. PlayerOnly, (v.Name, v.Type)] : PlayerOnly, title)
        {
            CustomSignature = $"public static void {java}(Player player, Level level{(value is { } w ? $", {w.JavaType} {w.Name}" : string.Empty)})",
        };
}
