using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>An item of a creative tab, with its place in the list: number, name, identifier, kind and image.</summary>
public sealed class CreativeTabEntryViewModel : ObservableObject, IReorderableRow
{
    private string? _iconPath;

    public CreativeTabEntryViewModel(int index, ContentReference reference, ReferencePickerViewModel picker, AssetEditingContext context, ICommand remove, ICommand moveUp, ICommand moveDown)
    {
        Index = index;
        Picker = picker;
        RemoveCommand = remove;
        MoveUpCommand = moveUp;
        MoveDownCommand = moveDown;
        Asset? asset = reference.Kind == ContentReferenceKind.Asset ? context.Project.Assets.Find(reference.AssetId) : null;
        Name = asset?.DisplayName ?? (reference.Id is { } id ? Pretty(id.Path) : Tr.T("(deleted asset)"));
        IdText = asset?.ResourceId.Value ?? reference.ToStorageText();
        TypeLabel = asset switch
        {
            BlockAsset => Tr.T("Block"),
            ItemAsset => Tr.T("Item"),
            null when reference.Id is { Namespace: "minecraft" } => "Minecraft",
            null => Tr.T("Other mod"),
            _ => asset.Type.ToString(),
        };
        _iconPath = ItemIcons.ProjectFile(context.Project, reference);
        if (_iconPath is null && reference.Kind == ContentReferenceKind.Id)
        {
            _ = LoadIconAsync(context, reference);
        }
    }

    /// <summary>Place in the tab, from 0.</summary>
    public int Index { get; }

    /// <summary>Place shown in the list, from 1.</summary>
    public int Order => Index + 1;

    public string Name { get; }

    /// <summary>"ruby" for an element of the mod, "minecraft:diamond" for another one.</summary>
    public string IdText { get; }

    public string TypeLabel { get; }

    public string? IconPath
    {
        get => _iconPath;
        private set
        {
            if (SetProperty(ref _iconPath, value))
            {
                OnPropertyChanged(nameof(ShowsPlaceholder));
            }
        }
    }

    /// <summary>True without image: a question mark shows that the slot holds an item.</summary>
    public bool ShowsPlaceholder => _iconPath is null;

    public ReferencePickerViewModel Picker { get; }

    public ICommand RemoveCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    /// <summary>"diamond_sword" gives "Diamond sword".</summary>
    private static string Pretty(string path)
    {
        string text = path[(path.LastIndexOf('/') + 1)..].Replace('_', ' ');
        return text.Length == 0 ? path : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
    }

    private async Task LoadIconAsync(AssetEditingContext context, ContentReference reference) =>
        IconPath = await ItemIcons.FindAsync(context.Project, reference);
}

/// <summary>
/// Properties of a creative tab (V0.6.2): its icon, its search bar and its items, in order. The list
/// is reordered with the arrows or by dragging a row; the preview shows the tab as in the game.
/// </summary>
public sealed class CreativeTabAssetViewModel : AssetViewModel, IReorderableList
{
    private readonly CreativeTabAsset _tab;
    private string? _tabIconPath;

    public CreativeTabAssetViewModel(CreativeTabAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _tab = model;
        Icon = new ReferencePickerViewModel(context, ReferenceDomain.Item, () => _tab.Icon,
            v => Change(Tr.T("Change the icon of the tab"), nameof(CreativeTabAsset.Icon), () => _tab.Icon, r => _tab.Icon = r, v));
        AddItemCommand = new RelayCommand(() => SetItems(Tr.T("Add an item to the tab"), [.. _tab.Items, ContentReference.ToId(NamespacedId.TryParse("minecraft:stone", out NamespacedId? stone) ? stone! : throw new InvalidOperationException())]));
        AddAllModItemsCommand = new RelayCommand(AddAllModItems);
        Rebuild();
        RefreshTabIcon();
    }

    public ReferencePickerViewModel Icon { get; }

    /// <summary>The list and the preview side by side.</summary>
    public override double EditorMaxWidth => 1180;

    public bool HasSearchBar
    {
        get => _tab.HasSearchBar;
        set => Change(value ? Tr.T("Add a search bar to the tab") : Tr.T("Remove the search bar of the tab"),
            nameof(CreativeTabAsset.HasSearchBar), () => _tab.HasSearchBar, v => _tab.HasSearchBar = v, value);
    }

    public ObservableCollection<CreativeTabEntryViewModel> Items { get; } = [];

    /// <summary>Title of the tab in the preview: the name of the element.</summary>
    public string PreviewTitle => Model.DisplayName;

