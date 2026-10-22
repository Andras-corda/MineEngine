using System.Windows;
using System.Windows.Controls;
using MineEngine.Editor.ViewModels.Puppeteer;

namespace MineEngine.Editor.Views.Puppeteer;

/// <summary>A node of the tree is drawn as a card; a comment as a titled frame; a tooltip as a small bubble.</summary>
public sealed class BtNodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? NodeTemplate { get; set; }

    public DataTemplate? CommentTemplate { get; set; }

    public DataTemplate? TooltipTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        BtNodeViewModel { IsComment: true } => CommentTemplate,
        BtNodeViewModel { IsTooltip: true } => TooltipTemplate,
        _ => NodeTemplate,
    };
}
