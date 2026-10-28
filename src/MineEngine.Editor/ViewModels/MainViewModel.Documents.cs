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
using MineEngine.Editor.Settings;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.ViewModels.Graphs;
using MineEngine.Editor.ViewModels.Puppeteer;
using MineEngine.Editor.ViewModels.Sparkle;
using MineEngine.Generator;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
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

    /// <summary>Identifiants des onglets permanents du projet (ContentId de la disposition).</summary>
    public const string DashboardTabId = "home";

    public const string ResourcesTabId = "resources";

    public const string LanguagesTabId = "languages";

    public const string GlobalsTabId = "globals";

    public const string TagsTabId = "tags";

    /// <summary>Demande à la vue d'afficher un onglet permanent (Ressources, Langues).</summary>
    public event EventHandler<string>? ProjectTabRequested;

    public ICommand ShowResourcesCommand { get; private set; } = null!;

    public ICommand ShowLanguagesCommand { get; private set; } = null!;

    public ICommand ShowGlobalsCommand { get; private set; } = null!;

    public ICommand ShowTagsCommand { get; private set; } = null!;

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
                NodeLibrary.Document = value as INodeLibraryHost;
                RecordNavigation();
            }
        }
    }

    public bool HasActiveDocument => _activeDocument is not null;

    /// <summary>
    /// View of the project shown in the work area from the toolbar (DashboardTabId, ResourcesTabId,
    /// LanguagesTabId), or null when a document tab is shown. Set by the window.
    /// </summary>
    public string? ActiveWorkspaceView
    {
        get => _activeWorkspaceView;
        set
        {
            if (SetProperty(ref _activeWorkspaceView, value))
            {
                OnPropertyChanged(nameof(IsDashboardShown));
                OnPropertyChanged(nameof(IsResourcesShown));
                OnPropertyChanged(nameof(IsLanguagesShown));
                OnPropertyChanged(nameof(IsGlobalsShown));
                OnPropertyChanged(nameof(IsTagsShown));
                OnPropertyChanged(nameof(ShowsViewModeButtons));
                OnPropertyChanged(nameof(SearchText));
                OnPropertyChanged(nameof(SearchHint));
                RecordNavigation();
            }
        }
    }

    public bool IsDashboardShown => _activeWorkspaceView == DashboardTabId;

    public bool IsResourcesShown => _activeWorkspaceView == ResourcesTabId;

    public bool IsLanguagesShown => _activeWorkspaceView == LanguagesTabId;

    public bool IsGlobalsShown => _activeWorkspaceView == GlobalsTabId;

    public bool IsTagsShown => _activeWorkspaceView == TagsTabId;

    /// <summary>List and grid buttons: they only change the Dashboard, hidden over Resources and Languages.</summary>
    public bool ShowsViewModeButtons => !IsResourcesShown && !IsLanguagesShown && !IsGlobalsShown && !IsTagsShown;

    /// <summary>Search of the toolbar: it filters the view shown (Resources, Languages), otherwise the Dashboard.</summary>
    public string SearchText
    {
        get => (IsResourcesShown ? _project?.Resources.SearchText
            : IsLanguagesShown ? Languages?.SearchText
            : IsGlobalsShown ? Globals?.SearchText
            : IsTagsShown ? Tags?.SearchText
            : _project?.SearchText) ?? string.Empty;
        set
        {
            if (IsResourcesShown && _project is not null)
            {
                _project.Resources.SearchText = value;
            }
            else if (IsLanguagesShown && Languages is not null)
            {
                Languages.SearchText = value;
            }
            else if (IsGlobalsShown && Globals is not null)
            {
                Globals.SearchText = value;
            }
            else if (IsTagsShown && Tags is not null)
            {
                Tags.SearchText = value;
            }
            else if (_project is not null)
            {
                _project.SearchText = value;
            }

            OnPropertyChanged();
        }
    }

    public string SearchHint => IsResourcesShown ? Tr.T("Search the resources...") : IsLanguagesShown ? Tr.T("Search for a key or a text...")
        : IsGlobalsShown ? Tr.T("Search for a variable...")
        : IsTagsShown ? Tr.T("Search for a tag or an entry...") : Tr.T("Search...");

    private string? _activeWorkspaceView = DashboardTabId;

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
                OnPropertyChanged(nameof(DetailsAsset));
            }
        }
    }

    public bool HasDetails => _details is not null;

    /// <summary>Element whose name and identifier the Details panel edits: the selected one, or the one of the active tab.</summary>
    public AssetViewModel? DetailsAsset => _details switch
    {
        AssetViewModel asset => asset,
        DocumentViewModel document when DocumentAssetId(document) is { } id => _project?.Assets.FirstOrDefault(a => a.Model.Id == id),
        _ => null,
    };

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

    /// <summary>Ouvre un arbre de comportement dans Puppeteer (paramètre : son ViewModel, sinon l'asset sélectionné).</summary>
    public ICommand OpenBehaviorTreeCommand { get; private set; } = null!;

    /// <summary>Opens a particle system in Sparkle (parameter: its ViewModel, otherwise the selected element).</summary>
    public ICommand OpenSparkleCommand { get; private set; } = null!;

    /// <summary>Ouvre l'élément sélectionné dans son onglet d'édition (barre d'outils).</summary>
    public ICommand EditSelectedAssetCommand { get; private set; } = null!;

    /// <summary>Ouvre l'éditeur nodal du graphe sélectionné (barre d'outils).</summary>
    public ICommand OpenSelectedGraphCommand { get; private set; } = null!;

    /// <summary>Écrit l'élément sélectionné dans un fichier (.asset.json, ou l'image / le son d'une texture / d'un son).</summary>
    public ICommand ExportAssetCommand { get; private set; } = null!;

    /// <summary>Ajoute au dossier ouvert un élément exporté depuis un autre projet (.asset.json).</summary>
    public ICommand ImportAssetCommand { get; private set; } = null!;

    /// <summary>Copie l'élément sélectionné dans le même dossier (Ctrl+D).</summary>
    public ICommand DuplicateAssetCommand { get; private set; } = null!;

    /// <summary>Génère le code du mod puis ouvre le fichier Java de l'élément sélectionné, à sa première ligne.</summary>
    public ICommand OpenAssetJavaCommand { get; private set; } = null!;

    /// <summary>Asks a new in-game name for an element (parameter: its ViewModel, otherwise the selected element).</summary>
    public ICommand RenameAssetCommand { get; private set; } = null!;

    /// <summary>Programs of the settings that open Java files (the IDE), for "Open the Java code in".</summary>
    public IReadOnlyList<ExternalProgram> JavaPrograms => ProgramsFor("Main.java");

    /// <summary>Generates the code of the mod, then opens the Java file of the selected element with a program (the IDE).</summary>
    public void OpenAssetJavaWith(ExternalProgram program) => _ = OpenAssetJavaAsync(program);

    // ----- Tab menu (right-click on a tab of the work area) -----

    /// <summary>File shown by a tab: the code file, or the .asset.json file of an element; null without a project.</summary>
    public string? DocumentFilePath(DocumentViewModel document) => document switch
    {
        FileDocumentViewModel file => file.FullPath,
        _ when _project is not null => _project.Model.Layout.ToAbsolutePath(document.DisplayPath),
        _ => null,
    };

    /// <summary>Element edited by a tab (element editor, graph, Puppeteer tree), or null for a code file.</summary>
    public static Guid? DocumentAssetId(DocumentViewModel document) => document switch
    {
        AssetEditorDocumentViewModel editor => editor.AssetId,
        GraphDocumentViewModel graph => graph.AssetId,
        BehaviorTreeDocumentViewModel tree => tree.AssetId,
        SparkleDocumentViewModel sparkle => sparkle.AssetId,
        _ => null,
    };

    /// <summary>Saves a tab (code files only have something to save; elements are saved with the project).</summary>
    public void SaveDocument(DocumentViewModel document)
    {
        if (document is { IsReadOnly: false, IsDirty: true })
        {
            document.Save();
        }
    }

    /// <summary>Opens the Windows file explorer on the file of a tab.</summary>
    public void RevealDocument(DocumentViewModel document)
    {
        if (DocumentFilePath(document) is { } path)
        {
            _shell.RevealFile(path);
        }
    }

    /// <summary>Selects the element of a tab and shows it in the Dashboard.</summary>
    public void SelectDocumentAsset(DocumentViewModel document)
    {
        if (_project is not null && DocumentAssetId(document) is { } id && _project.SelectAsset(id))
        {
            DocumentActivationRequested?.Invoke(this, null);
        }
    }

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
        ShowResourcesCommand = new RelayCommand(() => ProjectTabRequested?.Invoke(this, ResourcesTabId), () => HasProject);
        ShowLanguagesCommand = new RelayCommand(() => ProjectTabRequested?.Invoke(this, LanguagesTabId), () => HasProject);
        ShowGlobalsCommand = new RelayCommand(() => ProjectTabRequested?.Invoke(this, GlobalsTabId), () => HasProject);
        ShowTagsCommand = new RelayCommand(() => ProjectTabRequested?.Invoke(this, TagsTabId), () => HasProject);
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
        EditSelectedAssetCommand = new RelayCommand(
            () => OpenAsset(_project!.SelectedAsset!.Model), () => HasProject && _project?.SelectedAsset is not null);
        OpenBehaviorTreeCommand = new RelayCommand<AssetViewModel?>(
            asset =>
            {
                if ((asset ?? _project?.SelectedAsset)?.Model is BehaviorTreeAsset tree)
                {
                    OpenBehaviorTreeDocument(tree);
                }
            },
            asset => (asset ?? _project?.SelectedAsset)?.Model is BehaviorTreeAsset);
        OpenSparkleCommand = new RelayCommand<AssetViewModel?>(
            asset =>
            {
                if ((asset ?? _project?.SelectedAsset)?.Model is SparkleAsset sparkle)
                {
                    OpenSparkleDocument(sparkle);
                }
            },
            asset => (asset ?? _project?.SelectedAsset)?.Model is SparkleAsset);
        OpenSelectedGraphCommand = new RelayCommand(
            () => OpenGraphDocument((GraphAsset)_project!.SelectedAsset!.Model), () => _project?.SelectedAsset?.Model is GraphAsset);
        ExportAssetCommand = new RelayCommand(ExportSelectedAsset, () => HasProject && _project?.SelectedAsset is not null);
        ImportAssetCommand = new RelayCommand(ImportAssetFiles, () => CanEdit);
        DuplicateAssetCommand = new RelayCommand(
            () => Execute(Tr.T("Duplicate"), () =>
            {
                Asset copy = _project!.DuplicateAsset(_project.SelectedAsset!);
                StatusText = Tr.F("'{0}' created (Ctrl+Z to undo)", copy.ResourceId);
            }),
            () => CanEdit && ProjectViewModel.CanDuplicate(_project?.SelectedAsset));
        OpenAssetJavaCommand = new AsyncRelayCommand(() => OpenAssetJavaAsync(null), () => CanEdit && _project?.SelectedAsset is not null);
        RenameAssetCommand = new RelayCommand<AssetViewModel?>(
            asset =>
            {
                if ((asset ?? _project?.SelectedAsset) is { } target
                    && _dialogs.AskText(Tr.T("Rename"), Tr.F("New name for \"{0}\":", target.Header), target.DisplayName)?.Trim() is { Length: > 0 } name)
                {
                    target.DisplayName = name;
                }
            },
            asset => CanEdit && (asset ?? _project?.SelectedAsset) is not null);
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
            StatusText = moved.Count == 1 ? Tr.F("\"{0}\" moved", moved[0].DisplayName) : Tr.F("{0} elements moved", moved.Count);
        }
    }

    private void InitializeFolderCommands()
    {
        NewFolderCommand = new RelayCommand(NewFolder, () => CanEdit);
        RenameFolderCommand = new RelayCommand<FolderItemViewModel>(RenameFolder, _ => CanEdit);
        DeleteFolderCommand = new RelayCommand<FolderItemViewModel>(DeleteFolder, _ => CanEdit);
        MoveAssetToFolderCommand = new RelayCommand<string>(
            folder => MoveAssetsToFolder(_project!.SelectedAssets, folder ?? string.Empty),
            _ => CanEdit && _project?.SelectedAsset is not null);
    }

    private void NewFolder()
    {
        string parent = _project!.CurrentFolder;
        string? name = AskFolderName(Tr.T("New folder"), Tr.T("Folder name:"), Tr.T("New folder"), parent);
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
        string? name = AskFolderName(Tr.T("Rename folder"), Tr.F("New name for \"{0}\":", folder.Name), folder.Name, parent, folder.Name);
        if (name is null || name == folder.Name)
        {
            return;
        }

        _project!.History.Execute(FolderOperations.Move(
            _project.Model, folder.Path, AssetFolders.Combine(parent, name), Tr.F("Rename folder \"{0}\"", folder.Name)));
    }

    private void DeleteFolder(FolderItemViewModel folder)
    {
        string where = AssetFolders.Parent(folder.Path) is { Length: > 0 } parent ? Tr.F("\"{0}\"", AssetFolders.Name(parent)) : Tr.T("the root");
        if (folder.AssetCount + folder.FolderCount > 0
            && !_dialogs.Confirm(Tr.T("Delete folder"), Tr.F("Delete folder \"{0}\"? Its contents ({1}) move up to {2}.", folder.Name, folder.ContentLabel.ToLowerInvariant(), where)))
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
                _dialogs.ShowError(title, Tr.T("Invalid name: no \"/\", \"\\\" or character forbidden by Windows (64 characters at most)."));
            }
            else if (name != current && FolderOperations.Exists(_project!.Model, AssetFolders.Combine(parent, name)))
            {
                _dialogs.ShowError(title, Tr.F("A folder \"{0}\" already exists here.", name));
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
        if (asset is SparkleAsset sparkle)
        {
            OpenSparkleDocument(sparkle);
            return;
        }

        if (asset is BehaviorTreeAsset tree)
        {
            OpenBehaviorTreeDocument(tree);
            if (nodeId is { } item && _activeDocument is BehaviorTreeDocumentViewModel puppeteer)
            {
                puppeteer.Show(item);
            }

            return;
        }

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
                     d.Id == AssetEditorDocumentViewModel.IdFor(e.Asset.Id) || d.Id == "graph:" + e.Asset.Id.ToString("N")
                     || d.Id == BehaviorTreeDocumentViewModel.IdFor(e.Asset.Id) || d.Id == SparkleDocumentViewModel.IdFor(e.Asset.Id)).ToList())
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
                                         (kind, owner) => AddGraph(kind, owner),
                                         GraphOptions));
        Activate(document);
    }

    private GraphViewOptions? _graphOptions;

    /// <summary>Options of every graph tab (snapping to the grid), kept in the preferences.</summary>
    public GraphViewOptions GraphOptions => _graphOptions ??= CreateGraphOptions();

    private GraphViewOptions CreateGraphOptions()
    {
        var options = new GraphViewOptions(_preferences.Current.GraphSnapToGrid, snap =>
        {
            EditorSettings settings = _preferences.Current.Clone();
            settings.GraphSnapToGrid = snap;
            _preferences.Update(settings);
        });
        _preferences.Changed += (_, _) => options.Follow(_preferences.Current.GraphSnapToGrid);
        return options;
    }

    /// <summary>Ouvre (ou active) l'onglet Puppeteer d'un arbre de comportement.</summary>
    private void OpenBehaviorTreeDocument(BehaviorTreeAsset tree)
    {
        if (_project is null)
        {
            return;
        }

        DocumentViewModel document = Documents.FirstOrDefault(d => d.Id == BehaviorTreeDocumentViewModel.IdFor(tree.Id))
                                     ?? AddDocument(new BehaviorTreeDocumentViewModel(
                                         tree,
                                         _assetViewModels.CreateContext(_project.Model, assetId => _project.SelectAsset(assetId)),
                                         _project.Model.Layout.ToRelativePath(_project.Model.Layout.GetAssetFilePath(tree))));
        Activate(document);
    }

    /// <summary>Opens (or activates) the Sparkle tab of a particle system.</summary>
    private void OpenSparkleDocument(SparkleAsset sparkle)
    {
        if (_project is null)
        {
            return;
        }

        DocumentViewModel document = Documents.FirstOrDefault(d => d.Id == SparkleDocumentViewModel.IdFor(sparkle.Id))
                                     ?? AddDocument(new SparkleDocumentViewModel(
                                         sparkle,
                                         _assetViewModels.CreateContext(_project.Model, assetId => _project.SelectAsset(assetId)),
                                         _project.Model.Layout.ToRelativePath(_project.Model.Layout.GetAssetFilePath(sparkle))));
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
    /// <summary>
    /// Écrit le code du mod (sans compiler), puis ouvre le premier fichier Java produit par
    /// l'élément sélectionné. Un élément sans code Java (recette, texture...) le dit dans la barre d'état.
    /// </summary>
    private async Task OpenAssetJavaAsync(ExternalProgram? program)
    {
        if (_project?.SelectedAsset is not { } selected)
        {
            return;
        }

        Guid assetId = selected.Model.Id;
        SetBusy(new CancellationTokenSource());
        StatusText = Tr.T("Generating Java code...");
        try
        {
            GenerationResult? result = await _pipeline.GenerateAsync(_project.Model, _log, _buildCancellation!.Token);
            if (result is null)
            {
                StatusText = Tr.T("The code could not be generated: see Diagnostics and the output.");
                return;
            }

            SourceMapEntry? entry = result.SourceMap.Entries
                .Where(e => e.AssetId == assetId && e.File.EndsWith(".java", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.File, StringComparer.Ordinal)
                .ThenBy(e => e.FirstLine)
                .FirstOrDefault();
            if (entry is null)
            {
                StatusText = Tr.F("'{0}' produces no Java code (only data files).", selected.Header);
                return;
            }

            string javaFile = Path.Combine(result.WorkspaceDirectory, entry.File.Replace('/', Path.DirectorySeparatorChar));
            if (program is not null)
            {
                OpenWith(program, javaFile);
            }
            else
            {
                OpenFileAtLine(javaFile, entry.FirstLine);
            }

            StatusText = Tr.F("Java code of '{0}'", selected.Header);
        }
        catch (OperationCanceledException)
        {
            StatusText = Tr.T("Generation cancelled");
        }
        finally
        {
            SetBusy(null);
            Explorer.Refresh();
            RefreshGeneratedDocuments();
        }
    }

    private void ExportSelectedAsset()
    {
        AssetViewModel selected = _project!.SelectedAsset!;
        string fileName = _project.ExportFileName(selected);
        string extension = fileName.EndsWith(AssetSerializer.FileExtension, StringComparison.OrdinalIgnoreCase)
            ? AssetSerializer.FileExtension
            : Path.GetExtension(fileName);
        string? target = _dialogs.AskFileToSave(
            Tr.T("Export the element"), Tr.F("{0} file (*{1})|*{2}", extension, extension, extension), fileName);
        if (target is not null && Execute(Tr.T("Export the element"), () => _project.ExportAsset(selected, target)))
        {
            StatusText = Tr.F("'{0}' exported to {1}", selected.Header, target);
        }
    }

    private void ImportAssetFiles()
    {
        string filter = Tr.F("Mine Engine element (*{0})|*{1}", AssetSerializer.FileExtension, AssetSerializer.FileExtension);
        if (_dialogs.AskFileToOpen(Tr.T("Import an element"), filter) is { } file
            && Execute(Tr.T("Import an element"), () =>
            {
                Asset asset = _project!.ImportAssetFile(file);
                StatusText = Tr.F("'{0}' imported (Ctrl+Z to undo)", asset.ResourceId);
            }))
        {
            Details = _project!.SelectedAsset;
        }
    }

    /// <summary>Programmes de « Ouvrir avec » proposés pour ce fichier (d'après son extension).</summary>
    public IReadOnlyList<ExternalProgram> ProgramsFor(string file) =>
        [.. _preferences.Current.ExternalPrograms.Where(p => p.Handles(file))];

    /// <summary>Ouvre un fichier avec un programme externe ; une erreur (programme déplacé) est affichée.</summary>
    public void OpenWith(ExternalProgram program, string file) =>
        Execute(Tr.F("Open with {0}", program.Name), () => _shell.OpenWith(program.Path, file));

    /// <summary>Ouvre un fichier avec l'application associée par Windows.</summary>
    public void OpenWithDefault(string file) => Execute(Tr.T("Open"), () => _shell.OpenFile(file));

    /// <summary>Fenêtre « Ouvrir avec » de Windows.</summary>
    public void ChooseProgram(string file) => Execute(Tr.T("Open with"), () => _shell.ShowOpenWithDialog(file));

    /// <summary>Fichier d'un élément pour « Ouvrir avec » : l'image ou le son d'une texture ou d'un son, sinon son .asset.json.</summary>
    public string? AssetFile(AssetViewModel asset)
    {
        if (_project is null)
        {
            return null;
        }

        return asset.Model is FileAsset file
            ? _project.Model.Layout.ToAbsolutePath(file.FilePath)
            : _project.Model.Layout.GetAssetFilePath(asset.Model);
    }

    private void OpenFileAtLine(string fullPath, int line)
    {
        if (!File.Exists(fullPath))
        {
            StatusText = Tr.T("File not found: ") + Path.GetFileName(fullPath);
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
