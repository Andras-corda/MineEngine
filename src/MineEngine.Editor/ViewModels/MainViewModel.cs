using System.ComponentModel;
using System.Windows.Input;
using MineEngine.Build;
using MineEngine.Core.Assets;
using MineEngine.Core.Logging;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels;

/// <summary>ViewModel de la fenêtre principale : cycle de vie du projet et build.</summary>
public sealed class MainViewModel : ObservableObject
{
    private const string ApplicationName = "Mine Engine";

    private readonly ProjectRepository _repository;
    private readonly AssetViewModelFactory _assetViewModels;
    private readonly BuildPipeline _pipeline;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly MdkServices _mdks;

    private ProjectViewModel? _project;
    private CancellationTokenSource? _buildCancellation;
    private string _statusText = "Prêt";

    public MainViewModel(
        ProjectRepository repository,
        AssetViewModelFactory assetViewModels,
        BuildPipeline pipeline,
        IDialogService dialogs,
        IShellService shell,
        MdkServices mdks,
        OutputViewModel output)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _assetViewModels = assetViewModels ?? throw new ArgumentNullException(nameof(assetViewModels));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _mdks = mdks ?? throw new ArgumentNullException(nameof(mdks));
        Output = output ?? throw new ArgumentNullException(nameof(output));

        NewProjectCommand = new RelayCommand(NewProject, () => !IsBusy);
        OpenProjectCommand = new RelayCommand(OpenProject, () => !IsBusy);
        SaveProjectCommand = new RelayCommand(() => SaveProject(), () => HasProject && !IsBusy);
        CloseProjectCommand = new RelayCommand(() => CloseProject(), () => HasProject && !IsBusy);
        OpenProjectFolderCommand = new RelayCommand(() => OpenFolder(_project!.Model.Layout.RootDirectory), () => HasProject);
        OpenBuildFolderCommand = new RelayCommand(() => OpenFolder(_project!.Model.Layout.BuildDirectory), () => HasProject);
        EditProjectSettingsCommand = new RelayCommand(EditProjectSettings, () => CanEdit);
        OpenMdkManagerCommand = new RelayCommand(_dialogs.ShowMdkManager, () => !IsBusy);
        OpenSettingsCommand = new RelayCommand(_dialogs.ShowSettings);

        AddItemCommand = new RelayCommand(() => _project!.AddAsset(AssetType.Item), () => CanEdit);
        AddBlockCommand = new RelayCommand(() => _project!.AddAsset(AssetType.Block), () => CanEdit);
        DeleteAssetCommand = new RelayCommand(DeleteSelectedAsset, () => CanEdit && _project!.HasSelection);

