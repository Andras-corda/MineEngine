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
        var viewModel = ProjectPropertiesViewModel.ForNewProject(_mdks, _defaultProjectsDirectory, PickProjectsFolder, ShowMdkManager)
            .WithIconPicker(PickIcon);
        var window = new ProjectPropertiesWindow(viewModel) { Owner = Owner };
        return window.ShowDialog() == true ? new NewProjectRequest(viewModel.ParentDirectory, viewModel.CreateSettings()) : null;
    }

    public bool EditProjectSettings(ProjectSettings settings, string projectRoot)
    {
        var viewModel = ProjectPropertiesViewModel.ForExistingProject(_mdks, settings, projectRoot, PickProjectsFolder, ShowMdkManager)
            .WithIconPicker(PickIcon);
        var window = new ProjectPropertiesWindow(viewModel) { Owner = Owner };
        if (window.ShowDialog() != true)
        {
            return false;
        }

        viewModel.ApplyTo(settings);
        return true;
    }

    private string? PickIcon() => AskFileToOpen(Tr.T("Mod icon"), Tr.T("PNG images (*.png)|*.png"));

    public void ShowMdkManager()
    {
        var viewModel = new MdkManagerViewModel(_mdks, this, _shell);
        var window = new MdkManagerWindow(viewModel) { Owner = Owner };
        window.ShowDialog();
    }

    public bool ShowSettings()
    {
        var viewModel = new SettingsViewModel(_preferences, this);
        var window = new SettingsWindow(viewModel) { Owner = Owner };
        return window.ShowDialog() == true && viewModel.RestartRequested;
    }

    public string? AskFileToOpen(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    public string? AskFileToSave(string title, string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = string.Join("_", defaultFileName.Split(Path.GetInvalidFileNameChars())),
            OverwritePrompt = true,
        };
        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    public string? AskProjectToOpen()
    {
        var dialog = new OpenFileDialog
        {
            Title = Tr.T("Open a Mine Engine project"),
            Filter = Tr.F("Mine Engine project (*{0})|*{1};{2}|All files (*.*)|*.*", ProjectLayout.ProjectFileExtension, ProjectLayout.ProjectFileExtension, ProjectLayout.LegacyProjectFileName),
            InitialDirectory = Directory.Exists(_defaultProjectsDirectory) ? _defaultProjectsDirectory : string.Empty,
        };
        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    public string? AskTextureFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = Tr.T("Choose a texture"),
            Filter = Tr.T("PNG images (*.png)|*.png"),
        };
        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    public bool ShowModelImport(ViewModels.Assets.ModelImportViewModel viewModel) =>
        new Views.ModelImportWindow(viewModel) { Owner = Owner }.ShowDialog() == true;

    public IReadOnlyList<string> AskTextureFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = Tr.T("Import textures"),
            Filter = Tr.T("PNG images (*.png)|*.png"),
            Multiselect = true,
        };
        return ShowDialog(dialog) ? dialog.FileNames : [];
    }

    public IReadOnlyList<string> AskModelFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = Tr.T("Import 3D models"),
            Filter = Tr.T("3D models") + $" ({MineEngine.Assets.ModelFormats.DialogPattern})|{MineEngine.Assets.ModelFormats.DialogPattern}",
            Multiselect = true,
        };
        return ShowDialog(dialog) ? dialog.FileNames : [];
    }

    public IReadOnlyList<string> AskSoundFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = Tr.T("Import sounds"),
            Filter = Tr.T("OGG Vorbis sounds (*.ogg)|*.ogg"),
            Multiselect = true,
        };
        return ShowDialog(dialog) ? dialog.FileNames : [];
    }

    public string? AskMdkArchive()
    {
        var dialog = new OpenFileDialog
        {
            Title = Tr.T("Import an MDK"),
            Filter = Tr.T("MDK archive (*.zip)|*.zip"),
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
            Tr.F("The project '{0}' has unsaved changes.\n\nDo you want to save them?", projectName),
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

    public string? AskText(string title, string prompt, string defaultValue)
    {
        var window = new TextInputWindow(title, prompt, defaultValue) { Owner = Owner };
        return window.ShowDialog() == true ? window.Value : null;
    }

    public UnsavedChangesChoice AskUnsavedFile(string fileName)
    {
        MessageBoxResult result = ShowMessage(
            Tr.F("The file '{0}' has unsaved changes.\n\nDo you want to save them?", fileName),
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

    private static MessageBoxResult ShowMessage(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image)
            : MessageBox.Show(message, title, buttons, image);

    private string? PickProjectsFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = Tr.T("Folder in which to create the project"),
            InitialDirectory = Directory.Exists(_defaultProjectsDirectory) ? _defaultProjectsDirectory : string.Empty,
        };
        return ShowDialog(dialog) ? dialog.FolderName : null;
    }

    private static bool ShowDialog(CommonDialog dialog) =>
        (Owner is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true;
}
