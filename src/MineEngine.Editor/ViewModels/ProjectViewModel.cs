using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Windows.Data;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Json;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Project;
using MineEngine.Project.Commands;

namespace MineEngine.Editor.ViewModels;

/// <summary>Filtre du Dashboard par type d'asset (pastille "Items 3").</summary>
public sealed class AssetTypeFilter : ObservableObject
{
    private int _count;
    private bool _isActive;

    public AssetTypeFilter(string label, AssetType? type)
    {
        Label = label;
        Type = type;
    }

    public string Label { get; }

    /// <summary>Type affiché, ou null pour tous les types.</summary>
    public AssetType? Type { get; }

    /// <summary>Nombre d'assets de ce type dans le projet.</summary>
    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    /// <summary>Filtre actuellement appliqué.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public override string ToString() => Label;
}

/// <summary>
/// Projet ouvert dans l'éditeur : contenu, sélection courante, dossier ouvert et filtre du
/// Dashboard. La liste suit le registre d'assets et les dossiers, y compris lors d'une
/// annulation. Sans recherche, le Dashboard montre le contenu du dossier ouvert (ses
/// sous-dossiers puis ses éléments) ; une recherche porte sur tous les dossiers.
/// </summary>
public sealed class ProjectViewModel : ObservableObject, IDisposable
{
    private readonly AssetViewModelFactory _factory;
    private AssetViewModel? _selectedAsset;
    private IReadOnlyList<AssetViewModel> _selectedAssets = [];
    private string _searchText = string.Empty;
    private AssetTypeFilter _typeFilter;
    private bool _isTileView;
    private string _currentFolder = string.Empty;
    private IReadOnlyList<FolderCrumb> _breadcrumb = [];
    private IReadOnlyList<FolderChoice> _folderChoices = [];

    public ProjectViewModel(ModProject model, AssetViewModelFactory factory)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        TypeFilters =
        [
            new AssetTypeFilter(Tr.T("All"), null),
            .. factory.Catalog.Definitions.Where(d => d.Type is not (AssetType.GlobalVariable or AssetType.Tag)).Select(d => new AssetTypeFilter(PluralLabel(d), d.Type)),
        ];
        _typeFilter = TypeFilters[0];
        _typeFilter.IsActive = true;
        SelectTypeFilterCommand = new RelayCommand<AssetTypeFilter>(f => TypeFilter = f);
        ShowListCommand = new RelayCommand(() => IsTileView = false);
        ShowTilesCommand = new RelayCommand(() => IsTileView = true);
        OpenFolderCommand = new RelayCommand<string>(path => CurrentFolder = path ?? string.Empty);
        OpenParentFolderCommand = new RelayCommand(() => CurrentFolder = AssetFolders.Parent(_currentFolder), () => _currentFolder.Length > 0);

        foreach (Asset asset in model.Assets)
        {
            Assets.Add(factory.Create(asset, model, SelectAsset));
        }

        FilteredAssets = new ListCollectionView(Assets) { Filter = MatchesFilter };
        Resources = new ResourcesViewModel(this, factory.Catalog.Definitions.Where(d => ResourcesViewModel.IsResource(d.Type)).Select(d => (d.Type, PluralLabel(d))));
        UpdateFilterCounts();
        RefreshFolders();

