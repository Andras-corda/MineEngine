using System.Collections.Immutable;
using System.Globalization;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Comments and tooltips of a graph, like the comment boxes of Unreal's Blueprints. They are
/// stored as nodes (so that they are moved, copied, deleted and undone like the others) but they
/// do not change the program: the validation and the Java see the graph without them
/// (<see cref="Strip"/>, called by <see cref="Reroutes.Collapse"/>).
/// </summary>
/// <remarks>
/// A comment is a titled box drawn behind nodes (C on a selection); it has a size, a color and
/// an optional tooltip. A tooltip (T on a selection) is a small bubble whose message is shown
/// when the mouse is over the bubble or over one of the nodes it describes.
/// </remarks>
public static class GraphAnnotations
{
    /// <summary>Title of a comment, message of a tooltip.</summary>
    public const string TextKey = "text";

    public const string WidthKey = "width";

    public const string HeightKey = "height";

    /// <summary>Color of a comment ("#RRGGBB").</summary>
    public const string ColorKey = "color";

    /// <summary>Message shown when the mouse is over the title of a comment.</summary>
    public const string TooltipKey = "tooltip";

    /// <summary>Nodes described by a tooltip: their ids, separated by commas.</summary>
    public const string TargetsKey = "nodes";

    /// <summary>Room around the nodes framed by a new comment, and height of its title.</summary>
    public const double Margin = 24;

    public const double TitleHeight = 34;

    public const double MinWidth = 120;

    public const double MinHeight = 70;

    /// <summary>Colors offered for a comment (the first one is the default).</summary>
    public static IReadOnlyList<string> CommentColors { get; } = ["#5A6B7D", "#3B6EA5", "#3F7F4A", "#A8742A", "#A23B3B", "#6A4AAE", "#2D7D8C"];

    public static bool IsAnnotation(GraphNode node) => node.Kind is GraphNodeKind.Comment or GraphNodeKind.Tooltip;

    /// <summary>New comment that frames <paramref name="area"/> (the nodes selected), with room for its title.</summary>
    public static GraphNode CreateComment(double x, double y, double width, double height, string text) =>
        new(Guid.NewGuid(), GraphNodeKind.Comment, Math.Round(x - Margin), Math.Round(y - Margin - TitleHeight), CommentValues(width, height, text));

    /// <summary>Values of a new comment that frames an area of this size (also used by Puppeteer).</summary>
    public static ImmutableDictionary<string, string> CommentValues(double width, double height, string text) =>
        ImmutableDictionary<string, string>.Empty
            .Add(TextKey, text)
            .Add(WidthKey, Format(Math.Max(MinWidth, width + 2 * Margin)))
            .Add(HeightKey, Format(Math.Max(MinHeight, height + 2 * Margin + TitleHeight)))
            .Add(ColorKey, CommentColors[0]);

    /// <summary>New tooltip bubble, placed at (x, y), that describes <paramref name="targets"/>.</summary>
    public static GraphNode CreateTooltip(IEnumerable<Guid> targets, double x, double y, string text) =>
        new(Guid.NewGuid(), GraphNodeKind.Tooltip, Math.Round(x), Math.Round(y), TooltipValues(targets, text));

    /// <summary>Values of a new tooltip (also used by Puppeteer).</summary>
    public static ImmutableDictionary<string, string> TooltipValues(IEnumerable<Guid> targets, string text) =>
        ImmutableDictionary<string, string>.Empty
            .Add(TextKey, text)
            .Add(TargetsKey, string.Join(",", targets.Select(t => t.ToString("N"))));

    /// <summary>Nodes described by a tooltip.</summary>
    public static IReadOnlyList<Guid> Targets(GraphNode tooltip) => Targets(tooltip.Values);

    /// <summary>Nodes described by a tooltip, from its values.</summary>
    public static IReadOnlyList<Guid> Targets(IReadOnlyDictionary<string, string> values) =>
        [.. (values.GetValueOrDefault(TargetsKey) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Guid.TryParse(t, out Guid id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)];

    /// <summary>The tooltip with other targets (copy of nodes: the copy describes the copies).</summary>
    public static GraphNode WithTargets(GraphNode tooltip, IEnumerable<Guid> targets) =>
        tooltip.WithValue(TargetsKey, string.Join(",", targets.Select(t => t.ToString("N"))));

    public static string Text(GraphNode node, string key = TextKey) => Text(node.Values, key);

    public static string Text(IReadOnlyDictionary<string, string> values, string key = TextKey) => values.GetValueOrDefault(key) ?? string.Empty;

    /// <summary>Size of a comment, read from its values.</summary>
    public static (double Width, double Height) Size(GraphNode comment) => Size(comment.Values);

    public static (double Width, double Height) Size(IReadOnlyDictionary<string, string> values) =>
        (Math.Max(MinWidth, Number(values, WidthKey, 300)), Math.Max(MinHeight, Number(values, HeightKey, 200)));

    public static GraphNode Resize(GraphNode comment, double width, double height) => comment with { Values = Resize(comment.Values, width, height) };

    public static ImmutableDictionary<string, string> Resize(ImmutableDictionary<string, string> values, double width, double height) => values
        .SetItem(WidthKey, Format(Math.Max(MinWidth, Math.Round(width))))
        .SetItem(HeightKey, Format(Math.Max(MinHeight, Math.Round(height))));

    /// <summary>A comment or a tooltip: no pins, nothing in the program.</summary>
    public static NodeInfo Describe(GraphNode node) => node.Kind == GraphNodeKind.Comment
        ? new NodeInfo(GraphNodeKind.Comment, NodeCategory.Object, Tr.T("Comment"),
            Tr.T("Frames nodes with a title, like the comments of Blueprints: moving it moves the nodes inside. It does not change the program."),
            flow: NodeFlow.Pure)
        : new NodeInfo(GraphNodeKind.Tooltip, NodeCategory.Object, Tr.T("Tooltip"),
            Tr.T("Message shown when the mouse is over the bubble or over the nodes it describes. It does not change the program."),
            flow: NodeFlow.Pure);

    /// <summary>The graph without comments nor tooltips.</summary>
    public static NodeGraph Strip(NodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph.Nodes.Any(IsAnnotation) ? new NodeGraph(graph.Nodes.Where(n => !IsAnnotation(n)), graph.Connections) : graph;
    }

    private static double Number(IReadOnlyDictionary<string, string> values, string key, double fallback) =>
        double.TryParse(values.GetValueOrDefault(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : fallback;

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
}
