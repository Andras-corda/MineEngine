using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.ViewModels;

/// <summary>A tag in the Tags tab; each change is one undoable step on the asset.</summary>
public sealed class TagRowViewModel : ObservableObject
{
    private readonly TagsViewModel _owner;
    private string? _suggestion;

    public TagRowViewModel(TagsViewModel owner, TagAsset asset)
    {
        _owner = owner;
        Asset = asset;
        RemoveCommand = new RelayCommand(() => _owner.Remove(this), () => _owner.CanEdit);
        AddSuggestionCommand = new RelayCommand(AddSuggestion, () => !string.IsNullOrEmpty(_suggestion));
    }

    public TagAsset Asset { get; }

    public ICommand RemoveCommand { get; }

    public ICommand AddSuggestionCommand { get; }

    /// <summary>"#mymod:ores/ruby".</summary>
    public string Reference => Asset.Reference(_owner.ModId);

    public string RegistryLabel => TagAsset.RegistryLabel(Asset.Registry);

    /// <summary>"3 entries".</summary>
    public string Summary => Asset.Values.Count == 1 ? Tr.T("1 entry") : Tr.F("{0} entries", Asset.Values.Count);

    public Choice<TagRegistry> Registry
    {
        get => TagsViewModel.Registries.First(r => r.Value == Asset.Registry);
        set
        {
            if (value is not null && value.Value != Asset.Registry)
            {
                _owner.Change(Tr.T("Change the kind of a tag"), Asset, nameof(TagAsset.Registry), () => Asset.Registry, v => Asset.Registry = v, value.Value);
            }
        }
    }

    /// <summary>Namespace shown in the list: the id of the mod for its own tags.</summary>
    public string Namespace
    {
        get => Asset.Namespace.Length == 0 ? _owner.ModId : Asset.Namespace;
        set
        {
            string text = (value ?? string.Empty).Trim().ToLowerInvariant();
            string stored = text == _owner.ModId ? TagAsset.ModNamespace : text;
            if (stored != Asset.Namespace)
            {
                _owner.Change(Tr.T("Change the namespace of a tag"), Asset, nameof(TagAsset.Namespace), () => Asset.Namespace, v => Asset.Namespace = v, stored);
            }
        }
    }

    public string TagPath
    {
        get => Asset.TagPath;
        set
        {
            string text = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (text.Length > 0 && text != Asset.TagPath)
            {
                _owner.Change(Tr.T("Rename a tag"), Asset, nameof(TagAsset.TagPath), () => Asset.TagPath, v => Asset.TagPath = v, text);
            }
        }
    }

    public bool Replace
    {
        get => Asset.Replace;
        set => _owner.Change(Tr.T("Change \"replace\" of a tag"), Asset, nameof(TagAsset.Replace), () => Asset.Replace, v => Asset.Replace = v, value);
    }

