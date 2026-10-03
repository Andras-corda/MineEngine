using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Assets.Definitions;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Project;

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
/// Projet ouvert dans l'éditeur : contenu, sélection courante et filtre du Content
/// Browser. La liste suit le registre d'assets, y compris lors d'une annulation.
/// </summary>
public sealed class ProjectViewModel : ObservableObject, IDisposable
{
    private readonly AssetViewModelFactory _factory;
    private AssetViewModel? _selectedAsset;
    private string _searchText = string.Empty;
    private AssetTypeFilter _typeFilter;
    private bool _isTileView;

    public ProjectViewModel(ModProject model, AssetViewModelFactory factory)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        TypeFilters =
        [
            new AssetTypeFilter("Tout", null),
            .. factory.Catalog.Definitions.Select(d => new AssetTypeFilter(PluralLabel(d.Label), d.Type)),
        ];
        _typeFilter = TypeFilters[0];
        _typeFilter.IsActive = true;
        SelectTypeFilterCommand = new RelayCommand<AssetTypeFilter>(f => TypeFilter = f);
        ShowListCommand = new RelayCommand(() => IsTileView = false);
        ShowTilesCommand = new RelayCommand(() => IsTileView = true);

        foreach (Asset asset in model.Assets)
        {
            Assets.Add(factory.Create(asset, model, SelectAsset));
        }

        FilteredAssets = new ListCollectionView(Assets) { Filter = MatchesFilter };
        UpdateFilterCounts();

        Model.DirtyStateChanged += OnDirtyStateChanged;
        Model.Assets.Changed += OnRegistryChanged;
        Model.History.Changed += OnHistoryChanged;
    }

    public ModProject Model { get; }

    public UndoHistory History => Model.History;

    public string Name => Model.Name;

    public bool IsDirty => Model.IsDirty;

    public string TargetDescription =>
        $"{Model.Settings.ModId}  |  Minecraft {Model.Settings.MinecraftVersion}  |  {Model.Settings.LoaderId}  |  MDK {Model.Settings.MdkId ?? "non choisi"}";

    public ObservableCollection<AssetViewModel> Assets { get; } = [];

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

    public AssetViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (SetProperty(ref _selectedAsset, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
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
        History.Execute(new AddAssetCommand(Model.Assets, asset, definition.Label));
        SelectAsset(asset.Id);
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
                commands.Add(new AddAssetCommand(Model.Assets, last, label));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                errors.Add($"{Path.GetFileName(file)} : {exception.Message}");
            }
        }

        if (commands.Count > 0)
        {
            History.Execute(new CompositeCommand(commands.Count == 1 ? commands[0].Description : $"Importer {commands.Count} fichiers", commands));
            SelectAsset(last!.Id);
        }

        return errors;
    }

    /// <summary>Supprime un asset (étape annulable).</summary>
    public void RemoveAsset(AssetViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        History.Execute(new RemoveAssetCommand(Model.Assets, viewModel.Model, viewModel.TypeLabel));
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
            SearchText = string.Empty;
            TypeFilter = TypeFilters[0];
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

        return _searchText.Length == 0
               || asset.Header.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase)
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
    }

    private void UpdateFilterCounts()
    {
        foreach (AssetTypeFilter filter in TypeFilters)
        {
            filter.Count = filter.Type is { } type ? Model.Assets.Count(a => a.Type == type) : Model.Assets.Count;
        }
    }

    private static string PluralLabel(string label) => label switch
    {
        "Bloc" => "Blocs",
        "Item" => "Items",
        "Mob" => "Mobs",
        "Recette" => "Recettes",
        "Texture" => "Textures",
        "Son" => "Sons",
        _ => label,
    };

    private void OnHistoryChanged(object? sender, EventArgs e) => RefreshSettings();

    private void OnDirtyStateChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(IsDirty));
}
