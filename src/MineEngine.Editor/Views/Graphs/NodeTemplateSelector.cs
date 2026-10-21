using System.Windows;
using System.Windows.Controls;
using MineEngine.Editor.ViewModels.Graphs;

namespace MineEngine.Editor.Views.Graphs;

/// <summary>
/// Un nœud est dessiné en carte ; une redirection, en simple point sur le fil ; un commentaire, en
/// cadre titré ; une info-bulle, en petite bulle.
/// </summary>
public sealed class NodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? NodeTemplate { get; set; }

    public DataTemplate? RerouteTemplate { get; set; }

    public DataTemplate? CommentTemplate { get; set; }

    public DataTemplate? TooltipTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        GraphNodeViewModel { IsReroute: true } => RerouteTemplate,
        GraphNodeViewModel { IsComment: true } => CommentTemplate,
        GraphNodeViewModel { IsTooltip: true } => TooltipTemplate,
        _ => NodeTemplate,
    };
}
