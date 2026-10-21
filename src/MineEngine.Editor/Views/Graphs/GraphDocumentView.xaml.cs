using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MineEngine.Editor.ViewModels.Graphs;

namespace MineEngine.Editor.Views.Graphs;

/// <summary>
/// Gestes de l'éditeur nodal : déplacer la vue (clic droit glissé ou molette pressée),
/// zoomer (molette), déplacer des nœuds (en-tête), tirer un fil depuis une broche,
/// sélectionner au rectangle, ouvrir le menu d'ajout. Toute modification du graphe est
/// confiée au ViewModel ; la vue ne gère que la caméra et les gestes en cours.
/// </summary>
public partial class GraphDocumentView : UserControl
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
        Wire,
        Marquee,
        ResizeComment,
    }

    /// <summary>Alignment of the nodes selected (menu of a right-click on a node).</summary>
    private enum AlignMode
    {
        Left,
        Center,
        Right,
        Top,
        Middle,
        Bottom,
        DistributeHorizontally,
        DistributeVertically,
    }

    /// <summary>Smallest gap between two nodes spread out by a distribution.</summary>
    private const double MinDistributeGap = 30;

    private Gesture _gesture;
    private Point _gestureStart;
    private Point _lastPosition;
    private Dictionary<GraphNodeViewModel, Point> _moveOrigins = [];
    private GraphPinViewModel? _wireSource;
    private Point _paletteWorld;
    private GraphDocumentViewModel? _document;
    private bool _framed;
    private GraphNodeViewModel? _resized;
    private Size _resizeOrigin;

    /// <summary>Node under a right-click: releasing without dragging opens its menu (instead of the add menu).</summary>
    private GraphNodeViewModel? _menuNode;

    public GraphDocumentView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as GraphDocumentViewModel);
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

    private GraphDocumentViewModel? Document => _document;

    private double Scale => Zoom.ScaleX;

    /// <summary>Donne le focus au canevas (onglet activé) pour les raccourcis clavier.</summary>
    public void FocusCanvas() => Keyboard.Focus(this);

    // ----- Caméra -----

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

    /// <summary>Pas sur lequel caler les nœuds lâchés, si le bouton Grille (ou l'option des paramètres) est actif (sinon 0).</summary>
    private double SnapSize => Document?.Options.SnapToGrid == true ? GridSize : 0;

    /// <summary>Quadrillage aligné sur le monde : il suit le déplacement et le zoom.</summary>
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

    /// <summary>Cadre tous les nœuds (ou l'origine si le graphe est vide).</summary>
    private void FrameAll()
    {
        if (Document is null || Root.ActualWidth <= 0)
        {
            return;
        }

        _framed = true;
        List<Rect> bounds = [.. Document.Nodes.Select(NodeBounds)];
        if (bounds.Count == 0)
        {
            SetCamera(1, 60, 80);
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

    private void CenterOn(GraphNodeViewModel node)
    {
        Rect bounds = NodeBounds(node);
        SetCamera(
            Scale,
            Root.ActualWidth / 2 - (bounds.X + bounds.Width / 2) * Scale,
            Root.ActualHeight / 2 - (bounds.Y + bounds.Height / 2) * Scale);
    }

    private Rect NodeBounds(GraphNodeViewModel node) =>
        NodesHost.ItemContainerGenerator.ContainerFromItem(node) is FrameworkElement { ActualWidth: > 0 } container
            ? new Rect(node.X, node.Y, container.ActualWidth, container.ActualHeight)
            : new Rect(node.X, node.Y, 180, 80);

    // ----- Document -----

    private void Attach(GraphDocumentViewModel? document)
    {
        ShowBody(document?.HasBody ?? true);
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

    /// <summary>Tells the document which point of the graph is at the center of the view (nodes added from the Nodes window).</summary>
    private void UpdateViewCenter()
    {
        if (Document is not null && Root.ActualWidth > 0)
        {
            Document.ViewCenter = ToWorld(new Point(Root.ActualWidth / 2, Root.ActualHeight / 2));
        }
    }

    /// <summary>
    /// Un graphe avec des nœuds montre sa définition dans l'Inspecteur et prend toute la place ;
    /// une structure, une interface ou une énumération n'a pas de nœuds : sa définition remplit l'onglet.
    /// </summary>
    private void ShowBody(bool hasBody)
    {
        Root.Visibility = hasBody ? Visibility.Visible : Visibility.Collapsed;
        DefinitionPane.Visibility = hasBody ? Visibility.Collapsed : Visibility.Visible;
        DefinitionSplitter.Visibility = Visibility.Collapsed;
        DefinitionColumn.MinWidth = 0;
        DefinitionColumn.MaxWidth = double.PositiveInfinity;
        DefinitionColumn.Width = hasBody ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        BodyColumn.Width = hasBody ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void OnFocusRequested(object? sender, Guid id)
    {
        if (Document?.Nodes.FirstOrDefault(n => n.Id == id) is { } node)
        {
            Dispatcher.BeginInvoke(() => CenterOn(node), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    // ----- Position des broches -----

    private void OnNodeLoaded(object sender, RoutedEventArgs e) => MeasurePins((FrameworkElement)sender);

    private void OnNodeSizeChanged(object sender, SizeChangedEventArgs e) => MeasurePins((FrameworkElement)sender);

    /// <summary>Mesure le centre de chaque broche par rapport au coin du nœud : les fils s'y accrochent.</summary>
    private static void MeasurePins(FrameworkElement nodeRoot)
    {
        foreach (FrameworkElement pin in Descendants(nodeRoot).Where(d => d is FrameworkElement { Tag: "pin" }).Cast<FrameworkElement>())
        {
            if (pin.DataContext is GraphPinViewModel model && pin.IsVisible && pin.ActualWidth > 0)
            {
                model.Offset = pin.TranslatePoint(new Point(pin.ActualWidth / 2, pin.ActualHeight / 2), nodeRoot);
            }
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Premier élément marqué <paramref name="tag"/> en remontant depuis <paramref name="source"/>.</summary>
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

    private GraphPinViewModel? PinAt(Point screen)
    {
        HitTestResult? hit = VisualTreeHelper.HitTest(Root, screen);
        return FindTagged(hit?.VisualHit, "pin")?.DataContext as GraphPinViewModel;
    }

    /// <summary>Fil passant à quelques pixels du point (coordonnées de l'écran), ou null.</summary>
    private GraphConnectionViewModel? WireAt(Point screen)
    {
        if (Document is null)
        {
            return null;
        }

        Point world = ToWorld(screen);
        var pen = new Pen(Brushes.Black, 10 / Scale);
        return Document.Connections.LastOrDefault(c => c.Geometry.StrokeContains(pen, world));
    }

    // ----- Souris -----

    private void OnNodePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Document is not null && sender is FrameworkElement { DataContext: GraphNodeViewModel node }
            && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !node.IsSelected)
        {
            Document.SelectOnly(node);
        }
    }

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

        if (FindTagged(source, "pin")?.DataContext is GraphPinViewModel pin && pin.CanConnect)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                Document.DisconnectPin(pin);
            }
            else
            {
                _wireSource = pin;
                PendingWire.Stroke = pin.IsExec ? (Brush)FindResource("StrongTextBrush") : GraphBrushes.ForPin(pin.Type);
                Begin(Gesture.Wire, position);
            }

            e.Handled = true;
            return;
        }

        if (FindTagged(source, "resize")?.DataContext is GraphNodeViewModel { IsComment: true } resized)
        {
            Document.SelectOnly(resized);
            _resized = resized;
            _resizeOrigin = new Size(resized.Width, resized.Height);
            Begin(Gesture.ResizeComment, position);
            e.Handled = true;
            return;
        }

        if (FindTagged(source, "header")?.DataContext is GraphNodeViewModel node)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                node.IsSelected = !node.IsSelected;
                Document.OnSelectionChanged();
            }

            _moveOrigins = Document.SelectedNodes.ToDictionary(n => n, n => new Point(n.X, n.Y));

            // A comment carries the nodes it frames, like in Blueprints.
            foreach (GraphNodeViewModel comment in Document.SelectedNodes.Where(n => n.IsComment).ToList())
            {
                Rect frame = NodeBounds(comment);
                foreach (GraphNodeViewModel inside in Document.Nodes.Where(n => !_moveOrigins.ContainsKey(n) && frame.Contains(NodeBounds(n))))
                {
                    _moveOrigins[inside] = new Point(inside.X, inside.Y);
                }
            }

            Begin(Gesture.MoveNodes, position);
            e.Handled = true;
            return;
        }

        if (FindTagged(source, "node") is null && e.ClickCount == 2 && WireAt(position) is { } wire)
        {
            // Double-clic sur un fil : une redirection y est posée, comme dans Unreal.
            Point world = ToWorld(position);
            Document.InsertReroute(wire, world.X, world.Y);
            e.Handled = true;
            return;
        }

        if (FindTagged(source, "node") is null)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                Document.SelectOnly(null);
            }

            Begin(Gesture.Marquee, position);
            e.Handled = true;
        }
    }

    private void OnRootMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInPalette(e.OriginalSource as DependencyObject) || FindTagged(e.OriginalSource as DependencyObject, "node") is { } node
            && Keyboard.FocusedElement is DependencyObject focused && IsDescendant(node, focused))
        {
            return;
        }

        ClosePalette();
        Keyboard.Focus(this);
        _menuNode = FindTagged(e.OriginalSource as DependencyObject, "node")?.DataContext as GraphNodeViewModel;
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
                foreach ((GraphNodeViewModel node, Point origin) in _moveOrigins)
                {
                    node.X = origin.X + delta.X;
                    node.Y = origin.Y + delta.Y;
                }

                break;

            case Gesture.Wire when _wireSource is not null:
                Point end = ToWorld(position);
                PendingWire.Visibility = Visibility.Visible;
                PendingWire.Data = _wireSource.IsInput
                    ? GraphConnectionViewModel.Curve(end, _wireSource.Anchor)
                    : GraphConnectionViewModel.Curve(_wireSource.Anchor, end);
                break;

            case Gesture.ResizeComment when _resized is not null:
                Vector grown = (position - _gestureStart) / Scale;
                _resized.Width = _resizeOrigin.Width + grown.X;
                _resized.Height = _resizeOrigin.Height + grown.Y;
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
            case Gesture.PanPending when _menuNode is not null:
                OpenNodeMenu(_menuNode);
                break;

            case Gesture.PanPending:
                OpenPalette(position, null);
                break;

            case Gesture.ResizeComment when _resized is not null:
                Document.ResizeComment(_resized, SnapSize);
                _resized = null;
                break;

            case Gesture.MoveNodes:
                Document.CommitMove(SnapSize);
                break;

            case Gesture.Wire when _wireSource is not null:
                GraphPinViewModel source = _wireSource;
                _wireSource = null;
                if (PinAt(position) is { } target)
                {
                    if (!ReferenceEquals(target, source))
                    {
                        Document.TryConnect(source, target);
                    }
                }
                else if ((position - _gestureStart).Length > DragThreshold * 3)
                {
                    // Fil lâché dans le vide : le nœud choisi dans le menu s'y branchera.
                    OpenPalette(position, source);
                }

                break;

            case Gesture.Marquee:
                var area = new Rect(ToWorld(_gestureStart), ToWorld(position));
                if (area.Width + area.Height > DragThreshold / Scale)
                {
                    // A comment is taken only when the rectangle holds it entirely (it is often large).
                    foreach (GraphNodeViewModel node in Document.Nodes.Where(n => n.IsComment ? area.Contains(NodeBounds(n)) : NodeBounds(n).IntersectsWith(area)))
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
            PendingWire.Visibility = Visibility.Collapsed;
            Marquee.Visibility = Visibility.Collapsed;
        }
    }

    private void EndGesture()
    {
        _gesture = Gesture.None;
        PendingWire.Visibility = Visibility.Collapsed;
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

    // ----- Nodes window -----

    private void OnRootDragOver(object sender, DragEventArgs e)
    {
        e.Effects = Document is not null && e.Data.GetData(NodeLibraryViewModel.DragFormat) is NodeChoiceViewModel choice && Document.CanAdd(choice.Template)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    // The node is placed so that the pointer is on its header.
    private void OnRootDrop(object sender, DragEventArgs e)
    {
        if (Document is null || e.Data.GetData(NodeLibraryViewModel.DragFormat) is not NodeChoiceViewModel choice || !Document.CanAdd(choice.Template))
        {
            return;
        }

        Point world = ToWorld(e.GetPosition(Root));
        Document.PendingSource = null;
        Document.AddNode(choice, new Point(world.X - 20, world.Y - 14));
        Keyboard.Focus(this);
        e.Handled = true;
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

    // ----- Comments, tooltips, alignment -----

    /// <summary>Bounds of the selected nodes (without the tooltips), or null if nothing is selected.</summary>
    private Rect? SelectionBounds() =>
        Document?.SelectedNodes.Where(n => !n.IsTooltip).Select(NodeBounds).ToList() is { Count: > 0 } bounds ? bounds.Aggregate(Rect.Union) : null;

    /// <summary>C: frames the selected nodes with a comment (or adds one at the center of the view).</summary>
    private void CommentSelection()
    {
        if (Document is null)
        {
            return;
        }

        Point center = ToWorld(new Point(Root.ActualWidth / 2, Root.ActualHeight / 2));
        Document.AddComment(SelectionBounds() ?? new Rect(center.X - 150, center.Y - 80, 300, 160));
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

    /// <summary>Menu of a right-click on a node: alignment of the selection, comment, tooltip, duplicate, delete.</summary>
    private void OpenNodeMenu(GraphNodeViewModel node)
    {
        if (Document is null)
        {
            return;
        }

        if (!node.IsSelected)
        {
            Document.SelectOnly(node);
        }

        int count = Document.SelectedNodes.Count(n => !n.IsAnnotation);
        var menu = new ContextMenu { PlacementTarget = Root, Placement = PlacementMode.MousePoint };
        MenuItem Item(string header, Action action, bool enabled = true, string? gesture = null)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled, InputGestureText = gesture ?? string.Empty };
            item.Click += (_, _) => action();
            return item;
        }

        menu.Items.Add(Item(Tr.T("Align horizontally (in a row)"), () => Align(AlignMode.Middle), count >= 2));
        menu.Items.Add(Item(Tr.T("Align vertically (in a column)"), () => Align(AlignMode.Center), count >= 2));
        var more = new MenuItem { Header = Tr.T("Align"), IsEnabled = count >= 2 };
        more.Items.Add(Item(Tr.T("Left edges"), () => Align(AlignMode.Left)));
        more.Items.Add(Item(Tr.T("Right edges"), () => Align(AlignMode.Right)));
        more.Items.Add(Item(Tr.T("Top edges"), () => Align(AlignMode.Top)));
        more.Items.Add(Item(Tr.T("Bottom edges"), () => Align(AlignMode.Bottom)));
        more.Items.Add(new Separator());
        more.Items.Add(Item(Tr.T("Distribute horizontally"), () => Align(AlignMode.DistributeHorizontally), count >= 3));
        more.Items.Add(Item(Tr.T("Distribute vertically"), () => Align(AlignMode.DistributeVertically), count >= 3));
        menu.Items.Add(more);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Tr.T("Add a comment"), CommentSelection, gesture: "C"));
        menu.Items.Add(Item(Tr.T("Add a tooltip"), TooltipSelection, gesture: "T"));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Tr.T("Duplicate"), Document.DuplicateSelection, gesture: "Ctrl+D"));
        menu.Items.Add(Item(Tr.T("Delete"), Document.DeleteSelection, gesture: Tr.T("Del")));
        menu.Closed += (_, _) => Keyboard.Focus(this);
        menu.IsOpen = true;
    }

    /// <summary>Aligns or spreads out the selected nodes (comments and tooltips stay where they are); one step.</summary>
    private void Align(AlignMode mode)
    {
        if (Document is null)
        {
            return;
        }

        List<(GraphNodeViewModel Node, Rect Bounds)> items = [.. Document.SelectedNodes.Where(n => !n.IsAnnotation).Select(n => (n, NodeBounds(n)))];
        if (items.Count < 2)
        {
            return;
        }

        double left = items.Min(i => i.Bounds.Left);
        double right = items.Max(i => i.Bounds.Right);
        double top = items.Min(i => i.Bounds.Top);
        double bottom = items.Max(i => i.Bounds.Bottom);
        var positions = new Dictionary<Guid, (double X, double Y)>();
        switch (mode)
        {
            case AlignMode.DistributeHorizontally or AlignMode.DistributeVertically:
                bool horizontal = mode == AlignMode.DistributeHorizontally;
                var ordered = items.OrderBy(i => horizontal ? i.Bounds.X : i.Bounds.Y).ToList();
                double span = horizontal ? right - left : bottom - top;
                double sizes = ordered.Sum(i => horizontal ? i.Bounds.Width : i.Bounds.Height);
                double gap = Math.Max(MinDistributeGap, (span - sizes) / (ordered.Count - 1));
                double next = horizontal ? left : top;
                foreach ((GraphNodeViewModel node, Rect bounds) in ordered)
                {
                    positions[node.Id] = horizontal ? (next, bounds.Y) : (bounds.X, next);
                    next += (horizontal ? bounds.Width : bounds.Height) + gap;
                }

                break;
            default:
                foreach ((GraphNodeViewModel node, Rect b) in items)
                {
                    positions[node.Id] = mode switch
                    {
                        AlignMode.Left => (left, b.Y),
                        AlignMode.Right => (right - b.Width, b.Y),
                        AlignMode.Center => ((left + right) / 2 - b.Width / 2, b.Y),
                        AlignMode.Top => (b.X, top),
                        AlignMode.Bottom => (b.X, bottom - b.Height),
                        _ => (b.X, (top + bottom) / 2 - b.Height / 2),
                    };
                }

                break;
        }

        Document.MoveNodesTo(positions, items.Count == 2 ? Tr.T("Align 2 nodes") : Tr.F("Align {0} nodes", items.Count));
    }

    // ----- Menu d'ajout -----

    private void OnAddNodeClick(object sender, RoutedEventArgs e) =>
        OpenPalette(new Point(Root.ActualWidth / 2 - 170, Root.ActualHeight / 3), null);

    private void OnFrameAllClick(object sender, RoutedEventArgs e) => FrameAll();

    private void OpenPalette(Point screen, GraphPinViewModel? pending)
    {
        if (Document is null)
        {
            return;
        }

        Document.PendingSource = pending;
        Document.PaletteSearch = string.Empty;
        _paletteWorld = ToWorld(screen);
        Canvas.SetLeft(Palette, Math.Clamp(screen.X, 0, Math.Max(0, Root.ActualWidth - Palette.Width - 8)));
        Canvas.SetTop(Palette, Math.Clamp(screen.Y, 0, Math.Max(0, Root.ActualHeight - 420)));
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
            Document.PendingSource = null;
        }

        Keyboard.Focus(this);
    }

    private void ChoosePaletteItem()
    {
        if (Document is not null && (PaletteList.SelectedItem ?? PaletteList.Items.Cast<object>().FirstOrDefault()) is NodeChoiceViewModel choice)
        {
            Palette.Visibility = Visibility.Collapsed;
            Document.AddNode(choice, _paletteWorld);
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
                // La liste filtrée change : on resélectionne le premier choix.
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
