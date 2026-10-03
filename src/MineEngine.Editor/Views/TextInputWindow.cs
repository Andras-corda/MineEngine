using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ModernWpf.Controls.Primitives;

namespace MineEngine.Editor.Views;

/// <summary>Petite fenêtre qui demande un texte (nom de fichier, de dossier...).</summary>
public sealed class TextInputWindow : Window
{
    private readonly TextBox _input;

    public TextInputWindow(string title, string prompt, string defaultValue)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        WindowHelper.SetUseModernWindowStyle(this, true);

        _input = new TextBox { Text = defaultValue, Margin = new Thickness(0, 8, 0, 16) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        ok.SetResourceReference(StyleProperty, "AccentButtonStyle");
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = "Annuler", IsCancel = true, MinWidth = 90 };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_input);
        panel.Children.Add(buttons);
        Content = panel;

        // Sélectionne le nom sans l'extension, comme l'Explorateur de Windows.
        Loaded += (_, _) =>
        {
            _input.Focus();
            int dot = defaultValue.LastIndexOf('.');
            _input.Select(0, dot > 0 ? dot : defaultValue.Length);
            Keyboard.Focus(_input);
        };
    }

    public string Value => _input.Text.Trim();
}
