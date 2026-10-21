using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvalonDock.Controls;
using AvalonDock.Layout;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Documents;

namespace MineEngine.Editor.Views;

/// <summary>
/// Right-click menu of the tabs of the work area (V0.6.1): close the tab, the others, those on
/// the right or all of them; save; copy the path; show the file in Windows; open it with another
/// program; select the element in the Dashboard; split the work area into tab groups or float the tab.
/// The permanent tabs (Dashboard, Resources, Languages) only offer what applies to them.
/// </summary>
public partial class MainWindow
{
    private void InitializeTabMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) => FillTabMenu(menu);
        DockManager.DocumentContextMenu = menu;
    }

    /// <summary>The menu is rebuilt each time for the tab it was opened on (its DataContext).</summary>
    private void FillTabMenu(ContextMenu menu)
    {
        menu.Items.Clear();
        if (menu.DataContext is not LayoutDocumentItem { Model: LayoutDocument tab } item)
        {
            menu.IsOpen = false;
            return;
        }

        DocumentViewModel? document = (tab.Content as FrameworkElement)?.DataContext as DocumentViewModel;
        List<LayoutDocument> closable = [.. DockManager.Layout.Descendents().OfType<LayoutDocument>().Where(d => d.CanClose)];
        List<LayoutDocument> right = tab.Parent is LayoutDocumentPane pane
            ? [.. pane.Children.OfType<LayoutDocument>().Skip(pane.Children.IndexOf(tab) + 1).Where(d => d.CanClose)]
            : [];

        if (tab.CanClose)
        {
            menu.Items.Add(Item(Tr.T("Close"), () => item.CloseCommand.Execute(null), _viewModel.Shortcuts["file.closeTab"]));
        }

        menu.Items.Add(Item(Tr.T("Close other tabs"), () => CloseTabs(closable.Where(d => d != tab)), enabled: closable.Any(d => d != tab)));
        menu.Items.Add(Item(Tr.T("Close tabs to the right"), () => CloseTabs(right), enabled: right.Count > 0));
        menu.Items.Add(Item(Tr.T("Close all tabs"), () => CloseTabs(closable), enabled: closable.Count > 0));

        if (document is not null)
        {
            menu.Items.Add(new Separator());
            if (document is CodeDocumentViewModel { IsReadOnly: false })
            {
                menu.Items.Add(Item(Tr.T("Save"), () => _viewModel.SaveDocument(document), enabled: document.IsDirty));
            }

            string? path = _viewModel.DocumentFilePath(document);
            menu.Items.Add(Item(Tr.T("Copy the path"), () => Clipboard.SetText(path!), enabled: path is not null));
            menu.Items.Add(Item(Tr.T("Copy the path relative to the project"), () => Clipboard.SetText(document.DisplayPath)));
            menu.Items.Add(Item(Tr.T("Show in the Windows explorer"), () => _viewModel.RevealDocument(document), enabled: path is not null));
            if (document is FileDocumentViewModel file)
            {
                menu.Items.Add(new MenuItem { Header = Tr.T("Open with"), Tag = OpenWithMenu.Tag });
                OpenWithMenu.Fill(menu, file.FullPath, _viewModel);
            }

            if (MainViewModel.DocumentAssetId(document) is not null)
            {
                menu.Items.Add(Item(Tr.T("Select in the Dashboard"), () => _viewModel.SelectDocumentAsset(document)));
            }
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Tr.T("New vertical tab group"), item.NewVerticalTabGroupCommand));
        menu.Items.Add(Item(Tr.T("New horizontal tab group"), item.NewHorizontalTabGroupCommand));
        menu.Items.Add(Item(Tr.T("Move to the next tab group"), item.MoveToNextTabGroupCommand));
        menu.Items.Add(Item(Tr.T("Move to the previous tab group"), item.MoveToPreviousTabGroupCommand));
        if (tab.CanFloat)
        {
            menu.Items.Add(Item(Tr.T("Float in a window"), item.FloatCommand));
        }
    }

    /// <summary>Closes tabs the way their close button does: a modified file asks to be saved first.</summary>
    private void CloseTabs(IEnumerable<LayoutDocument> tabs)
    {
        foreach (LayoutDocument tab in tabs.ToList())
        {
            if (DockManager.GetLayoutItemFromModel(tab) is LayoutDocumentItem item && item.CloseCommand.CanExecute(null))
            {
                item.CloseCommand.Execute(null);
            }
        }
    }

    private static MenuItem Item(string header, Action action, string? gesture = null, bool enabled = true)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture ?? string.Empty, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem Item(string header, ICommand command) => new() { Header = header, Command = command };
}
