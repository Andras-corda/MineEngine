using System.Windows;
using System.Windows.Controls;

namespace MineEngine.Editor.Views;

/// <summary>
/// A line of an element tab (V0.7.13, like MCreator): the label on the left with a "?" that shows its
/// help, an optional note under the label, and the field on the right (the content).
/// </summary>
public sealed class PropertyRow : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(PropertyRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HelpProperty = DependencyProperty.Register(
        nameof(Help), typeof(string), typeof(PropertyRow), new PropertyMetadata(null));

    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note), typeof(string), typeof(PropertyRow), new PropertyMetadata(null));

    public PropertyRow()
    {
        Focusable = false;
        IsTabStop = false;
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Explanation shown when the pointer is on the "?" of the label; none hides the "?".</summary>
    public string? Help
    {
        get => (string?)GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
    }

    /// <summary>Short line under the label ("Leave 0 to disable damage"), or none.</summary>
    public string? Note
    {
        get => (string?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }
}
