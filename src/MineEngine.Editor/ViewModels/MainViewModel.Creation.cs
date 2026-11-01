using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Creation;
using MineEngine.Project.Commands;

namespace MineEngine.Editor.ViewModels;

/// <summary>Creation window (V0.7.14): every addition goes through it, with the name and the template.</summary>
public sealed partial class MainViewModel
{
    /// <summary>Opens the creation window.</summary>
    public ICommand NewElementCommand { get; private set; } = null!;

    /// <summary>Opens the creation window on the <see cref="NewElementEntry"/> given as parameter.</summary>
    public ICommand NewElementOfCommand { get; private set; } = null!;

    private void InitializeCreationCommands()
    {
        NewElementCommand = new RelayCommand(() => OpenNewElement(null), () => CanEdit);
        NewElementOfCommand = new RelayCommand<NewElementEntry>(OpenNewElement, _ => CanEdit);
    }

    /// <summary>Opens the creation window on <paramref name="entry"/>.</summary>
    private void OpenNewElement(NewElementEntry? entry)
    {
        if (_project is null)
        {
            return;
        }

        _dialogs.ShowNewElement(new NewElementViewModel(CreateElement, entry, Explorer.JavaClassPackage()));
    }

    /// <summary>Makes what the window asks and opens it; returns why it could not, or null.</summary>
    private string? CreateElement(NewElementRequest request)
    {
        if (_project is null)
        {
            return Tr.T("No project is open.");
        }

        NewElementEntry entry = request.Entry;
        switch (entry.Action)
        {
            case NewElementAction.Asset when entry.AssetType is { } type:
                Asset asset = _project.AddAsset(type, request.Name, request.Template?.Apply ?? entry.Prepare);
                OpenAsset(asset);
                return null;

            case NewElementAction.Graph when entry.GraphKind is { } kind:
                OpenGraphDocument(_project.AddGraph(kind, displayName: request.Name));
                return null;

            case NewElementAction.JavaClass when entry.JavaKind is { } javaKind:
                return Explorer.CreateJavaClass(javaKind, request.Name);

            case NewElementAction.Folder:
                string path = AssetFolders.Combine(_project.CurrentFolder, request.Name);
                if (!AssetFolders.IsValidName(request.Name))
                {
                    return Tr.T("Invalid name: no \"/\", \"\\\" or character forbidden by Windows (64 characters at most).");
                }

                if (FolderOperations.Exists(_project.Model, path))
                {
                    return Tr.F("A folder \"{0}\" already exists here.", request.Name);
                }

                _project.History.Execute(FolderOperations.Create(_project.Model, path));
                _project.CurrentFolder = path;
                return null;

            default:
                return Tr.T("This element is planned for a later version.");
        }
    }
}
