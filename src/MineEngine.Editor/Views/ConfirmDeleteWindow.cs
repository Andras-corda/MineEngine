using System.Windows;
using System.Windows.Controls;
using ModernWpf.Controls.Primitives;

namespace MineEngine.Editor.Views;

/// <summary>
/// "Are you sure?" before a deletion (V0.7.14), with a box to stop asking until Mine Engine is
/// closed; the Settings can stop asking for good.
/// </summary>
public sealed class ConfirmDeleteWindow : Window
{
    private readonly CheckBox _skip;

    public ConfirmDeleteWindow(string message)
    {
        Title = Tr.T("Delete");
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        WindowHelper.SetUseModernWindowStyle(this, true);
        SetResourceReference(BackgroundProperty, "HubBackgroundBrush");

        _skip = new CheckBox { Content = Tr.T("Do not ask again until Mine Engine is closed"), Margin = new Thickness(0, 14, 0, 4) };
        var hint = new TextBlock
        {
            Text = Tr.T("To never ask again: Settings > General."),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 16),
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "HintBrush");

        var delete = new Button { Content = Tr.T("Delete"), IsDefault = true, MinWidth = 100, Margin = new Thickness(0, 0, 8, 0) };
        delete.SetResourceReference(StyleProperty, "AccentButtonStyle");
        delete.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = Tr.T("Cancel"), IsCancel = true, MinWidth = 100 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { delete, cancel } };

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock { Text = Tr.T("Are you sure?"), FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(0, 0, 0, 8) },
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                _skip,
                hint,
                buttons,
            },
        };
        Loaded += (_, _) => cancel.Focus();
    }

    /// <summary>The box "do not ask again" is ticked.</summary>
    public bool SkipUntilRestart => _skip.IsChecked == true;
}
