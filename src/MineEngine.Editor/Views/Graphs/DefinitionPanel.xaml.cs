using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MineEngine.Editor.Views.Graphs;

/// <summary>Panneau "Définition" d'un graphe (voir DefinitionViewModel).</summary>
public partial class DefinitionPanel : UserControl
{
    public DefinitionPanel()
    {
        InitializeComponent();
    }

    /// <summary>Un bouton avec un menu l'ouvre sous lui (le menu reprend le DataContext du bouton).</summary>
    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.DataContext = button.DataContext;
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
