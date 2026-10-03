using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Hub;

namespace MineEngine.Editor.Views.Hub;

/// <summary>
/// Accueil (sélection de projet). Le code-behind ne fait que relayer les gestes propres à
/// la vue (double-clic, menus contextuels, molette) vers les commandes de l'accueil.
/// </summary>
public partial class ProjectHubView : UserControl
{
    /// <summary>En dessous, la colonne "Projet sélectionné" passe sous la liste.</summary>
    private const double TwoColumnWidth = 1000;

    /// <summary>En dessous, la barre latérale ne garde que ses icônes.</summary>
    private const double FullSidebarWidth = 760;

    public ProjectHubView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => AdaptToWidth(e.NewSize.Width);
    }

    /// <summary>Mise en page adaptée à la largeur : deux colonnes, une colonne, barre latérale réduite.</summary>
    private void AdaptToWidth(double width)
    {
        bool twoColumns = width >= TwoColumnWidth;
        Grid.SetRow(SideColumn, twoColumns ? 0 : 1);
        Grid.SetColumn(SideColumn, twoColumns ? 2 : 0);
        Grid.SetColumnSpan(SideColumn, twoColumns ? 1 : 3);
        SideColumn.Margin = twoColumns ? new Thickness(0) : new Thickness(0, 20, 0, 0);
        SideColumn.MaxWidth = twoColumns ? double.PositiveInfinity : 520;
        SideColumn.HorizontalAlignment = twoColumns ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        ProjectsGrid.ColumnDefinitions[1].Width = new GridLength(twoColumns ? 24 : 0);
        ProjectsGrid.ColumnDefinitions[2].Width = new GridLength(twoColumns ? 300 : 0);

        bool fullSidebar = width >= FullSidebarWidth;
        Sidebar.Width = fullSidebar ? 220 : 58;
        SidebarCaption.Visibility = fullSidebar ? Visibility.Visible : Visibility.Collapsed;
        SidebarFooter.Visibility = fullSidebar ? Visibility.Visible : Visibility.Collapsed;
        PageGrid.Margin = fullSidebar ? new Thickness(36, 28, 36, 24) : new Thickness(18, 18, 18, 16);
    }

    private ProjectHubViewModel? Hub => (DataContext as MainViewModel)?.Hub;

    private static void Run(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    private void OnProjectDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: ProjectCardViewModel card })
        {
            Run(Hub?.OpenProjectCommand, card);
            e.Handled = true;
        }
    }

    private void OnOpenMenuClick(object sender, RoutedEventArgs e) => Run(Hub?.OpenProjectCommand, CardOf(sender));

    private void OnOpenFolderMenuClick(object sender, RoutedEventArgs e) => Run(Hub?.OpenFolderCommand, CardOf(sender));

    private void OnRemoveMenuClick(object sender, RoutedEventArgs e) => Run(Hub?.RemoveCommand, CardOf(sender));

    /// <summary>Bouton "..." du projet sélectionné : mêmes actions que le clic droit.</summary>
    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button || Hub?.SelectedProject is not { } card)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = button, DataContext = card };
        menu.Items.Add(CreateItem("Ouvrir le dossier", () => Run(Hub.OpenFolderCommand, card)));
        menu.Items.Add(CreateItem("Actualiser l'aperçu", () => Run(Hub.RefreshCommand, null)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateItem("Retirer de la liste", () => Run(Hub.RemoveCommand, card)));
        menu.IsOpen = true;
    }

    /// <summary>La liste ne défile pas elle-même : la molette fait défiler toute la page.</summary>
    private void OnListMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not UIElement list)
        {
            return;
        }

        e.Handled = true;
        var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = list };
        (VisualParentOf(list) as UIElement)?.RaiseEvent(forwarded);
    }

    private static DependencyObject? VisualParentOf(DependencyObject element) => System.Windows.Media.VisualTreeHelper.GetParent(element);

    private static ProjectCardViewModel? CardOf(object sender) => (sender as FrameworkElement)?.DataContext as ProjectCardViewModel;

    private static MenuItem CreateItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
