using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.ViewModels.Graphs;

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

    private void AddGraph()
    {
        _project!.AddAsset(AssetType.Graph);
        if (_project.SelectedAsset?.Model is GraphAsset graph)
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
                                         _project.Model.Layout.ToRelativePath(_project.Model.Layout.GetAssetFilePath(graph))));
        Activate(document);
    }

    /// <summary>Le serializer des assets sert à l'aperçu JSON ; il est créé au premier besoin.</summary>
    private AssetSerializer AssetSerializer => _assetSerializer ??= new AssetSerializer(_assetViewModels.Catalog);

    /// <summary>Ouvre un fichier de l'explorateur : asset, onglet de code, ou application de Windows.</summary>
    private void OpenFileFromExplorer(string fullPath)
    {
        if (_project is null)
        {
            return;
        }

        if (FindAssetForFile(fullPath) is { } asset && fullPath.EndsWith(AssetSerializer.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            if (asset is GraphAsset graph)
            {
                OpenGraphDocument(graph);
            }
            else
            {
                OpenAssetDocument(asset);
            }

            return;
        }

        if (!FileDocumentViewModel.CanOpen(fullPath))
        {
            _shell.OpenFile(fullPath);
            return;
        }

        string id = "file:" + Path.GetFullPath(fullPath).ToLowerInvariant();
        DocumentViewModel document = Documents.FirstOrDefault(d => d.Id == id)
                                         ?? AddDocument(new FileDocumentViewModel(fullPath, _project.Model.Layout));
        Activate(document);
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
