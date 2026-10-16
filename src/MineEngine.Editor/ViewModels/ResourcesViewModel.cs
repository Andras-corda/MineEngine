using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
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

/// <summary>
/// Onglet Ressources : tous les éléments du projet, quel que soit leur dossier, rangés par
/// sorte (Items, Blocs, Textures...) et triés au choix. Il complète le Dashboard, qui suit
/// les dossiers ; la sélection est partagée avec lui et avec le panneau Détails.
/// </summary>
public sealed class ResourcesViewModel : ObservableObject
{
    private readonly ProjectViewModel _project;
    private readonly ListCollectionView _view;
    private AssetTypeFilter _category;
    private ResourceSort _sort = ResourceSort.Name;
    private bool _descending;
    private string _searchText = string.Empty;

    public ResourcesViewModel(ProjectViewModel project, IEnumerable<(AssetType Type, string Label)> types)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        Categories = [new AssetTypeFilter(Tr.T("Tout"), null), .. types.Select(t => new AssetTypeFilter(t.Label, t.Type))];
        _category = Categories[0];
        _category.IsActive = true;

        _view = new ListCollectionView(project.Assets) { Filter = Matches, CustomSort = new Comparer(this) };
        project.Assets.CollectionChanged += OnAssetsChanged;
        foreach (AssetViewModel asset in project.Assets)
        {
            asset.PropertyChanged += OnAssetChanged;
        }

        SortChoices =
        [
            new(ResourceSort.Name, Tr.T("Nom")),
            new(ResourceSort.Identifier, Tr.T("Identifiant")),
            new(ResourceSort.Type, Tr.T("Sorte")),
            new(ResourceSort.Folder, Tr.T("Dossier")),
            new(ResourceSort.Status, Tr.T("État (problèmes d'abord)")),
            new(ResourceSort.Usage, Tr.T("Utilisations")),
        ];
        SelectCategoryCommand = new RelayCommand<AssetTypeFilter>(c => Category = c);
        ToggleOrderCommand = new RelayCommand(() => Descending = !Descending);
        UpdateCounts();
    }

    public IReadOnlyList<AssetTypeFilter> Categories { get; }

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
        if (item is not AssetViewModel asset || (_category.Type is { } type && asset.Model.Type != type))
        {
            return false;
        }

        return _searchText.Length == 0
            || asset.Header.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase)
            || asset.Model.ResourceId.Value.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

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
            category.Count = category.Type is { } type ? _project.Assets.Count(a => a.Model.Type == type) : _project.Assets.Count;
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
        if (e.PropertyName is nameof(AssetViewModel.Header) or nameof(AssetViewModel.DisplayName) or nameof(AssetViewModel.ResourceIdText)
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
