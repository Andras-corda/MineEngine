using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.ViewModels;

/// <summary>Ordre de la liste de l'onglet Ressources.</summary>
public enum ResourceSort
{
    Name,
    Identifier,
    Type,
    Folder,
    Status,
    Usage,
}

/// <summary>A sub-category of the Resources tab (V0.7.14): what the files are for, or all of them.</summary>
public sealed class ResourceGroupFilter(string label, ResourceGroup? group) : ObservableObject
{
    private int _count;
    private bool _isActive;

    public string Label { get; } = label;

    /// <summary>The sub-category, or null for all.</summary>
    public ResourceGroup? Group { get; } = group;

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public static string LabelOf(ResourceGroup group) => group switch
    {
        ResourceGroup.Items => Tr.T("Items"),
        ResourceGroup.Blocks => Tr.T("Blocks"),
        ResourceGroup.Mobs => Tr.T("Mobs"),
        ResourceGroup.Interface => Tr.T("Interface (GUI)"),
        ResourceGroup.Particles => Tr.T("Particles"),
        ResourceGroup.World => Tr.T("World"),
        ResourceGroup.Graphs => Tr.T("Graphs"),
        ResourceGroup.Other => Tr.T("Other"),
        _ => Tr.T("Unused"),
    };
}

/// <summary>
/// Resources tab: the asset files of the project (textures, sounds, 3D models), whatever their folder,
/// grouped by kind and sorted at will. Game content (items, blocks, mobs, graphs...) stays in
/// the Dashboard. The selection is shared with the Dashboard and the Details panel.
/// </summary>
public sealed class ResourcesViewModel : ObservableObject
{
    private readonly ProjectViewModel _project;
    private readonly ListCollectionView _view;
    private AssetTypeFilter _category;
    private ResourceSort _sort = ResourceSort.Name;
    private bool _descending;
    private string _searchText = string.Empty;
    private readonly List<ResourceGroupFilter> _allGroups;
    private ResourceGroupFilter _group;
    private bool _isTileView;
    private bool _updatePending;

    public ResourcesViewModel(ProjectViewModel project, IEnumerable<(AssetType Type, string Label)> types)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        Categories = [new AssetTypeFilter(Tr.T("All"), null), .. types.Select(t => new AssetTypeFilter(t.Label, t.Type))];
        _category = Categories[0];
        _category.IsActive = true;
        _allGroups = [new ResourceGroupFilter(Tr.T("All"), null), .. Enum.GetValues<ResourceGroup>().Select(g => new ResourceGroupFilter(ResourceGroupFilter.LabelOf(g), g))];
        _group = _allGroups[0];
        _group.IsActive = true;

        _view = new ListCollectionView(project.Assets) { Filter = Matches, CustomSort = new Comparer(this) };
        project.Assets.CollectionChanged += OnAssetsChanged;
        foreach (AssetViewModel asset in project.Assets)
        {
            asset.PropertyChanged += OnAssetChanged;
        }

