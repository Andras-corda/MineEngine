using System.Windows;
using System.Windows.Media;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Puppeteer;

/// <summary>Link from a parent to a child: an elbow line from the bottom pin to the top one.</summary>
public sealed class BtLinkViewModel : ObservableObject, IDisposable
{
    public BtLinkViewModel(BtNodeViewModel parent, BtNodeViewModel child)
    {
        Parent = parent;
        Child = child;
        Parent.AnchorsMoved += OnMoved;
        Child.AnchorsMoved += OnMoved;
    }

    public BtNodeViewModel Parent { get; }

    public BtNodeViewModel Child { get; }

    public Geometry Geometry => Elbow(Parent.BottomAnchor, Child.TopAnchor);

    /// <summary>Goes down from the parent, runs horizontally at mid-height, then goes down to the child.</summary>
    public static Geometry Elbow(Point from, Point to)
    {
        double middle = from.Y + Math.Max(12, (to.Y - from.Y) / 2);
        var figure = new PathFigure { StartPoint = from, IsFilled = false };
        figure.Segments.Add(new PolyLineSegment([new Point(from.X, middle), new Point(to.X, middle), to], isStroked: true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    public void Dispose()
    {
        Parent.AnchorsMoved -= OnMoved;
        Child.AnchorsMoved -= OnMoved;
    }

    private void OnMoved(object? sender, EventArgs e) => OnPropertyChanged(nameof(Geometry));
}
