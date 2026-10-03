using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Panels;

/// <summary>Un fichier ou un dossier de l'explorateur. Les enfants d'un dossier sont lus à son ouverture.</summary>
public sealed class FileNodeViewModel : ObservableObject
{
    private static readonly FileNodeViewModel Placeholder = new(string.Empty, isDirectory: false, explorer: null);

    private readonly ProjectExplorerViewModel? _explorer;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isModified;
    private bool _isLoaded;

    public FileNodeViewModel(string fullPath, bool isDirectory, ProjectExplorerViewModel? explorer, string? label = null)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        _explorer = explorer;
        Name = label ?? Path.GetFileName(fullPath);
        if (isDirectory && explorer is not null)
        {
            Children.Add(Placeholder);
        }
    }

    public string FullPath { get; }

    public string Name { get; }

    public bool IsDirectory { get; }

    /// <summary>Glyphe Segoe MDL2 Assets selon le type de fichier.</summary>
    public string Glyph => IsDirectory
        ? (_isExpanded ? "" : "")
        : Path.GetExtension(FullPath).ToLowerInvariant() switch
        {
            ".java" or ".gradle" => "",
            ".json" or ".mcmeta" or ".toml" or ".properties" => "",
            ".png" or ".jpg" => "",
            ".ogg" or ".wav" => "",
            ".jar" or ".zip" => "",
            ".md" or ".txt" => "",
            _ => "",
        };

    public ObservableCollection<FileNodeViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(Glyph));
                if (value)
                {
                    EnsureLoaded();
                }
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Le fichier est ouvert dans un onglet avec des modifications non enregistrées.</summary>
    public bool IsModified
    {
        get => _isModified;
        set => SetProperty(ref _isModified, value);
    }

    public void EnsureLoaded()
    {
        if (_isLoaded || !IsDirectory || _explorer is null)
        {
            return;
        }

        _isLoaded = true;
        Children.Clear();
        foreach (FileNodeViewModel child in _explorer.ReadChildren(FullPath))
        {
            Children.Add(child);
        }
    }

    /// <summary>Nœuds déjà chargés (sans lire le disque).</summary>
    public IEnumerable<FileNodeViewModel> LoadedDescendants()
    {
        foreach (FileNodeViewModel child in Children.Where(c => !ReferenceEquals(c, Placeholder)))
        {
            yield return child;
            foreach (FileNodeViewModel descendant in child.LoadedDescendants())
            {
                yield return descendant;
            }
        }
    }
}

