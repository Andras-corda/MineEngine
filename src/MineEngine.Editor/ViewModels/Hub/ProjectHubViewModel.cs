using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Project;
using MineEngine.Project.Serialization;
using MineEngine.Project.Workspace;

namespace MineEngine.Editor.ViewModels.Hub;

/// <summary>Page de l'accueil choisie dans la barre latérale.</summary>
public enum HubPage
{
    Home,
    Projects,
    Help,
}

/// <summary>Tri de la liste des projets.</summary>
public enum ProjectSort
{
    Name,
    LastOpened,
    MinecraftVersion,
}

/// <summary>
/// Accueil de l'éditeur, affiché quand aucun projet n'est ouvert : projets connus
/// (recherche, tri, récents), projet sélectionné et raccourcis. La liste est enregistrée
/// dans projects.json ; chaque ouverture ou création de projet y est ajoutée.
/// </summary>
public sealed class ProjectHubViewModel : ObservableObject
{
    private const int RecentCount = 5;

    private readonly KnownProjectsStore _store;
    private readonly ProjectSettingsSerializer _settingsSerializer;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly Action<string> _openProject;
    private readonly ListCollectionView _view;
    private HubPage _page = HubPage.Home;
    private string _searchText = string.Empty;
    private ProjectSort _sort = ProjectSort.Name;
    private bool _showRecentOnly;
    private ProjectCardViewModel? _selectedProject;

    /// <param name="defaultProjectsDirectory">Dossier exploré au premier lancement pour remplir la liste.</param>
    /// <param name="openProject">Ouvre un projet à partir de son dossier.</param>
    public ProjectHubViewModel(
        KnownProjectsStore store,
        ProjectSettingsSerializer settingsSerializer,
        IDialogService dialogs,
        IShellService shell,
        string defaultProjectsDirectory,
        Action<string> openProject)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _settingsSerializer = settingsSerializer ?? throw new ArgumentNullException(nameof(settingsSerializer));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _openProject = openProject ?? throw new ArgumentNullException(nameof(openProject));

        _view = new ListCollectionView(Projects) { Filter = Matches };
        ApplySort();

        NavigateCommand = new RelayCommand<HubPage>(page => Page = page);
        ShowAllCommand = new RelayCommand(() => ShowRecentOnly = false);
        ShowRecentCommand = new RelayCommand(() => ShowRecentOnly = true);
        OpenSelectedCommand = new RelayCommand(() => Open(_selectedProject!), () => _selectedProject is { IsMissing: false });
        OpenProjectCommand = new RelayCommand<ProjectCardViewModel>(Open, card => !card.IsMissing);
        AddExistingCommand = new RelayCommand(AddExisting);
        RemoveCommand = new RelayCommand<ProjectCardViewModel>(Remove);
        OpenFolderCommand = new RelayCommand<ProjectCardViewModel>(card => _shell.OpenFolder(card.RootDirectory), card => !card.IsMissing);
        RefreshCommand = new RelayCommand(Refresh);

