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

namespace MineEngine.Editor.ViewModels;

/// <summary>
/// Partie "onglets et panneau Détails" de la fenêtre principale : fichiers ouverts dans
/// des onglets de code (le Dashboard est l'onglet permanent), onglet actif et cible du
/// panneau Détails (asset sélectionné ou fichier actif).
/// </summary>
public sealed partial class MainViewModel
{
    private CodeDocumentViewModel? _activeDocument;
    private IDetailsTarget? _details;
    private AssetSerializer? _assetSerializer;

    /// <summary>Demande à la vue d'afficher un onglet (null : le Dashboard).</summary>
    public event EventHandler<CodeDocumentViewModel?>? DocumentActivationRequested;

    /// <summary>Onglets de code ouverts (sans le Dashboard).</summary>
    public ObservableCollection<CodeDocumentViewModel> Documents { get; } = [];

    /// <summary>Onglet de code actif, ou null quand le Dashboard est affiché.</summary>
    public CodeDocumentViewModel? ActiveDocument
    {
        get => _activeDocument;
        set
        {
            if (SetProperty(ref _activeDocument, value))
            {
                OnPropertyChanged(nameof(HasActiveDocument));
                Details = value ?? (IDetailsTarget?)_project?.SelectedAsset;
            }
        }
    }

    public bool HasActiveDocument => _activeDocument is not null;

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

    /// <summary>Appelé par la vue quand l'utilisateur ferme un onglet ; faux pour l'annuler.</summary>
    public bool RequestCloseDocument(CodeDocumentViewModel document)
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
        CloseDocumentCommand = new RelayCommand<CodeDocumentViewModel>(d => RequestCloseDocument(d));
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
            OpenAssetDocument(asset);
            return;
        }

        if (!FileDocumentViewModel.CanOpen(fullPath))
        {
            _shell.OpenFile(fullPath);
            return;
        }

        string id = "file:" + Path.GetFullPath(fullPath).ToLowerInvariant();
        CodeDocumentViewModel document = Documents.FirstOrDefault(d => d.Id == id)
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
        CodeDocumentViewModel document = Documents.FirstOrDefault(d => d.Id == id)
                                         ?? AddDocument(new AssetDocumentViewModel(asset, AssetSerializer, _project.Model.Layout));
        Activate(document);
    }

    private CodeDocumentViewModel AddDocument(CodeDocumentViewModel document)
    {
        document.PropertyChanged += OnDocumentPropertyChanged;
        Documents.Add(document);
        return document;
    }

    private void Activate(CodeDocumentViewModel document)
    {
        ActiveDocument = document;
        DocumentActivationRequested?.Invoke(this, document);
    }

    private void RemoveDocument(CodeDocumentViewModel document)
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
        foreach (CodeDocumentViewModel document in Documents.ToList())
        {
            RemoveDocument(document);
        }

        Details = null;
    }

    /// <summary>Demande quoi faire des fichiers modifiés ; faux si l'utilisateur annule.</summary>
    private bool ConfirmDirtyDocuments()
    {
        foreach (CodeDocumentViewModel document in Documents.Where(d => d.IsDirty).ToList())
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
        if (e.PropertyName == nameof(CodeDocumentViewModel.IsDirty) && sender is CodeDocumentViewModel document)
        {
            if (document is FileDocumentViewModel file)
            {
                Explorer.MarkModified(file.FullPath, file.IsDirty);
            }

            OnPropertyChanged(nameof(WindowTitle));
        }
    }
}
