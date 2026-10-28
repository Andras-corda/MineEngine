using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MineEngine.Assets.Sparkle;
using MineEngine.Editor.ViewModels.Sparkle;

namespace MineEngine.Editor.Views.Sparkle;

/// <summary>
/// Sparkle tab: the canvas of the emitters (pan, zoom, nodes moved by their head, comments moved by
/// their title with what they frame, tooltips, menus of the right-click, snapping), the square
/// preview docked on the left or detached in its own window, the user parameters under it, and the
/// timeline. What is edited goes through the document, so that it can be undone.
/// </summary>
public partial class SparkleView : UserControl
{
    private const double MinZoom = 0.25;
    private const double MaxZoom = 2;
    private const double GridSize = 32;
    private const double DragThreshold = 4;

    /// <summary>Height of a node used to frame the canvas before the node is drawn.</summary>
    private const double DefaultNodeHeight = 420;

    private enum Gesture
    {
        None,
        PanPending,
        Pan,
        MoveNode,
        MoveNote,
        ResizeNote,
    }

    private Gesture _gesture;
    private Point _pressed;
    private Point _panStart;
    private SparkleEmitterViewModel? _moved;
    private Point _movedStart;
    private Point _menuPoint;

    /// <summary>Last point of the canvas under the mouse (Ctrl+V pastes there).</summary>
    private Point _mouseWorld;

    /// <summary>Note dragged or resized, and what it carries: the emitters and tooltips inside a comment, with their start.</summary>
    private SparkleNoteViewModel? _note;
    private Rect _noteStart;
    private List<(SparkleEmitterViewModel Emitter, Point Start)> _carriedEmitters = [];
    private List<(SparkleNoteViewModel Note, Point Start)> _carriedNotes = [];
    private bool _framed;
    private SparkleDocumentViewModel? _document;
    private SparklePreviewWindow? _window;
    private FrameworkElement? _dockedPreview;

    public SparkleView()
    {
        InitializeComponent();
        FillTemplates((ContextMenu)FindResource("CanvasMenu"));
        DataContextChanged += (_, _) => Attach(DataContext as SparkleDocumentViewModel);
        Loaded += (_, _) =>
        {
            UpdateGrid();
            if (!_framed)
            {
                Dispatcher.BeginInvoke(ShowStart, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };
    }

    private SparkleDocumentViewModel? Document => _document;

    private double Scale => Zoom.ScaleX;

    /// <summary>Gives the focus to the canvas (activated tab) for keyboard shortcuts.</summary>
    public void FocusCanvas() => Keyboard.Focus(this);

    // ----- Document and preview -----

    private void Attach(SparkleDocumentViewModel? document)
    {
        if (_document is not null)
        {
            _document.PropertyChanged -= OnDocumentPropertyChanged;
        }

        _document = document;
        if (document is not null)
        {
            document.PropertyChanged += OnDocumentPropertyChanged;
            // Snapping starts like the one of the graphs (setting of the interface).
            document.SnapToGrid = TryFindResource(Services.WpfInterfaceService.GraphSnapToGridKey) is true;
        }

        PlacePreview();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SparkleDocumentViewModel.IsPreviewDetached))
        {
            PlacePreview();
        }
    }

    /// <summary>Docked: the preview is next to the canvas. Detached: in its own window, and its column gives its room to the canvas.</summary>
    private void PlacePreview()
    {
        bool detached = _document?.IsPreviewDetached == true;
        if (detached && _window is null && _document is { } document)
        {
            PreviewSlot.Content = null;
            _window = new SparklePreviewWindow(document) { Owner = Window.GetWindow(this) };
            _window.Closed += (_, _) =>
            {
                _window = null;
                document.IsPreviewDetached = false;
            };
            _window.Show();
        }
        else if (!detached)
        {
            _window?.Close();
            _window = null;
            _dockedPreview ??= CreatePreview();
            PreviewSlot.Content = _dockedPreview;
        }

        // Detached, the column keeps the user parameters: it only gets narrower.
        PreviewSquare.Visibility = detached ? Visibility.Collapsed : Visibility.Visible;
        PreviewColumn.Width = new GridLength(detached ? 280 : 360);
        DetachText.Text = detached ? Tr.T("Dock the preview") : Tr.T("Detach the preview");
    }