        Model.DirtyStateChanged += OnDirtyStateChanged;
        Model.Assets.Changed += OnRegistryChanged;
        Model.History.Changed += OnHistoryChanged;
    }

    public ModProject Model { get; }

    public UndoHistory History => Model.History;

    public string Name => Model.Name;

    public bool IsDirty => Model.IsDirty;

    public string TargetDescription =>
        Tr.F("{0}  |  Minecraft {1}  |  {2}  |  MDK {3}", Model.Settings.ModId, Model.Settings.MinecraftVersion, Model.Settings.LoaderId, Model.Settings.MdkId ?? Tr.T("not chosen"));

    public ObservableCollection<AssetViewModel> Assets { get; } = [];

    /// <summary>Onglet Ressources : tous les éléments, rangés par sorte et triés.</summary>
    public ResourcesViewModel Resources { get; }

    /// <summary>Vue filtrée affichée par le Content Browser.</summary>
    public ICollectionView FilteredAssets { get; }

    public IReadOnlyList<AssetTypeFilter> TypeFilters { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                FilteredAssets.Refresh();
                OnPropertyChanged(nameof(VisibleCount));
                OnPropertyChanged(nameof(IsSearching));
                OnPropertyChanged(nameof(ShowFolders));
                OnPropertyChanged(nameof(IsFolderEmpty));
            }
        }
    }

    public AssetTypeFilter TypeFilter
    {
        get => _typeFilter;
        set
        {
            AssetTypeFilter previous = _typeFilter;
            if (SetProperty(ref _typeFilter, value ?? TypeFilters[0]))
            {
                previous.IsActive = false;
                _typeFilter.IsActive = true;
                FilteredAssets.Refresh();
                OnPropertyChanged(nameof(VisibleCount));
            }
        }
    }

    public ICommand SelectTypeFilterCommand { get; }

    /// <summary>Affichage du Dashboard en vignettes (comme l'espace de travail de MCreator) plutôt qu'en liste.</summary>
    public bool IsTileView
    {
        get => _isTileView;
        set => SetProperty(ref _isTileView, value);
    }

    public ICommand ShowListCommand { get; }

    public ICommand ShowTilesCommand { get; }

    /// <summary>Nombre d'éléments affichés après recherche et filtre.</summary>
    public int VisibleCount => FilteredAssets.Cast<object>().Count();

    public bool IsEmpty => Assets.Count == 0;

    // ----- Dossiers -----

    /// <summary>Dossier ouvert ("" : la racine). Un dossier qui n'existe plus ramène à son parent le plus proche.</summary>
    public string CurrentFolder
    {
        get => _currentFolder;
        set
        {
            string target = AssetFolders.Normalize(value) ?? string.Empty;
            while (!FolderOperations.Exists(Model, target))
            {
                target = AssetFolders.Parent(target);
            }

            if (SetProperty(ref _currentFolder, target))
            {
                SearchText = string.Empty;
                FilteredAssets.Refresh();
                RefreshFolders();
                OnPropertyChanged(nameof(VisibleCount));
                OnPropertyChanged(nameof(IsInFolder));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsInFolder => _currentFolder.Length > 0;

    /// <summary>Sous-dossiers directs du dossier ouvert.</summary>
    public ObservableCollection<FolderItemViewModel> Folders { get; } = [];

    /// <summary>Fil d'Ariane du dossier ouvert, de la racine à lui.</summary>
    public IReadOnlyList<FolderCrumb> Breadcrumb
    {
        get => _breadcrumb;
        private set => SetProperty(ref _breadcrumb, value);
    }

    /// <summary>Destinations de "Déplacer vers" : la racine puis tous les dossiers.</summary>
    public IReadOnlyList<FolderChoice> FolderChoices
    {
        get => _folderChoices;
        private set => SetProperty(ref _folderChoices, value);
    }

    public bool IsSearching => _searchText.Length > 0;

    /// <summary>Les dossiers s'affichent hors recherche (une recherche porte sur tout le projet).</summary>
    public bool ShowFolders => !IsSearching && Folders.Count > 0;

    /// <summary>Le dossier ouvert ne contient rien (ni élément ni sous-dossier).</summary>
    public bool IsFolderEmpty => IsInFolder && !IsSearching && Folders.Count == 0 && VisibleCount == 0;

    /// <summary>Ouvre un dossier (paramètre : son chemin, "" pour la racine).</summary>
    public ICommand OpenFolderCommand { get; }

    public ICommand OpenParentFolderCommand { get; }

    /// <summary>Relit les dossiers (création, renommage, annulation, élément déplacé).</summary>
    public void RefreshFolders()
    {
        if (!FolderOperations.Exists(Model, _currentFolder))
        {
            CurrentFolder = _currentFolder;
            return;
        }

        IReadOnlyList<string> all = FolderOperations.AllFolders(Model);
        List<FolderItemViewModel> folders =
        [
            .. all.Where(f => f.Length > 0 && AssetFolders.Parent(f) == _currentFolder)
                .Select(folder => new FolderItemViewModel(
                    folder,
                    Model.Assets.Count(a => AssetFolders.IsWithin(a.Folder, folder)),
                    all.Count(f => f.Length > 0 && AssetFolders.Parent(f) == folder))),
        ];

        // Reconstruite seulement si elle change : une saisie dans un élément ne fait pas clignoter la liste.
        if (!folders.Select(f => (f.Path, f.AssetCount, f.FolderCount)).SequenceEqual(Folders.Select(f => (f.Path, f.AssetCount, f.FolderCount))))
        {
            Folders.Clear();
            foreach (FolderItemViewModel folder in folders)
            {
                Folders.Add(folder);
            }
        }

        string[] segments = _currentFolder.Length == 0 ? [] : _currentFolder.Split(AssetFolders.Separator);
        Breadcrumb =
        [
            new FolderCrumb(Tr.T("Mod content"), string.Empty, segments.Length == 0),
            .. segments.Select((s, i) => new FolderCrumb(s, string.Join(AssetFolders.Separator, segments[..(i + 1)]), i == segments.Length - 1)),
        ];
        FolderChoices =
        [
            new FolderChoice(string.Empty, Tr.T("Mod content (root)")),
            .. all.Where(f => f.Length > 0).Select(f => new FolderChoice(f, new string(' ', 4 * f.Count(c => c == AssetFolders.Separator)) + AssetFolders.Name(f))),
        ];
        OnPropertyChanged(nameof(ShowFolders));
        OnPropertyChanged(nameof(IsFolderEmpty));
    }

    public AssetViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (SetProperty(ref _selectedAsset, value))
            {
                // Selected from elsewhere (search, Diagnostics...): the multiple selection ends.
                if (value is null || !_selectedAssets.Contains(value))
                {
                    _selectedAssets = [];
                    OnPropertyChanged(nameof(SelectedAssets));
                }

                OnPropertyChanged(nameof(HasSelection));
                Resources?.OnSelectionChanged();
            }
        }
    }

    /// <summary>
    /// Elements selected in the Dashboard (Ctrl+click, Shift+click), in the order of the list;
    /// <see cref="SelectedAsset"/> is the one shown in Properties. Set by the Dashboard.
    /// </summary>
    public IReadOnlyList<AssetViewModel> SelectedAssets
    {
        get => _selectedAssets.Count > 0 ? _selectedAssets : _selectedAsset is { } single ? [single] : [];
        set
        {
            _selectedAssets = [.. value ?? []];
            OnPropertyChanged();
            if (_selectedAsset is null || !_selectedAssets.Contains(_selectedAsset))
            {
                SelectedAsset = _selectedAssets.FirstOrDefault();
            }
        }
    }

    /// <summary>Removes elements in one step that can be undone.</summary>
    public void RemoveAssets(IReadOnlyList<AssetViewModel> viewModels)
    {
        ArgumentNullException.ThrowIfNull(viewModels);
        if (viewModels.Count == 1)
        {
            RemoveAsset(viewModels[0]);
        }
        else if (viewModels.Count > 1)
        {
            History.Execute(new CompositeCommand(
                Tr.F("Delete {0} elements", viewModels.Count),
                [.. viewModels.SelectMany(v => RemovalCommands(v.Model, v.TypeLabel))]));
        }
    }

    /// <summary>Removal of an element, with the graphs of its methods (V0.7.13), which only exist through it.</summary>
    private IEnumerable<IUndoableCommand> RemovalCommands(Asset asset, string typeLabel)
    {
        yield return new RemoveAssetCommand(Model.Assets, asset, typeLabel);
        foreach (GraphAsset graph in Model.Assets.OfType<GraphAsset>().Where(g => g.ElementId == asset.Id).ToList())
        {
            yield return new RemoveAssetCommand(Model.Assets, graph, Tr.T("Graph"));
        }
    }

    public bool HasSelection => _selectedAsset is not null;

    public int ItemCount => Model.Assets.OfType<ItemAsset>().Count();

    public int BlockCount => Model.Assets.OfType<BlockAsset>().Count();

    public int MobCount => Model.Assets.OfType<MobAsset>().Count();

    public int RecipeCount => Model.Assets.OfType<RecipeAsset>().Count();

    /// <summary>Crée un nouvel asset avec un identifiant libre (étape annulable) et le sélectionne.</summary>
    public void AddAsset(AssetType type)
    {
        IAssetDefinition definition = _factory.Catalog.Get(type);
        Asset asset = definition.CreateNew(
            Model.Assets.CreateUniqueResourceId(definition.DefaultResourceIdBase, type), definition.DefaultDisplayName);
        asset.Folder = _currentFolder;
        History.Execute(new AddAssetCommand(Model.Assets, asset, definition.Label));
        SelectAsset(asset.Id);
    }

    /// <summary>
    /// Crée un graphe (étape annulable) : réactions aux événements, fonction, procédure, classe,
    /// structure, interface ou énumération, avec ses nœuds de départ. Avec <paramref name="owner"/>,
    /// la définition est intégrée à cette classe et rangée dans son dossier.
    /// </summary>
    public GraphAsset AddGraph(GraphKind kind, Guid? owner = null)
    {
        GraphAsset? ownerGraph = owner is { } id ? Model.Assets.Find(id) as GraphAsset : null;
        string baseId = ownerGraph is null ? ProgramPalette.ResourceIdBase(kind) : $"{ownerGraph.ResourceId}_{kind.ToString().ToLowerInvariant()}";
        bool inLibrary = ownerGraph?.Kind == GraphKind.Library;
        string name = kind is GraphKind.Function or GraphKind.Procedure && ownerGraph is not null && !inLibrary ? Tr.T("new method") : ProgramPalette.DefaultName(kind);
        GraphAsset graph = ProgramPalette.CreateGraph(kind, Model.Assets.CreateUniqueResourceId(baseId, AssetType.Graph), name, owner);
        if (inLibrary)
        {
            // The functions of a library are usable everywhere, without an object.
            graph.Access = AccessLevel.Public;
            graph.IsStatic = true;
        }

        graph.Folder = ownerGraph?.Folder ?? _currentFolder;
        History.Execute(new AddAssetCommand(Model.Assets, graph, kind.Label()));
        SelectAsset(graph.Id);
        return graph;
    }

    /// <summary>
    /// Importe des fichiers (images ou sons) comme nouveaux assets, en une seule étape
    /// annulable. Retourne les fichiers refusés avec la raison.
    /// </summary>
    public IReadOnlyList<string> ImportFiles(
        IReadOnlyList<string> files, Func<ModProject, string, ISet<ResourceId>, Asset> import, string label)
    {
        var commands = new List<IUndoableCommand>();
        var errors = new List<string>();
        var reservedIds = new HashSet<ResourceId>();
        Asset? last = null;
        foreach (string file in files)
        {
            try
            {
                last = import(Model, file, reservedIds);
                last.Folder = _currentFolder;
                commands.Add(new AddAssetCommand(Model.Assets, last, label));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                errors.Add(Tr.F("{0}: {1}", Path.GetFileName(file), exception.Message));
            }
        }

        if (commands.Count > 0)
        {
            History.Execute(new CompositeCommand(commands.Count == 1 ? commands[0].Description : Tr.F("Import {0} files", commands.Count), commands));
            SelectAsset(last!.Id);
        }

        return errors;
    }

    /// <summary>
    /// Imports 3D models: a Blockbench or Java JSON model goes through its import window (textures,
    /// variants, layers, element created with it), the other formats are added as they are. Everything
    /// is one undoable step; the last element created is selected. Returns the errors.
    /// </summary>
    public IReadOnlyList<string> ImportModels(IReadOnlyList<string> files, AssetFileImporter importer, Services.IDialogService dialogs)
    {
        var commands = new List<IUndoableCommand>();
        var errors = new List<string>();
        var reservedIds = new HashSet<ResourceId>();
        Asset? last = null;
        foreach (string file in files)
        {
            try
            {
                if (ModelFormats.DescribeProblem(file) is { } problem)
                {
                    errors.Add(problem);
                    continue;
                }

                IReadOnlyList<Asset> assets;
                if (ModelFormats.FromPath(file) is ModelFormat.Blockbench or ModelFormat.JavaJson)
                {
                    var import = new ModelImportViewModel(Model, importer, dialogs, file);
                    if (!dialogs.ShowModelImport(import))
                    {
                        continue;
                    }

                    assets = import.CreateAssets(reservedIds, _currentFolder);
                }
                else
                {
                    ModelAsset model = importer.ImportModel(Model, file, reservedIds);
                    model.Folder = _currentFolder;
                    assets = [model];
                }

                commands.AddRange(assets.Select(a => new AddAssetCommand(Model.Assets, a, a.Type.ToString())));
                last = assets[^1];
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                errors.Add(Tr.F("{0}: {1}", Path.GetFileName(file), exception.Message));
            }
        }

        if (commands.Count > 0)
        {
            History.Execute(new CompositeCommand(Tr.T("Import a 3D model"), commands));
            History.Seal();
            SelectAsset(last!.Id);
        }

        return errors;
    }

    /// <summary>
    /// Vrai pour les assets qui peuvent être dupliqués : contenu du mod et graphes. Une texture
    /// ou un son pointe vers un fichier ; sa copie partagerait ce fichier.
    /// </summary>
    public static bool CanDuplicate(AssetViewModel? viewModel) =>
        viewModel is not null && viewModel.Model.Type is not (AssetType.Texture or AssetType.Sound or AssetType.Model);

    /// <summary>
    /// Copie un asset avec un nouvel identifiant (« magic_sword_copy », puis « magic_sword_copy_2 »...)
    /// dans le même dossier (étape annulable), puis la sélectionne.
    /// </summary>
    public Asset DuplicateAsset(AssetViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!CanDuplicate(source))
        {
            throw new InvalidOperationException(Tr.T("Textures and sounds cannot be duplicated: import the file again."));
        }

        // La copie passe par le format de fichier des assets : toutes les propriétés suivent, y compris celles à venir.
        var serializer = new AssetSerializer(_factory.Catalog);
        JsonObject root = JsonFormatting.ParseObject(serializer.Serialize(source.Model), source.Model.ResourceId.Value);
        root["guid"] = Guid.NewGuid().ToString("D");
        root["resourceId"] = Model.Assets.CreateUniqueResourceId(source.Model.ResourceId.Value + "_copy", source.Model.Type).Value;
        root["displayName"] = string.IsNullOrWhiteSpace(source.Model.DisplayName)
            ? string.Empty
            : Tr.F("{0} (copy)", source.Model.DisplayName);
        Asset copy = serializer.Deserialize(root, source.Model.ResourceId.Value);
        History.Execute(new AddAssetCommand(Model.Assets, copy, source.TypeLabel));
        SelectAsset(copy.Id);
        return copy;
    }

    /// <summary>
    /// Fichier à écrire pour exporter un asset : l'image ou le son d'une texture ou d'un son,
    /// sinon son fichier .asset.json (que l'on peut importer dans un autre projet).
    /// </summary>
    public string ExportFileName(AssetViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return viewModel.Model is FileAsset file
            ? Path.GetFileName(file.FilePath)
            : viewModel.Model.ResourceId.Value + AssetSerializer.FileExtension;
    }

    /// <summary>Écrit l'asset dans le fichier choisi (voir <see cref="ExportFileName"/>).</summary>
    public void ExportAsset(AssetViewModel viewModel, string targetFile)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFile);
        if (viewModel.Model is FileAsset file)
        {
            File.Copy(Model.Layout.ToAbsolutePath(file.FilePath), targetFile, overwrite: true);
            return;
        }

        Core.IO.AtomicFile.WriteAllText(targetFile, new AssetSerializer(_factory.Catalog).Serialize(viewModel.Model));
    }

    /// <summary>
    /// Ajoute au dossier ouvert un élément exporté depuis un projet (.asset.json), avec un nouvel
    /// identifiant interne et un identifiant libre (étape annulable). Les liens vers des éléments
    /// absents de ce projet sont signalés par la validation.
    /// </summary>
    public Asset ImportAssetFile(string sourceFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        var serializer = new AssetSerializer(_factory.Catalog);
        string name = Path.GetFileName(sourceFile);
        JsonObject root = JsonFormatting.ParseObject(File.ReadAllText(sourceFile), name);
        Asset read = serializer.Deserialize(root, name);
        if (read is FileAsset)
        {
            throw new InvalidDataException(Tr.T("A texture or a sound is imported directly from its PNG or OGG file."));
        }

        root["guid"] = Guid.NewGuid().ToString("D");
        root["resourceId"] = Model.Assets.CreateUniqueResourceId(read.ResourceId.Value, read.Type).Value;
        Asset asset = serializer.Deserialize(root, name);
        asset.Folder = _currentFolder;
        History.Execute(new AddAssetCommand(Model.Assets, asset, _factory.Catalog.Get(asset.Type).Label));
        SelectAsset(asset.Id);
        return asset;
    }

    /// <summary>Supprime un asset (étape annulable).</summary>
    public void RemoveAsset(AssetViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        List<IUndoableCommand> commands = [.. RemovalCommands(viewModel.Model, viewModel.TypeLabel)];
        History.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(Tr.F("Delete {0}", viewModel.Header), commands));
    }

    /// <summary>Sélectionne un asset par son identifiant interne ; faux s'il n'existe plus.</summary>
    public bool SelectAsset(Guid id)
    {
        AssetViewModel? target = Assets.FirstOrDefault(a => a.Model.Id == id);
        if (target is null)
        {
            return false;
        }

        if (!FilteredAssets.Contains(target))
        {
            // L'élément est ailleurs : on ouvre son dossier, sans recherche ni filtre.
            TypeFilter = TypeFilters[0];
            CurrentFolder = target.Model.Folder;
            SearchText = string.Empty;
        }

        SelectedAsset = target;
        return true;
    }

    /// <summary>À appeler après une modification des paramètres du projet.</summary>
    public void RefreshSettings()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TargetDescription));
    }

    public void Dispose()
    {
        Model.DirtyStateChanged -= OnDirtyStateChanged;
        Model.Assets.Changed -= OnRegistryChanged;
        Model.History.Changed -= OnHistoryChanged;
        foreach (AssetViewModel asset in Assets)
        {
            asset.Dispose();
        }
    }

    private bool MatchesFilter(object item)
    {
        if (item is not AssetViewModel asset)
        {
            return false;
        }

        if (_typeFilter.Type is { } type && asset.Model.Type != type)
        {
            return false;
        }

        // Global variables and tags have their own tabs (Global, Tags); the graph of a method of an element is opened from it (V0.7.13).
        if (asset.Model.Type is AssetType.GlobalVariable or AssetType.Tag || asset.Model is GraphAsset { ElementId: not null })
        {
            return false;
        }

        if (_searchText.Length == 0)
        {
            return asset.Model.Folder == _currentFolder;
        }

        return asset.Header.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase)
               || asset.Model.ResourceId.Value.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void OnRegistryChanged(object? sender, AssetRegistryChangedEventArgs e)
    {
        switch (e.Change)
        {
            case AssetRegistryChange.Added:
                int index = Math.Clamp(Model.Assets.IndexOf(e.Asset), 0, Assets.Count);
                Assets.Insert(index, _factory.Create(e.Asset, Model, SelectAsset));
                break;

            case AssetRegistryChange.Removed:
                AssetViewModel? removed = Assets.FirstOrDefault(a => ReferenceEquals(a.Model, e.Asset));
                if (removed is not null)
                {
                    int position = Assets.IndexOf(removed);
                    Assets.RemoveAt(position);
                    removed.Dispose();
                    if (ReferenceEquals(SelectedAsset, removed))
                    {
                        SelectedAsset = Assets.Count == 0 ? null : Assets[Math.Min(position, Assets.Count - 1)];
                    }
                }

                break;

            case AssetRegistryChange.Modified:
                FilteredAssets.Refresh();
                break;
        }

        // Les autres assets peuvent afficher celui-ci (listes de textures, d'items, "Utilisé par").
        foreach (AssetViewModel asset in Assets)
        {
            asset.OnProjectContentChanged();
        }

        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(BlockCount));
        OnPropertyChanged(nameof(MobCount));
        OnPropertyChanged(nameof(RecipeCount));
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(IsEmpty));
        UpdateFilterCounts();
        RefreshFolders();
    }

    private void UpdateFilterCounts()
    {
        foreach (AssetTypeFilter filter in TypeFilters)
        {
            filter.Count = filter.Type is { } type ? Model.Assets.Count(a => a.Type == type) : Model.Assets.Count;
        }
    }

    private static string PluralLabel(IAssetDefinition definition) => definition.Type switch
    {
        AssetType.Block => Tr.T("Blocks"),
        AssetType.Item => Tr.T("Items"),
        AssetType.Mob => Tr.T("Mobs"),
        AssetType.Recipe => Tr.T("Recipes"),
        AssetType.Texture => Tr.T("Textures"),
        AssetType.Sound => Tr.T("Sounds"),
        AssetType.Graph => Tr.T("Graphs"),
        AssetType.BehaviorTree => Tr.T("Behavior trees"),
        AssetType.Model => Tr.T("3D models"),
        AssetType.Command => Tr.T("Commands"),
        AssetType.CreativeTab => Tr.T("Creative tabs"),
        AssetType.Keybind => Tr.T("Keybinds"),
        AssetType.Enchantment => Tr.T("Enchantments"),
        AssetType.Fluid => Tr.T("Fluids"),
        AssetType.GameRule => Tr.T("Game rules"),
        AssetType.DamageType => Tr.T("Damage types"),
        AssetType.LootTable => Tr.T("Drop tables"),
        AssetType.Effect => Tr.T("Effects"),
        AssetType.GlobalVariable => Tr.T("Global variables"),
        AssetType.Tag => Tr.T("Tags"),
        AssetType.Plant => Tr.T("Plants"),
        AssetType.Biome => Tr.T("Biomes"),
        AssetType.Sparkle => Tr.T("Particle systems"),
        _ => definition.Label,
    };

    /// <summary>Les dossiers déclarés font partie des paramètres : une annulation peut les changer.</summary>
    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        RefreshSettings();
        RefreshFolders();
    }

    private void OnDirtyStateChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(IsDirty));
}
