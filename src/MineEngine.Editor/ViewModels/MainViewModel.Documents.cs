using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.ViewModels.Graphs;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;
using MineEngine.Project.Commands;

namespace MineEngine.Editor.ViewModels;

/// <summary>
/// Partie "onglets et panneau Détails" de la fenêtre principale : fichiers ouverts dans
/// des onglets de code (le Dashboard est l'onglet permanent), onglet actif et cible du
/// panneau Détails (asset sélectionné ou fichier actif).
/// </summary>
public sealed partial class MainViewModel
{
    private DocumentViewModel? _activeDocument;
    private IDetailsTarget? _details;
    private AssetSerializer? _assetSerializer;
    private ProjectFiles? _files;
    private IReadOnlyList<Diagnostic> _buildDiagnostics = [];
    private IReadOnlyList<JavaCompletionEntry>? _javaCompletions;

    /// <summary>Demande à la vue d'afficher un onglet (null : le Dashboard).</summary>
    public event EventHandler<DocumentViewModel?>? DocumentActivationRequested;

    /// <summary>Onglets ouverts (fichiers et graphes, sans le Dashboard).</summary>
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    /// <summary>Onglet actif, ou null quand le Dashboard est affiché.</summary>
    public DocumentViewModel? ActiveDocument
    {
        get => _activeDocument;
        set
        {
            if (SetProperty(ref _activeDocument, value))
            {
                OnPropertyChanged(nameof(HasActiveDocument));
                OnPropertyChanged(nameof(ActiveCodeDocument));
                OnPropertyChanged(nameof(HasActiveCodeDocument));
                Details = value ?? (IDetailsTarget?)_project?.SelectedAsset;
            }
        }
    }

    public bool HasActiveDocument => _activeDocument is not null;

    /// <summary>Onglet actif s'il contient du texte (curseur, encodage... dans la barre de statut).</summary>
    public CodeDocumentViewModel? ActiveCodeDocument => _activeDocument as CodeDocumentViewModel;

    public bool HasActiveCodeDocument => _activeDocument is CodeDocumentViewModel;