    /// <summary>Image of the tab: its icon, or its first item.</summary>
    public string? TabIconPath
    {
        get => _tabIconPath;
        private set => SetProperty(ref _tabIconPath, value);
    }

    public bool IsEmpty => _tab.Items.Count == 0;

    public ICommand AddItemCommand { get; }

    /// <summary>Adds the items and blocks of the project that are not in the tab yet.</summary>
    public ICommand AddAllModItemsCommand { get; }

    /// <summary>Moves an item of the list to another place (a row dragged onto another one).</summary>
    public void MoveItem(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= _tab.Items.Count || to >= _tab.Items.Count)
        {
            return;
        }

        List<ContentReference> items = [.. _tab.Items];
        ContentReference moved = items[from];
        items.RemoveAt(from);
        items.Insert(to, moved);
        SetItems(Tr.T("Move an item of the tab"), items);
    }

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        Icon.Refresh();

        // An element of the tab renamed, deleted or given another texture: the rows show it.
        // The list itself may have changed first (an item removed): the rows are then built again.
        if (Items.Count != _tab.Items.Count
            || !Items.Select(e => (e.Name, e.IdText, e.IconPath)).SequenceEqual(Items.Select(e => Describe(e))))
        {
            Rebuild();
        }
        else
        {
            foreach (CreativeTabEntryViewModel entry in Items)
            {
                entry.Picker.Refresh();
            }
        }

        RefreshTabIcon();
    }

    /// <summary>What a row would show now for its element (the image of a Minecraft item does not change).</summary>
    private (string Name, string IdText, string? IconPath) Describe(CreativeTabEntryViewModel entry)
    {
        ContentReference reference = _tab.Items[entry.Index];
        if (reference.Kind != ContentReferenceKind.Asset)
        {
            return (entry.Name, entry.IdText, entry.IconPath);
        }

        Asset? asset = Context.Project.Assets.Find(reference.AssetId);
        return (asset?.DisplayName ?? Tr.T("(deleted asset)"), asset?.ResourceId.Value ?? reference.ToStorageText(), ItemIcons.ProjectFile(Context.Project, reference));
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName == nameof(CreativeTabAsset.Items))
        {
            Rebuild();
            RefreshTabIcon();
        }
        else if (propertyName == nameof(CreativeTabAsset.Icon))
        {
            Icon.Refresh();
            RefreshTabIcon();
        }
        else if (propertyName == nameof(CreativeTabAsset.HasSearchBar))
        {
            OnPropertyChanged(nameof(HasSearchBar));
        }
        else if (propertyName == nameof(Asset.DisplayName))
        {
            OnPropertyChanged(nameof(PreviewTitle));
        }
    }

    private void SetItems(string description, IReadOnlyList<ContentReference> items)
    {
        Change(description, nameof(CreativeTabAsset.Items), () => _tab.Items, v => _tab.Items = v, items);
        Context.History.Seal();
    }

    private void AddAllModItems()
    {
        IEnumerable<ContentReference> missing = Context.Project.Assets
            .Where(a => a is ItemAsset or BlockAsset { HasItemForm: true })
            .OrderBy(a => a.ResourceId)
            .Select(a => ContentReference.ToAsset(a.Id))
            .Where(r => !_tab.Items.Contains(r));
        SetItems(Tr.T("Add the items of the mod to the tab"), [.. _tab.Items, .. missing]);
    }

    private void Rebuild()
    {
        Items.Clear();
        for (int i = 0; i < _tab.Items.Count; i++)
        {
            int index = i;
            var picker = new ReferencePickerViewModel(Context, ReferenceDomain.Item, () => index < _tab.Items.Count ? _tab.Items[index] : null,
                v =>
                {
                    if (v is not null)
                    {
                        SetItems(Tr.T("Change an item of the tab"), [.. _tab.Items.Select((r, j) => j == index ? v : r)]);
                    }
                });
            Items.Add(new CreativeTabEntryViewModel(
                index,
                _tab.Items[index],
                picker,
                Context,
                new RelayCommand(() => SetItems(Tr.T("Remove an item from the tab"), [.. _tab.Items.Where((_, j) => j != index)])),
                new RelayCommand(() => MoveItem(index, index - 1), () => index > 0),
                new RelayCommand(() => MoveItem(index, index + 1), () => index < _tab.Items.Count - 1)));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    private async void RefreshTabIcon()
    {
        ContentReference? icon = _tab.Icon ?? _tab.Items.FirstOrDefault();
        string? path = await ItemIcons.FindAsync(Context.Project, icon);
        if (Equals(icon, _tab.Icon ?? _tab.Items.FirstOrDefault()))
        {
            TabIconPath = path;
        }
    }
}
