using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MineEngine.Editor.Services;

namespace MineEngine.Editor.Views;

/// <summary>
/// Button that reads the next key of the keyboard or button of the mouse, like the keybinds of
/// Unreal: a click starts listening, the key pressed becomes <see cref="KeyName"/>; Escape, or the
/// focus leaving the button, stops without change.
/// </summary>
public sealed class KeyCaptureButton : ToggleButton
{
    public static readonly DependencyProperty KeyNameProperty = DependencyProperty.Register(
        nameof(KeyName), typeof(string), typeof(KeyCaptureButton), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>Name of the key of the game (see KeybindAsset.Keys).</summary>
    public string KeyName
    {
        get => (string)GetValue(KeyNameProperty);
        set => SetValue(KeyNameProperty, value);
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        Focus();
        Mouse.Capture(this, CaptureMode.Element);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsChecked != true)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        // Alt comes as a system key.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;
        if (key == Key.Escape)
        {
            IsChecked = false;
        }
        else if (PhysicalKeys.FromKey(key) is { } name)
        {
            Choose(name);
        }
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        if (IsChecked != true)
        {
            base.OnPreviewMouseDown(e);
            return;
        }

        e.Handled = true;
        Choose(PhysicalKeys.FromMouse(e.ChangedButton));
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        IsChecked = false;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        IsChecked = false;
    }

    private void Choose(string name)
    {
        KeyName = name;
        IsChecked = false;
    }
}
