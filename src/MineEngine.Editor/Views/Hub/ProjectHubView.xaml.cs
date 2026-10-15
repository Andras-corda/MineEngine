using System.ComponentModel;
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
    /// <summary>En dessous, la barre latérale ne garde que ses icônes.</summary>
    private const double FullSidebarWidth = 760;

    private ProjectHubViewModel? _hub;

    public ProjectHubView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateSidebar();
        DataContextChanged += (_, _) => WatchHub();
    }

    private ProjectHubViewModel? Hub => (DataContext as MainViewModel)?.Hub;

    private void WatchHub()
    {
        if (_hub is not null)
        {
            _hub.PropertyChanged -= OnHubPropertyChanged;
        }

        _hub = Hub;
        if (_hub is not null)
        {
            _hub.PropertyChanged += OnHubPropertyChanged;
        }

        UpdateSidebar();
    }

    private void OnHubPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectHubViewModel.IsSidebarCollapsed))
        {
            UpdateSidebar();
        }
    }

    /// <summary>
    /// Barre latérale complète, ou seulement ses icônes (comme VS Code) : au choix de l'utilisateur,
    /// et d'office quand la fenêtre est trop étroite.
    /// </summary>
    private void UpdateSidebar()
    {
        bool narrow = ActualWidth > 0 && ActualWidth < FullSidebarWidth;
        bool compact = narrow || _hub?.IsSidebarCollapsed == true;
        Sidebar.Width = compact ? 58 : 220;
        Sidebar.Tag = compact;
        SidebarCaption.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SidebarFooter.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SidebarToggle.ToolTip = compact ? Tr.T("Agrandir la barre latérale") : Tr.T("Réduire la barre latérale");
        SidebarToggle.IsEnabled = !narrow;
        PageGrid.Margin = narrow ? new Thickness(18, 18, 18, 16) : new Thickness(36, 28, 36, 24);
    }

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
}
