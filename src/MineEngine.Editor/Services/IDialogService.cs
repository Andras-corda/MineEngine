using MineEngine.Project;

namespace MineEngine.Editor.Services;

public enum UnsavedChangesChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Informations saisies pour créer un nouveau projet.</summary>
public sealed class NewProjectRequest
{
    public NewProjectRequest(string parentDirectory, ProjectSettings settings)
    {
        ParentDirectory = parentDirectory;
        Settings = settings;
    }

    public string ParentDirectory { get; }

    public ProjectSettings Settings { get; }
}

/// <summary>
/// Boîtes de dialogue utilisées par les ViewModels. Les ViewModels ne créent
/// jamais de fenêtre eux-mêmes : ils passent par cette interface.
/// </summary>
public interface IDialogService
{
    NewProjectRequest? AskNewProject();

    /// <summary>Affiche les paramètres du projet ; vrai si l'utilisateur a validé des modifications.</summary>
    bool EditProjectSettings(ProjectSettings settings);

    void ShowMdkManager();

    /// <summary>Fenêtre des paramètres de l'éditeur (thème...).</summary>
    void ShowSettings();

    string? AskProjectToOpen();

    string? AskTextureFile();

    string? AskMdkArchive();

    string? AskFolder(string title);

    UnsavedChangesChoice AskUnsavedChanges(string projectName);

    bool Confirm(string title, string message);

    void ShowError(string title, string message);
}

/// <summary>Interactions avec le système (explorateur de fichiers...).</summary>
public interface IShellService
{
    void OpenFolder(string directory);
}