/// <summary>
/// Explorateur du projet, façon VS Code : arborescence des fichiers (chargée dossier par
/// dossier), recherche par nom, création, renommage et mise à la corbeille. Les dossiers
/// lourds (caches Gradle, sorties de compilation) sont masqués. L'arbre se met à jour
/// quand les fichiers changent sur le disque.
/// </summary>
public sealed class ProjectExplorerViewModel : ObservableObject, IDisposable
{
    private const int MaxSearchResults = 200;
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>Dossiers jamais affichés (caches, sorties de Gradle, outils).</summary>
    private static readonly HashSet<string> HiddenDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mineengine", ".gradle", ".git", ".idea", ".vscode", "build", "run", "runs", "bin", "obj", "out",
    };

    private readonly IShellService _shell;
    private readonly IDialogService _dialogs;
    private readonly Action<string> _openFile;
    private readonly Action<string> _selectFile;
    private readonly DispatcherTimer _refreshTimer;
    private ModProject? _project;
    private FileSystemWatcher? _watcher;
    private FileNodeViewModel? _selectedNode;
    private string _searchText = string.Empty;

    /// <param name="openFile">Ouvre un fichier (onglet, asset ou application de Windows).</param>
    /// <param name="selectFile">Réagit à la sélection d'un fichier (asset affiché dans Détails).</param>
    public ProjectExplorerViewModel(IShellService shell, IDialogService dialogs, Action<string> openFile, Action<string> selectFile)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _openFile = openFile ?? throw new ArgumentNullException(nameof(openFile));
        _selectFile = selectFile ?? throw new ArgumentNullException(nameof(selectFile));
        _refreshTimer = new DispatcherTimer { Interval = RefreshDelay };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            Refresh();
        };

        RefreshCommand = new RelayCommand(Refresh, () => _project is not null);
        CollapseAllCommand = new RelayCommand(CollapseAll, () => _project is not null);
        OpenCommand = new RelayCommand<FileNodeViewModel>(Open);
        NewFileCommand = new RelayCommand(() => CreateEntry(isDirectory: false), () => _project is not null);
        NewFolderCommand = new RelayCommand(() => CreateEntry(isDirectory: true), () => _project is not null);
        RenameCommand = new RelayCommand<FileNodeViewModel>(Rename, node => ProtectionReason(node.FullPath) is null);
        DeleteCommand = new RelayCommand<FileNodeViewModel>(Delete, node => ProtectionReason(node.FullPath) is null);
        RevealCommand = new RelayCommand<FileNodeViewModel>(node => Reveal(node.FullPath));
        RevealProjectCommand = new RelayCommand(() => Reveal(_project!.Layout.RootDirectory), () => _project is not null);
        CopyPathCommand = new RelayCommand<FileNodeViewModel>(node => System.Windows.Clipboard.SetText(node.FullPath));
    }

    public ObservableCollection<FileNodeViewModel> Roots { get; } = [];

    /// <summary>Résultats de la recherche (fichiers dont le nom contient le texte).</summary>
    public ObservableCollection<FileNodeViewModel> SearchResults { get; } = [];

    public bool IsSearching => _searchText.Length > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value?.Trim() ?? string.Empty))
            {
                UpdateSearch();
                OnPropertyChanged(nameof(IsSearching));
            }
        }
    }

    public FileNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value) && value is { IsDirectory: false })
            {
                _selectFile(value.FullPath);
            }
        }
    }

    public ICommand RefreshCommand { get; }

    public ICommand CollapseAllCommand { get; }

    /// <summary>Ouvre le nœud passé en paramètre (double-clic, Entrée).</summary>
    public ICommand OpenCommand { get; }

    public ICommand NewFileCommand { get; }

    public ICommand NewFolderCommand { get; }

    public ICommand RenameCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand RevealCommand { get; }

    public ICommand RevealProjectCommand { get; }

    public ICommand CopyPathCommand { get; }

    public void SetProject(ModProject? project)
    {
        _project = project;
        _watcher?.Dispose();
        _watcher = null;
        SearchText = string.Empty;
        Roots.Clear();
        if (project is null || !Directory.Exists(project.Layout.RootDirectory))
        {
            return;
        }

        var root = new FileNodeViewModel(project.Layout.RootDirectory, isDirectory: true, this, label: project.Name);
        root.IsExpanded = true;
        Roots.Add(root);
        Watch(project.Layout.RootDirectory);
    }

    /// <summary>Relit l'arbre en gardant les dossiers ouverts et la sélection.</summary>
    public void Refresh()
    {
        if (_project is null)
        {
            return;
        }

        var expanded = Roots.SelectMany(r => r.LoadedDescendants().Prepend(r))
            .Where(n => n.IsDirectory && n.IsExpanded)
            .Select(n => n.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? selected = _selectedNode?.FullPath;
        var modified = Roots.SelectMany(r => r.LoadedDescendants()).Where(n => n.IsModified).Select(n => n.FullPath).ToList();

        Roots.Clear();
        var root = new FileNodeViewModel(_project.Layout.RootDirectory, isDirectory: true, this, label: _project.Name);
        Roots.Add(root);
        Restore(root, expanded);

        foreach (string path in modified)
        {
            MarkModified(path, true);
        }

        if (selected is not null && Find(selected) is { } node)
        {
            node.IsSelected = true;
        }

        UpdateSearch();
    }

    /// <summary>Signale qu'un fichier ouvert a (ou n'a plus) des modifications non enregistrées.</summary>
    public void MarkModified(string fullPath, bool isModified)
    {
        if (Find(fullPath) is { } node)
        {
            node.IsModified = isModified;
        }
    }

    public void Dispose() => _watcher?.Dispose();

    internal IReadOnlyList<FileNodeViewModel> ReadChildren(string directory)
    {
        try
        {
            IEnumerable<FileNodeViewModel> directories = Directory.EnumerateDirectories(directory)
                .Where(d => !HiddenDirectories.Contains(Path.GetFileName(d)))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(d => new FileNodeViewModel(d, isDirectory: true, this));
            IEnumerable<FileNodeViewModel> files = Directory.EnumerateFiles(directory)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(f => new FileNodeViewModel(f, isDirectory: false, this));
            return [.. directories, .. files];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Raison pour laquelle un fichier ne peut être ni renommé ni supprimé ici, ou null.</summary>
    public string? ProtectionReason(string fullPath)
    {
        if (_project is null)
        {
            return "Aucun projet ouvert.";
        }

        ProjectLayout layout = _project.Layout;
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullPath));
        string[] standard =
        [
            layout.RootDirectory, layout.ProjectFile, layout.ContentDirectory, layout.TexturesDirectory, layout.SoundsDirectory,
            layout.ScriptsDirectory, layout.GraphsDirectory, layout.GeneratedDirectory, layout.BuildDirectory,
        ];
        if (standard.Any(s => string.Equals(Path.TrimEndingDirectorySeparator(s), path, StringComparison.OrdinalIgnoreCase)))
        {
            return "Dossier ou fichier indispensable au projet.";
        }

        return path.StartsWith(layout.ContentDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? "Les fichiers d'assets se gèrent depuis le Dashboard (renommer l'identifiant, supprimer l'asset)."
            : null;
    }

    private void Restore(FileNodeViewModel node, HashSet<string> expanded)
    {
        if (!node.IsDirectory || (!expanded.Contains(node.FullPath) && !ReferenceEquals(node, Roots[0])))
        {
            return;
        }

        node.IsExpanded = true;
        foreach (FileNodeViewModel child in node.Children.ToList())
        {
            Restore(child, expanded);
        }
    }

    private FileNodeViewModel? Find(string fullPath) =>
        Roots.SelectMany(r => r.LoadedDescendants().Prepend(r))
            .FirstOrDefault(n => string.Equals(n.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));

    private void Open(FileNodeViewModel node)
    {
        if (node.IsDirectory)
        {
            node.IsExpanded = !node.IsExpanded;
        }
        else
        {
            _openFile(node.FullPath);
        }
    }

    private void CollapseAll()
    {
        foreach (FileNodeViewModel node in Roots.SelectMany(r => r.LoadedDescendants()))
        {
            node.IsExpanded = false;
        }
    }

    /// <summary>Crée un fichier ou un dossier dans le dossier sélectionné (ou celui du fichier sélectionné).</summary>
    private void CreateEntry(bool isDirectory)
    {
        if (_project is null)
        {
            return;
        }

        string parent = _selectedNode switch
        {
            { IsDirectory: true } directory => directory.FullPath,
            { } file => Path.GetDirectoryName(file.FullPath)!,
            _ => _project.Layout.ScriptsDirectory,
        };
        if (parent.StartsWith(_project.Layout.ContentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            parent = _project.Layout.ScriptsDirectory;
        }

        string? name = _dialogs.AskText(
            isDirectory ? "Nouveau dossier" : "Nouveau fichier",
            $"Nom du {(isDirectory ? "dossier" : "fichier")} dans {_project.Layout.ToRelativePath(parent)} :",
            isDirectory ? "nouveau_dossier" : "nouveau_fichier.java");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            _dialogs.ShowError("Nom invalide", $"« {name} » contient des caractères interdits dans un nom de fichier.");
            return;
        }

        string path = Path.Combine(parent, name.Trim());
        if (File.Exists(path) || Directory.Exists(path))
        {
            _dialogs.ShowError("Nom déjà utilisé", $"« {name} » existe déjà dans ce dossier.");
            return;
        }

        try
        {
            Directory.CreateDirectory(parent);
            if (isDirectory)
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                File.WriteAllText(path, string.Empty);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Création impossible", exception.Message);
            return;
        }

        Refresh();
        if (Find(parent) is { } parentNode)
        {
            parentNode.IsExpanded = true;
        }

        if (!isDirectory)
        {
            _openFile(path);
        }
    }

    private void Rename(FileNodeViewModel node)
    {
        string? name = _dialogs.AskText("Renommer", $"Nouveau nom de « {node.Name} » :", node.Name);
        if (string.IsNullOrWhiteSpace(name) || name == node.Name)
        {
            return;
        }

        string target = Path.Combine(Path.GetDirectoryName(node.FullPath)!, name.Trim());
        try
        {
            if (node.IsDirectory)
            {
                Directory.Move(node.FullPath, target);
            }
            else
            {
                File.Move(node.FullPath, target);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Renommage impossible", exception.Message);
            return;
        }

        Refresh();
    }

    /// <summary>Envoie le fichier ou le dossier dans la corbeille de Windows (récupérable).</summary>
    private void Delete(FileNodeViewModel node)
    {
        if (!_dialogs.Confirm("Mettre à la corbeille", $"Mettre « {node.Name} » à la corbeille ?"))
        {
            return;
        }

        try
        {
            if (node.IsDirectory)
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    node.FullPath, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            else
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    node.FullPath, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _dialogs.ShowError("Suppression impossible", exception.Message);
            return;
        }

        Refresh();
    }

    private void Reveal(string path) => _shell.OpenFolder(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);

    private void UpdateSearch()
    {
        SearchResults.Clear();
        if (_project is null || _searchText.Length == 0)
        {
            return;
        }

        foreach (string file in EnumerateVisibleFiles(_project.Layout.RootDirectory)
                     .Where(f => Path.GetFileName(f).Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                     .Take(MaxSearchResults))
        {
            SearchResults.Add(new FileNodeViewModel(file, isDirectory: false, explorer: null, label: _project.Layout.ToRelativePath(file)));
        }
    }

    private static IEnumerable<string> EnumerateVisibleFiles(string directory)
    {
        var pending = new Stack<string>([directory]);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            string[] files;
            string[] directories;
            try
            {
                files = Directory.GetFiles(current);
                directories = Directory.GetDirectories(current);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string file in files.Order(StringComparer.OrdinalIgnoreCase))
            {
                yield return file;
            }

            foreach (string child in directories.Where(d => !HiddenDirectories.Contains(Path.GetFileName(d))))
            {
                pending.Push(child);
            }
        }
    }

    /// <summary>Surveille le dossier du projet ; les changements dans les dossiers masqués sont ignorés.</summary>
    private void Watch(string root)
    {
        try
        {
            _watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                InternalBufferSize = 64 * 1024,
            };
            _watcher.Created += OnFileSystemChanged;
            _watcher.Deleted += OnFileSystemChanged;
            _watcher.Renamed += OnFileSystemChanged;
            _watcher.Error += (_, _) => ScheduleRefresh();
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            _watcher = null;
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        string[] segments = e.FullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!segments.Any(HiddenDirectories.Contains))
        {
            ScheduleRefresh();
        }
    }

    private void ScheduleRefresh() =>
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        });
}
