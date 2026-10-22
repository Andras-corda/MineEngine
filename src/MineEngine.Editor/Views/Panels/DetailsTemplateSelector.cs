using System.Windows;
using System.Windows.Controls;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.Views.Panels;

/// <summary>
/// Template of the content of Properties: an element selected in the Dashboard shows a summary
/// (it is edited in its tab); other targets (open file, graph, tab of an element) use their own template.
/// </summary>
public sealed class DetailsTemplateSelector : DataTemplateSelector
{
    public DataTemplate? AssetTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is AssetViewModel ? AssetTemplate : null;
}