    /// <summary>
    /// A framed preview bound to the document it inherits (DataContext): the docked one is kept
    /// while the tab lives, the detached window makes its own.
    /// </summary>
    internal static FrameworkElement CreatePreview()
    {
        var preview = new SparklePreview();
        preview.SetBinding(SparklePreview.PlayerProperty, nameof(SparkleDocumentViewModel.Player));
        preview.SetBinding(SparklePreview.SpriteResolverProperty, nameof(SparkleDocumentViewModel.SpriteResolver));
        System.Windows.Automation.AutomationProperties.SetName(preview, Tr.T("Preview"));
        preview.SetBinding(SparklePreview.ShowAxesProperty, nameof(SparkleDocumentViewModel.ShowAxes));
        preview.SetBinding(SparklePreview.ShowZonesProperty, nameof(SparkleDocumentViewModel.ShowZones));
        preview.SetBinding(SparklePreview.ShowBeamsProperty, nameof(SparkleDocumentViewModel.ShowBeams));
        preview.SetBinding(SparklePreview.ShowVelocityProperty, nameof(SparkleDocumentViewModel.ShowVelocity));
        preview.SetBinding(SparklePreview.ShowCollisionProperty, nameof(SparkleDocumentViewModel.ShowCollision));

        // Right-click on the preview: what it draws besides the particles.
        var menu = new ContextMenu();
        foreach ((string header, string property) in new[]
                 {
                     (Tr.T("Axes"), nameof(SparkleDocumentViewModel.ShowAxes)),
                     (Tr.T("Zones of the location events"), nameof(SparkleDocumentViewModel.ShowZones)),
                     (Tr.T("Start and end of the beams"), nameof(SparkleDocumentViewModel.ShowBeams)),
                     (Tr.T("Direction of the velocity"), nameof(SparkleDocumentViewModel.ShowVelocity)),
                     (Tr.T("Collision boxes"), nameof(SparkleDocumentViewModel.ShowCollision)),
                 })
        {
            var item = new MenuItem { Header = header, IsCheckable = true };
            item.SetBinding(MenuItem.IsCheckedProperty, new System.Windows.Data.Binding(property));
            menu.Items.Add(item);
        }

        preview.ContextMenu = menu;
        var frame = new Border
        {
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1.5),
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x18, 0x1E)),
            Child = preview,
        };
        frame.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
        return frame;
    }

    // ----- Camera -----

    private Point ToWorld(Point screen) => new((screen.X - Pan.X) / Scale, (screen.Y - Pan.Y) / Scale);

    private void SetCamera(double scale, double x, double y)
    {
        Zoom.ScaleX = Zoom.ScaleY = Math.Clamp(scale, MinZoom, MaxZoom);
        Pan.X = x;
        Pan.Y = y;
        UpdateGrid();
    }

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

    /// <summary>First view of the tab: the nodes at their real size (readable), from the first one at the top left.</summary>
    private void ShowStart()
    {
        if (Document is null || Root.ActualWidth <= 0)
        {
            return;
        }

        _framed = true;
        const double margin = 40;
        Rect all = Document.Emitters.Select(NodeBounds).DefaultIfEmpty(new Rect(0, 0, 1, 1)).Aggregate(Rect.Union);
        SetCamera(1, margin - all.X, margin - all.Y);
    }

    /// <summary>Frames all the emitters.</summary>
    private void FrameAll()
    {
        if (Document is null || Root.ActualWidth <= 0)
        {
            return;
        }

        _framed = true;
        List<Rect> bounds = [.. Document.Emitters.Select(NodeBounds)];
        if (bounds.Count == 0)
        {
            SetCamera(1, 40, 40);
            return;
        }

        bounds.AddRange(Document.Notes.Where(n => n.IsComment).Select(n => new Rect(n.X, n.Y, n.Width, n.Height)));
        Rect all = bounds.Aggregate(Rect.Union);
        const double margin = 40;
        double scale = Math.Min(1, Math.Min(
            (Root.ActualWidth - 2 * margin) / Math.Max(all.Width, 1),
            (Root.ActualHeight - 2 * margin) / Math.Max(all.Height, 1)));
        scale = Math.Clamp(scale, MinZoom, 1);
        SetCamera(
            scale,
            (Root.ActualWidth - all.Width * scale) / 2 - all.X * scale,
            (Root.ActualHeight - all.Height * scale) / 2 - all.Y * scale);
    }

    private Rect NodeBounds(SparkleEmitterViewModel emitter)
    {
        double height = NodesHost.ItemContainerGenerator.ContainerFromItem(emitter) is FrameworkElement { ActualHeight: > 0 } container
            ? container.ActualHeight
            : DefaultNodeHeight;
        return new Rect(emitter.X, emitter.Y, SparkleEmitterViewModel.NodeWidth, height);
    }

    private void OnFrameAllClick(object sender, RoutedEventArgs e) => FrameAll();

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_framed && e.NewSize.Width > 0 && IsLoaded)
        {
            ShowStart();
        }
    }

    // ----- Mouse -----

    /// <summary>
    /// Element under the mouse marked by its Tag ("head", "module", "node" of an emitter; "comment-head",
    /// "comment-resize", "tooltip" of a note), and the object it shows. The inside of a comment is the canvas.
    /// </summary>
    private static (string? Part, object? Item) PartAt(object? source)
    {
        for (DependencyObject? current = source as DependencyObject; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Tag: string part } element && part is "head" or "module" or "node" or "comment-head" or "comment-resize" or "tooltip")
            {
                return (part, element.DataContext);
            }
        }

        return (null, null);
    }

    /// <summary>A click on a field or a box of a node goes to it, not to the canvas.</summary>
    private static bool IsInControl(object? source)
    {
        for (DependencyObject? current = source as DependencyObject; current is not null and not ItemsControl; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ButtonBase or TextBoxBase)
            {
                return true;
            }
        }

        return false;
    }

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Document is not { } document)
        {
            return;
        }

        Keyboard.Focus(this);
        (string? part, object? item) = PartAt(e.OriginalSource);
        if (IsInControl(e.OriginalSource))
        {
            // The box of a module or of the head also selects what it belongs to.
            switch (item)
            {
                case SparkleModuleViewModel module:
                    document.Select(module.Emitter, module);
                    break;
                case SparkleEmitterViewModel emitter:
                    document.Select(emitter, null);
                    break;
            }

            return;
        }

        _pressed = e.GetPosition(Root);
        switch (part, item)
        {
            case ("module", SparkleModuleViewModel module):
                document.Select(module.Emitter, module);
                e.Handled = true;
                break;
            case ("head", SparkleEmitterViewModel emitter):
                document.Select(emitter, null);
                _moved = emitter;
                _movedStart = new Point(emitter.X, emitter.Y);
                _gesture = Gesture.MoveNode;
                Root.CaptureMouse();
                e.Handled = true;
                break;
            case ("node", SparkleEmitterViewModel emitter):
                document.Select(emitter, null);
                e.Handled = true;
                break;
            case ("comment-head" or "tooltip", SparkleNoteViewModel note):
                document.SelectNote(note);
                StartNoteGesture(note, Gesture.MoveNote);
                e.Handled = true;
                break;
            case ("comment-resize", SparkleNoteViewModel note):
                document.SelectNote(note);
                StartNoteGesture(note, Gesture.ResizeNote);
                e.Handled = true;
                break;
            default:
                _panStart = new Point(Pan.X, Pan.Y);
                _gesture = Gesture.PanPending;
                Root.CaptureMouse();
                e.Handled = true;
                break;
        }
    }

    /// <summary>A comment carries the emitters and the tooltips whose box is inside it when the drag starts.</summary>
    private void StartNoteGesture(SparkleNoteViewModel note, Gesture gesture)
    {
        _note = note;
        _noteStart = new Rect(note.X, note.Y, note.Width, note.Height);
        _carriedEmitters = [];
        _carriedNotes = [];
        if (gesture == Gesture.MoveNote && note.IsComment && Document is { } document)
        {
            _carriedEmitters = [.. document.Emitters.Where(em => _noteStart.Contains(NodeBounds(em))).Select(em => (em, new Point(em.X, em.Y)))];
            _carriedNotes = [.. document.Notes.Where(n => n.IsTooltip && _noteStart.Contains(new Point(n.X, n.Y))).Select(n => (n, new Point(n.X, n.Y)))];
        }

        _gesture = gesture;
        Root.CaptureMouse();
    }

    private void OnRootMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            _pressed = e.GetPosition(Root);
            _panStart = new Point(Pan.X, Pan.Y);
            _gesture = Gesture.Pan;
            Root.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnRootMouseMove(object sender, MouseEventArgs e)
    {
        Point position = e.GetPosition(Root);
        _mouseWorld = ToWorld(position);
        Vector moved = position - _pressed;
        double Snap(double value) => Document?.Snap(value) ?? Math.Round(value);
        switch (_gesture)
        {
            case Gesture.PanPending when moved.Length >= DragThreshold:
                _gesture = Gesture.Pan;
                goto case Gesture.Pan;
            case Gesture.Pan:
                SetCamera(Scale, _panStart.X + moved.X, _panStart.Y + moved.Y);
                break;
            case Gesture.MoveNode when _moved is not null && (moved.Length >= DragThreshold || _moved.X != _movedStart.X || _moved.Y != _movedStart.Y):
                _moved.X = Snap(_movedStart.X + moved.X / Scale);
                _moved.Y = Snap(_movedStart.Y + moved.Y / Scale);
                break;
            case Gesture.MoveNote when _note is not null:
                double x = Snap(_noteStart.X + moved.X / Scale);
                double y = Snap(_noteStart.Y + moved.Y / Scale);
                _note.X = x;
                _note.Y = y;
                foreach ((SparkleEmitterViewModel emitter, Point start) in _carriedEmitters)
                {
                    emitter.X = start.X + x - _noteStart.X;
                    emitter.Y = start.Y + y - _noteStart.Y;
                }

                foreach ((SparkleNoteViewModel note, Point start) in _carriedNotes)
                {
                    note.X = start.X + x - _noteStart.X;
                    note.Y = start.Y + y - _noteStart.Y;
                }

                break;
            case Gesture.ResizeNote when _note is not null:
                _note.Width = Snap(_noteStart.Right + moved.X / Scale) - _noteStart.X;
                _note.Height = Snap(_noteStart.Bottom + moved.Y / Scale) - _noteStart.Y;
                break;
        }
    }

    private void OnRootMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_gesture == Gesture.None || e.ChangedButton == MouseButton.Right)
        {
            return;
        }

        Gesture gesture = _gesture;
        EndGesture();
        if (gesture == Gesture.PanPending)
        {
            // A click in the empty space: nothing is selected any more.
            Document?.Select(null, null);
        }
    }

    private void OnRootLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_gesture != Gesture.None)
        {
            EndGesture();
        }
    }

    private void EndGesture()
    {
        Gesture gesture = _gesture;
        _gesture = Gesture.None;
        if (gesture == Gesture.MoveNode && _moved is { } node)
        {
            _moved = null;
            Document?.MoveItems([node], []);
        }

        if (gesture is Gesture.MoveNote or Gesture.ResizeNote && _note is { } note)
        {
            _note = null;
            Document?.MoveItems(_carriedEmitters.Select(c => c.Emitter), [note, .. _carriedNotes.Select(c => c.Note)]);
        }

        if (Root.IsMouseCaptured)
        {
            Root.ReleaseMouseCapture();
        }
    }

    private void OnRootMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Point position = e.GetPosition(Root);
        Point world = ToWorld(position);
        double scale = Math.Clamp(Scale * (e.Delta > 0 ? 1.1 : 1 / 1.1), MinZoom, MaxZoom);
        SetCamera(scale, position.X - world.X * scale, position.Y - world.Y * scale);
        e.Handled = true;
    }

    /// <summary>Right-click: the menu of the emitter under the mouse, or the menu of the canvas (add an emitter there).</summary>
    private void OnRootMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Document is not { } document || IsInControl(e.OriginalSource))
        {
            return;
        }

        _menuPoint = ToWorld(e.GetPosition(Root));
        (_, object? item) = PartAt(e.OriginalSource);
        if (item is SparkleNoteViewModel note)
        {
            document.SelectNote(note);
            OpenMenu("NoteMenu", note);
            e.Handled = true;
            return;
        }

        SparkleEmitterViewModel? emitter = item switch
        {
            SparkleEmitterViewModel node => node,
            SparkleModuleViewModel module => module.Emitter,
            _ => null,
        };
        if (emitter is not null)
        {
            document.Select(emitter, item as SparkleModuleViewModel);
        }

        OpenMenu(emitter is null ? "CanvasMenu" : "NodeMenu", emitter ?? (object)document);
        e.Handled = true;
    }

    private void OpenMenu(string key, object dataContext)
    {
        var menu = (ContextMenu)FindResource(key);
        if (menu.Items.OfType<MenuItem>().FirstOrDefault(i => Equals(i.Tag, "paste")) is { } paste)
        {
            paste.IsEnabled = SparkleDocumentViewModel.CanPaste;
        }

        menu.DataContext = dataContext;
        menu.PlacementTarget = Root;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    /// <summary>"Add template emitter": an item per template (pre-configured emitters).</summary>
    private void FillTemplates(ContextMenu menu)
    {
        MenuItem add = menu.Items.OfType<MenuItem>().First(i => Equals(i.Tag, "templates"));
        foreach (SparkleTemplateInfo info in SparkleTemplates.All.Where(t => t.Template != SparkleTemplate.Empty))
        {
            var item = new MenuItem { Header = info.Title, ToolTip = info.Description };
            System.Windows.Automation.AutomationProperties.SetName(item, info.Title);
            item.Click += (_, _) => AddEmitter(info.Template);
            add.Items.Add(item);
        }
    }

    private void OnAddEmptyClick(object sender, RoutedEventArgs e) => AddEmitter(SparkleTemplate.Empty);

    private void OnPasteClick(object sender, RoutedEventArgs e) => Document?.Paste(_menuPoint.X - 40, _menuPoint.Y - 20);

    private void OnCopyClick(object sender, RoutedEventArgs e) => Document?.CopyCommand.Execute(null);

    private void OnAddCommentClick(object sender, RoutedEventArgs e) => Document?.AddNote(SparkleNoteKind.Comment, _menuPoint.X, _menuPoint.Y);

    private void OnAddTooltipClick(object sender, RoutedEventArgs e) => Document?.AddNote(SparkleNoteKind.Tooltip, _menuPoint.X, _menuPoint.Y);

    private void OnDeleteNoteClick(object sender, RoutedEventArgs e) => Document?.DeleteSelection();

    /// <summary>"Add a parameter": a menu of the types (number, vector, color, yes / no).</summary>
    private void OnAddParameterClick(object sender, RoutedEventArgs e)
    {
        if (Document is not { } document)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = AddParameterButton, Placement = PlacementMode.Bottom };
        foreach (ViewModels.Assets.Choice<SparkleUserParameterType> type in document.UserParameterTypes)
        {
            var item = new MenuItem { Header = type.Label };
            System.Windows.Automation.AutomationProperties.SetName(item, type.Label);
            item.Click += (_, _) => document.AddUserParameter(type.Value);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void AddEmitter(SparkleTemplate template)
    {
        if (Document is { } document)
        {
            // The node is placed with its head under the point of the right-click.
            document.AddEmitter(template, Math.Round(_menuPoint.X - 40), Math.Round(_menuPoint.Y - 20));
        }
    }

    // ----- Keyboard -----

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Document is not { } document || Keyboard.FocusedElement is TextBoxBase or ComboBox or ComboBoxItem)
        {
            return;
        }

        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        ICommand? command = (e.Key, control) switch
        {
            (Key.Delete or Key.Back, false) => document.DeleteSelectedCommand,
            (Key.D, true) => document.DuplicateSelectedCommand,
            (Key.C, true) => document.CopyCommand,
            (Key.Space, false) => document.Player.PlayPauseCommand,
            _ => null,
        };
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
            return;
        }

        switch (e.Key, control)
        {
            case (Key.V, true):
                document.Paste(_mouseWorld.X - 40, _mouseWorld.Y - 20);
                break;
            case (Key.F, false):
                FrameAll();
                break;
            case (Key.C, false):
                document.AddNote(SparkleNoteKind.Comment, _mouseWorld.X, _mouseWorld.Y);
                break;
            case (Key.T, false):
                document.AddNote(SparkleNoteKind.Tooltip, _mouseWorld.X, _mouseWorld.Y);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
