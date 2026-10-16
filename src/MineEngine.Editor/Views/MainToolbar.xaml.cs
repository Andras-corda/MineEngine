using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MineEngine.Editor.ViewModels;

namespace MineEngine.Editor.Views;

/// <summary>Barre d'outils de la fenêtre principale (voir MainToolbar.xaml).</summary>
public partial class MainToolbar : UserControl
{
    public MainToolbar()
    {
        InitializeComponent();
    }

    /// <summary>Place le curseur dans la recherche (Ctrl+F hors d'un éditeur).</summary>
    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    /// <summary>Le bouton + ouvre son menu sous lui (le menu reprend le DataContext du bouton).</summary>
    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (AddButton.ContextMenu is { } menu)
        {
            menu.DataContext = AddButton.DataContext;
            menu.PlacementTarget = AddButton;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    /// <summary>La recherche et le mode d'affichage portent sur le Dashboard : on l'affiche.</summary>
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchBox.IsKeyboardFocused)
        {
            ShowDashboard();
        }
    }

    private void OnShowDashboardClick(object sender, RoutedEventArgs e) => ShowDashboard();

    private void ShowDashboard()
    {
        if (DataContext is MainViewModel { ShowDashboardCommand: { } command } && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
