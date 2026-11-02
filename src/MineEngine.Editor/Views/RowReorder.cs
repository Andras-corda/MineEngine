using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.Views;

/// <summary>
/// Reorders the rows of a list (items of a creative tab, parameters of a command...) by dragging their
/// handle (an element with <see cref="IsHandleProperty"/>) onto another row of the same list. The rows
/// are <see cref="IReorderableRow"/>; the list is an ItemsControl whose DataContext is an <see cref="IReorderableList"/>.
/// </summary>
public static class RowReorder
{
    private const string Format = "MineEngine.ReorderableRow";

    public static readonly DependencyProperty IsHandleProperty =
        DependencyProperty.RegisterAttached("IsHandle", typeof(bool), typeof(RowReorder), new PropertyMetadata(false, OnIsHandleChanged));

    public static readonly DependencyProperty IsRowProperty =
        DependencyProperty.RegisterAttached("IsRow", typeof(bool), typeof(RowReorder), new PropertyMetadata(false, OnIsRowChanged));

    private static Point? _dragStart;

    public static bool GetIsHandle(DependencyObject element) => (bool)element.GetValue(IsHandleProperty);

    public static void SetIsHandle(DependencyObject element, bool value) => element.SetValue(IsHandleProperty, value);

    public static bool GetIsRow(DependencyObject element) => (bool)element.GetValue(IsRowProperty);

    public static void SetIsRow(DependencyObject element, bool value) => element.SetValue(IsRowProperty, value);

    private static void OnIsHandleChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is UIElement handle && e.NewValue is true)
        {
            handle.PreviewMouseLeftButtonDown += (_, args) => _dragStart = args.GetPosition(null);
            handle.PreviewMouseMove += OnHandleMouseMove;
        }
    }

    private static void OnIsRowChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is UIElement row && e.NewValue is true)
        {
            row.AllowDrop = true;
            row.DragOver += OnRowDragOver;
            row.Drop += OnRowDrop;
        }
    }

    private static void OnHandleMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStart is not { } start
            || sender is not FrameworkElement { DataContext: IReorderableRow entry } handle)
        {
            return;
        }

        Vector moved = e.GetPosition(null) - start;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStart = null;
        DragDrop.DoDragDrop(handle, new DataObject(Format, entry), DragDropEffects.Move);
    }

    private static void OnRowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetData(Format) is IReorderableRow dragged && Owner(sender) is { } list && list.Items.Contains(dragged)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnRowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(Format) is IReorderableRow dragged && sender is FrameworkElement { DataContext: IReorderableRow target }
            && Owner(sender) is { } list && list.Items.Contains(dragged) && list.Items.Contains(target)
            && list.DataContext is IReorderableList owner)
        {
            owner.MoveItem(dragged.Index, target.Index);
            e.Handled = true;
        }
    }

    /// <summary>The list (ItemsControl of an <see cref="IReorderableList"/>) that shows the row.</summary>
    private static ItemsControl? Owner(object row)
    {
        for (DependencyObject? current = row as DependencyObject; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ItemsControl { DataContext: IReorderableList } list)
            {
                return list;
            }
        }

        return null;
    }
}