        SortChoices =
        [
            new(ResourceSort.Name, Tr.T("Name")),
            new(ResourceSort.Identifier, Tr.T("Identifier")),
            new(ResourceSort.Type, Tr.T("Kind")),
            new(ResourceSort.Folder, Tr.T("Folder")),
            new(ResourceSort.Status, Tr.T("Status (problems first)")),
            new(ResourceSort.Usage, Tr.T("Uses")),
        ];
        SelectCategoryCommand = new RelayCommand<AssetTypeFilter>(c => Category = c);
        SelectGroupCommand = new RelayCommand<ResourceGroupFilter>(g => Group = g);
        ShowListCommand = new RelayCommand(() => IsTileView = false);
        ShowTilesCommand = new RelayCommand(() => IsTileView = true);
        ToggleOrderCommand = new RelayCommand(() => Descending = !Descending);
        UpdateCounts();
    }

    /// <summary>The tabs: all the resources, then one per kind (textures, 3D models, sounds).</summary>
    public IReadOnlyList<AssetTypeFilter> Categories { get; }

    /// <summary>Sub-categories of the open tab that hold something ("All" first).</summary>
    public ObservableCollection<ResourceGroupFilter> Groups { get; } = [];

    public ICommand SelectGroupCommand { get; }

    public ICommand ShowListCommand { get; }

    public ICommand ShowTilesCommand { get; }

    /// <summary>Tiles (a grid of previews) rather than the list with columns.</summary>
    public bool IsTileView
    {
        get => _isTileView;
        set => SetProperty(ref _isTileView, value);
    }

    public ResourceGroupFilter Group
    {
        get => _group;
        set
        {
            if (value is null || ReferenceEquals(value, _group))
            {
                return;
            }

            _group.IsActive = false;
            _group = value;
            _group.IsActive = true;
            OnPropertyChanged();
            Refresh();
        }
    }

    /// <summary>True for the kinds listed by the Resources tab: imported files, not game content.</summary>
    public static bool IsResource(AssetType type) => type is AssetType.Texture or AssetType.Sound or AssetType.Model;

    public IReadOnlyList<Choice<ResourceSort>> SortChoices { get; }

    /// <summary>Éléments de la sorte choisie, triés ; tous les dossiers sont parcourus.</summary>
    public ICollectionView Items => _view;

    public ICommand SelectCategoryCommand { get; }

    public ICommand ToggleOrderCommand { get; }

    public AssetTypeFilter Category
    {
        get => _category;
        set
        {
            if (value is null || ReferenceEquals(value, _category))
            {
                return;
            }

            _category.IsActive = false;
            _category = value;
            _category.IsActive = true;
            OnPropertyChanged();
            UpdateCounts();
            Refresh();
        }
    }

    public Choice<ResourceSort> SelectedSort
    {
        get => SortChoices.First(c => c.Value == _sort);
        set
        {
            if (value is not null && value.Value != _sort)
            {
                _sort = value.Value;
                OnPropertyChanged();
                Refresh();
            }
        }
    }

    /// <summary>Ordre décroissant (Z à A, plus utilisés d'abord...).</summary>
    public bool Descending
    {
        get => _descending;
        set
        {
            if (SetProperty(ref _descending, value))
            {
                Refresh();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                Refresh();
            }
        }
    }

    /// <summary>Élément sélectionné : le même que dans le Dashboard et le panneau Détails.</summary>
    public AssetViewModel? SelectedAsset
    {
        get => _project.SelectedAsset;
        set
        {
            if (value is not null)
            {
                _project.SelectAsset(value.Model.Id);
            }
        }
    }

    public int VisibleCount => _view.Count;

    public bool IsEmpty => _view.IsEmpty;

    /// <summary>La sélection du projet a changé (depuis le Dashboard ou l'Explorateur).</summary>
    internal void OnSelectionChanged() => OnPropertyChanged(nameof(SelectedAsset));

    private bool Matches(object item)
    {
        if (item is not AssetViewModel asset || !InCategory(asset)
            || (_group.Group is { } group && !ResourceGroups.Of(asset.Model, _project.Model.Assets).Contains(group)))
        {
            return false;
        }

        return _searchText.Length == 0
            || asset.Header.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase)
            || asset.Model.ResourceId.Value.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private bool InCategory(AssetViewModel asset) =>
        IsResource(asset.Model.Type) && (_category.Type is not { } type || asset.Model.Type == type);

    private void Refresh()
    {
        _view.Refresh();
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void UpdateCounts()
    {
        foreach (AssetTypeFilter category in Categories)
        {
            category.Count = category.Type is { } type
                ? _project.Assets.Count(a => a.Model.Type == type)
                : _project.Assets.Count(a => IsResource(a.Model.Type));
        }

        // The sub-categories of the open tab: only those that hold something, the chosen one kept.
        List<IReadOnlySet<ResourceGroup>> groups = [.. _project.Assets.Where(InCategory).Select(a => ResourceGroups.Of(a.Model, _project.Model.Assets))];
        foreach (ResourceGroupFilter filter in _allGroups)
        {
            filter.Count = filter.Group is { } group ? groups.Count(g => g.Contains(group)) : groups.Count;
        }

        List<ResourceGroupFilter> visible = [.. _allGroups.Where(f => f.Group is null || f.Count > 0 || ReferenceEquals(f, _group))];
        if (!visible.SequenceEqual(Groups))
        {
            Groups.Clear();
            foreach (ResourceGroupFilter filter in visible)
            {
                Groups.Add(filter);
            }
        }
    }

    private void OnAssetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (AssetViewModel asset in e.OldItems?.OfType<AssetViewModel>() ?? [])
        {
            asset.PropertyChanged -= OnAssetChanged;
        }

        foreach (AssetViewModel asset in e.NewItems?.OfType<AssetViewModel>() ?? [])
        {
            asset.PropertyChanged += OnAssetChanged;
        }

        UpdateCounts();
        Refresh();
    }

    /// <summary>Un nom, un identifiant ou un état change : l'ordre peut changer.</summary>
    private void OnAssetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AssetViewModel.UsageCount))
        {
            // Every element says it when the project changes: the sub-categories are counted once after.
            if (!_updatePending)
            {
                _updatePending = true;
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
                {
                    _updatePending = false;
                    UpdateCounts();
                    Refresh();
                });
            }
        }
        else if (e.PropertyName is nameof(AssetViewModel.Header) or nameof(AssetViewModel.DisplayName) or nameof(AssetViewModel.ResourceIdText)
            or nameof(AssetViewModel.IssueLabel) or nameof(AssetViewModel.FolderLabel))
        {
            Refresh();
        }
    }

    private int Compare(AssetViewModel a, AssetViewModel b)
    {
        int result = _sort switch
        {
            ResourceSort.Identifier => string.Compare(a.Model.ResourceId.Value, b.Model.ResourceId.Value, StringComparison.Ordinal),
            ResourceSort.Type => string.Compare(a.TypeLabel, b.TypeLabel, StringComparison.CurrentCultureIgnoreCase),
            ResourceSort.Folder => string.Compare(a.Model.Folder, b.Model.Folder, StringComparison.CurrentCultureIgnoreCase),
            ResourceSort.Status => b.IssueRank.CompareTo(a.IssueRank),
            ResourceSort.Usage => b.UsageCount.CompareTo(a.UsageCount),
            _ => 0,
        };

        // À égalité, l'ordre des noms départage, toujours de A à Z.
        if (result == 0 && _sort != ResourceSort.Name)
        {
            return string.Compare(a.Header, b.Header, StringComparison.CurrentCultureIgnoreCase);
        }

        result = result != 0 ? result : string.Compare(a.Header, b.Header, StringComparison.CurrentCultureIgnoreCase);
        return _descending ? -result : result;
    }

    private sealed class Comparer(ResourcesViewModel owner) : IComparer
    {
        public int Compare(object? x, object? y) =>
            x is AssetViewModel a && y is AssetViewModel b ? owner.Compare(a, b) : 0;
    }
}
