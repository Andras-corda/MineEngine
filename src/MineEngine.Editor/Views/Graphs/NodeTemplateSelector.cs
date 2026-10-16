using System.Windows;
using System.Windows.Controls;
using MineEngine.Editor.ViewModels.Graphs;

namespace MineEngine.Editor.Views.Graphs;

/// <summary>Un nœud est dessiné en carte ; une redirection, en simple point sur le fil.</summary>
public sealed class NodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? NodeTemplate { get; set; }

    public DataTemplate? RerouteTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is GraphNodeViewModel { IsReroute: true } ? RerouteTemplate : NodeTemplate;
}