    /// <summary>Ce qu'affiche le panneau Détails : l'asset sélectionné ou l'onglet de code actif.</summary>
    public IDetailsTarget? Details
    {
        get => _details;
        private set
        {
            if (SetProperty(ref _details, value))
            {
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public bool HasDetails => _details is not null;

    public ICommand CloseDocumentCommand { get; private set; } = null!;

    public ICommand CloseActiveDocumentCommand { get; private set; } = null!;

    public ICommand ReloadDocumentCommand { get; private set; } = null!;

    public ICommand SaveDocumentCommand { get; private set; } = null!;

    public ICommand RevealDocumentCommand { get; private set; } = null!;

    public ICommand ShowDashboardCommand { get; private set; } = null!;

    /// <summary>Ouvre l'aperçu JSON de l'asset sélectionné.</summary>
    public ICommand OpenAssetSourceCommand { get; private set; } = null!;

    /// <summary>Affiche l'asset passé en paramètre dans le panneau Détails (double-clic dans le Dashboard).</summary>
    public ICommand ShowAssetDetailsCommand { get; private set; } = null!;

    /// <summary>Ouvre l'éditeur nodal d'un graphe (paramètre : son ViewModel, sinon l'asset sélectionné).</summary>
    public ICommand OpenGraphEditorCommand { get; private set; } = null!;

    /// <summary>Appelé par la vue quand l'utilisateur ferme un onglet ; faux pour l'annuler.</summary>
    public bool RequestCloseDocument(DocumentViewModel document)
    {
        if (document.IsDirty)
        {
            switch (_dialogs.AskUnsavedFile(document.FileName))
            {
                case UnsavedChangesChoice.Save when !document.Save():
                case UnsavedChangesChoice.Cancel:
                    return false;
            }
        }

        RemoveDocument(document);
        return true;
    }

    private void InitializeDocumentCommands()
    {
        CloseDocumentCommand = new RelayCommand<DocumentViewModel>(d => RequestCloseDocument(d));
        CloseActiveDocumentCommand = new RelayCommand(() => RequestCloseDocument(_activeDocument!), () => _activeDocument is not null);
        ReloadDocumentCommand = new RelayCommand(
            () => ((FileDocumentViewModel)_activeDocument!).Reload(), () => _activeDocument is FileDocumentViewModel);
        SaveDocumentCommand = new RelayCommand(
            () => _activeDocument!.Save(), () => _activeDocument is { IsReadOnly: false, IsDirty: true });
        RevealDocumentCommand = new RelayCommand(
            () => _shell.OpenFolder(Path.GetDirectoryName(((FileDocumentViewModel)_activeDocument!).FullPath)!),
            () => _activeDocument is FileDocumentViewModel);
        ShowDashboardCommand = new RelayCommand(() => DocumentActivationRequested?.Invoke(this, null), () => HasProject);
        OpenAssetSourceCommand = new RelayCommand(
            () => OpenAssetDocument(_project!.SelectedAsset!.Model), () => _project?.SelectedAsset is not null);
        ShowAssetDetailsCommand = new RelayCommand<AssetViewModel>(asset =>
        {
            _project?.SelectAsset(asset.Model.Id);
            Details = asset;
            PanelRequested?.Invoke(this, InspectorPanelId);
        });
        OpenGraphEditorCommand = new RelayCommand<AssetViewModel?>(
            asset =>
            {
                if ((asset ?? _project?.SelectedAsset)?.Model is GraphAsset graph)
                {
                    OpenGraphDocument(graph);
                }
            },
            asset => (asset ?? _project?.SelectedAsset)?.Model is GraphAsset);
        OpenAssetEditorCommand = new RelayCommand<AssetViewModel?>(
            asset =>
            {
                if ((asset ?? _project?.SelectedAsset)?.Model is { } model)
                {
                    OpenAsset(model);
                }
            },
            asset => (asset ?? _project?.SelectedAsset) is not null);
    }

    // ----- Dossiers du Dashboard -----

    /// <summary>Crée un dossier dans le dossier ouvert et l'ouvre.</summary>
    public ICommand NewFolderCommand { get; private set; } = null!;

    /// <summary>Renomme un dossier (paramètre : le dossier) ; son contenu suit.</summary>
    public ICommand RenameFolderCommand { get; private set; } = null!;

    /// <summary>Supprime un dossier (paramètre : le dossier) ; son contenu remonte d'un niveau.</summary>
    public ICommand DeleteFolderCommand { get; private set; } = null!;

    /// <summary>Déplace l'élément sélectionné vers un dossier (paramètre : son chemin, "" pour la racine).</summary>
    public ICommand MoveAssetToFolderCommand { get; private set; } = null!;

    /// <summary>Déplace des éléments vers un dossier (glisser-déposer), en une étape annulable.</summary>
    public void MoveAssetsToFolder(IEnumerable<AssetViewModel> assets, string folder)
    {
        if (_project is null)
        {
            return;
        }

        List<Asset> moved = [.. assets.Select(a => a.Model).Where(a => a.Folder != folder)];
        if (moved.Count > 0)
        {
            _project.History.Execute(FolderOperations.MoveAssets(moved, folder));
            StatusText = moved.Count == 1 ? Tr.F("« {0} » déplacé", moved[0].DisplayName) : Tr.F("{0} éléments déplacés", moved.Count);
        }
    }

    private void InitializeFolderCommands()
    {
        NewFolderCommand = new RelayCommand(NewFolder, () => CanEdit);
        RenameFolderCommand = new RelayCommand<FolderItemViewModel>(RenameFolder, _ => CanEdit);
        DeleteFolderCommand = new RelayCommand<FolderItemViewModel>(DeleteFolder, _ => CanEdit);
        MoveAssetToFolderCommand = new RelayCommand<string>(
            folder => MoveAssetsToFolder([_project!.SelectedAsset!], folder ?? string.Empty),
            _ => CanEdit && _project?.SelectedAsset is not null);
    }

    private void NewFolder()
    {
        string parent = _project!.CurrentFolder;
        string? name = AskFolderName(Tr.T("Nouveau dossier"), Tr.T("Nom du dossier :"), Tr.T("Nouveau dossier"), parent);
        if (name is null)
        {
            return;
        }

        string path = AssetFolders.Combine(parent, name);
        _project.History.Execute(FolderOperations.Create(_project.Model, path));
        _project.CurrentFolder = path;
    }

    private void RenameFolder(FolderItemViewModel folder)
    {
        string parent = AssetFolders.Parent(folder.Path);
        string? name = AskFolderName(Tr.T("Renommer le dossier"), Tr.F("Nouveau nom de « {0} » :", folder.Name), folder.Name, parent, folder.Name);
        if (name is null || name == folder.Name)
        {
            return;
        }

        _project!.History.Execute(FolderOperations.Move(
            _project.Model, folder.Path, AssetFolders.Combine(parent, name), Tr.F("Renommer le dossier « {0} »", folder.Name)));
    }

    private void DeleteFolder(FolderItemViewModel folder)
    {
        string where = AssetFolders.Parent(folder.Path) is { Length: > 0 } parent ? Tr.F("« {0} »", AssetFolders.Name(parent)) : Tr.T("la racine");
        if (folder.AssetCount + folder.FolderCount > 0
            && !_dialogs.Confirm(Tr.T("Supprimer le dossier"), Tr.F("Supprimer le dossier « {0} » ? Son contenu ({1}) remonte dans {2}.", folder.Name, folder.ContentLabel.ToLowerInvariant(), where)))
        {
            return;
        }

        _project!.History.Execute(FolderOperations.Dissolve(_project.Model, folder.Path));
    }

    /// <summary>Demande un nom de dossier valide et libre dans <paramref name="parent"/> ; null si l'utilisateur annule.</summary>
    private string? AskFolderName(string title, string prompt, string suggestion, string parent, string? current = null)
    {
        while (true)
        {
            string? name = _dialogs.AskText(title, prompt, suggestion)?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (!AssetFolders.IsValidName(name))
            {
                _dialogs.ShowError(title, Tr.T("Nom invalide : pas de « / », de « \\ » ni de caractère interdit par Windows (64 caractères au plus)."));
            }
            else if (name != current && FolderOperations.Exists(_project!.Model, AssetFolders.Combine(parent, name)))
            {
                _dialogs.ShowError(title, Tr.F("Un dossier « {0} » existe déjà ici.", name));
            }
            else
            {
                return name;
            }

            suggestion = name;
        }
    }

    /// <summary>Ouvre l'onglet d'édition d'un élément (paramètre : son ViewModel, sinon l'élément sélectionné).</summary>
    public ICommand OpenAssetEditorCommand { get; private set; } = null!;

    /// <summary>Crée un élément et l'ouvre dans son onglet d'édition.</summary>
    private void AddAndEdit(AssetType type)
    {
        _project!.AddAsset(type);
        if (_project.SelectedAsset is { } created)
        {
            OpenAsset(created.Model);
        }
    }

    /// <summary>Ouvre un asset dans son onglet : éditeur nodal pour un graphe, éditeur complet sinon.</summary>
    private void OpenAsset(Asset asset, Guid? nodeId = null)
    {
        if (_project is null)
        {
            return;
        }

        _project.SelectAsset(asset.Id);
        if (asset is GraphAsset graph)
        {
            OpenGraphDocument(graph);
            if (nodeId is { } node && _activeDocument is GraphDocumentViewModel editor)
            {
                editor.ShowNode(node);
            }

            return;
        }

        if (_project.Assets.FirstOrDefault(a => a.Model.Id == asset.Id) is not { } viewModel)
        {
            return;
        }

        string id = AssetEditorDocumentViewModel.IdFor(asset.Id);
        DocumentViewModel document = Documents.FirstOrDefault(d => d.Id == id)
                                     ?? AddDocument(new AssetEditorDocumentViewModel(
                                         viewModel, _project.Model.Layout.ToRelativePath(_project.Model.Layout.GetAssetFilePath(asset))));
        Activate(document);
    }

    /// <summary>Un asset supprimé (ou retiré par une annulation) ferme ses onglets.</summary>
    private void OnRegistryChanged(object? sender, AssetRegistryChangedEventArgs e)
    {
        if (e.Change != AssetRegistryChange.Removed)
        {
            return;
        }

        foreach (DocumentViewModel document in Documents.Where(d =>
                     d.Id == AssetEditorDocumentViewModel.IdFor(e.Asset.Id) || d.Id == "graph:" + e.Asset.Id.ToString("N")).ToList())
        {
            RemoveDocument(document);
        }
    }

    /// <summary>
    /// Crée un graphe (événements, fonction, classe...) et l'ouvre. Avec <paramref name="owner"/>,
    /// la définition est intégrée à cette classe (méthode, type imbriqué).
    /// </summary>
    private void AddGraph(GraphKind kind, Guid? owner = null)
    {
        if (_project?.AddGraph(kind, owner) is { } graph)
        {
            OpenGraphDocument(graph);
        }
    }

    /// <summary>Ouvre (ou active) l'onglet d'édition nodale d'un graphe.</summary>
    private void OpenGraphDocument(GraphAsset graph)
    {
        if (_project is null)
        {
            return;
        }

        string id = "graph:" + graph.Id.ToString("N");
        DocumentViewModel document = Documents.FirstOrDefault(d => d.Id == id)
                                     ?? AddDocument(new GraphDocumentViewModel(
                                         graph,
                                         _assetViewModels.CreateContext(_project.Model, assetId => _project.SelectAsset(assetId)),
                                         _project.Model.Layout.ToRelativePath(_project.Model.Layout.GetAssetFilePath(graph)),
                                         (kind, owner) => AddGraph(kind, owner)));
        Activate(document);
    }

    /// <summary>Le serializer des assets sert à l'aperçu JSON ; il est créé au premier besoin.</summary>
    private AssetSerializer AssetSerializer => _assetSerializer ??= new AssetSerializer(_assetViewModels.Catalog);

    /// <summary>Ouvre un fichier de l'explorateur : élément (son onglet d'édition), onglet de code, ou application de Windows.</summary>
    private void OpenFileFromExplorer(string fullPath)
    {
        if (_project is null || _files is null)
        {
            return;
        }

        if (FindAssetForFile(fullPath) is { } asset && fullPath.EndsWith(AssetSerializer.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            OpenAsset(asset);
            return;
        }

        if (!FileDocumentViewModel.CanOpen(fullPath))
        {
            _shell.OpenFile(fullPath);
            return;
        }

        string id = "file:" + Path.GetFullPath(fullPath).ToLowerInvariant();
        DocumentViewModel? document = Documents.FirstOrDefault(d => d.Id == id);
        if (document is null)
        {
            var file = new FileDocumentViewModel(fullPath, _files);
            if (file.IsJava)
            {
                file.Completions = JavaCompletions();
            }

            file.Markers = MarkersFor(file);
            document = AddDocument(file);
        }

        Activate(document);
    }

    /// <summary>Ouvre un fichier du projet et place le curseur sur une ligne (erreur de compilation).</summary>
    private void OpenFileAtLine(string fullPath, int line)
    {
        if (!File.Exists(fullPath))
        {
            StatusText = Tr.T("Fichier introuvable : ") + Path.GetFileName(fullPath);
            return;
        }

        OpenFileFromExplorer(fullPath);
        if (line > 0 && _activeDocument is CodeDocumentViewModel code)
        {
            code.GoToLine(line);
        }
    }

    /// <summary>Un fichier d'asset, une texture ou un son sélectionné dans l'explorateur affiche l'asset.</summary>
    private void SelectFileFromExplorer(string fullPath)
    {
        if (FindAssetForFile(fullPath) is { } asset && _project!.SelectAsset(asset.Id))
        {
            Details = _project.SelectedAsset;
        }
    }

    private Asset? FindAssetForFile(string fullPath)
    {
        if (_project is null)
        {
            return null;
        }

        Project.ModProject model = _project.Model;
        return model.Assets.FirstOrDefault(a =>
            string.Equals(model.Layout.GetAssetFilePath(a), fullPath, StringComparison.OrdinalIgnoreCase)
            || (a is FileAsset file && file.FilePath.Length > 0
                && string.Equals(model.Layout.ToAbsolutePath(file.FilePath), fullPath, StringComparison.OrdinalIgnoreCase)));
    }

    private void OpenAssetDocument(Asset asset)
    {
        if (_project is null)
        {
            return;
        }

        string id = "asset:" + asset.Id.ToString("N");
        DocumentViewModel document = Documents.FirstOrDefault(d => d.Id == id)
                                         ?? AddDocument(new AssetDocumentViewModel(asset, AssetSerializer, _project.Model.Layout));
        Activate(document);
    }

    private DocumentViewModel AddDocument(DocumentViewModel document)
    {
        document.PropertyChanged += OnDocumentPropertyChanged;
        Documents.Add(document);
        return document;
    }

    private void Activate(DocumentViewModel document)
    {
        ActiveDocument = document;
        DocumentActivationRequested?.Invoke(this, document);
    }

    private void RemoveDocument(DocumentViewModel document)
    {
        document.PropertyChanged -= OnDocumentPropertyChanged;
        Documents.Remove(document);
        if (document is FileDocumentViewModel file)
        {
            Explorer.MarkModified(file.FullPath, false);
        }

        (document as IDisposable)?.Dispose();
        if (ReferenceEquals(_activeDocument, document))
        {
            ActiveDocument = null;
        }

        OnPropertyChanged(nameof(WindowTitle));
    }

    /// <summary>Ferme tous les onglets de code, sans confirmation (projet fermé).</summary>
    private void CloseAllDocuments()
    {
        foreach (DocumentViewModel document in Documents.ToList())
        {
            RemoveDocument(document);
        }

        Details = null;
    }

    /// <summary>Demande quoi faire des fichiers modifiés ; faux si l'utilisateur annule.</summary>
    private bool ConfirmDirtyDocuments()
    {
        foreach (DocumentViewModel document in Documents.Where(d => d.IsDirty).ToList())
        {
            switch (_dialogs.AskUnsavedFile(document.FileName))
            {
                case UnsavedChangesChoice.Save when !document.Save():
                case UnsavedChangesChoice.Cancel:
                    return false;
            }
        }

        return true;
    }

    /// <summary>Après une compilation : statut des fichiers relu, onglets non modifiés rechargés depuis le disque.</summary>
    private void RefreshGeneratedDocuments()
    {
        _files?.Refresh();
        foreach (FileDocumentViewModel file in Documents.OfType<FileDocumentViewModel>())
        {
            file.ReloadIfChangedOnDisk();
            file.Markers = MarkersFor(file);
        }
    }

    /// <summary>Erreurs et avertissements javac de la dernière compilation situés dans un fichier.</summary>
    private IReadOnlyList<CodeMarker> MarkersFor(FileDocumentViewModel file) =>
        [.. _buildDiagnostics
            .Where(d => d.Line > 0 && d.FilePath is not null
                        && string.Equals(d.FilePath, file.DisplayPath, StringComparison.OrdinalIgnoreCase))
            .Select(d => new CodeMarker(d.Line, d.Severity, d.Message))];

    /// <summary>Propositions de complétion Java, selon le loader du projet et les classes générées du mod.</summary>
    private IReadOnlyList<JavaCompletionEntry> JavaCompletions()
    {
        if (_javaCompletions is null && _project is not null)
        {
            Project.ProjectSettings settings = _project.Model.Settings;
            var naming = new JavaModNaming(new IRModInfo(
                settings.ModId, settings.ModName, settings.ModVersion, settings.Authors, settings.Description,
                settings.License, settings.Website, settings.MinecraftVersion, settings.LoaderId));
            _javaCompletions = JavaCompletionData.For(ModLoaderKindExtensions.FromId(settings.LoaderId), JavaCompletionData.ModTypes(naming));
        }

        return _javaCompletions ?? [];
    }

    private int SaveDirtyDocuments() => Documents.Where(d => d.IsDirty).Count(d => d.Save());

    private bool HasDirtyDocuments => Documents.Any(d => d.IsDirty);

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.IsDirty) && sender is DocumentViewModel document)
        {
            if (document is FileDocumentViewModel file)
            {
                Explorer.MarkModified(file.FullPath, file.IsDirty);
            }

            OnPropertyChanged(nameof(WindowTitle));
        }
    }
}
