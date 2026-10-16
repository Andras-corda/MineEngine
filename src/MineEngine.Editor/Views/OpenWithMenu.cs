using System.Windows;
using System.Windows.Controls;
using MineEngine.Editor.Settings;
using MineEngine.Editor.ViewModels;

namespace MineEngine.Editor.Views;

/// <summary>
/// Sous-menu « Ouvrir avec » d'un menu contextuel (entrée marquée <see cref="Tag"/>), rempli à
/// l'ouverture : programmes configurés pour l'extension du fichier, application par défaut de
/// Windows, choix d'une autre application, puis lien vers les paramètres.
/// </summary>
public static class OpenWithMenu
{
    public const string Tag = "openWith";

    public static void Fill(ContextMenu menu, string? file, MainViewModel main)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(main);
        if (menu.Items.OfType<MenuItem>().FirstOrDefault(i => Equals(i.Tag, Tag)) is not { } openWith)
        {
            return;
        }

        openWith.Items.Clear();
        bool available = file is not null && File.Exists(file);
        openWith.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        if (!available)
        {
            return;
        }

        IReadOnlyList<ExternalProgram> programs = main.ProgramsFor(file!);
        foreach (ExternalProgram program in programs)
        {
            var item = new MenuItem { Header = program.Name, ToolTip = program.Path, IsEnabled = program.Exists };
            item.Click += (_, _) => main.OpenWith(program, file!);
            openWith.Items.Add(item);
        }

        if (programs.Count > 0)
        {
            openWith.Items.Add(new Separator());
        }

        var standard = new MenuItem { Header = Tr.T("Application par défaut de Windows") };
        standard.Click += (_, _) => main.OpenWithDefault(file!);
        openWith.Items.Add(standard);

        var other = new MenuItem { Header = Tr.T("Choisir une autre application...") };
        other.Click += (_, _) => main.ChooseProgram(file!);
        openWith.Items.Add(other);

        openWith.Items.Add(new Separator());
        openWith.Items.Add(new MenuItem { Header = Tr.T("Configurer les programmes..."), Command = main.OpenSettingsCommand });
    }
}
