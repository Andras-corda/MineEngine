using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Graphs;

/// <summary>
/// Options shared by every graph tab, kept in the editor preferences: the button of the graph
/// toolbar and the setting of Settings > Editors change the same value.
/// </summary>
public sealed class GraphViewOptions : ObservableObject
{
    private readonly Action<bool> _saveSnapToGrid;
    private bool _snapToGrid;

    /// <param name="saveSnapToGrid">Writes the choice in the preferences.</param>
    public GraphViewOptions(bool snapToGrid, Action<bool> saveSnapToGrid)
    {
        _snapToGrid = snapToGrid;
        _saveSnapToGrid = saveSnapToGrid ?? throw new ArgumentNullException(nameof(saveSnapToGrid));
    }

    /// <summary>Moved nodes are placed on the grid, so that the wires stay straight.</summary>
    public bool SnapToGrid
    {
        get => _snapToGrid;
        set
        {
            if (SetProperty(ref _snapToGrid, value))
            {
                _saveSnapToGrid(value);
            }
        }
    }

    /// <summary>The preferences changed elsewhere (Settings window): the button follows, nothing is saved again.</summary>
    internal void Follow(bool snapToGrid)
    {
        if (_snapToGrid != snapToGrid)
        {
            _snapToGrid = snapToGrid;
            OnPropertyChanged(nameof(SnapToGrid));
        }
    }
}
