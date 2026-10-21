using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MineEngine.Editor.ViewModels.Puppeteer;

namespace MineEngine.Editor.Views.Puppeteer;

/// <summary>Menus shared by the Puppeteer canvas and its Inspector part.</summary>
internal static class PuppeteerMenus
{
    /// <summary>One entry per kind of piece; a separator between the conditions and the modifiers.</summary>
    public static IEnumerable<Control> AttachmentItems(BtNodeViewModel node, IReadOnlyList<BtChoiceViewModel> choices)
    {
        bool? conditions = null;
        foreach (BtChoiceViewModel choice in choices)
        {
            if (conditions is { } previous && previous != choice.Info.IsCondition)
            {
                yield return new Separator();
            }

            conditions = choice.Info.IsCondition;
            yield return Item(choice.Title, () => node.Document.AddAttachment(node, choice.Kind), toolTip: choice.Description);
        }
    }

    public static MenuItem Item(string header, Action action, string? gesture = null, string? toolTip = null)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture ?? string.Empty, ToolTip = toolTip };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Opens a choice menu under a button.</summary>
    public static void ShowMenuUnder(FrameworkElement button, IEnumerable<Control> items)
    {
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        foreach (Control item in items)
        {
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }
}
