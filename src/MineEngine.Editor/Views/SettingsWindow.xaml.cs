using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MineEngine.Editor.ViewModels;

namespace MineEngine.Editor.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) => DialogResult = accepted;
        Closed += (_, _) => viewModel.DiscardPreview();

        // Le thème actif peut être loin dans la liste : on l'amène à l'écran.
        ThemeList.Loaded += (_, _) =>
        {
            if (ThemeList.SelectedItem is { } selected)
            {
                ThemeList.ScrollIntoView(selected);
            }
        };
    }

    /// <summary>
    /// Case d'un raccourci : la touche pressée (avec Ctrl, Maj, Alt) devient le raccourci de la commande.
    /// Retour arrière ou Suppr seuls le retirent ; Échap et Tab gardent leur rôle habituel.
    /// </summary>
    private void OnShortcutKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ShortcutRowViewModel row })
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (key is Key.Escape or Key.Tab || (key == Key.Enter && modifiers == ModifierKeys.None))
        {
            return;
        }

        e.Handled = true;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return;
        }

        if (modifiers == ModifierKeys.None && key is Key.Back or Key.Delete)
        {
            row.Gesture = string.Empty;
            return;
        }

        try
        {
            row.Gesture = ShortcutCatalog.Format(new KeyGesture(key, modifiers));
        }
        catch (NotSupportedException)
        {
            // Une lettre ou un chiffre seul n'est pas un raccourci : il faut Ctrl, Maj ou Alt.
            row.Conflict = Tr.T("Ajoutez Ctrl, Maj ou Alt à cette touche.");
        }
    }
}
