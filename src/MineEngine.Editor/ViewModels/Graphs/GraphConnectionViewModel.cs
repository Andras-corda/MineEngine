using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using MineEngine.Assets.Graphs;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Graphs;

/// <summary>
/// Fil entre deux broches, dessiné en courbe de Bézier horizontale comme dans les
/// Blueprints. Sa forme suit les broches quand les nœuds bougent.
/// </summary>
public sealed class GraphConnectionViewModel : ObservableObject, IDisposable
{
    public GraphConnectionViewModel(GraphConnection model, GraphPinViewModel from, GraphPinViewModel to)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        From.PropertyChanged += OnPinChanged;
        To.PropertyChanged += OnPinChanged;
    }

    public GraphConnection Model { get; }

    public GraphPinViewModel From { get; }

    public GraphPinViewModel To { get; }

    /// <summary>Type porté par le fil : sa couleur (blanc pour l'exécution).</summary>
    public DataType Type => From.Type;

    public bool IsExec => From.IsExec;

    public Geometry Geometry => Curve(From.Anchor, To.Anchor);

    /// <summary>Courbe entre deux points, avec des tangentes horizontales.</summary>
    public static Geometry Curve(Point start, Point end)
    {
        double handle = Math.Max(40, Math.Abs(end.X - start.X) * 0.5);
        var figure = new PathFigure { StartPoint = start, IsFilled = false };
        figure.Segments.Add(new BezierSegment(new Point(start.X + handle, start.Y), new Point(end.X - handle, end.Y), end, isStroked: true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    public void Dispose()
    {
        From.PropertyChanged -= OnPinChanged;
        To.PropertyChanged -= OnPinChanged;
    }

    private void OnPinChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraphPinViewModel.Anchor))
        {
            OnPropertyChanged(nameof(Geometry));
        }
    }
}

/// <summary>Une entrée du menu "Ajouter un nœud" (nœud du catalogue, ou appel, objet, variable du programme).</summary>
public sealed record NodeChoiceViewModel(NodeTemplate Template)
{
    public NodeInfo Info => Template.Info;

    public string Title => Template.Info.Kind switch
    {
        GraphNodeKind.GetVariable or GraphNodeKind.GetField => "Lire " + Info.Title,
        GraphNodeKind.EnumEquals => Info.Title,
        GraphNodeKind.EnumLiteral => Info.Title + " (valeur)",
        GraphNodeKind.CallFunction => "Appeler " + Info.Title,
        _ => Info.Title,
    };

    public string Description => Info.Description;

    public NodeCategory Category => Info.Category;

    public string CategoryLabel => NodeCatalog.CategoryLabel(Info.Category);
}
