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
    bool EditProjectSettings(ProjectSettings settings, string projectRoot);

    void ShowMdkManager();

    /// <summary>Fenêtre des paramètres de l'éditeur ; vrai si l'utilisateur demande de relancer Mine Engine.</summary>
    bool ShowSettings();

    /// <summary>Fichier à importer (filtre au format "Libellé|*.ext") ; null si l'utilisateur annule.</summary>
    string? AskFileToOpen(string title, string filter);

    /// <summary>Emplacement où exporter un fichier ; null si l'utilisateur annule.</summary>
    string? AskFileToSave(string title, string filter, string defaultFileName);

    string? AskProjectToOpen();

    string? AskTextureFile();

    /// <summary>Une ou plusieurs images PNG à importer ; liste vide si l'utilisateur annule.</summary>
    IReadOnlyList<string> AskTextureFiles();

    /// <summary>Un ou plusieurs sons OGG à importer ; liste vide si l'utilisateur annule.</summary>
    IReadOnlyList<string> AskSoundFiles();

    /// <summary>One or more 3D models to import; empty list if the user cancels.</summary>
    IReadOnlyList<string> AskModelFiles();

    /// <summary>Import window of a 3D model (textures, variants, layers, element to create); true to import.</summary>
    bool ShowModelImport(ViewModels.Assets.ModelImportViewModel viewModel);

    /// <summary>Asset browser (textures or 3D models of the project and of Minecraft); true when a choice was made.</summary>
    bool ShowAssetBrowser(ViewModels.Assets.AssetBrowserViewModel viewModel);

    string? AskMdkArchive();

    string? AskFolder(string title);

    UnsavedChangesChoice AskUnsavedChanges(string projectName);

    bool Confirm(string title, string message);

    /// <summary>Demande un texte court (nom de fichier...) ; null si l'utilisateur annule.</summary>
    string? AskText(string title, string prompt, string defaultValue);

    /// <summary>Fichier modifié à la fermeture de son onglet : enregistrer, abandonner ou annuler.</summary>
    UnsavedChangesChoice AskUnsavedFile(string fileName);

    void ShowError(string title, string message);
}

/// <summary>Interactions avec le système (explorateur de fichiers...).</summary>
public interface IShellService
{
    void OpenFolder(string directory);

    /// <summary>Opens the Windows file explorer with the file selected.</summary>
    void RevealFile(string file);

    /// <summary>Ouvre un fichier avec l'application associée par Windows.</summary>
    void OpenFile(string file);

    /// <summary>Ouvre une adresse web (http ou https) dans le navigateur par défaut.</summary>
    void OpenUrl(string url);

    /// <summary>Ouvre un fichier avec un programme choisi (chemin de son .exe).</summary>
    void OpenWith(string program, string file);

    /// <summary>Fenêtre « Ouvrir avec » de Windows, pour choisir une autre application.</summary>
    void ShowOpenWithDialog(string file);
}
