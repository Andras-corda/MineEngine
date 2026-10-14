using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.Views.Panels;

/// <summary>
/// Dashboard. Le code-behind adapte les colonnes à la largeur (les colonnes secondaires
/// disparaissent sur un écran étroit), ouvre les éléments et les dossiers au double-clic,
/// et gère le glisser-déposer d'un élément sur un dossier ou sur le fil d'Ariane.
/// </summary>
public partial class DashboardPanel : UserControl
{
    /// <summary>Format des données glissées : le ViewModel de l'élément.</summary>
    private const string AssetDragFormat = "MineEngine.Asset";

    private Point? _dragStart;
    private ListBoxItem? _dragItem;

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
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public GridLength WideColumn
    {
        get => (GridLength)GetValue(WideColumnProperty);
        set => SetValue(WideColumnProperty, value);
    }

    private MainViewModel? Main => DataContext as MainViewModel;

    private void OnAssetDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: AssetViewModel asset } || Main is not { } main)
        {
            return;
        }

        // Le double-clic ouvre l'élément dans son onglet (éditeur nodal pour un graphe).
        ICommand command = main.OpenAssetEditorCommand;
        command.Execute(asset);
        e.Handled = true;
    }

    /// <summary>Les listes ne défilent pas elles-mêmes : la molette fait défiler toute la page.</summary>
    private void OnListMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Page.ScrollToVerticalOffset(Page.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    /// <summary>Le bouton Importer ouvre son menu sous lui (le DataContext suit celui du bouton).</summary>
    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.DataContext = button.DataContext;
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    /// <summary>Le clic droit sélectionne aussi l'élément, pour que le menu agisse sur lui.</summary>
    private void OnAssetRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    // ----- Dossiers -----

    private void OnFolderDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: FolderItemViewModel folder } && Main?.Project is { } project)
        {
            project.CurrentFolder = folder.Path;
            e.Handled = true;
        }
    }

    /// <summary>Retour arrière (hors d'un champ de saisie) remonte au dossier parent.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back && Keyboard.FocusedElement is not TextBoxBase && Main?.Project is { IsInFolder: true } project)
        {
            project.OpenParentFolderCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ----- Glisser-déposer -----

    private void OnAssetMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragItem = sender as ListBoxItem;
    }

    /// <summary>Au-delà du seuil de Windows, l'élément sous la souris part en glisser-déposer.</summary>
    private void OnAssetMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStart is not { } start || _dragItem?.DataContext is not AssetViewModel asset)
        {
            return;
        }

        Vector moved = e.GetPosition(this) - start;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStart = null;
        DragDrop.DoDragDrop(_dragItem, new DataObject(AssetDragFormat, asset), DragDropEffects.Move);
    }

    /// <summary>Destination d'un dépôt : le dossier sous la souris, ou le segment du fil d'Ariane.</summary>
    private static string? DropTarget(object sender) => sender switch
    {
        ListBoxItem { DataContext: FolderItemViewModel folder } => folder.Path,
        Button { Tag: string path } => path,
        _ => null,
    };

    private void OnFolderDragOver(object sender, DragEventArgs e)
    {
        bool accepted = e.Data.GetData(AssetDragFormat) is AssetViewModel asset && DropTarget(sender) is { } target && asset.Model.Folder != target;
        e.Effects = accepted ? DragDropEffects.Move : DragDropEffects.None;
        if (sender is ListBoxItem item)
        {
            item.IsSelected = accepted;
        }

        e.Handled = true;
    }

    private void OnFolderDragLeave(object sender, DragEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = false;
        }
    }

    private void OnFolderDrop(object sender, DragEventArgs e)
    {
        OnFolderDragLeave(sender, e);
        if (e.Data.GetData(AssetDragFormat) is AssetViewModel asset && DropTarget(sender) is { } target && Main is { } main)
        {
            main.MoveAssetsToFolder([asset], target);
            e.Handled = true;
        }
    }

    /// <summary>"Déplacer vers" : la racine et tous les dossiers, sauf celui de l'élément.</summary>
    private void OnAssetMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: AssetViewModel asset, ContextMenu: { } menu } || Main is not { Project: { } project } main)
        {
            return;
        }

        MenuItem? move = menu.Items.OfType<MenuItem>().FirstOrDefault(i => Equals(i.Tag, "move"));
        if (move is null)
        {
            return;
        }

        move.Items.Clear();
        foreach (FolderChoice choice in project.FolderChoices)
        {
            var item = new MenuItem { Header = choice.Label, IsEnabled = choice.Path != asset.Model.Folder };
            item.Click += (_, _) => main.MoveAssetsToFolder([asset], choice.Path);
            move.Items.Add(item);
        }
    }
}
