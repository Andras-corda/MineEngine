using System.Windows.Controls;
using System.Windows.Data;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Creation;

namespace MineEngine.Editor.Views;

/// <summary>
/// The Add menus (V0.7.14): "New element..." then the 9 categories of <see cref="NewElementCatalog"/>.
/// Each choice opens the creation window on it; the planned ones are shown greyed.
/// </summary>
public static class NewElementMenu
{
    /// <summary>Puts the creation items at the top of <paramref name="menu"/>, before what it already holds.</summary>
    public static void Fill(ItemsControl menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        int index = 0;
        var all = new MenuItem
        {
            Header = Tr.T("New element..."),
            Icon = new IconView { Icon = "Glyph.NewItem", IconSize = 14 },
            ToolTip = Tr.T("Window with everything that can be created, the name and the templates"),
        };
        all.SetBinding(MenuItem.CommandProperty, new Binding(nameof(MainViewModel.NewElementCommand)));
        menu.Items.Insert(index++, all);
        menu.Items.Insert(index++, new Separator());

        foreach (NewElementCategory category in NewElementCatalog.Categories)
        {
            // A category with a single choice (particles, AI, folder) is that choice.
            if (category.Entries.Count == 1)
            {
                menu.Items.Insert(index++, Item(category.Entries[0], category.Title + "..."));
                continue;
            }

            var group = new MenuItem { Header = category.Title, Icon = new IconView { Icon = category.Icon, IconSize = 14 } };
            foreach (NewElementEntry entry in category.Entries)
            {
                group.Items.Add(Item(entry, entry.IsAvailable ? entry.Title + "..." : entry.Label));
            }

            menu.Items.Insert(index++, group);
        }
    }

    private static MenuItem Item(NewElementEntry entry, string header)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = new IconView { Icon = entry.Icon, IconSize = 14 },
            ToolTip = entry.Description,
        };
        if (entry.IsAvailable)
        {
            item.SetBinding(MenuItem.CommandProperty, new Binding(nameof(MainViewModel.NewElementOfCommand)));
            item.CommandParameter = entry;
        }
        else
        {
            item.IsEnabled = false;
            ToolTipService.SetShowOnDisabled(item, true);
        }

        return item;
    }
}
