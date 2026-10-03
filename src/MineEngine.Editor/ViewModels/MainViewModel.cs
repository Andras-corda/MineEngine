using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
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
    private string _statusText = "Prêt";
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
        Diagnostics = new DiagnosticsViewModel(NavigateToAsset);
        Explorer = new ProjectExplorerViewModel(shell, dialogs, OpenFileFromExplorer, SelectFileFromExplorer);
        InitializeDocumentCommands();

        _validationTimer = new DispatcherTimer { Interval = ValidationDelay };
        _validationTimer.Tick += (_, _) =>
        {
            _validationTimer.Stop();
            Validate();
        };

        _autoSaveTimer = new DispatcherTimer();
        _autoSaveTimer.Tick += (_, _) => AutoSave();
        _preferences.Changed += (_, _) => ConfigureAutoSave();
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
        AddItemCommand = new RelayCommand(() => _project!.AddAsset(AssetType.Item), () => CanEdit);
        AddBlockCommand = new RelayCommand(() => _project!.AddAsset(AssetType.Block), () => CanEdit);
        AddMobCommand = new RelayCommand(() => _project!.AddAsset(AssetType.Mob), () => CanEdit);
        AddRecipeCommand = new RelayCommand(() => _project!.AddAsset(AssetType.Recipe), () => CanEdit);
        ImportTexturesCommand = new RelayCommand(ImportTextures, () => CanEdit);
        ImportSoundsCommand = new RelayCommand(ImportSounds, () => CanEdit);
        ValidateCommand = new RelayCommand(ValidateAndShow, () => HasProject);

        // Build
        BuildCommand = new AsyncRelayCommand(() => RunPipelineAsync(runClient: false), () => HasProject && !IsBusy);
        RunClientCommand = new AsyncRelayCommand(() => RunPipelineAsync(runClient: true), () => HasProject && !IsBusy);
        CancelBuildCommand = new RelayCommand(() => _buildCancellation?.Cancel(), () => IsBusy);
        OpenBuildFolderCommand = new RelayCommand(() => OpenFolder(_project!.Model.Layout.BuildDirectory), () => HasProject);

        // Outils
        OpenMdkManagerCommand = new RelayCommand(_dialogs.ShowMdkManager, () => !IsBusy);
        OpenSettingsCommand = new RelayCommand(_dialogs.ShowSettings);
    }

    /// <summary>Demande à la vue d'afficher un panneau (par exemple l'Inspector après une navigation).</summary>
    public event EventHandler<string>? PanelRequested;

    public OutputViewModel Output { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public ProjectExplorerViewModel Explorer { get; }

    public ProjectViewModel? Project
    {
        get => _project;
        private set
        {
            if (_project is not null)
            {
                _project.PropertyChanged -= OnProjectPropertyChanged;
                _project.Model.Assets.Changed -= OnProjectContentChanged;
                _project.History.Changed -= OnHistoryChanged;
                _project.Dispose();
            }

            _project = value;
            if (_project is not null)
            {
                _project.PropertyChanged += OnProjectPropertyChanged;
                _project.Model.Assets.Changed += OnProjectContentChanged;
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
    public string ProjectStatus => _project is null ? "Aucun projet ouvert" : _project.Name;

    public bool IsBusy => _buildCancellation is not null;

    public bool CanEdit => HasProject && !IsBusy;

    public string WindowTitle => _project is null
        ? $"{ApplicationName} — Sélection de projet"
        : $"{_project.Name}{(_project.IsDirty || HasDirtyDocuments ? " *" : string.Empty)} — {ApplicationName}";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string LastBuildSummary => _lastBuildSummary ?? "Aucun build depuis l'ouverture du projet.";

    public string UndoLabel => _project?.History.UndoDescription is { } description ? $"Annuler : {description}" : "Annuler";

    public string RedoLabel => _project?.History.RedoDescription is { } description ? $"Rétablir : {description}" : "Rétablir";

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

    /// <summary>Importe des images PNG comme assets Texture.</summary>
    public ICommand ImportTexturesCommand { get; }

    /// <summary>Importe des sons OGG comme assets Sound.</summary>
    public ICommand ImportSoundsCommand { get; }

    public ICommand ValidateCommand { get; }

    public ICommand BuildCommand { get; }

    public ICommand RunClientCommand { get; }

    public ICommand CancelBuildCommand { get; }

    public ICommand OpenBuildFolderCommand { get; }

    public ICommand OpenMdkManagerCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    /// <summary>Ouvre un projet passé en argument de la ligne de commande.</summary>
    public void OpenProjectAtStartup(string path) => TryOpen(path);

    /// <summary>Appelé à la fermeture de la fenêtre ; faux pour l'annuler.</summary>
    public bool ConfirmShutdown()
    {
        if (IsBusy)
        {
            if (!_dialogs.Confirm(ApplicationName, "Un build est en cours. Voulez-vous l'arrêter et quitter ?"))
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

        Execute("Création du projet", () =>
        {
            ModProject model = _repository.Create(request.ParentDirectory, request.Settings);
            Project = new ProjectViewModel(model, _assetViewModels);
            Hub.Remember(model.Layout.RootDirectory);
            _log.Info($"Projet créé : {model.Layout.RootDirectory} (MDK {model.Settings.MdkId})");
            StatusText = "Projet créé";
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
        Execute("Ouverture du projet", () =>
        {
            ModProject model = _repository.Open(path);
            Project = new ProjectViewModel(model, _assetViewModels);
            Hub.Remember(model.Layout.RootDirectory);
            _log.Info($"Projet ouvert : {model.Layout.RootDirectory} ({model.Assets.Count} assets)");
            foreach (string note in model.LoadNotes)
            {
                _log.Warning("Conversion du projet : " + note + " Enregistrez pour écrire le nouveau format.");
            }

            StatusText = model.LoadNotes.Count > 0 ? "Projet converti au format de la V0.3 (à enregistrer)" : "Projet ouvert";
        });
    }

    private bool SaveProject(bool isAutoSave)
    {
        if (_project is null)
        {
            return true;
        }

        ModProject model = _project.Model;
        bool saved = Execute("Enregistrement", () =>
        {
            _repository.Save(model);
            if (!isAutoSave)
            {
                SaveDirtyDocuments();
            }

            StatusText = isAutoSave
                ? $"Enregistrement automatique à {DateTime.Now:HH:mm:ss}"
                : $"Enregistré à {DateTime.Now:HH:mm:ss}";
        });

        if (saved)
        {
            _project.History.Seal();
            CreateBackupIfDue(model);
            Explorer.Refresh();
            if (isAutoSave)
            {
                _log.Info("Projet enregistré automatiquement.");
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
                _log.Debug("Sauvegarde de secours créée : " + backup.File);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.Warning("La sauvegarde de secours a échoué : " + exception.Message);
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
        StatusText = "Prêt";
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
                "Supprimer un asset utilisé",
                $"'{selected.Header}' est utilisé par :\n{string.Join("\n", usedBy.Select(l => "  - " + l.Label))}\n\n" +
                "Le supprimer quand même ? Ces liens seront signalés dans Diagnostics (Ctrl+Z pour annuler)."))
        {
            return;
        }

        _project!.RemoveAsset(selected);
        StatusText = $"'{selected.Header}' supprimé (Ctrl+Z pour annuler)";
    }

    private void ImportTextures() =>
        ImportFiles(_dialogs.AskTextureFiles(), (project, file, reserved) => _assetViewModels.Importer.ImportTexture(project, file, reserved), "Texture");

    private void ImportSounds() =>
        ImportFiles(_dialogs.AskSoundFiles(), (project, file, reserved) => _assetViewModels.Importer.ImportSound(project, file, reserved), "Son");

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
            StatusText = $"{imported} fichier(s) importé(s)";
        }

        if (errors.Count > 0)
        {
            _dialogs.ShowError("Import", string.Join("\n", errors));
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
        _log.Info($"Paramètres du projet modifiés (MDK {current.MdkId}).");
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
                $"Le MDK '{settings.MdkId ?? "aucun"}' n'est pas installé. Choisissez-en un dans Projet > Paramètres du projet.",
                "Projet");
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
        StatusText = "Validation : " + Diagnostics.Summary;
        PanelRequested?.Invoke(this, DiagnosticsPanelId);
    }

    private async Task RunPipelineAsync(bool runClient)
    {
        if (_project is null || (_project.IsDirty && !SaveProject(isAutoSave: false)))
        {
            return;
        }

        ModProject project = _project.Model;
        SetBusy(new CancellationTokenSource());
        StatusText = runClient ? "Lancement de Minecraft..." : "Build en cours...";
        _log.Info(new string('-', 60));

        try
        {
            BuildResult result = runClient
                ? await _pipeline.RunClientAsync(project, _log, _buildCancellation!.Token)
                : await _pipeline.BuildAsync(project, _log, _buildCancellation!.Token);

            Diagnostics.SetBuildResults(result.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info));
            StatusText = result.Success ? (runClient ? "Minecraft fermé" : "Build réussi") : "Échec du build";
            _lastBuildSummary = result.Success
                ? $"{(runClient ? "Lancement" : "Build")} réussi le {DateTime.Now:dd/MM à HH:mm} en {result.Duration:mm\\:ss}"
                  + (result.JarPath is null ? string.Empty : $" : {Path.GetFileName(result.JarPath)}")
                : $"Échec du {(runClient ? "lancement" : "build")} le {DateTime.Now:dd/MM à HH:mm}";
            OnPropertyChanged(nameof(LastBuildSummary));

            if (!result.Success)
            {
                PanelRequested?.Invoke(this, DiagnosticsPanelId);
            }
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Opération annulée.");
            StatusText = "Build annulé";
        }
        catch (Exception exception)
        {
            _log.Error("Erreur inattendue : " + exception.Message);
            StatusText = "Échec du build";
        }
        finally
        {
            SetBusy(null);
            Explorer.Refresh();
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

    private void NavigateToAsset(Guid assetId)
    {
        if (_project?.SelectAsset(assetId) == true)
        {
            PanelRequested?.Invoke(this, InspectorPanelId);
        }
        else
        {
            StatusText = "Cet asset n'existe plus.";
        }
    }

    private void OpenFolder(string directory) => Execute("Ouverture du dossier", () => _shell.OpenFolder(directory));

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
            _log.Error($"{operation} : {exception.Message}");
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
