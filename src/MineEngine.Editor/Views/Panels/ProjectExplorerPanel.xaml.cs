using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.ViewModels.Panels;

namespace MineEngine.Editor.Views.Panels;

/// <summary>
/// Le TreeView de WPF n'expose pas sa sélection en liaison : le code-behind la transmet
/// au ViewModel et relaie le double-clic, la touche Entrée et les clics dans la structure.
/// </summary>
public partial class ProjectExplorerPanel : UserControl
{
    public ProjectExplorerPanel()
    {
        InitializeComponent();
    }

    private MainViewModel? Main => DataContext as MainViewModel;

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (Main?.Explorer is { } explorer)
        {
            explorer.SelectedNode = e.NewValue as FileNodeViewModel;
        }
    }

    private void OnNodeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Le double-clic remonte les TreeViewItem parents : seul l'élément cliqué est traité.
        if (sender is FrameworkElement { DataContext: FileNodeViewModel node } element && element.IsMouseOver && !e.Handled)
        {
            if (sender is TreeViewItem item && !item.IsSelected)
            {
                return;
            }

            Run(Main?.Explorer.OpenCommand, node);
            e.Handled = true;
        }
    }

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Main?.Explorer.SelectedNode is { } node)
        {
            Run(Main.Explorer.OpenCommand, node);
            e.Handled = true;
        }
        else if (e.Key == Key.F2 && Main?.Explorer.SelectedNode is { } renamed)
        {
            Run(Main.Explorer.RenameCommand, renamed);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && Main?.Explorer.SelectedNode is { } deleted)
        {
            Run(Main.Explorer.DeleteCommand, deleted);
            e.Handled = true;
        }
    }

    /// <summary>Comme dans VS Code, un résultat de recherche s'ouvre d'un simple clic.</summary>
    private void OnSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: FileNodeViewModel node })
        {
            Run(Main?.Explorer.OpenCommand, node);
        }
    }

    private void OnOutlineSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is OutlineItem item)
        {
            Main?.ActiveDocument?.GoToLine(item.Line);
            list.SelectedItem = null;
        }
    }

    private static void Run(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }
}
