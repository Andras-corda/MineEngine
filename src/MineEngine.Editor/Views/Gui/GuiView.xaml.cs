using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Editor.ViewModels.Gui;

namespace MineEngine.Editor.Views.Gui;

/// <summary>
/// GUI editor (see GuiDocumentViewModel): dragging, resizing, nudging and zooming; double-click to edit
/// (text on the canvas, texture of an image, kind of a slot); right-click for the actions of a component.
/// </summary>
public partial class GuiView : UserControl
{
    private const double MinZoom = 1;
    private const double MaxZoom = 6;

    private GuiComponentViewModel? _dragged;
    private bool _resizing;
    private Vector _grab;

    public GuiView()
    {
        InitializeComponent();
    }

    private GuiDocumentViewModel? Document => DataContext as GuiDocumentViewModel;

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        // A click in the text being typed stays in it.
        if (Document is not { } document || IsInTextBox(e.OriginalSource as DependencyObject))
        {
            return;
        }

        Focus();
        GuiComponentViewModel? component = (e.OriginalSource as FrameworkElement)?.DataContext as GuiComponentViewModel;
        document.Selected = component;
        if (component is null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            document.Edit(component);
            e.Handled = true;
            return;
        }

        Point position = e.GetPosition(Canvas);
        _resizing = e.OriginalSource is FrameworkElement { Tag: "resize" };
        _grab = _resizing ? default : position - new Point(component.X, component.Y);
        _dragged = component;
        document.BeginGesture();
        Canvas.CaptureMouse();
        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragged is null || Document is not { } document || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point position = e.GetPosition(Canvas);
        if (_resizing)
        {
            document.ResizeTo(_dragged, position.X, position.Y);
        }
        else
        {
            document.MoveTo(_dragged, position.X - _grab.X, position.Y - _grab.Y);
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragged is not null)
        {
            _dragged = null;
            Canvas.ReleaseMouseCapture();
            Document?.EndGesture();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // The keys belong to the fields of the Properties while one of them is typed in.
        if (Document is not { } document || Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Left:
                document.Nudge(-step, 0);
                break;
            case Key.Right:
                document.Nudge(step, 0);
                break;
            case Key.Up:
                document.Nudge(0, -step);
                break;
            case Key.Down:
                document.Nudge(0, step);
                break;
            case Key.Delete or Key.Back:
                Run(document.DeleteSelectedCommand);
                break;
            case Key.D when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                Run(document.DuplicateSelectedCommand);
                break;
            case Key.C when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                Run(document.CopyCommand);
                break;
            case Key.V when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                Run(document.PasteCommand);
                break;
            case Key.F2:
                Run(document.RenameSelectedCommand);
                break;
            case Key.Enter:
                Run(document.EditSelectedCommand);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>A right-click selects the component under the mouse before the menu opens.</summary>
    private void OnCanvasRightDown(object sender, MouseButtonEventArgs e)
    {
        if (Document is { } document)
        {
            document.Selected = (e.OriginalSource as FrameworkElement)?.DataContext as GuiComponentViewModel;
        }
    }

    /// <summary>The actions of the selected component (canvas and list), or Paste alone on an empty place.</summary>
    private void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || Document is not { } document)
        {
            return;
        }

        menu.Items.Clear();
        MenuItem Item(string header, ICommand command, string? gesture = null, string? icon = null) => new()
        {
            Header = header,
            Command = command,
            InputGestureText = gesture ?? string.Empty,
            Icon = icon is null ? null : new IconView { Icon = icon, IconSize = 14 },
        };
        if (document.Selected is { } selected)
        {
            string edit = selected.Kind switch
            {
                GuiComponentKind.Image => Tr.T("Choose the image..."),
                GuiComponentKind.Slot => Tr.T("Change the kind of slot"),
                _ => Tr.T("Edit the text"),
            };
            menu.Items.Add(Item(edit, document.EditSelectedCommand, Tr.T("Double-click"), "Glyph.Rename"));
            menu.Items.Add(Item(Tr.T("Rename..."), document.RenameSelectedCommand, "F2"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(Tr.T("Duplicate"), document.DuplicateSelectedCommand, "Ctrl+D", "Glyph.Copy"));
            menu.Items.Add(Item(Tr.T("Copy"), document.CopyCommand, "Ctrl+C"));
            menu.Items.Add(Item(Tr.T("Paste"), document.PasteCommand, "Ctrl+V", "Glyph.Paste"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(Tr.T("Delete"), document.DeleteSelectedCommand, Tr.T("Del"), "Glyph.Delete"));
        }
        else
        {
            menu.Items.Add(Item(Tr.T("Paste"), document.PasteCommand, "Ctrl+V", "Glyph.Paste"));
        }
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Document is { } document && (e.OriginalSource as FrameworkElement)?.DataContext is GuiComponentViewModel)
        {
            Run(document.RenameSelectedCommand);
        }
    }

    // ----- Typing on the canvas -----

    private void OnInlineVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox { IsVisible: true } box)
        {
            box.Dispatcher.BeginInvoke(() =>
            {
                box.Focus();
                box.SelectAll();
            });
        }
    }

    private void OnInlineKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox { DataContext: GuiComponentViewModel component } && e.Key is Key.Enter or Key.Escape)
        {
            component.EndEdit(keep: e.Key == Key.Enter);
            Focus();
            e.Handled = true;
        }
    }

    private void OnInlineLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: GuiComponentViewModel component })
        {
            component.EndEdit(keep: true);
        }
    }

    private static bool IsInTextBox(DependencyObject? element)
    {
        for (DependencyObject? node = element; node is not null; node = node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                 ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                 : LogicalTreeHelper.GetParent(node))
        {
            if (node is TextBox)
            {
                return true;
            }
        }

        return false;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        double zoom = Math.Clamp(Zoom.ScaleX + (e.Delta > 0 ? 0.5 : -0.5), MinZoom, MaxZoom);
        Zoom.ScaleX = zoom;
        Zoom.ScaleY = zoom;
        e.Handled = true;
    }

    private static void Run(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}

/// <summary>Name of a kind of component (toolbox).</summary>
public sealed class GuiKindLabelConverter : IValueConverter
{
    public static GuiKindLabelConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GuiComponentKind kind ? Tr.F("Add: {0}", GuiComponentViewModel.LabelOf(kind)) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Icon of a kind of component (toolbox).</summary>
public sealed class GuiKindIconConverter : IValueConverter
{
    public static GuiKindIconConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GuiComponentKind kind ? GuiComponentViewModel.IconOf(kind) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
