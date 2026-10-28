using System.Collections.Immutable;

namespace MineEngine.Assets.Sparkle;

/// <summary>Note of the canvas of a system, like the annotations of the graphs.</summary>
public enum SparkleNoteKind
{
    /// <summary>A colored frame with a title around emitters (moving it moves them).</summary>
    Comment,

    /// <summary>A small bubble whose message shows over it, or over the emitters it describes.</summary>
    Tooltip,
}

/// <summary>
/// Comment or tooltip of the canvas of a Sparkle system (V0.7.6). It has no effect on the
/// particles; it is saved with the system. Immutable, like the emitters.
/// </summary>
public sealed record SparkleNote(Guid Id, SparkleNoteKind Kind)
{
    /// <summary>Size of a new comment around nothing.</summary>
    public const double DefaultWidth = 360;

    public const double DefaultHeight = 260;

    public double X { get; init; }

    public double Y { get; init; }

    /// <summary>Size of a comment (a tooltip takes the size of its bubble).</summary>
    public double Width { get; init; } = DefaultWidth;

    public double Height { get; init; } = DefaultHeight;

    /// <summary>Title of a comment, message of a tooltip.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Color of a comment ("#RRGGBB", one of the colors of the graph comments).</summary>
    public string Color { get; init; } = Graphs.GraphAnnotations.CommentColors[0];

    /// <summary>Message shown when the mouse is over the title of a comment.</summary>
    public string Tooltip { get; init; } = string.Empty;

    /// <summary>Emitters described by a tooltip (they show a small i).</summary>
    public ImmutableList<Guid> Targets { get; init; } = [];
}
