using System.Windows;
using Microsoft.Win32;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.Settings;
using MineEngine.Editor.ViewModels.Mdk;
using MineEngine.Editor.Views;
using MineEngine.Project;

namespace MineEngine.Editor.Services;

/// <summary>Implémentation WPF des boîtes de dialogue.</summary>
public sealed class WpfDialogService : IDialogService
{
    private const string ApplicationTitle = "Mine Engine";

    private readonly string _defaultProjectsDirectory;
    private readonly MdkServices _mdks;
    private readonly IShellService _shell;
    private readonly EditorPreferences _preferences;

    public WpfDialogService(string defaultProjectsDirectory, MdkServices mdks, IShellService shell, EditorPreferences preferences)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultProjectsDirectory);
        _defaultProjectsDirectory = defaultProjectsDirectory;
        _mdks = mdks ?? throw new ArgumentNullException(nameof(mdks));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    /// <summary>Fenêtre active (une boîte de dialogue peut en ouvrir une autre), sinon la fenêtre principale.</summary>
    private static Window? Owner
    {
        get
        {
            Application? application = Application.Current;
            if (application is null)
            {
                return null;
            }

            return application.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                   ?? (application.MainWindow is { IsVisible: true } main ? main : null);
        }
    }

    public NewProjectRequest? AskNewProject()
    {
        var viewModel = ProjectPropertiesViewModel.ForNewProject(_mdks, _defaultProjectsDirectory, PickProjectsFolder, ShowMdkManager);
        var window = new ProjectPropertiesWindow(viewModel) { Owner = Owner };
        return window.ShowDialog() == true ? new NewProjectRequest(viewModel.ParentDirectory, viewModel.CreateSettings()) : null;
    }

    public bool EditProjectSettings(ProjectSettings settings)
    {
        var viewModel = ProjectPropertiesViewModel.ForExistingProject(_mdks, settings, PickProjectsFolder, ShowMdkManager);
        var window = new ProjectPropertiesWindow(viewModel) { Owner = Owner };
        if (window.ShowDialog() != true)
        {
            return false;
        }

        viewModel.ApplyTo(settings);
        return true;
    }

    public void ShowMdkManager()
    {
        var viewModel = new MdkManagerViewModel(_mdks, this, _shell);
        var window = new MdkManagerWindow(viewModel) { Owner = Owner };
        window.ShowDialog();
    }

    public void ShowSettings()
    {
        var window = new SettingsWindow(new SettingsViewModel(_preferences)) { Owner = Owner };
        window.ShowDialog();
    }

    public string? AskProjectToOpen()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Ouvrir un projet Mine Engine",
            Filter = $"Projet Mine Engine ({ProjectLayout.ProjectFileName})|{ProjectLayout.ProjectFileName}|Tous les fichiers (*.*)|*.*",
            InitialDirectory = Directory.Exists(_defaultProjectsDirectory) ? _defaultProjectsDirectory : string.Empty,
        };
        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    public string? AskTextureFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choisir une texture",
            Filter = "Images PNG (*.png)|*.png",
        };
        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    public string? AskMdkArchive()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importer un MDK",
            Filter = "Archive de MDK (*.zip)|*.zip",
        };
        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    public string? AskFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return ShowDialog(dialog) ? dialog.FolderName : null;
    }

    public UnsavedChangesChoice AskUnsavedChanges(string projectName)
    {
        MessageBoxResult result = ShowMessage(
            $"Le projet '{projectName}' contient des modifications non enregistrées.\n\nVoulez-vous les enregistrer ?",
            ApplicationTitle,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => UnsavedChangesChoice.Save,
            MessageBoxResult.No => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };
    }

    public bool Confirm(string title, string message) =>
        ShowMessage(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowError(string title, string message) =>
        ShowMessage(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    private static MessageBoxResult ShowMessage(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image)
            : MessageBox.Show(message, title, buttons, image);

    private string? PickProjectsFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Dossier dans lequel créer le projet",
            InitialDirectory = Directory.Exists(_defaultProjectsDirectory) ? _defaultProjectsDirectory : string.Empty,
        };
        return ShowDialog(dialog) ? dialog.FolderName : null;
    }

    private static bool ShowDialog(CommonDialog dialog) =>
        (Owner is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true;
}
