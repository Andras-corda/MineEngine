using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MineEngine.Assets.Puppeteer;
using MineEngine.Editor.ViewModels.Puppeteer;
using MineEngine.Editor.ViewModels.Graphs;

namespace MineEngine.Editor.Views.Puppeteer;

/// <summary>
/// Puppeteer gestures: move the view (right-drag or pressed wheel), zoom (wheel), move
/// nodes (header or attached piece), drag a link from a pin, select with a rectangle,
/// open the add menu or the menu of a node. Every change of the tree is handed to the
/// ViewModel; the view only handles the camera and the gestures in progress.
/// </summary>
public partial class BehaviorTreeView : UserControl
{
    private const double MinZoom = 0.25;
    private const double MaxZoom = 2;
    private const double GridSize = 32;
    private const double DragThreshold = 4;

    private enum Gesture
    {
        None,
        PanPending,
        Pan,
        MoveNodes,
        Link,
        Marquee,
        ResizeComment,
    }

    private Gesture _gesture;
    private Point _gestureStart;
    private Point _lastPosition;
    private Dictionary<BtNodeViewModel, Point> _moveOrigins = [];
    private (BtNodeViewModel Node, bool FromChildrenPin)? _linkSource;
    private object? _rightClickTarget;
    private Point _paletteWorld;
    private BehaviorTreeDocumentViewModel? _document;
    private bool _framed;
    private BtNodeViewModel? _resized;
    private Rect _resizeOrigin;
    private string _resizeEdges = string.Empty;

