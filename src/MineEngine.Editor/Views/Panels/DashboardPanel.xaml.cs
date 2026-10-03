using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.Views.Panels;

/// <summary>
/// Dashboard. Le code-behind adapte les colonnes à la largeur (les colonnes secondaires
/// disparaissent sur un écran étroit) et relaie le double-clic vers le panneau Détails.
/// </summary>
public partial class DashboardPanel : UserControl
{
    public static readonly DependencyProperty WideColumnProperty =
        DependencyProperty.Register(nameof(WideColumn), typeof(GridLength), typeof(DashboardPanel), new PropertyMetadata(new GridLength(150)));

    /// <summary>Largeur des colonnes "État" et "Utilisé par" ; nulle quand le panneau est étroit.</summary>
    private const double CompactWidth = 640;

    public DashboardPanel()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            bool compact = e.NewSize.Width < CompactWidth;
            WideColumn = compact ? new GridLength(0) : new GridLength(150);

            // Écran étroit : les boutons de compilation passent sous le titre.
            DockPanel.SetDock(HeaderActions, compact ? Dock.Bottom : Dock.Right);
            HeaderActions.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(0);
        };
    }

    public GridLength WideColumn
    {
        get => (GridLength)GetValue(WideColumnProperty);
        set => SetValue(WideColumnProperty, value);
    }

    private MainViewModel? Main => DataContext as MainViewModel;

    private void OnAssetDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: AssetViewModel asset } && Main?.ShowAssetDetailsCommand is { } command)
        {
            command.Execute(asset);
            e.Handled = true;
        }
    }

    /// <summary>Les listes ne défilent pas elles-mêmes : la molette fait défiler toute la page.</summary>
    private void OnListMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Page.ScrollToVerticalOffset(Page.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    /// <summary>Le clic droit sélectionne aussi l'élément, pour que le menu agisse sur lui.</summary>
    private void OnAssetRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }
}
