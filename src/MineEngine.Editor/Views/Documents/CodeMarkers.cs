using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using MineEngine.Core.Diagnostics;
using MineEngine.Editor.ViewModels.Documents;

namespace MineEngine.Editor.Views.Documents;

/// <summary>
/// Lignes en erreur dans le texte : fond teinté et soulignement ondulé sous le code
/// (rouge pour une erreur, orange pour un avertissement).
/// </summary>
public sealed class CodeMarkerRenderer : IBackgroundRenderer
{
    private IReadOnlyList<CodeMarker> _markers = [];

    public Brush LineBrush { get; set; } = Brushes.Transparent;

    public Brush ErrorBrush { get; set; } = Brushes.Red;

    public Brush WarningBrush { get; set; } = Brushes.Orange;

    public KnownLayer Layer => KnownLayer.Background;

    public IReadOnlyList<CodeMarker> Markers
    {
        get => _markers;
        set => _markers = value ?? [];
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_markers.Count == 0 || !textView.VisualLinesValid || textView.Document is not { } document)
        {
            return;
        }

        foreach (CodeMarker marker in _markers.Where(m => m.Line <= document.LineCount))
        {
            DocumentLine line = document.GetLineByNumber(marker.Line);
            Brush underline = marker.Severity == DiagnosticSeverity.Error ? ErrorBrush : WarningBrush;
            var pen = new Pen(underline, 1);
            pen.Freeze();

            foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, TrimmedSegment(document, line)))
            {
                drawingContext.DrawRectangle(LineBrush, null, new Rect(0, rect.Top, textView.ActualWidth, rect.Height));
                drawingContext.DrawGeometry(null, pen, Wave(rect.Left, rect.Right, rect.Bottom - 1));
            }
        }
    }

    /// <summary>La ligne sans son indentation : le soulignement suit le code, pas les espaces.</summary>
    private static TextSegment TrimmedSegment(TextDocument document, DocumentLine line)
    {
        string text = document.GetText(line);
        int start = text.Length - text.TrimStart().Length;
        return new TextSegment { StartOffset = line.Offset + start, EndOffset = line.Offset + Math.Max(start + 1, text.TrimEnd().Length) };
    }

    private static StreamGeometry Wave(double left, double right, double y)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(new Point(left, y), false, false);
            bool up = true;
            for (double x = left + 2; x <= right; x += 2)
            {
                context.LineTo(new Point(x, up ? y - 2 : y), true, false);
                up = !up;
            }
        }

        geometry.Freeze();
        return geometry;
    }
}

/// <summary>Marge à gauche des numéros de ligne : un point devant chaque ligne qui a un problème.</summary>
public sealed class CodeMarkerMargin : AbstractMargin
{
    private const double MarginWidth = 14;

    private IReadOnlyList<CodeMarker> _markers = [];

    public Brush ErrorBrush { get; set; } = Brushes.Red;

    public Brush WarningBrush { get; set; } = Brushes.Orange;

    public IReadOnlyList<CodeMarker> Markers
    {
        get => _markers;
        set
        {
            _markers = value ?? [];
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(MarginWidth, 0);

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView is not null)
        {
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView is not null)
        {
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (TextView is not { VisualLinesValid: true } textView || _markers.Count == 0)
        {
            return;
        }

        var byLine = _markers.GroupBy(m => m.Line).ToDictionary(g => g.Key, g => g.Max(m => m.Severity));
        foreach (VisualLine line in textView.VisualLines)
        {
            if (byLine.TryGetValue(line.FirstDocumentLine.LineNumber, out DiagnosticSeverity severity))
            {
                double y = line.VisualTop - textView.VerticalOffset + (line.Height / 2);
                drawingContext.DrawEllipse(severity == DiagnosticSeverity.Error ? ErrorBrush : WarningBrush, null,
                    new Point(MarginWidth / 2, y), 3.5, 3.5);
            }
        }
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();
}
