using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MineEngine.Editor.Views;

/// <summary>
/// Wrap panel for tiles of the same size that only creates the tiles in view, so a list of
/// thousands of textures (the asset browser) opens and scrolls at once.
/// </summary>
public sealed class VirtualizingTilePanel : VirtualizingPanel, IScrollInfo
{
    private const double WheelStep = 48;

    private Size _itemSize = new(96, 120);
    private Size _extent;
    private Size _viewport;
    private Point _offset;

    public ScrollViewer? ScrollOwner { get; set; }

    public bool CanHorizontallyScroll { get; set; }

    public bool CanVerticallyScroll { get; set; }

    public double ExtentWidth => _extent.Width;

    public double ExtentHeight => _extent.Height;

    public double ViewportWidth => _viewport.Width;

    public double ViewportHeight => _viewport.Height;

    public double HorizontalOffset => 0;

    public double VerticalOffset => _offset.Y;

    protected override Size MeasureOverride(Size availableSize)
    {
        // Touching InternalChildren first makes the generator usable.
        _ = InternalChildren;
        ItemsControl? owner = ItemsControl.GetItemsOwner(this);
        int count = owner?.Items.Count ?? 0;
        double width = double.IsInfinity(availableSize.Width) ? _itemSize.Width : availableSize.Width;
        int columns = Columns(width);
        int rows = (count + columns - 1) / columns;
        double viewportHeight = double.IsInfinity(availableSize.Height) ? rows * _itemSize.Height : availableSize.Height;

        _viewport = new Size(width, viewportHeight);
        _extent = new Size(width, rows * _itemSize.Height);
        _offset.Y = Math.Max(0, Math.Min(_offset.Y, _extent.Height - _viewport.Height));

        int first = count == 0 ? 0 : (int)(_offset.Y / _itemSize.Height) * columns;
        int last = count == 0 ? -1 : Math.Min(count - 1, ((int)Math.Ceiling((_offset.Y + _viewport.Height) / _itemSize.Height) * columns) + columns - 1);
        Size measured = Realize(first, last);
        Clean(first, last);

        if (measured.Width > 0 && measured.Height > 0 && measured != _itemSize)
        {
            // The real size of a tile is known once one is measured: lay out again with it.
            _itemSize = measured;
            Dispatcher.BeginInvoke(InvalidateMeasure);
        }

        ScrollOwner?.InvalidateScrollInfo();
        return new Size(width, double.IsInfinity(availableSize.Height) ? _extent.Height : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        IItemContainerGenerator generator = ItemContainerGenerator;
        int columns = Columns(finalSize.Width);
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            int index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            var origin = new Point(index % columns * _itemSize.Width, (index / columns * _itemSize.Height) - _offset.Y);
            InternalChildren[i].Arrange(new Rect(origin, _itemSize));
        }

        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Reset:
                _offset.Y = 0;
                break;
        }
    }

    /// <summary>Creates the tiles from first to last; returns the largest size they ask for.</summary>
    private Size Realize(int first, int last)
    {
        if (last < first)
        {
            return Size.Empty;
        }

        IItemContainerGenerator generator = ItemContainerGenerator;
        GeneratorPosition start = generator.GeneratorPositionFromIndex(first);
        int childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
        double widest = 0;
        double tallest = 0;
        using (generator.StartAt(start, GeneratorDirection.Forward, true))
        {
            for (int index = first; index <= last; index++, childIndex++)
            {
                var child = (UIElement)generator.GenerateNext(out bool isNew);
                if (isNew)
                {
                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else
                    {
                        InsertInternalChild(childIndex, child);
                    }

                    generator.PrepareItemContainer(child);
                }

                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                widest = Math.Max(widest, child.DesiredSize.Width);
                tallest = Math.Max(tallest, child.DesiredSize.Height);
            }
        }

        return new Size(widest, tallest);
    }

    /// <summary>Removes the tiles out of view.</summary>
    private void Clean(int first, int last)
    {
        IItemContainerGenerator generator = ItemContainerGenerator;
        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var position = new GeneratorPosition(i, 0);
            int index = generator.IndexFromGeneratorPosition(position);
            if (index < first || index > last)
            {
                generator.Remove(position, 1);
                RemoveInternalChildRange(i, 1);
            }
        }
    }

    private int Columns(double width) => Math.Max(1, (int)(width / _itemSize.Width));

    public void SetVerticalOffset(double offset)
    {
        double clamped = Math.Max(0, Math.Min(offset, _extent.Height - _viewport.Height));
        if (clamped != _offset.Y)
        {
            _offset.Y = clamped;
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }
    }

    public void SetHorizontalOffset(double offset)
    {
    }

    public void LineUp() => SetVerticalOffset(VerticalOffset - (_itemSize.Height / 3));

    public void LineDown() => SetVerticalOffset(VerticalOffset + (_itemSize.Height / 3));

    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);

    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);

    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - WheelStep);

    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + WheelStep);

    public void LineLeft()
    {
    }

    public void LineRight()
    {
    }

    public void PageLeft()
    {
    }

    public void PageRight()
    {
    }

    public void MouseWheelLeft()
    {
    }

    public void MouseWheelRight()
    {
    }

    /// <summary>Scrolls so that a tile (the one selected with the keyboard) is in view.</summary>
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            if (InternalChildren[i] == visual || InternalChildren[i].IsAncestorOf(visual))
            {
                int index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
                double top = index / Columns(_viewport.Width) * _itemSize.Height;
                if (top < _offset.Y)
                {
                    SetVerticalOffset(top);
                }
                else if (top + _itemSize.Height > _offset.Y + _viewport.Height)
                {
                    SetVerticalOffset(top + _itemSize.Height - _viewport.Height);
                }

                break;
            }
        }

        return rectangle;
    }

    /// <summary>Keyboard navigation to a tile not created yet.</summary>
    protected override void BringIndexIntoView(int index)
    {
        SetVerticalOffset(index / Columns(_viewport.Width) * _itemSize.Height);
    }
}