        BuildCommand = new AsyncRelayCommand(() => RunPipelineAsync(runClient: false), () => HasProject && !IsBusy);
        RunClientCommand = new AsyncRelayCommand(() => RunPipelineAsync(runClient: true), () => HasProject && !IsBusy);
        CancelBuildCommand = new RelayCommand(() => _buildCancellation?.Cancel(), () => IsBusy);
    }

    public OutputViewModel Output { get; }

    public ProjectViewModel? Project
    {
        get => _project;
        private set
        {
            if (_project is not null)
            {
                _project.PropertyChanged -= OnProjectPropertyChanged;
                _project.Dispose();
            }

            _project = value;
            if (_project is not null)
            {
                _project.PropertyChanged += OnProjectPropertyChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProject));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(WindowTitle));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasProject => _project is not null;

    public bool IsBusy => _buildCancellation is not null;

    public bool CanEdit => HasProject && !IsBusy;

    public string WindowTitle => _project is null
        ? ApplicationName
        : $"{_project.Name}{(_project.IsDirty ? " *" : string.Empty)} - {ApplicationName}";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public ICommand NewProjectCommand { get; }

    public ICommand OpenProjectCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand CloseProjectCommand { get; }

    public ICommand OpenProjectFolderCommand { get; }

    public ICommand OpenBuildFolderCommand { get; }

    public ICommand EditProjectSettingsCommand { get; }

    public ICommand OpenMdkManagerCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public ICommand AddItemCommand { get; }

    public ICommand AddBlockCommand { get; }

    public ICommand DeleteAssetCommand { get; }

    public ICommand BuildCommand { get; }

    public ICommand RunClientCommand { get; }

    public ICommand CancelBuildCommand { get; }

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
            Output.Info($"Projet créé : {model.Layout.RootDirectory} (MDK {model.Settings.MdkId})");
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
            Output.Info($"Projet ouvert : {model.Layout.RootDirectory} ({model.Assets.Count} assets)");
            StatusText = "Projet ouvert";
            WarnIfMdkMissing(model);
        });
    }

    private bool SaveProject()
    {
        if (_project is null)
        {
            return true;
        }

        bool saved = Execute("Enregistrement", () =>
        {
            _repository.Save(_project.Model);
            StatusText = $"Enregistré à {DateTime.Now:HH:mm:ss}";
        });
        return saved;
    }

    private bool CloseProject()
    {
        if (!ConfirmDiscardChanges())
        {
            return false;
        }

        Project = null;
        StatusText = "Prêt";
        return true;
    }

    private void EditProjectSettings()
    {
        if (_project is null || !_dialogs.EditProjectSettings(_project.Model.Settings))
        {
            return;
        }

        _project.Model.MarkDirty();
        _project.RefreshSettings();
        OnPropertyChanged(nameof(WindowTitle));
        Output.Info($"Paramètres du projet modifiés (MDK {_project.Model.Settings.MdkId}).");
    }

    private void WarnIfMdkMissing(ModProject model)
    {
        if (_mdks.Library.Find(model.Settings.MdkId) is null)
        {
            Output.Warning(
                $"Le MDK de ce projet ({model.Settings.MdkId ?? "aucun"}) n'est pas installé. " +
                "Choisissez-en un dans Projet > Paramètres du projet, ou installez-le avec Outils > Gestionnaire de MDK.");
        }
    }

    private void DeleteSelectedAsset()
    {
        AssetViewModel? selected = _project?.SelectedAsset;
        if (selected is null)
        {
            return;
        }

        if (_dialogs.Confirm("Supprimer l'asset", $"Supprimer '{selected.Header}' ({selected.TypeLabel}) du projet ?"))
        {
            _project!.RemoveAsset(selected);
        }
    }

    private async Task RunPipelineAsync(bool runClient)
    {
        if (_project is null || (_project.IsDirty && !SaveProject()))
        {
            return;
        }

        ModProject project = _project.Model;
        SetBusy(new CancellationTokenSource());
        StatusText = runClient ? "Lancement de Minecraft..." : "Build en cours...";
        Output.Info(new string('-', 60));

        try
        {
            BuildResult result = runClient
                ? await _pipeline.RunClientAsync(project, Output, _buildCancellation!.Token)
                : await _pipeline.BuildAsync(project, Output, _buildCancellation!.Token);
            StatusText = result.Success ? (runClient ? "Minecraft fermé" : "Build réussi") : "Échec du build";
        }
        catch (OperationCanceledException)
        {
            Output.Warning("Opération annulée.");
            StatusText = "Build annulé";
        }
        catch (Exception exception)
        {
            Output.Error("Erreur inattendue : " + exception.Message);
            StatusText = "Échec du build";
        }
        finally
        {
            SetBusy(null);
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
        if (_project is null || !_project.IsDirty)
        {
            return true;
        }

        return _dialogs.AskUnsavedChanges(_project.Name) switch
        {
            UnsavedChangesChoice.Save => SaveProject(),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
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
            Output.Error($"{operation} : {exception.Message}");
            _dialogs.ShowError(operation, exception.Message);
            return false;
        }
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectViewModel.IsDirty) or nameof(ProjectViewModel.Name))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }
}
