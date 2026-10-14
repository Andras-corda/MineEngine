using System.Windows;
using System.Windows.Controls;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Graphs;

namespace MineEngine.Editor.Views.Panels;

public partial class InspectorPanel : UserControl
{
    public InspectorPanel()
    {
        InitializeComponent();
    }

    /// <summary>Un problème du graphe actif : on montre le nœud concerné.</summary>
    private void OnGraphIssueClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GraphIssueItem { NodeId: { } node } }
            && DataContext is MainViewModel { ActiveDocument: GraphDocumentViewModel graph })
        {
            graph.ShowNode(node);
        }
    }
}