    /// <summary>Entries, one per line.</summary>
    public string Values
    {
        get => string.Join(Environment.NewLine, Asset.Values);
        set
        {
            IReadOnlyList<string> values = [.. (value ?? string.Empty).Split(['\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            if (!values.SequenceEqual(Asset.Values))
            {
                _owner.Change(Tr.T("Change the entries of a tag"), Asset, nameof(TagAsset.Values), () => Asset.Values, v => Asset.Values = v, values);
            }
        }
    }

    /// <summary>Elements of the project (and other tags) that fit the tag.</summary>
    public IReadOnlyList<string> Suggestions => _owner.SuggestionsFor(this);

    public string? Suggestion
    {
        get => _suggestion;
        set
        {
            SetProperty(ref _suggestion, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void AddSuggestion()
    {
        if (_suggestion is { Length: > 0 } entry && !Asset.Values.Contains(entry))
        {
            _owner.Change(Tr.T("Change the entries of a tag"), Asset, nameof(TagAsset.Values), () => Asset.Values, v => Asset.Values = v, [.. Asset.Values, entry]);
        }

        Suggestion = null;
    }

    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>
/// Tags tab (V0.7.1, like the Elements Tag of MCreator): the tags of the mod, with the kind of
/// what they group (tags.md), their namespace (the mod, minecraft, c, forge, neoforge...), their
/// name and their entries. The search of the toolbar filters the list.
/// </summary>
public sealed class TagsViewModel : ObservableObject, IDisposable
{
    private readonly ProjectViewModel _project;
    private readonly Func<bool> _canEdit;
    private readonly Dictionary<Guid, TagRowViewModel> _rows = [];
    private string _searchText = string.Empty;
    private TagRowViewModel? _selectedRow;

    public TagsViewModel(ProjectViewModel project, Func<bool> canEdit)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
        AddCommand = new RelayCommand(Add, () => CanEdit);
        _project.Model.Assets.Changed += OnAssetsChanged;
        _project.History.Changed += OnHistoryChanged;
        Refresh();
    }

    public static IReadOnlyList<Choice<TagRegistry>> Registries { get; } =
        [.. Enum.GetValues<TagRegistry>().Select(r => new Choice<TagRegistry>(r, $"{TagAsset.RegistryLabel(r)} · {TagAsset.RegistryDescription(r)}"))];

    public IReadOnlyList<string> Namespaces => [.. TagAsset.CommonNamespaces.Select(n => n.Length == 0 ? ModId : n)];

    public string ModId => _project.Model.Settings.ModId.Value;

    public ObservableCollection<TagRowViewModel> Rows { get; } = [];

    public ICommand AddCommand { get; }

    public bool CanEdit => _canEdit();

    public TagRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => _selectedRow is not null;

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

    public bool IsEmpty => _rows.Count == 0;

    public string Summary => _rows.Count == 1 ? Tr.T("1 tag") : Tr.F("{0} tags", _rows.Count);

    /// <summary>A new tag of items "new_tag", selected.</summary>
    public void Add()
    {
        AssetRegistry assets = _project.Model.Assets;
        ResourceId id = assets.CreateUniqueResourceId("new_tag", AssetType.Tag);
        var tag = new TagAsset(Guid.NewGuid(), id, id.Value);
        _project.History.Execute(new AddAssetCommand(assets, tag, Tr.T("Tag")));
        SelectedRow = _rows.GetValueOrDefault(tag.Id);
    }

    public void Remove(TagRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _project.History.Execute(new RemoveAssetCommand(_project.Model.Assets, row.Asset, Tr.T("Tag")));
    }

    internal void Change<T>(string description, TagAsset asset, string property, Func<T> getter, Action<T> setter, T value)
    {
        if (CanEdit && !EqualityComparer<T>.Default.Equals(getter(), value))
        {
            _project.History.Execute(new PropertyChangeCommand<T>(description, asset, property, getter, setter, value));
        }
    }

    /// <summary>Ids of the elements of the project that fit the registry of the tag, then the other tags of the same kind.</summary>
    internal IReadOnlyList<string> SuggestionsFor(TagRowViewModel row)
    {
        AssetRegistry assets = _project.Model.Assets;
        IEnumerable<Asset> elements = row.Asset.Registry switch
        {
            TagRegistry.Blocks => assets.Where(a => a is BlockAsset or PlantAsset or FluidAsset),
            TagRegistry.Items => assets.Where(a => a is ItemAsset or PlantAsset || a is BlockAsset { HasItemForm: true }),
            TagRegistry.EntityTypes => assets.Where(a => a is MobAsset),
            TagRegistry.Fluids => assets.Where(a => a is FluidAsset),
            TagRegistry.Biomes => assets.Where(a => a is BiomeAsset),
            TagRegistry.Enchantments => assets.Where(a => a is EnchantmentAsset),
            _ => [],
        };
        return
        [
            .. elements.Select(a => $"{ModId}:{a.ResourceId}").Order(StringComparer.Ordinal),
            .. assets.OfType<TagAsset>().Where(t => t.Registry == row.Asset.Registry && t.Id != row.Asset.Id).Select(t => t.Reference(ModId)).Order(StringComparer.Ordinal),
        ];
    }

    public void Dispose()
    {
        _project.Model.Assets.Changed -= OnAssetsChanged;
        _project.History.Changed -= OnHistoryChanged;
    }

    private void OnAssetsChanged(object? sender, AssetRegistryChangedEventArgs e)
    {
        if (e.Asset is TagAsset)
        {
            Refresh();
        }
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        foreach (TagRowViewModel row in _rows.Values)
        {
            row.Refresh();
        }

        Refresh();
    }

    private void Refresh()
    {
        var assets = _project.Model.Assets.OfType<TagAsset>().ToList();
        foreach (Guid removed in _rows.Keys.Except(assets.Select(a => a.Id)).ToList())
        {
            _rows.Remove(removed);
        }

        foreach (TagAsset asset in assets.Where(a => !_rows.ContainsKey(a.Id)))
        {
            _rows[asset.Id] = new TagRowViewModel(this, asset);
        }

        string search = _searchText.Trim();
        List<TagRowViewModel> shown =
        [
            .. _rows.Values
                .Where(r => search.Length == 0
                            || r.Reference.Contains(search, StringComparison.OrdinalIgnoreCase)
                            || r.RegistryLabel.Contains(search, StringComparison.OrdinalIgnoreCase)
                            || r.Asset.Values.Any(v => v.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(r => r.Asset.Registry).ThenBy(r => r.Reference, StringComparer.Ordinal),
        ];
        if (!shown.SequenceEqual(Rows))
        {
            TagRowViewModel? selected = _selectedRow;
            Rows.Clear();
            foreach (TagRowViewModel row in shown)
            {
                Rows.Add(row);
            }

            SelectedRow = selected is not null && Rows.Contains(selected) ? selected : null;
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Summary));
    }
}
