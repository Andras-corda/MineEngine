using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using MineEngine.Assets.Graphs;
using MineEngine.Build;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Editor.Settings;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.ViewModels.Hub;
using MineEngine.Editor.ViewModels.Panels;
using MineEngine.Project;
using MineEngine.Project.Backups;
using MineEngine.Project.Commands;

namespace MineEngine.Editor.ViewModels;

/// <summary>
/// ViewModel de la fenêtre principale : cycle de vie du projet, annuler/rétablir,
/// validation continue, build, sauvegarde automatique et panneaux de l'éditeur.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public const string InspectorPanelId = "inspector";
    public const string DiagnosticsPanelId = "diagnostics";
    public const string OutputPanelId = "output";
    public const string ExplorerPanelId = "explorer";

    private const string ApplicationName = "Mine Engine";
    private static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ValidationDelay = TimeSpan.FromMilliseconds(400);

    private readonly ProjectRepository _repository;
    private readonly ProjectBackupService _backups;
    private readonly AssetViewModelFactory _assetViewModels;
    private readonly BuildPipeline _pipeline;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly MdkServices _mdks;
    private readonly EditorPreferences _preferences;
    private readonly ILog _log;
    private readonly DispatcherTimer _validationTimer;
    private readonly DispatcherTimer _autoSaveTimer;

    private ProjectViewModel? _project;
    private CancellationTokenSource? _buildCancellation;
    private string _statusText = Tr.T("Prêt");
    private string? _lastBuildSummary;

    public MainViewModel(
        ProjectRepository repository,
        ProjectBackupService backups,
        AssetViewModelFactory assetViewModels,
        BuildPipeline pipeline,
        IDialogService dialogs,
        IShellService shell,
        MdkServices mdks,
        EditorPreferences preferences,
        OutputViewModel output,
        ILog log,
        Func<Action<string>, ProjectHubViewModel> createHub)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _backups = backups ?? throw new ArgumentNullException(nameof(backups));
        _assetViewModels = assetViewModels ?? throw new ArgumentNullException(nameof(assetViewModels));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _mdks = mdks ?? throw new ArgumentNullException(nameof(mdks));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        ArgumentNullException.ThrowIfNull(createHub);
        Hub = createHub(OpenProjectFromHub);
        Diagnostics = new DiagnosticsViewModel(NavigateToDiagnostic);
        Explorer = new ProjectExplorerViewModel(shell, dialogs, OpenFileFromExplorer, SelectFileFromExplorer);
        InitializeDocumentCommands();
        InitializeFolderCommands();
        _assetViewModels.Navigation.OpenRequested += (_, e) => OpenAsset(e.Asset, e.NodeId);

        _validationTimer = new DispatcherTimer { Interval = ValidationDelay };
        _validationTimer.Tick += (_, _) =>
        {
            _validationTimer.Stop();
            Validate();
        };

        _autoSaveTimer = new DispatcherTimer();
        _autoSaveTimer.Tick += (_, _) => AutoSave();
        _preferences.Changed += (_, _) =>
        {
            ConfigureAutoSave();
            Shortcuts.Update(_preferences.Current.Shortcuts);
        };
        Shortcuts.Update(_preferences.Current.Shortcuts);
        ConfigureAutoSave();

        // Fichier
        NewProjectCommand = new RelayCommand(NewProject, () => !IsBusy);
        OpenProjectCommand = new RelayCommand(OpenProject, () => !IsBusy);
        SaveProjectCommand = new RelayCommand(() => SaveProject(isAutoSave: false), () => HasProject && !IsBusy);
        CloseProjectCommand = new RelayCommand(() => CloseProject(), () => HasProject && !IsBusy);
        OpenProjectFolderCommand = new RelayCommand(() => OpenFolder(_project!.Model.Layout.RootDirectory), () => HasProject);
        OpenBackupsFolderCommand = new RelayCommand(() => OpenFolder(ProjectBackupService.GetBackupDirectory(_project!.Model)), () => HasProject);

        // Édition
        UndoCommand = new RelayCommand(() => _project!.History.Undo(), () => CanEdit && _project!.History.CanUndo);
        RedoCommand = new RelayCommand(() => _project!.History.Redo(), () => CanEdit && _project!.History.CanRedo);
        DeleteAssetCommand = new RelayCommand(DeleteSelectedAsset, () => CanEdit && _project!.HasSelection);

        // Projet
        EditProjectSettingsCommand = new RelayCommand(EditProjectSettings, () => CanEdit);
        // Un nouvel élément s'ouvre directement dans son onglet d'édition.
        AddItemCommand = new RelayCommand(() => AddAndEdit(AssetType.Item), () => CanEdit);
        AddBlockCommand = new RelayCommand(() => AddAndEdit(AssetType.Block), () => CanEdit);
        AddMobCommand = new RelayCommand(() => AddAndEdit(AssetType.Mob), () => CanEdit);
        AddRecipeCommand = new RelayCommand(() => AddAndEdit(AssetType.Recipe), () => CanEdit);
        AddGraphCommand = new RelayCommand<GraphKind>(kind => AddGraph(kind), _ => CanEdit);
        ImportTexturesCommand = new RelayCommand(ImportTextures, () => CanEdit);
        ImportSoundsCommand = new RelayCommand(ImportSounds, () => CanEdit);
        ValidateCommand = new RelayCommand(ValidateAndShow, () => HasProject);

        // Build
        BuildCommand = new AsyncRelayCommand(() => RunPipelineAsync(PipelineAction.Build), () => HasProject && !IsBusy);
        RunClientCommand = new AsyncRelayCommand(() => RunPipelineAsync(PipelineAction.RunClient), () => HasProject && !IsBusy);
        ExportModCommand = new AsyncRelayCommand(() => RunPipelineAsync(PipelineAction.Export), () => HasProject && !IsBusy);
        ReloadGradleCommand = new AsyncRelayCommand(() => RunPipelineAsync(PipelineAction.ReloadGradle), () => HasProject && !IsBusy);
        CancelBuildCommand = new RelayCommand(() => _buildCancellation?.Cancel(), () => IsBusy);
        OpenBuildFolderCommand = new RelayCommand(() => OpenFolder(_project!.Model.Layout.BuildDirectory), () => HasProject);

        // Outils
        OpenMdkManagerCommand = new RelayCommand(_dialogs.ShowMdkManager, () => !IsBusy);
        OpenSettingsCommand = new RelayCommand(OpenSettings);

        // Mine Engine
        OpenRepositoryCommand = new RelayCommand(() => _shell.OpenUrl(EditorInfo.RepositoryUrl));
        OpenWebsiteCommand = new RelayCommand(() => _shell.OpenUrl(EditorInfo.WebsiteUrl!), () => EditorInfo.WebsiteUrl is not null);
    }

    /// <summary>
    /// L'utilisateur a demandé de relancer Mine Engine (changement de langue) : la vue ferme la
    /// fenêtre, ce qui propose d'enregistrer le projet, puis relance l'application sur le projet donné.
    /// </summary>
    public event EventHandler<string?>? RestartRequested;

    /// <summary>Demande à la vue d'afficher un panneau (par exemple l'Inspector après une navigation).</summary>
    public event EventHandler<string>? PanelRequested;

    public OutputViewModel Output { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public ProjectExplorerViewModel Explorer { get; }

    /// <summary>Raccourcis clavier en cours (affichés dans les menus, appliqués par la fenêtre).</summary>
    public ShortcutMap Shortcuts { get; } = new();

    /// <summary>Onglet Langues du projet ouvert (langues du mod et traductions).</summary>
    public LanguagesViewModel? Languages { get; private set; }

    public ProjectViewModel? Project
    {
        get => _project;
        private set
        {
            Languages?.Dispose();
            if (_project is not null)
            {
                _project.PropertyChanged -= OnProjectPropertyChanged;
                _project.Model.Assets.Changed -= OnProjectContentChanged;
                _project.Model.Assets.Changed -= OnRegistryChanged;
                _project.History.Changed -= OnHistoryChanged;
                _project.Dispose();
            }

            _files?.Dispose();
            _files = value is null ? null : new ProjectFiles(value.Model);
            _buildDiagnostics = [];
            _javaCompletions = null;
            _project = value;
            Languages = value is null ? null : new LanguagesViewModel(value, _dialogs, () => CanEdit);
            OnPropertyChanged(nameof(Languages));
            if (_project is not null)
            {
                _project.PropertyChanged += OnProjectPropertyChanged;
                _project.Model.Assets.Changed += OnProjectContentChanged;
                _project.Model.Assets.Changed += OnRegistryChanged;
                _project.History.Changed += OnHistoryChanged;
            }

            _lastBuildSummary = null;
            CloseAllDocuments();
            Explorer.SetProject(_project?.Model);
            Diagnostics.Clear();
            if (_project is not null)
            {
                Validate();
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProject));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(ProjectStatus));
            OnPropertyChanged(nameof(LastBuildSummary));
            RaiseHistoryLabels();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasProject => _project is not null;

    /// <summary>Accueil (sélection de projet), affiché quand aucun projet n'est ouvert.</summary>
    public ProjectHubViewModel Hub { get; }

    /// <summary>Version de l'éditeur ("V0.3").</summary>
    public string VersionLabel => EditorInfo.VersionLabel;

    /// <summary>Deuxième partie de la barre de statut : projet ouvert ou "Aucun projet ouvert".</summary>
    public string ProjectStatus => _project is null ? Tr.T("Aucun projet ouvert") : _project.Name;

    public bool IsBusy => _buildCancellation is not null;

    public bool CanEdit => HasProject && !IsBusy;

    public string WindowTitle => _project is null
        ? Tr.F("{0} — Sélection de projet", ApplicationName)
        : $"{_project.Name}{(_project.IsDirty || HasDirtyDocuments ? " *" : string.Empty)} — {ApplicationName}";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string LastBuildSummary => _lastBuildSummary ?? Tr.T("Aucun build depuis l'ouverture du projet.");

    public string UndoLabel => _project?.History.UndoDescription is { } description ? Tr.F("_Annuler : {0}", description) : Tr.T("_Annuler");

    public string RedoLabel => _project?.History.RedoDescription is { } description ? Tr.F("_Rétablir : {0}", description) : Tr.T("_Rétablir");

    public ICommand NewProjectCommand { get; }

    public ICommand OpenProjectCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand CloseProjectCommand { get; }

    public ICommand OpenProjectFolderCommand { get; }

    public ICommand OpenBackupsFolderCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public ICommand DeleteAssetCommand { get; }

    public ICommand EditProjectSettingsCommand { get; }

    public ICommand AddItemCommand { get; }

    public ICommand AddBlockCommand { get; }

    public ICommand AddMobCommand { get; }

    public ICommand AddRecipeCommand { get; }

    /// <summary>Crée un graphe nodal et l'ouvre dans l'éditeur.</summary>
    public ICommand AddGraphCommand { get; }

    /// <summary>Importe des images PNG comme assets Texture.</summary>
    public ICommand ImportTexturesCommand { get; }

    /// <summary>Importe des sons OGG comme assets Sound.</summary>
    public ICommand ImportSoundsCommand { get; }

    public ICommand ValidateCommand { get; }

    public ICommand BuildCommand { get; }

    public ICommand RunClientCommand { get; }

    public ICommand CancelBuildCommand { get; }

    /// <summary>Compile le mod puis copie le .jar à l'endroit choisi.</summary>
    public ICommand ExportModCommand { get; }

    /// <summary>Régénère le projet et fait relire à Gradle sa configuration et ses dépendances.</summary>
    public ICommand ReloadGradleCommand { get; }

    public ICommand OpenBuildFolderCommand { get; }

    public ICommand OpenMdkManagerCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    /// <summary>Ouvre le dépôt GitHub de Mine Engine.</summary>
    public ICommand OpenRepositoryCommand { get; }

    /// <summary>Ouvre le site web de Mine Engine.</summary>
    public ICommand OpenWebsiteCommand { get; }

    private void OpenSettings()
    {
        if (_dialogs.ShowSettings())
        {
            RestartRequested?.Invoke(this, _project?.Model.Layout.RootDirectory);
        }
    }

    /// <summary>Rouvre au démarrage le dernier projet ouvert, si l'option est cochée dans les paramètres.</summary>
    public void ReopenLastProjectAtStartup()
    {
        if (_preferences.Current.ReopenLastProject && Hub.LastOpenedProject is { } directory)
        {
            TryOpen(directory);
        }
    }

    /// <summary>Ouvre un projet passé en argument de la ligne de commande.</summary>
    public void OpenProjectAtStartup(string path) => TryOpen(path);

    /// <summary>Appelé à la fermeture de la fenêtre ; faux pour l'annuler.</summary>
    public bool ConfirmShutdown()
    {
        if (IsBusy)
        {
            if (!_dialogs.Confirm(ApplicationName, Tr.T("Un build est en cours. Voulez-vous l'arrêter et quitter ?")))
            {
                return false;
            }

            _buildCancellation?.Cancel();
        }

        return ConfirmDiscardChanges();
    }

    private void NewProject()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        NewProjectRequest? request = _dialogs.AskNewProject();
        if (request is null)
        {
            return;
        }

        Execute(Tr.T("Création du projet"), () =>
        {
            ModProject model = _repository.Create(request.ParentDirectory, request.Settings);
            Project = new ProjectViewModel(model, _assetViewModels);
            Hub.Remember(model.Layout.RootDirectory);
            _log.Info(Tr.F("Projet créé : {0} (MDK {1})", model.Layout.RootDirectory, model.Settings.MdkId));
            StatusText = Tr.T("Projet créé");
        });
    }

    private void OpenProject()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        string? file = _dialogs.AskProjectToOpen();
        if (file is not null)
        {
            TryOpen(file);
        }
    }

    private void TryOpen(string path)
    {
        Execute(Tr.T("Ouverture du projet"), () =>
        {
            ModProject model = _repository.Open(path);
            Project = new ProjectViewModel(model, _assetViewModels);
            Hub.Remember(model.Layout.RootDirectory);
            _log.Info(Tr.F("Projet ouvert : {0} ({1} assets)", model.Layout.RootDirectory, model.Assets.Count));
            foreach (string note in model.LoadNotes)
            {
                _log.Warning(Tr.T("Conversion du projet : ") + note + Tr.T(" Enregistrez pour écrire le nouveau format."));
            }

            StatusText = model.LoadNotes.Count > 0 ? Tr.T("Projet converti au format de la V0.3 (à enregistrer)") : Tr.T("Projet ouvert");
        });
    }

    private bool SaveProject(bool isAutoSave)
    {
        if (_project is null)
        {
            return true;
        }

        ModProject model = _project.Model;
        bool saved = Execute(Tr.T("Enregistrement"), () =>
        {
            _repository.Save(model);
            if (!isAutoSave)
            {
                SaveDirtyDocuments();
            }

            StatusText = isAutoSave
                ? Tr.F("Enregistrement automatique à {0:HH:mm:ss}", DateTime.Now)
                : Tr.F("Enregistré à {0:HH:mm:ss}", DateTime.Now);
        });

        if (saved)
        {
            _project.History.Seal();
            CreateBackupIfDue(model);
            Explorer.Refresh();
            if (isAutoSave)
            {
                _log.Info(Tr.T("Projet enregistré automatiquement."));
            }
        }

        return saved;
    }

    private void CreateBackupIfDue(ModProject model)
    {
        try
        {
            ProjectBackup? backup = _backups.CreateIfDue(model, BackupInterval, _preferences.Current.BackupsToKeep);
            if (backup is not null)
            {
                _log.Debug(Tr.T("Sauvegarde de secours créée : ") + backup.File);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.Warning(Tr.T("La sauvegarde de secours a échoué : ") + exception.Message);
        }
    }

    private void AutoSave()
    {
        if (_project is { IsDirty: true } && !IsBusy)
        {
            SaveProject(isAutoSave: true);
        }
    }

    private void ConfigureAutoSave()
    {
        int minutes = _preferences.Current.AutoSaveMinutes;
        _autoSaveTimer.Stop();
        if (minutes > 0)
        {
            _autoSaveTimer.Interval = TimeSpan.FromMinutes(minutes);
            _autoSaveTimer.Start();
        }
    }

    private bool CloseProject()
    {
        if (!ConfirmDiscardChanges())
        {
            return false;
        }

        Project = null;
        StatusText = Tr.T("Prêt");
        Hub.Refresh();
        return true;
    }

    /// <summary>Ouverture depuis la liste de l'accueil (dossier du projet).</summary>
    private void OpenProjectFromHub(string rootDirectory)
    {
        if (ConfirmDiscardChanges())
        {
            TryOpen(rootDirectory);
        }
    }

    private void DeleteSelectedAsset()
    {
        AssetViewModel? selected = _project?.SelectedAsset;
        if (selected is null)
        {
            return;
        }

        // Supprimer un asset utilisé casse les références : on prévient avant.
        IReadOnlyList<AssetLinkViewModel> usedBy = selected.UsedBy;
        if (usedBy.Count > 0
            && !_dialogs.Confirm(
                Tr.T("Supprimer un asset utilisé"),
                Tr.F("'{0}' est utilisé par :\n{1}\n\n", selected.Header, string.Join("\n", usedBy.Select(l => "  - " + l.Label))) +
                Tr.T("Le supprimer quand même ? Ces liens seront signalés dans Diagnostics (Ctrl+Z pour annuler).")))
        {
            return;
        }

        _project!.RemoveAsset(selected);
        StatusText = Tr.F("'{0}' supprimé (Ctrl+Z pour annuler)", selected.Header);
    }

    private void ImportTextures() =>
        ImportFiles(_dialogs.AskTextureFiles(), (project, file, reserved) => _assetViewModels.Importer.ImportTexture(project, file, reserved), Tr.T("Texture"));

    private void ImportSounds() =>
        ImportFiles(_dialogs.AskSoundFiles(), (project, file, reserved) => _assetViewModels.Importer.ImportSound(project, file, reserved), Tr.T("Son"));

    private void ImportFiles(IReadOnlyList<string> files, Func<ModProject, string, ISet<ResourceId>, Asset> import, string label)
    {
        if (_project is null || files.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> errors = _project.ImportFiles(files, import, label);
        int imported = files.Count - errors.Count;
        if (imported > 0)
        {
            StatusText = Tr.F("{0} fichier(s) importé(s)", imported);
        }

        if (errors.Count > 0)
        {
            _dialogs.ShowError(Tr.T("Import"), string.Join("\n", errors));
        }
    }

    private void EditProjectSettings()
    {
        if (_project is null)
        {
            return;
        }

        ProjectSettings current = _project.Model.Settings;
        ProjectSettings draft = current.Clone();
        if (!_dialogs.EditProjectSettings(draft))
        {
            return;
        }

        _project.History.Execute(new ChangeProjectSettingsCommand(current, draft));
        _project.RefreshSettings();
        _log.Info(Tr.F("Paramètres du projet modifiés (MDK {0}).", current.MdkId));
    }

    /// <summary>Valide le projet et met à jour le panneau Diagnostics.</summary>
    private void Validate()
    {
        if (_project is null)
        {
            return;
        }

        var diagnostics = new DiagnosticBag();
        ProjectSettings settings = _project.Model.Settings;
        if (_mdks.Library.Find(settings.MdkId) is null)
        {
            diagnostics.Warning(
                Tr.F("Le MDK '{0}' n'est pas installé. Choisissez-en un dans Espace de travail > Paramètres du projet.", settings.MdkId ?? Tr.T("aucun")),
                Tr.T("Projet"));
        }

        _project.Model.Assets.Validate(diagnostics);
        Diagnostics.SetValidationResults(diagnostics);

        // État de chaque élément affiché dans le Dashboard.
        var byAsset = diagnostics.Where(d => d.AssetId is not null).ToLookup(d => d.AssetId!.Value);
        foreach (AssetViewModel asset in _project.Assets)
        {
            IEnumerable<Diagnostic> issues = byAsset[asset.Model.Id];
            asset.SetIssues(issues.Count(d => d.Severity == DiagnosticSeverity.Error), issues.Count(d => d.Severity == DiagnosticSeverity.Warning));
        }
    }

    private void ValidateAndShow()
    {
        Validate();
        StatusText = Tr.T("Validation : ") + Diagnostics.Summary;
        PanelRequested?.Invoke(this, DiagnosticsPanelId);
    }

    private enum PipelineAction
    {
        Build,
        RunClient,
        Export,
        ReloadGradle,
    }

    private async Task RunPipelineAsync(PipelineAction action)
    {
        if (_project is null || (_project.IsDirty && !SaveProject(isAutoSave: false)))
        {
            return;
        }

        string? exportPath = null;
        if (action == PipelineAction.Export)
        {
            exportPath = _dialogs.AskFileToSave(Tr.T("Exporter le mod"), Tr.T("Mod Minecraft (*.jar)|*.jar"), _project.Model.Settings.ModId + ".jar");
            if (exportPath is null)
            {
                return;
            }
        }

        // Le code Java ouvert dans les onglets est compilé tel qu'il est affiché.
        SaveDirtyDocuments();
        ModProject project = _project.Model;
        SetBusy(new CancellationTokenSource());
        StatusText = action switch
        {
            PipelineAction.RunClient => Tr.T("Lancement de Minecraft..."),
            PipelineAction.ReloadGradle => Tr.T("Rechargement de Gradle..."),
            _ => Tr.T("Build en cours..."),
        };
        _log.Info(new string('-', 60));

        try
        {
            CancellationToken token = _buildCancellation!.Token;
            BuildResult result = action switch
            {
                PipelineAction.RunClient => await _pipeline.RunClientAsync(project, _log, token),
                PipelineAction.ReloadGradle => await _pipeline.ReloadGradleAsync(project, _log, token),
                _ => await _pipeline.BuildAsync(project, _log, token),
            };

            Diagnostics.SetBuildResults(result.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info));
            _buildDiagnostics = [.. result.Diagnostics];
            if (action == PipelineAction.ReloadGradle)
            {
                StatusText = result.Success ? Tr.T("Projet Gradle rechargé") : Tr.T("Échec du rechargement de Gradle");
            }
            else
            {
                bool runClient = action == PipelineAction.RunClient;
                StatusText = result.Success ? (runClient ? Tr.T("Minecraft fermé") : Tr.T("Build réussi")) : Tr.T("Échec du build");
                _lastBuildSummary = result.Success
                    ? (runClient ? Tr.F("Lancement réussi le {0:g} en {1:mm\\:ss}", DateTime.Now, result.Duration)
                                 : Tr.F("Build réussi le {0:g} en {1:mm\\:ss}", DateTime.Now, result.Duration))
                      + (result.JarPath is null ? string.Empty : $" : {Path.GetFileName(result.JarPath)}")
                    : runClient ? Tr.F("Échec du lancement le {0:g}", DateTime.Now) : Tr.F("Échec du build le {0:g}", DateTime.Now);
                OnPropertyChanged(nameof(LastBuildSummary));
            }

            if (result.Success && exportPath is not null && result.JarPath is not null)
            {
                File.Copy(result.JarPath, exportPath, overwrite: true);
                _log.Success(Tr.F("Mod exporté : {0}", exportPath));
                StatusText = Tr.F("Mod exporté : {0}", Path.GetFileName(exportPath));
            }

            if (!result.Success)
            {
                PanelRequested?.Invoke(this, DiagnosticsPanelId);
            }
        }
        catch (OperationCanceledException)
        {
            _log.Warning(Tr.T("Opération annulée."));
            StatusText = Tr.T("Build annulé");
        }
        catch (Exception exception)
        {
            _log.Error(Tr.T("Erreur inattendue : ") + exception.Message);
            StatusText = Tr.T("Échec du build");
        }
        finally
        {
            SetBusy(null);
            Explorer.Refresh();
            RefreshGeneratedDocuments();
        }
    }

    private void SetBusy(CancellationTokenSource? cancellation)
    {
        _buildCancellation?.Dispose();
        _buildCancellation = cancellation;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanEdit));
        CommandManager.InvalidateRequerySuggested();
    }

    private bool ConfirmDiscardChanges()
    {
        if (!ConfirmDirtyDocuments())
        {
            return false;
        }

        if (_project is null || !_project.IsDirty)
        {
            return true;
        }

        return _dialogs.AskUnsavedChanges(_project.Name) switch
        {
            UnsavedChangesChoice.Save => SaveProject(isAutoSave: false),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
    }

    /// <summary>
    /// Double-clic dans Diagnostics : l'élément concerné s'ouvre dans son onglet ; une erreur
    /// Java dans du code écrit à la main ou verrouillé ouvre le fichier à la ligne fautive.
    /// </summary>
    private void NavigateToDiagnostic(Diagnostic diagnostic)
    {
        if (_project is null)
        {
            return;
        }

        if (diagnostic.AssetId is { } assetId)
        {
            if (_project.Model.Assets.Find(assetId) is { } asset)
            {
                OpenAsset(asset);
            }
            else
            {
                StatusText = Tr.T("Cet élément n'existe plus.");
            }

            return;
        }

        if (diagnostic.FilePath is { } path)
        {
            OpenFileAtLine(_project.Model.Layout.ToAbsolutePath(path), diagnostic.Line);
        }
    }

    private void OpenFolder(string directory) => Execute(Tr.T("Ouverture du dossier"), () => _shell.OpenFolder(directory));

    /// <summary>Exécute une action et affiche l'erreur à l'utilisateur si elle échoue.</summary>
    private bool Execute(string operation, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
                                              or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _log.Error(Tr.F("{0} : {1}", operation, exception.Message));
            _dialogs.ShowError(operation, exception.Message);
            return false;
        }
    }

    private void OnProjectContentChanged(object? sender, EventArgs e)
    {
        _validationTimer.Stop();
        _validationTimer.Start();
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        RaiseHistoryLabels();
        OnProjectContentChanged(sender, e);
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseHistoryLabels()
    {
        OnPropertyChanged(nameof(UndoLabel));
        OnPropertyChanged(nameof(RedoLabel));
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectViewModel.IsDirty) or nameof(ProjectViewModel.Name))
        {
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(ProjectStatus));
        }

        if (e.PropertyName == nameof(ProjectViewModel.SelectedAsset))
        {
            Details = _project?.SelectedAsset ?? (IDetailsTarget?)_activeDocument;
        }
    }
}
