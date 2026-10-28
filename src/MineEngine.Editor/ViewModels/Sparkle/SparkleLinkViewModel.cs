using System.Windows;
using System.Windows.Media;
using MineEngine.Assets.Sparkle;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Sparkle;

/// <summary>
/// Wire of the canvas from an emitter that sends events (deaths, times, zones) to the emitter that
/// receives them (Receive event module): from the right side of the source to the left side of the target.
/// </summary>
public sealed class SparkleLinkViewModel : ObservableObject
{
    private Geometry _geometry = Geometry.Empty;

    public SparkleLinkViewModel(SparkleEmitterViewModel source, SparkleEmitterViewModel target)
    {
        Source = source;
        Target = target;
        Update();
    }

    public SparkleEmitterViewModel Source { get; }

    public SparkleEmitterViewModel Target { get; }

    /// <summary>Kind of the events the wire carries (its color).</summary>
    public SparkleEventKind Kind => Target.Model.ReceivedEvent ?? SparkleEventKind.Death;

    /// <summary>The source does not send this kind of event: the wire is drawn dashed and the problem is listed.</summary>
    public bool IsBroken => !Source.Model.Sends(Kind);

    public Geometry Geometry
    {
        get => _geometry;
        private set => SetProperty(ref _geometry, value);
    }

    internal void Update()
    {
        var from = new Point(Source.X + SparkleEmitterViewModel.NodeWidth, Source.Y + SparkleEmitterViewModel.HeadHeight / 2);
        var to = new Point(Target.X, Target.Y + SparkleEmitterViewModel.HeadHeight / 2);
        double bend = Math.Max(60, Math.Abs(to.X - from.X) / 2);
        var figure = new PathFigure { StartPoint = from };
        figure.Segments.Add(new BezierSegment(new Point(from.X + bend, from.Y), new Point(to.X - bend, to.Y), to, true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        Geometry = geometry;
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(IsBroken));
    }
}
