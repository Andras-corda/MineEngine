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

    /// <summary>Clicked element of a multiple selection: it is selected alone on release, unless dragged.</summary>
    private ListBoxItem? _pendingSingle;

    public static readonly DependencyProperty WideColumnProperty =
        DependencyProperty.Register(nameof(WideColumn), typeof(GridLength), typeof(DashboardPanel), new PropertyMetadata(new GridLength(150)));

    public static readonly DependencyProperty TileScaleProperty =
        DependencyProperty.Register(nameof(TileScale), typeof(double), typeof(DashboardPanel), new PropertyMetadata(1.0));

    private const double MinTileScale = 0.6;
    private const double MaxTileScale = 2.5;

    /// <summary>Largeur des colonnes "État" et "Utilisé par" ; nulle quand le panneau est étroit.</summary>
    private const double CompactWidth = 640;

    public DashboardPanel()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            bool compact = e.NewSize.Width < CompactWidth;
            WideColumn = compact ? new GridLength(0) : new GridLength(150);
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public GridLength WideColumn
    {
        get => (GridLength)GetValue(WideColumnProperty);
        set => SetValue(WideColumnProperty, value);
    }

    /// <summary>Size of the tiles of the grid view (Ctrl+wheel).</summary>
    public double TileScale
    {
        get => (double)GetValue(TileScaleProperty);
        set => SetValue(TileScaleProperty, value);
    }

    private MainViewModel? Main => DataContext as MainViewModel;

    /// <summary>Ctrl+wheel zooms the tiles in or out, like the Windows Explorer.</summary>
    private void OnPageMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || Main?.Project is not { IsTileView: true })
        {
            return;
        }

        TileScale = Math.Clamp(Math.Round(TileScale * (e.Delta > 0 ? 1.1 : 1 / 1.1), 2), MinTileScale, MaxTileScale);
        e.Handled = true;
    }

    /// <summary>The selection of the visible list (list or tiles) becomes the one of the project.</summary>
    private void OnAssetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // The hidden list follows SelectedAsset alone: it must not reduce the selection.
        if (sender is ListBox { IsVisible: true } list && Main?.Project is { } project)
        {
            project.SelectedAssets = [.. list.SelectedItems.OfType<AssetViewModel>().OrderBy(a => list.Items.IndexOf(a))];
        }
    }

    /// <summary>Elements a drag or a menu acts on: the whole selection if the element is part of it.</summary>
    private IReadOnlyList<AssetViewModel> TargetsOf(AssetViewModel asset) =>
        Main?.Project?.SelectedAssets is { } selected && selected.Contains(asset) ? selected : [asset];

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

    /// <summary>
    /// Le clic droit sélectionne aussi l'élément, pour que le menu agisse sur lui ; sur un élément
    /// d'une sélection multiple, la sélection reste (le menu agit sur toute la sélection).
    /// </summary>
    private void OnAssetRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { IsSelected: false } item && ItemsControl.ItemsControlFromItemContainer(item) is ListBox list)
        {
            list.SelectedItems.Clear();
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

    /// <summary>Retour arrière (hors d'un champ de saisie) remonte au dossier parent ; F2 renames the selected element.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back && Keyboard.FocusedElement is not TextBoxBase && Main?.Project is { IsInFolder: true } project)
        {
            project.OpenParentFolderCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F2 && Keyboard.FocusedElement is not TextBoxBase && Main is { } main && main.RenameAssetCommand.CanExecute(null))
        {
            main.RenameAssetCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ----- Glisser-déposer -----

    private void OnAssetMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragItem = sender as ListBoxItem;
        _pendingSingle = null;

        // A plain click on an element of a multiple selection keeps it, so that it can be dragged.
        if (e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.None && _dragItem is { IsSelected: true } item
            && ItemsControl.ItemsControlFromItemContainer(item) is ListBox { SelectedItems.Count: > 1 })
        {
            _pendingSingle = item;
            item.Focus();
            e.Handled = true;
        }
    }

    /// <summary>Released without dragging: the clicked element is now selected alone.</summary>
    private void OnAssetMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_pendingSingle is { } item && ReferenceEquals(sender, item) && ItemsControl.ItemsControlFromItemContainer(item) is ListBox list)
        {
            list.SelectedItems.Clear();
            item.IsSelected = true;
        }

        _pendingSingle = null;
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
        _pendingSingle = null;
        DragDrop.DoDragDrop(_dragItem, new DataObject(AssetDragFormat, TargetsOf(asset)), DragDropEffects.Move);
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
        bool accepted = e.Data.GetData(AssetDragFormat) is IReadOnlyList<AssetViewModel> assets && DropTarget(sender) is { } target
                        && assets.Any(a => a.Model.Folder != target);
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
        if (e.Data.GetData(AssetDragFormat) is IReadOnlyList<AssetViewModel> assets && DropTarget(sender) is { } target && Main is { } main)
        {
            main.MoveAssetsToFolder(assets, target);
            e.Handled = true;
        }
    }

    /// <summary>
    /// The menu of the page needs a project. On an element or a folder, WPF opens their own menu (the
    /// nearest one); this event bubbles up to the page from them too, so it must not be cancelled then.
    /// </summary>
    private void OnPageMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (Main?.Project is null)
        {
            e.Handled = true;
        }
    }

    /// <summary>"Open the Java code in": the programs of the settings that open Java files; hidden without any.</summary>
    private static void FillJavaPrograms(ContextMenu menu, MainViewModel main)
    {
        if (menu.Items.OfType<MenuItem>().FirstOrDefault(i => Equals(i.Tag, "javaWith")) is not { } javaWith)
        {
            return;
        }

        javaWith.Items.Clear();
        foreach (Settings.ExternalProgram program in main.JavaPrograms)
        {
            var item = new MenuItem { Header = program.Name, ToolTip = program.Path, IsEnabled = program.Exists && main.CanEdit };
            item.Click += (_, _) => main.OpenAssetJavaWith(program);
            javaWith.Items.Add(item);
        }

        javaWith.Items.Add(new Separator());
        javaWith.Items.Add(new MenuItem { Header = Tr.T("Configure programs..."), Command = main.OpenSettingsCommand });
    }

    /// <summary>"Déplacer vers" : la racine et tous les dossiers, sauf celui de l'élément.</summary>
    private void OnAssetMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: AssetViewModel asset, ContextMenu: { } menu } || Main is not { Project: { } project } main)
        {
            return;
        }

        OpenWithMenu.Fill(menu, main.AssetFile(asset), main);
        FillJavaPrograms(menu, main);
        MenuItem? move = menu.Items.OfType<MenuItem>().FirstOrDefault(i => Equals(i.Tag, "move"));
        if (move is null)
        {
            return;
        }

        move.Items.Clear();
        foreach (FolderChoice choice in project.FolderChoices)
        {
            IReadOnlyList<AssetViewModel> targets = TargetsOf(asset);
            var item = new MenuItem { Header = choice.Label, IsEnabled = targets.Any(a => a.Model.Folder != choice.Path) };
            item.Click += (_, _) => main.MoveAssetsToFolder(targets, choice.Path);
            move.Items.Add(item);
        }
    }
}
