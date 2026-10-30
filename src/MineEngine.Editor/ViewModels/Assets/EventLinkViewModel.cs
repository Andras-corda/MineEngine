using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// An event of an asset (item, mob) in its Events tab: the graphs that react to it, or a button
/// that adds its event node to the events graph of the asset.
/// </summary>
public abstract class EventLinkViewModel : ObservableObject
{
    private IReadOnlyList<(GraphAsset Graph, GraphNode Node)> _links = [];

    protected EventLinkViewModel(NodeInfo info, string titlePrefix, string addTooltip, Action add, Action<GraphAsset, GraphNode> open)
    {
        string title = info.Title.Replace(titlePrefix, string.Empty, StringComparison.Ordinal);
        Title = title.Length == 0 ? title : char.ToUpper(title[0], System.Globalization.CultureInfo.CurrentCulture) + title[1..];
        Description = info.Description;
        AddTooltip = addTooltip;
        AddCommand = new RelayCommand(add);
        OpenCommand = new RelayCommand(() => open(_links[0].Graph, _links[0].Node), () => _links.Count > 0);
    }

    public string Title { get; }

    public string Description { get; }

    public string AddTooltip { get; }

    public bool IsLinked => _links.Count > 0;

    /// <summary>"Connected to graph "Sword events"", or "No graph".</summary>
    public string Status => _links.Count switch
    {
        0 => Tr.T("No graph reacts to this event."),
        1 => Tr.F("Connected to graph \"{0}\".", Header(_links[0].Graph)),
        _ => Tr.F("Connected to {0} nodes ({1}).", _links.Count, string.Join(", ", _links.Select(l => Header(l.Graph)).Distinct())),
    };

    public ICommand AddCommand { get; }

    public ICommand OpenCommand { get; }

    internal void SetLinks(IReadOnlyList<(GraphAsset Graph, GraphNode Node)> links)
    {
        _links = links;
        OnPropertyChanged(nameof(IsLinked));
        OnPropertyChanged(nameof(Status));
        CommandManager.InvalidateRequerySuggested();
    }

    private static string Header(GraphAsset graph) =>
        string.IsNullOrWhiteSpace(graph.DisplayName) ? graph.ResourceId.Value : graph.DisplayName;
}