    public BehaviorTreeView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as BehaviorTreeDocumentViewModel);
        KeyDown += OnKeyDown;
        Loaded += (_, _) =>
        {
            UpdateGrid();
            Dispatcher.BeginInvoke(() =>
            {
                if (!_framed)
                {
                    FrameAll();
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        };
    }

    private BehaviorTreeDocumentViewModel? Document => _document;

    private double Scale => Zoom.ScaleX;

    /// <summary>Gives the focus to the canvas (activated tab) for keyboard shortcuts.</summary>
    public void FocusCanvas() => Keyboard.Focus(this);

    // ----- Camera -----

    private Point ToWorld(Point screen) => new((screen.X - Pan.X) / Scale, (screen.Y - Pan.Y) / Scale);

    private void SetCamera(double scale, double x, double y)
    {
        Zoom.ScaleX = Zoom.ScaleY = Math.Clamp(scale, MinZoom, MaxZoom);
        Pan.X = x;
        Pan.Y = y;
        ZoomLabel.Text = string.Create(CultureInfo.CurrentCulture, $"{Scale * 100:0} %");
        UpdateGrid();
        UpdateViewCenter();
    }

    /// <summary>Tells the document which point of the tree is at the center of the view (nodes added from the Nodes window).</summary>
    private void UpdateViewCenter()
    {
        if (Document is not null && Root.ActualWidth > 0)
        {
            Document.ViewCenter = ToWorld(new Point(Root.ActualWidth / 2, Root.ActualHeight / 2));
        }
    }

    // ----- Nodes window -----

    /// <summary>Node under a point of the canvas (a decorator or a service gives its node), or null.</summary>
    private static BtNodeViewModel? NodeAt(object? source)
    {
        for (DependencyObject? element = source as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            switch ((element as FrameworkElement)?.DataContext)
            {
                case BtNodeViewModel node:
                    return node;
                case BtAttachmentViewModel attachment:
                    return attachment.Owner;
            }

            if (element is FrameworkElement { Name: "Root" })
            {
                break;
            }
        }

        return null;
    }

    private void OnRootDragOver(object sender, DragEventArgs e)
    {
        e.Effects = Document is not null && e.Data.GetData(NodeLibraryViewModel.DragFormat) is BtChoiceViewModel choice
                    && (choice.Category is not (BtCategory.Decorator or BtCategory.Service) || NodeAt(e.OriginalSource) is { IsRoot: false })
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    // A composite or a task is placed under the pointer; a decorator or a service goes on the node under it.
    private void OnRootDrop(object sender, DragEventArgs e)
    {
        if (Document is null || e.Data.GetData(NodeLibraryViewModel.DragFormat) is not BtChoiceViewModel choice)
        {
            return;
        }

        if (choice.Category is BtCategory.Decorator or BtCategory.Service)
        {
            if (NodeAt(e.OriginalSource) is { } node)
            {
                Document.AddAttachment(node, choice.Kind);
            }
        }
        else
        {
            Point world = ToWorld(e.GetPosition(Root));
            Document.Pending = null;
            Document.AddNode(choice, new Point(world.X - 20, world.Y - 14));
        }

        Keyboard.Focus(this);
        e.Handled = true;
    }

    private double SnapSize => TryFindResource(Services.WpfInterfaceService.GraphSnapToGridKey) is true ? GridSize : 0;

    private void UpdateGrid()
    {
        double size = GridSize * Scale;
        var line = new Pen(TryFindResource("PreviewGridBrush") as Brush ?? Brushes.Gray, 1);
        line.Freeze();
        var drawing = new GeometryDrawing(
            null, line, new GeometryGroup { Children = { new LineGeometry(new Point(0, 0), new Point(size, 0)), new LineGeometry(new Point(0, 0), new Point(0, size)) } });
        GridLayer.Fill = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, size, size),
            Viewport = new Rect(Pan.X % size, Pan.Y % size, size, size),
            Opacity = Scale < 0.5 ? 0.5 : 1,
        };
    }

    /// <summary>Frames the whole tree.</summary>
    private void FrameAll()
    {
        if (Document is null || Root.ActualWidth <= 0)
        {
            return;
        }

        _framed = true;
        List<Rect> bounds = [.. Document.Nodes.Select(n => n.Bounds)];
        if (bounds.Count == 0)
        {
            SetCamera(1, Root.ActualWidth / 2 - 115, 60);
            return;
        }

        Rect all = bounds.Aggregate(Rect.Union);
        const double margin = 60;
        double scale = Math.Min(1, Math.Min(
            (Root.ActualWidth - 2 * margin) / Math.Max(all.Width, 1),
            (Root.ActualHeight - 2 * margin) / Math.Max(all.Height, 1)));
        scale = Math.Clamp(scale, MinZoom, 1);
        SetCamera(
            scale,
            (Root.ActualWidth - all.Width * scale) / 2 - all.X * scale,
            (Root.ActualHeight - all.Height * scale) / 2 - all.Y * scale);
    }

    private void CenterOn(BtNodeViewModel node)
    {
        Rect bounds = node.Bounds;
        SetCamera(
            Scale,
            Root.ActualWidth / 2 - (bounds.X + bounds.Width / 2) * Scale,
            Root.ActualHeight / 2 - (bounds.Y + bounds.Height / 2) * Scale);
    }

    // ----- Document -----

    private void Attach(BehaviorTreeDocumentViewModel? document)
    {
        if (_document is not null)
        {
            _document.FocusRequested -= OnFocusRequested;
        }

        _document = document;
        if (_document is not null)
        {
            _document.FocusRequested += OnFocusRequested;
        }

        UpdateViewCenter();
    }

    private void OnFocusRequested(object? sender, Guid id)
    {
        if (Document?.Nodes.FirstOrDefault(n => n.Id == id) is { } node)
        {
            Dispatcher.BeginInvoke(() => CenterOn(node), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnNodeLoaded(object sender, RoutedEventArgs e) => Measure((FrameworkElement)sender);

    private void OnNodeSizeChanged(object sender, SizeChangedEventArgs e) => Measure((FrameworkElement)sender);

    /// <summary>The displayed size of the node is used to attach the links to the middle of its edges.</summary>
    private static void Measure(FrameworkElement nodeRoot)
    {
        if (nodeRoot.DataContext is BtNodeViewModel node && nodeRoot.ActualWidth > 0)
        {
            node.SetSize(nodeRoot.ActualWidth, nodeRoot.ActualHeight);
        }
    }

    /// <summary>First element tagged <paramref name="tag"/> going up from <paramref name="source"/>.</summary>
    private FrameworkElement? FindTagged(DependencyObject? source, string tag)
    {
        while (source is not null && !ReferenceEquals(source, Root))
        {
            if (source is FrameworkElement { Tag: string value } element && value == tag)
            {
                return element;
            }

            source = source is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    private BtNodeViewModel? NodeAt(Point screen) =>
        FindTagged(VisualTreeHelper.HitTest(Root, screen)?.VisualHit, "node")?.DataContext as BtNodeViewModel;

    // ----- Souris -----

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Document is null || IsInPalette(e.OriginalSource as DependencyObject))
        {
            return;
        }

        ClosePalette();
        Keyboard.Focus(this);
        Point position = e.GetPosition(Root);
        var source = e.OriginalSource as DependencyObject;

        // A text box of the canvas (title of a comment being edited) keeps the mouse.
        if (FindAncestor<TextBoxBase>(source) is not null)
        {
            return;
        }

        if (ResizeHandle(source) is ({ IsComment: true } resized, string edges))
        {
            Document.SelectOnly(resized);
            _resized = resized;
            _resizeOrigin = new Rect(resized.X, resized.Y, resized.CommentWidth, resized.CommentHeight);
            _resizeEdges = edges;
            Begin(Gesture.ResizeComment, position);
            e.Handled = true;
            return;
        }

        // Double-click on the title of a comment: it is edited on the canvas.
        if (e.ClickCount == 2 && FindTagged(source, "header") is { DataContext: BtNodeViewModel { IsComment: true } edited } commentHeader)
        {
            Document.SelectOnly(edited);
            EditCommentTitle(edited, commentHeader);
            e.Handled = true;
            return;
        }

        // Pin: drag a link, or detach with Alt.
        foreach ((string tag, bool fromChildrenPin) in new[] { ("pin-out", true), ("pin-in", false) })
        {
            if (FindTagged(source, tag)?.DataContext is BtNodeViewModel pinNode)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    if (fromChildrenPin)
                    {
                        Document.DisconnectChildren(pinNode);
                    }
                    else
                    {
                        Document.DisconnectParent(pinNode);
                    }
                }
                else
                {
                    _linkSource = (pinNode, fromChildrenPin);
                    Begin(Gesture.Link, position);
                }

                e.Handled = true;
                return;
            }
        }

        // Double-click on a node or an attached piece: its settings, in the Inspector.
        if (e.ClickCount == 2 && (FindTagged(source, "attachment") ?? FindTagged(source, "node")) is not null)
        {
            (Window.GetWindow(this)?.DataContext as ViewModels.MainViewModel)?.ShowInspector();
        }

        // Attached piece: select it; dragging moves its node.
        if (FindTagged(source, "attachment")?.DataContext is BtAttachmentViewModel attachment)
        {
            Document.Select(attachment);
            _moveOrigins = new Dictionary<BtNodeViewModel, Point> { [attachment.Owner] = new(attachment.Owner.X, attachment.Owner.Y) };
            Begin(Gesture.MoveNodes, position);
            e.Handled = true;
            return;
        }

        if (FindTagged(source, "node")?.DataContext is BtNodeViewModel node)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                node.IsSelected = !node.IsSelected;
                Document.OnSelectionChanged();
            }
            else if (!node.IsSelected || Document.SelectedAttachment is not null)
            {
                Document.SelectOnly(node);
            }

            _moveOrigins = Document.SelectedNodes.ToDictionary(n => n, n => new Point(n.X, n.Y));

            // A comment carries the nodes it frames, like in Blueprints.
            foreach (BtNodeViewModel comment in Document.SelectedNodes.Where(n => n.IsComment).ToList())
            {
                Rect frame = comment.Bounds;
                foreach (BtNodeViewModel inside in Document.Nodes.Where(n => !_moveOrigins.ContainsKey(n) && frame.Contains(n.Bounds)))
                {
                    _moveOrigins[inside] = new Point(inside.X, inside.Y);
                }
            }

            Begin(Gesture.MoveNodes, position);
            e.Handled = true;
            return;
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Document.SelectOnly(null);
        }

        Begin(Gesture.Marquee, position);
        e.Handled = true;
    }

    private void OnRootMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInPalette(e.OriginalSource as DependencyObject))
        {
            return;
        }

        ClosePalette();
        Keyboard.Focus(this);
        var source = e.OriginalSource as DependencyObject;
        _rightClickTarget = FindTagged(source, "attachment")?.DataContext ?? FindTagged(source, "node")?.DataContext;
        Begin(Gesture.PanPending, e.GetPosition(Root));
        e.Handled = true;
    }

    private void OnRootMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            Begin(Gesture.Pan, e.GetPosition(Root));
            e.Handled = true;
        }
    }

    private void Begin(Gesture gesture, Point position)
    {
        _gesture = gesture;
        _gestureStart = position;
        _lastPosition = position;
        Root.CaptureMouse();
    }

    private void OnRootMouseMove(object sender, MouseEventArgs e)
    {
        if (_gesture == Gesture.None || Document is null)
        {
            return;
        }

        Point position = e.GetPosition(Root);
        switch (_gesture)
        {
            case Gesture.PanPending when (position - _gestureStart).Length > DragThreshold:
                _gesture = Gesture.Pan;
                goto case Gesture.Pan;

            case Gesture.Pan:
                SetCamera(Scale, Pan.X + position.X - _lastPosition.X, Pan.Y + position.Y - _lastPosition.Y);
                break;

            case Gesture.MoveNodes:
                Vector delta = (position - _gestureStart) / Scale;
                foreach ((BtNodeViewModel node, Point origin) in _moveOrigins)
                {
                    node.X = origin.X + delta.X;
                    node.Y = origin.Y + delta.Y;
                }

                break;

            case Gesture.Link when _linkSource is { } link:
                Point end = ToWorld(position);
                PendingLink.Visibility = Visibility.Visible;
                PendingLink.Data = link.FromChildrenPin
                    ? BtLinkViewModel.Elbow(link.Node.BottomAnchor, end)
                    : BtLinkViewModel.Elbow(end, link.Node.TopAnchor);
                break;

            case Gesture.ResizeComment when _resized is not null:
                Rect resizedFrame = Graphs.CommentFrames.Resized(_resizeOrigin, _resizeEdges, (position - _gestureStart) / Scale);
                _resized.X = resizedFrame.X;
                _resized.Y = resizedFrame.Y;
                _resized.CommentWidth = resizedFrame.Width;
                _resized.CommentHeight = resizedFrame.Height;
                break;

            case Gesture.Marquee:
                Rect rect = new(_gestureStart, position);
                Canvas.SetLeft(Marquee, rect.X);
                Canvas.SetTop(Marquee, rect.Y);
                Marquee.Width = rect.Width;
                Marquee.Height = rect.Height;
                Marquee.Visibility = rect.Width + rect.Height > DragThreshold ? Visibility.Visible : Visibility.Collapsed;
                break;
        }

        _lastPosition = position;
    }

    private void OnRootMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_gesture == Gesture.None || Document is null)
        {
            return;
        }

        Point position = e.GetPosition(Root);
        Gesture gesture = _gesture;
        bool expected = gesture switch
        {
            Gesture.PanPending or Gesture.Pan => e.ChangedButton is MouseButton.Right or MouseButton.Middle,
            _ => e.ChangedButton == MouseButton.Left,
        };
        if (!expected)
        {
            return;
        }

        EndGesture();
        switch (gesture)
        {
            case Gesture.PanPending:
                if (_rightClickTarget is not null)
                {
                    OpenItemMenu(_rightClickTarget);
                }
                else
                {
                    OpenPalette(position, null);
                }

                break;

            case Gesture.MoveNodes:
                Document.CommitMove(SnapSize);
                break;

            case Gesture.ResizeComment when _resized is not null:
                Document.ResizeComment(_resized, SnapSize);
                _resized = null;
                break;

            case Gesture.Link when _linkSource is { } link:
                _linkSource = null;
                if (NodeAt(position) is { } target)
                {
                    if (!ReferenceEquals(target, link.Node))
                    {
                        _ = link.FromChildrenPin ? Document.TryConnect(link.Node, target) : Document.TryConnect(target, link.Node);
                    }
                }
                else if ((position - _gestureStart).Length > DragThreshold * 3)
                {
                    // Link dropped in empty space: the node chosen in the menu will be connected to it.
                    OpenPalette(position, (link.Node, link.FromChildrenPin));
                }

                break;

            case Gesture.Marquee:
                var area = new Rect(ToWorld(_gestureStart), ToWorld(position));
                if (area.Width + area.Height > DragThreshold / Scale)
                {
                    foreach (BtNodeViewModel node in Document.Nodes.Where(n => n.IsComment ? area.Contains(n.Bounds) : n.Bounds.IntersectsWith(area)))
                    {
                        node.IsSelected = true;
                    }

                    Document.OnSelectionChanged();
                }

                break;
        }

        e.Handled = true;
    }

    private void OnRootLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_gesture == Gesture.MoveNodes)
        {
            Document?.CommitMove(SnapSize);
        }

        if (_gesture == Gesture.ResizeComment && _resized is not null)
        {
            Document?.ResizeComment(_resized, SnapSize);
            _resized = null;
        }

        if (_gesture != Gesture.None)
        {
            _gesture = Gesture.None;
            PendingLink.Visibility = Visibility.Collapsed;
            Marquee.Visibility = Visibility.Collapsed;
        }
    }

    private void EndGesture()
    {
        _gesture = Gesture.None;
        PendingLink.Visibility = Visibility.Collapsed;
        Marquee.Visibility = Visibility.Collapsed;
        if (Root.IsMouseCaptured)
        {
            Root.ReleaseMouseCapture();
        }
    }

    private void OnRootMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (IsInPalette(e.OriginalSource as DependencyObject))
        {
            return;
        }

        Point position = e.GetPosition(Root);
        Point world = ToWorld(position);
        double scale = Math.Clamp(Scale * (e.Delta > 0 ? 1.1 : 1 / 1.1), MinZoom, MaxZoom);
        SetCamera(scale, position.X - world.X * scale, position.Y - world.Y * scale);
        e.Handled = true;
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateViewCenter();
        if (!_framed && e.NewSize.Width > 0 && IsLoaded)
        {
            FrameAll();
        }
    }

    // ----- Clavier -----

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Document is null || Keyboard.FocusedElement is TextBoxBase or ComboBox or ComboBoxItem || Palette.IsVisible)
        {
            return;
        }

        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        switch (e.Key)
        {
            case Key.Delete or Key.Back:
                Document.DeleteSelection();
                break;
            case Key.D when control:
                Document.DuplicateSelection();
                break;
            case Key.A when control:
                Document.SelectAll();
                break;
            case Key.F when !control:
                FrameAll();
                break;
            case Key.Escape:
                Document.SelectOnly(null);
                break;
            case Key.C when !control:
                CommentSelection();
                break;
            case Key.T when !control:
                TooltipSelection();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    // ----- Comments and tooltips -----

    /// <summary>Bounds of the selected nodes (without the tooltips), or null.</summary>
    private Rect? SelectionBounds() =>
        Document?.SelectedNodes.Where(n => !n.IsTooltip).Select(n => n.Bounds).ToList() is { Count: > 0 } bounds ? bounds.Aggregate(Rect.Union) : null;

    /// <summary>C: frames the selected nodes with a comment (or adds one at the center of the view).</summary>
    private void CommentSelection()
    {
        if (Document is not null)
        {
            Point center = ToWorld(new Point(Root.ActualWidth / 2, Root.ActualHeight / 2));
            Document.AddComment(SelectionBounds() ?? new Rect(center.X - 150, center.Y - 80, 300, 160));
        }
    }

    /// <summary>T: adds a tooltip bubble above the selected nodes.</summary>
    private void TooltipSelection()
    {
        if (Document is not null)
        {
            Rect? bounds = SelectionBounds();
            Document.AddTooltip(bounds is { } area ? new Point(area.Right - 70, area.Top - 34) : ToWorld(new Point(Root.ActualWidth / 2, Root.ActualHeight / 2)));
        }
    }

    /// <summary>Comment and edges ("l", "tr"...) of the resize handle under the mouse, or null.</summary>
    private (BtNodeViewModel Node, string Edges)? ResizeHandle(DependencyObject? source)
    {
        for (DependencyObject? current = source; current is not null && !ReferenceEquals(current, Root); current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Tag: string tag, DataContext: BtNodeViewModel node } && tag.StartsWith("resize:", StringComparison.Ordinal))
            {
                return (node, tag[7..]);
            }
        }

        return null;
    }

    private void EditCommentTitle(BtNodeViewModel comment, FrameworkElement header)
    {
        comment.IsEditingTitle = true;
        Dispatcher.BeginInvoke(() =>
        {
            if (Graphs.CommentFrames.FindTextBox(header) is { } box)
            {
                box.Focus();
                box.SelectAll();
            }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnCommentEditLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BtNodeViewModel comment })
        {
            comment.IsEditingTitle = false;
        }
    }

    private void OnCommentEditKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            Keyboard.Focus(this);
            e.Handled = true;
        }
    }

    // ----- Menus -----

    /// <summary>Menu of a node (decorators, services, detach, duplicate, delete) or of an attached piece.</summary>
    private void OpenItemMenu(object target)
    {
        if (Document is not { } document)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = Root };
        switch (target)
        {
            case BtAttachmentViewModel attachment:
                document.Select(attachment);
                menu.Items.Add(Item(Tr.T("Move up"), () => document.MoveAttachment(attachment, -1)));
                menu.Items.Add(Item(Tr.T("Move down"), () => document.MoveAttachment(attachment, 1)));
                menu.Items.Add(new Separator());
                menu.Items.Add(Item(Tr.T("Remove"), () => document.RemoveAttachment(attachment)));
                break;

            case BtNodeViewModel { IsAnnotation: true } annotation:
                if (!annotation.IsSelected)
                {
                    document.SelectOnly(annotation);
                }

                if (annotation.IsComment)
                {
                    menu.Items.Add(Item(Tr.T("Rename the comment"), () =>
                    {
                        if (NodesHost.ItemContainerGenerator.ContainerFromItem(annotation) is FrameworkElement container)
                        {
                            EditCommentTitle(annotation, container);
                        }
                    }));
                }

                menu.Items.Add(Item(Tr.T("Duplicate"), document.DuplicateSelection, "Ctrl+D"));
                menu.Items.Add(Item(Tr.T("Delete"), document.DeleteSelection, Tr.T("Del")));
                break;

            case BtNodeViewModel node:
                if (!node.IsSelected)
                {
                    document.SelectOnly(node);
                }

                if (!node.IsRoot)
                {
                    menu.Items.Add(AttachmentMenu(Tr.T("Add a decorator"), node, document.DecoratorChoices));
                    menu.Items.Add(AttachmentMenu(Tr.T("Add a service"), node, document.ServiceChoices));
                    menu.Items.Add(new Separator());
                    menu.Items.Add(Item(Tr.T("Detach from the parent"), () => document.DisconnectParent(node)));
                }

                if (node.HasChildrenPin)
                {
                    menu.Items.Add(Item(Tr.T("Detach the children"), () => document.DisconnectChildren(node)));
                }

                menu.Items.Add(new Separator());
                menu.Items.Add(Item(Tr.T("Add a comment"), CommentSelection, "C"));
                menu.Items.Add(Item(Tr.T("Add a tooltip"), TooltipSelection, "T"));

                if (!node.IsRoot)
                {
                    menu.Items.Add(new Separator());
                    menu.Items.Add(Item(Tr.T("Duplicate"), document.DuplicateSelection, "Ctrl+D"));
                    menu.Items.Add(Item(Tr.T("Delete"), document.DeleteSelection, Tr.T("Del")));
                }

                break;
        }

        if (menu.Items.Count > 0)
        {
            menu.IsOpen = true;
        }
    }

    private static MenuItem AttachmentMenu(string header, BtNodeViewModel node, IReadOnlyList<BtChoiceViewModel> choices)
    {
        var parent = new MenuItem { Header = header };
        foreach (Control item in PuppeteerMenus.AttachmentItems(node, choices))
        {
            parent.Items.Add(item);
        }

        return parent;
    }

    private static MenuItem Item(string header, Action action, string? gesture = null) => PuppeteerMenus.Item(header, action, gesture);

    // ----- Add-node menu -----

    private void OnAddNodeClick(object sender, RoutedEventArgs e) =>
        OpenPalette(new Point(Root.ActualWidth / 2 - 170, Root.ActualHeight / 3), null);

    private void OnArrangeClick(object sender, RoutedEventArgs e)
    {
        Document?.ArrangeTree();
        Dispatcher.BeginInvoke(FrameAll, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnFrameAllClick(object sender, RoutedEventArgs e) => FrameAll();

    private void OpenPalette(Point screen, (BtNodeViewModel Node, bool AsParent)? pending)
    {
        if (Document is null)
        {
            return;
        }

        Document.Pending = pending;
        Document.PaletteSearch = string.Empty;
        _paletteWorld = ToWorld(screen);
        Canvas.SetLeft(Palette, Math.Clamp(screen.X, 0, Math.Max(0, Root.ActualWidth - Palette.Width - 8)));
        Canvas.SetTop(Palette, Math.Clamp(screen.Y, 0, Math.Max(0, Root.ActualHeight - 430)));
        Palette.Visibility = Visibility.Visible;
        PaletteList.SelectedIndex = 0;
        Dispatcher.BeginInvoke(() => PaletteSearchBox.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void ClosePalette()
    {
        if (Palette.Visibility != Visibility.Visible)
        {
            return;
        }

        Palette.Visibility = Visibility.Collapsed;
        if (Document is not null)
        {
            Document.Pending = null;
        }

        Keyboard.Focus(this);
    }

    private void ChoosePaletteItem()
    {
        if (Document is not null && (PaletteList.SelectedItem ?? PaletteList.Items.Cast<object>().FirstOrDefault()) is BtChoiceViewModel choice)
        {
            Palette.Visibility = Visibility.Collapsed;
            // The node is centered on the clicked point, its top pin just below.
            Document.AddNode(choice, new Point(_paletteWorld.X - 115, _paletteWorld.Y));
            Keyboard.Focus(this);
        }
    }

    private void OnPaletteSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ChoosePaletteItem();
                break;
            case Key.Escape:
                ClosePalette();
                break;
            case Key.Down:
                PaletteList.SelectedIndex = Math.Min(PaletteList.SelectedIndex + 1, PaletteList.Items.Count - 1);
                PaletteList.ScrollIntoView(PaletteList.SelectedItem);
                break;
            case Key.Up:
                PaletteList.SelectedIndex = Math.Max(PaletteList.SelectedIndex - 1, 0);
                PaletteList.ScrollIntoView(PaletteList.SelectedItem);
                break;
            default:
                Dispatcher.BeginInvoke(() =>
                {
                    if (PaletteList.SelectedIndex < 0 && PaletteList.Items.Count > 0)
                    {
                        PaletteList.SelectedIndex = 0;
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
                return;
        }

        e.Handled = true;
    }

    private void OnPaletteListMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not null)
        {
            ChoosePaletteItem();
        }
    }

    private void OnPaletteListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ChoosePaletteItem();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClosePalette();
            e.Handled = true;
        }
    }

    private bool IsInPalette(DependencyObject? source) => source is not null && IsDescendant(Palette, source);

    private static bool IsDescendant(DependencyObject ancestor, DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? element)
        where T : DependencyObject
    {
        for (DependencyObject? current = element; current is not null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
