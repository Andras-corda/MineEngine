using System.Windows;
using System.Windows.Controls;
using MineEngine.Assets.Puppeteer;
using MineEngine.Editor.ViewModels.Puppeteer;

namespace MineEngine.Editor.Views.Puppeteer;

/// <summary>Puppeteer part of the Inspector: details of the selected element and blackboard (DataContext: the Puppeteer tab).</summary>
public partial class BehaviorTreeDetailsPanel : UserControl
{
    public BehaviorTreeDetailsPanel()
    {
        InitializeComponent();
    }

    private BehaviorTreeDocumentViewModel? Document => DataContext as BehaviorTreeDocumentViewModel;

    private void OnAddDecoratorClick(object sender, RoutedEventArgs e)
    {
        if (Document?.SelectedNode is { } node)
        {
            PuppeteerMenus.ShowMenuUnder((FrameworkElement)sender, PuppeteerMenus.AttachmentItems(node, Document.DecoratorChoices));
        }
    }

    private void OnAddServiceClick(object sender, RoutedEventArgs e)
    {
        if (Document?.SelectedNode is { } node)
        {
            PuppeteerMenus.ShowMenuUnder((FrameworkElement)sender, PuppeteerMenus.AttachmentItems(node, Document.ServiceChoices));
        }
    }

    private void OnAddKeyClick(object sender, RoutedEventArgs e)
    {
        if (Document is { } document)
        {
            PuppeteerMenus.ShowMenuUnder((FrameworkElement)sender, Enum.GetValues<BlackboardKeyType>().Select(t =>
                PuppeteerMenus.Item(BtCatalog.KeyTypeLabel(t), () => document.AddKey(t))));
        }
    }

    private void OnRemoveKeyClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BlackboardKeyViewModel key })
        {
            Document?.RemoveKey(key);
        }
    }

    private void OnSelectAttachmentClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BtAttachmentViewModel attachment })
        {
            Document?.Select(attachment);
        }
    }

    private void OnRemoveAttachmentClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BtAttachmentViewModel attachment })
        {
            Document?.RemoveAttachment(attachment);
        }
    }

    private void OnMoveAttachmentUpClick(object sender, RoutedEventArgs e)
    {
        if (Document?.SelectedAttachment is { } attachment)
        {
            Document.MoveAttachment(attachment, -1);
        }
    }

    private void OnMoveAttachmentDownClick(object sender, RoutedEventArgs e)
    {
        if (Document?.SelectedAttachment is { } attachment)
        {
            Document.MoveAttachment(attachment, 1);
        }
    }

    private void OnRemoveSelectedAttachmentClick(object sender, RoutedEventArgs e)
    {
        if (Document?.SelectedAttachment is { } attachment)
        {
            Document.RemoveAttachment(attachment);
        }
    }

    private void OnSelectOwnerClick(object sender, RoutedEventArgs e)
    {
        if (Document?.SelectedAttachment is { } attachment)
        {
            Document.Show(attachment.Owner.Id);
        }
    }
}