        Load(defaultProjectsDirectory);
    }

    public ObservableCollection<ProjectCardViewModel> Projects { get; } = [];

    /// <summary>Liste filtrée et triée affichée par l'accueil.</summary>
    public ICollectionView VisibleProjects => _view;

    public HubPage Page
    {
        get => _page;
        set
        {
            if (SetProperty(ref _page, value))
            {
                OnPropertyChanged(nameof(IsHomePage));
                OnPropertyChanged(nameof(IsProjectsPage));
                OnPropertyChanged(nameof(IsHelpPage));
                OnPropertyChanged(nameof(ShowsProjectList));
            }
        }
    }

    public bool IsHomePage => _page == HubPage.Home;

    public bool IsProjectsPage => _page == HubPage.Projects;

    public bool IsHelpPage => _page == HubPage.Help;

    public bool ShowsProjectList => _page != HubPage.Help;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                _view.Refresh();
                OnPropertyChanged(nameof(IsListEmpty));
            }
        }
    }

    public IReadOnlyList<Assets.Choice<ProjectSort>> SortChoices { get; } =
    [
        new(ProjectSort.Name, "Nom du projet"),
        new(ProjectSort.LastOpened, "Ouvert récemment"),
        new(ProjectSort.MinecraftVersion, "Version de Minecraft"),
    ];

    public ProjectSort Sort
    {
        get => _sort;
        set
        {
            if (SetProperty(ref _sort, value))
            {
                ApplySort();
            }
        }
    }

    /// <summary>Onglet "Récents" : les derniers projets ouverts, du plus récent au plus ancien.</summary>
    public bool ShowRecentOnly
    {
        get => _showRecentOnly;
        set
        {
            if (SetProperty(ref _showRecentOnly, value))
            {
                ApplySort();
                OnPropertyChanged(nameof(IsListEmpty));
            }
        }
    }

    public ProjectCardViewModel? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => _selectedProject is not null;

    public string CountLabel => Projects.Count switch
    {
        0 => "aucun projet",
        1 => "1 projet",
        int count => $"{count} projets",
    };

    public bool IsListEmpty => _view.IsEmpty;

    public string EmptyListMessage => Projects.Count == 0
        ? "Aucun projet pour l'instant. Créez-en un, ou ajoutez un projet existant."
        : ShowRecentOnly && _searchText.Length == 0
            ? "Aucun projet ouvert récemment."
            : "Aucun projet ne correspond à la recherche.";

    public ICommand NavigateCommand { get; }

    public ICommand ShowAllCommand { get; }

    public ICommand ShowRecentCommand { get; }

    public ICommand OpenSelectedCommand { get; }

    /// <summary>Ouvre le projet passé en paramètre (double-clic sur une carte).</summary>
    public ICommand OpenProjectCommand { get; }

    /// <summary>Ajoute un projet existant à la liste sans l'ouvrir.</summary>
    public ICommand AddExistingCommand { get; }

    /// <summary>Retire un projet de la liste (le dossier n'est pas touché).</summary>
    public ICommand RemoveCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public ICommand RefreshCommand { get; }

    /// <summary>Note qu'un projet vient d'être ouvert ou créé.</summary>
    public void Remember(string rootDirectory)
    {
        string root = KnownProjectsStore.Normalize(rootDirectory);
        Upsert(new KnownProject(root, DateTimeOffset.Now));
        Save();
    }

    /// <summary>Relit l'aperçu de chaque projet (contenu modifié, dossier supprimé).</summary>
    public void Refresh()
    {
        foreach (ProjectCardViewModel card in Projects)
        {
            card.Update(card.Known, ProjectSummary.TryRead(card.RootDirectory, _settingsSerializer));
        }

        _view.Refresh();
        OnPropertyChanged(nameof(IsListEmpty));
    }

    private void Load(string defaultProjectsDirectory)
    {
        bool firstRun = !_store.Exists;
        foreach (KnownProject known in _store.Load())
        {
            Add(known);
        }

        // Premier lancement : les projets du dossier par défaut sont proposés d'emblée.
        if (firstRun && Directory.Exists(defaultProjectsDirectory))
        {
            foreach (string directory in Directory.EnumerateDirectories(defaultProjectsDirectory)
                         .Where(d => File.Exists(Path.Combine(d, ProjectLayout.ProjectFileName))))
            {
                Upsert(new KnownProject(KnownProjectsStore.Normalize(directory), null));
            }

            Save();
        }

        SelectedProject = _view.Cast<ProjectCardViewModel>().FirstOrDefault();
    }

    private void Open(ProjectCardViewModel card)
    {
        SelectedProject = card;
        _openProject(card.RootDirectory);
    }

    private void AddExisting()
    {
        string? directory = _dialogs.AskFolder("Choisir le dossier d'un projet Mine Engine");
        if (directory is null)
        {
            return;
        }

        if (ProjectSummary.TryRead(directory, _settingsSerializer) is null)
        {
            _dialogs.ShowError("Ajouter un projet", $"Le dossier '{directory}' ne contient pas de projet Mine Engine ({ProjectLayout.ProjectFileName}).");
            return;
        }

        string root = KnownProjectsStore.Normalize(directory);
        ProjectCardViewModel card = Find(root) ?? Add(new KnownProject(root, null));
        Save();
        SearchText = string.Empty;
        ShowRecentOnly = false;
        SelectedProject = card;
    }

    private void Remove(ProjectCardViewModel card)
    {
        if (!_dialogs.Confirm("Retirer de la liste", $"Retirer '{card.Name}' de la liste ? Le dossier du projet n'est pas supprimé."))
        {
            return;
        }

        Projects.Remove(card);
        if (ReferenceEquals(SelectedProject, card))
        {
            SelectedProject = _view.Cast<ProjectCardViewModel>().FirstOrDefault();
        }

        Save();
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(IsListEmpty));
        OnPropertyChanged(nameof(EmptyListMessage));
    }

    private ProjectCardViewModel Add(KnownProject known)
    {
        var card = new ProjectCardViewModel(known, ProjectSummary.TryRead(known.RootDirectory, _settingsSerializer));
        Projects.Add(card);
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(IsListEmpty));
        OnPropertyChanged(nameof(EmptyListMessage));
        return card;
    }

    private void Upsert(KnownProject known)
    {
        if (Find(known.RootDirectory) is { } card)
        {
            card.Update(known with { LastOpened = known.LastOpened ?? card.LastOpened },
                ProjectSummary.TryRead(known.RootDirectory, _settingsSerializer));
            _view.Refresh();
        }
        else
        {
            Add(known);
        }
    }

    private ProjectCardViewModel? Find(string root) =>
        Projects.FirstOrDefault(p => string.Equals(p.RootDirectory, root, StringComparison.OrdinalIgnoreCase));

    private void Save() => _store.Save(Projects.Select(p => p.Known));

    private bool Matches(object item)
    {
        if (item is not ProjectCardViewModel card)
        {
            return false;
        }

        if (_showRecentOnly && card.LastOpened is null)
        {
            return false;
        }

        if (_showRecentOnly && _searchText.Length == 0
            && Projects.Where(p => p.LastOpened is not null).OrderByDescending(p => p.SortDate).Take(RecentCount).All(p => !ReferenceEquals(p, card)))
        {
            return false;
        }

        return _searchText.Length == 0
               || card.Name.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase)
               || card.PathLabel.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase)
               || card.Description.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private void ApplySort()
    {
        using (_view.DeferRefresh())
        {
            _view.SortDescriptions.Clear();
            ProjectSort sort = _showRecentOnly ? ProjectSort.LastOpened : _sort;
            switch (sort)
            {
                case ProjectSort.LastOpened:
                    _view.SortDescriptions.Add(new SortDescription(nameof(ProjectCardViewModel.SortDate), ListSortDirection.Descending));
                    break;

                case ProjectSort.MinecraftVersion:
                    _view.SortDescriptions.Add(new SortDescription(nameof(ProjectCardViewModel.SortVersion), ListSortDirection.Descending));
                    break;
            }

            _view.SortDescriptions.Add(new SortDescription(nameof(ProjectCardViewModel.Name), ListSortDirection.Ascending));
        }

        OnPropertyChanged(nameof(EmptyListMessage));
    }
}
