using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MineEngine.Editor.ViewModels.Graphs;

namespace MineEngine.Editor.Views.Panels;

/// <summary>Nodes window: a node is dragged onto the graph, or added by a double-click.</summary>
public partial class NodeLibraryPanel : UserControl
{
    private Point _dragStart;
    private NodeLibraryItem? _pressed;

    public NodeLibraryPanel()
    {
        InitializeComponent();
    }

    // Below this width, the domains go above the nodes so that the blocks keep a readable width.
    private const double NarrowWidth = 360;

    private NodeLibraryViewModel? Library => DataContext as NodeLibraryViewModel;

    private void OnBodySizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = e.NewSize.Width < NarrowWidth;
        if (narrow == (Grid.GetRow(GroupList) == 0))
        {
            return;
        }

        Grid.SetRow(GroupList, narrow ? 0 : 1);
        Grid.SetColumnSpan(GroupList, narrow ? 2 : 1);
        Grid.SetColumn(NodeScroller, narrow ? 0 : 1);
        Grid.SetColumnSpan(NodeScroller, narrow ? 2 : 1);
        NodeScroller.Margin = narrow ? new Thickness(0, 6, 0, 0) : new Thickness(6, 0, 0, 0);
        GroupList.ItemsPanel = narrow ? WrapPanelTemplate : StackPanelTemplate;
    }

    private static readonly ItemsPanelTemplate WrapPanelTemplate = new(new FrameworkElementFactory(typeof(WrapPanel)));

    private static readonly ItemsPanelTemplate StackPanelTemplate = new(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));

    private void OnNodeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NodeLibraryItem item })
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            _pressed = null;
            Library?.Add(item);
            e.Handled = true;
            return;
        }

        _pressed = item;
        _dragStart = e.GetPosition(this);
    }

    private void OnNodeMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed is null || e.LeftButton != MouseButtonState.Pressed)
        {
            _pressed = null;
            return;
        }

        Vector moved = e.GetPosition(this) - _dragStart;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        NodeLibraryItem item = _pressed;
        _pressed = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(NodeLibraryViewModel.DragFormat, item.Payload), DragDropEffects.Copy);
    }
}
