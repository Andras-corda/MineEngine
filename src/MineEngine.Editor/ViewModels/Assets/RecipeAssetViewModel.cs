using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// A slot of the preview of a recipe (an ingredient, the result) or an item that unlocks it: its image
/// and its identifier; a double-click opens the asset selector, the menu empties it.
/// </summary>
public sealed class RecipeSlotViewModel : ObservableObject
{
    private readonly AssetEditingContext _context;
    private readonly Func<ContentReference?> _getter;
    private readonly Action<ContentReference?> _setter;
    private readonly bool _allowTags;
    private string? _iconPath;

    public RecipeSlotViewModel(AssetEditingContext context, string label, Func<ContentReference?> getter, Action<ContentReference?> setter, bool allowTags)
    {
        _context = context;
        Label = label;
        _getter = getter;
        _setter = setter;
        _allowTags = allowTags;
        BrowseCommand = new RelayCommand(Browse);
        ClearCommand = new RelayCommand(() => _setter(null), () => _getter() is not null);
        Refresh();
    }

    /// <summary>Role of the slot ("Ingredient 1", "Result", "Template"...), for the selector and the accessibility.</summary>
    public string Label { get; }

    public ContentReference? Value => _getter();

    public bool IsEmpty => Value is null;

    public bool IsTag => Value?.Kind == ContentReferenceKind.Tag;

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

    /// <summary>An item without image: a question mark.</summary>
    public bool ShowsPlaceholder => _iconPath is null && Value is { Kind: not ContentReferenceKind.Tag };

    /// <summary>"ruby", "minecraft:diamond", "#minecraft:planks"; empty for an empty slot.</summary>
    public string Text => Value switch
    {
        null => string.Empty,
        { Kind: ContentReferenceKind.Asset } reference => _context.Project.Assets.Find(reference.AssetId)?.ResourceId.Value ?? Tr.T("(deleted asset)"),
        { } reference => reference.ToStorageText(),
    };

    public string ToolTip => Value is null ? Tr.F("{0}: empty (double-click to choose)", Label) : $"{Label}: {Text}";

    public ICommand BrowseCommand { get; }

    public ICommand ClearCommand { get; }

    /// <summary>To call when the value or the project changed.</summary>
    public async void Refresh()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsTag));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(ToolTip));
        OnPropertyChanged(nameof(ShowsPlaceholder));
        ContentReference? value = Value;
        string? icon = await ItemIcons.FindAsync(_context.Project, value);
        if (Equals(value, Value))
        {
            IconPath = icon;
        }
    }

    private void Browse()
    {
        var browser = new AssetBrowserViewModel(_context, Value, Label, Tr.T("(empty)"), _allowTags);
        if (_context.Dialogs.ShowAssetBrowser(browser) && !Equals(browser.ReferenceResult, Value))
        {
            _setter(browser.ReferenceResult);
        }
    }
}

/// <summary>
/// Editor of a recipe: its station (crafting table, furnace, blast furnace, smoker, campfire, stonecutter,
/// smithing table), its registry name, group, namespace and tab of the recipe book, the items that unlock
/// it, and a preview of the station whose slots are chosen with the asset selector (double-click).
/// </summary>
public sealed class RecipeAssetViewModel : AssetViewModel
{
    private readonly RecipeAsset _recipe;

    public RecipeAssetViewModel(RecipeAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _recipe = model;
        GridSlots =
        [
            .. Enumerable.Range(0, RecipeAsset.GridSize).Select(index => new RecipeSlotViewModel(context, Tr.F("Slot {0}", index + 1),
                () => _recipe.Grid[index], v => SetGrid(index, v), allowTags: true)),
        ];
        SmithingSlots =
        [
            new RecipeSlotViewModel(context, Tr.T("Template"), () => _recipe.Grid[RecipeAsset.SmithingTemplate], v => SetGrid(RecipeAsset.SmithingTemplate, v), true),
            new RecipeSlotViewModel(context, Tr.T("Base item"), () => _recipe.Grid[RecipeAsset.SmithingBase], v => SetGrid(RecipeAsset.SmithingBase, v), true),
            new RecipeSlotViewModel(context, Tr.T("Material"), () => _recipe.Grid[RecipeAsset.SmithingAddition], v => SetGrid(RecipeAsset.SmithingAddition, v), true),
        ];
        InputSlot = new RecipeSlotViewModel(context, Tr.T("Ingredient"), () => _recipe.SmeltingInput,
            v => Apply(Tr.T("Change ingredient"), nameof(RecipeAsset.SmeltingInput), () => _recipe.SmeltingInput, r => _recipe.SmeltingInput = r, v), true);
        ResultSlot = new RecipeSlotViewModel(context, Tr.T("Result"), () => _recipe.Result,
            v => Apply(Tr.T("Change result"), nameof(RecipeAsset.Result), () => _recipe.Result, r => _recipe.Result = r, v), false);
        AddUnlockItemCommand = new RelayCommand(AddUnlockItem);
        RebuildUnlockItems();
    }

    /// <summary>The settings and the preview side by side.</summary>
    public override double EditorMaxWidth => 1300;

    public IReadOnlyList<Choice<RecipeKind>> StationChoices { get; } =
    [
        new(RecipeKind.Shaped, Tr.T("Crafting table")),
        new(RecipeKind.Smelting, Tr.T("Furnace")),
        new(RecipeKind.Blasting, Tr.T("Blast furnace")),
        new(RecipeKind.Smoking, Tr.T("Smoker")),
        new(RecipeKind.CampfireCooking, Tr.T("Campfire")),
        new(RecipeKind.Stonecutting, Tr.T("Stonecutter")),
        new(RecipeKind.Smithing, Tr.T("Smithing table")),
    ];

    /// <summary>The station; a shapeless recipe is one of the crafting table.</summary>
    public RecipeKind Station
    {
        get => _recipe.Kind == RecipeKind.Shapeless ? RecipeKind.Shaped : _recipe.Kind;
        set
        {
            RecipeKind kind = value == RecipeKind.Shaped && _recipe.Kind == RecipeKind.Shapeless ? RecipeKind.Shapeless : value;
            if (kind == _recipe.Kind)
            {
                return;
            }

            // The cooking time of the game follows the station, unless it was changed by hand.
            var commands = new List<IUndoableCommand>
            {
                new PropertyChangeCommand<RecipeKind>(Tr.T("Change recipe type"), _recipe, nameof(RecipeAsset.Kind), () => _recipe.Kind, v => _recipe.Kind = v, kind),
            };
            if (_recipe.CookingTime == RecipeAsset.DefaultCookingTimeOf(_recipe.Kind))
            {
                commands.Add(new PropertyChangeCommand<int>(Tr.T("Change cooking time"), _recipe, nameof(RecipeAsset.CookingTime), () => _recipe.CookingTime,
                    v => _recipe.CookingTime = v, RecipeAsset.DefaultCookingTimeOf(kind)));
            }

            if (!RecipeAsset.CategoriesOf(kind).Contains(_recipe.Category))
            {
                commands.Add(new PropertyChangeCommand<string>(Tr.T("Change the category"), _recipe, nameof(RecipeAsset.Category), () => _recipe.Category,
                    v => _recipe.Category = v, "misc"));
            }

            Context.History.Execute(new CompositeCommand(Tr.T("Change recipe type"), commands));
            Context.History.Seal();
        }
    }

    public bool IsCrafting => _recipe.IsCrafting;

    public bool IsShapeless
    {
        get => _recipe.Kind == RecipeKind.Shapeless;
        set => Apply(Tr.T("Change recipe type"), nameof(RecipeAsset.Kind), () => _recipe.Kind, v => _recipe.Kind = v, value ? RecipeKind.Shapeless : RecipeKind.Shaped);
    }

    public bool IsCooking => _recipe.IsCooking;

    public bool IsStonecutting => _recipe.Kind == RecipeKind.Stonecutting;

    public bool IsSmithing => _recipe.Kind == RecipeKind.Smithing;

    public bool HasSingleInput => _recipe.HasSingleInput;

    public bool HasResultCount => _recipe.HasResultCount;

    /// <summary>Name of the file of the recipe ("ruby_block"): its identifier.</summary>
    public string RegistryName
    {
        get => ResourceIdText;
        set => ResourceIdText = value;
    }

    public string Group
    {
        get => _recipe.Group;
        set => ChangeIfDifferent(Tr.T("Change the group"), nameof(RecipeAsset.Group), () => _recipe.Group, v => _recipe.Group = v, value ?? string.Empty);
    }

    /// <summary>Namespace of the file; empty for the mod.</summary>
    public string Namespace
    {
        get => _recipe.Namespace;
        set => ChangeIfDifferent(Tr.T("Change the namespace"), nameof(RecipeAsset.Namespace), () => _recipe.Namespace, v => _recipe.Namespace = v, value ?? string.Empty);
    }

    /// <summary>"e2emod:ruby_block": the identifier of the recipe in the game.</summary>
    public string FullName => $"{(_recipe.Namespace.Length == 0 ? Context.Project.Settings.ModId.Value : _recipe.Namespace)}:{_recipe.ResourceId}";

    public IReadOnlyList<Choice<string>> CategoryChoices => [.. RecipeAsset.CategoriesOf(_recipe.Kind).Select(c => new Choice<string>(c, CategoryLabel(c)))];

    public bool HasCategory => CategoryChoices.Count > 0;

    public string Category
    {
        get => _recipe.Category;
        set => ChangeIfDifferent(Tr.T("Change the category"), nameof(RecipeAsset.Category), () => _recipe.Category, v => _recipe.Category = v, value ?? "misc");
    }

    /// <summary>Items or tags that unlock the recipe in the recipe book.</summary>
    public ObservableCollection<RecipeSlotViewModel> UnlockItems { get; } = [];

    public bool HasUnlockItems => UnlockItems.Count > 0;

    public ICommand AddUnlockItemCommand { get; }

    /// <summary>Cases de la grille, ligne par ligne.</summary>
    public IReadOnlyList<RecipeSlotViewModel> GridSlots { get; }

    /// <summary>Template, base item and material of the smithing table.</summary>
    public IReadOnlyList<RecipeSlotViewModel> SmithingSlots { get; }

    public RecipeSlotViewModel InputSlot { get; }

    public RecipeSlotViewModel ResultSlot { get; }

    public int MaxResultCount => RecipeAsset.MaxResultCount;

    public double ResultCount
    {
        get => _recipe.ResultCount;
        set => SetNumber(Tr.T("Change result count"), nameof(RecipeAsset.ResultCount), value, () => _recipe.ResultCount,
            v => _recipe.ResultCount = v, v => Math.Clamp((int)Math.Round(v), 1, RecipeAsset.MaxResultCount));
    }

    public double Experience
    {
        get => _recipe.Experience;
        set => SetNumber(Tr.T("Change experience"), nameof(RecipeAsset.Experience), value, () => _recipe.Experience,
            v => _recipe.Experience = v, v => (float)Math.Clamp(v, 0, RecipeAsset.MaxExperience));
    }

    /// <summary>Durée de cuisson affichée en secondes (20 ticks par seconde).</summary>
    public double CookingSeconds
    {
        get => _recipe.CookingTime / 20.0;
        set => SetNumber(Tr.T("Change cooking time"), nameof(RecipeAsset.CookingTime), value, () => _recipe.CookingTime,
            v => _recipe.CookingTime = v, v => Math.Clamp((int)Math.Round(v * 20), 1, RecipeAsset.MaxCookingTime));
    }

    public string GridHint => _recipe.Kind switch
    {
        RecipeKind.Shaped => Tr.T("Arrange the ingredients as on the crafting table. The pattern can be placed anywhere in the game grid."),
        RecipeKind.Shapeless => Tr.T("The ingredients can be placed in any order."),
        RecipeKind.Smithing => Tr.T("A smithing template, the item to improve and the material (like a netherite ingot)."),
        RecipeKind.Stonecutting => Tr.T("The stonecutter turns one item into the result, in as many copies as the count."),
        _ => Tr.T("The ingredient cooks into the result; the station gives experience."),
    };

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        RefreshSlots();
        RebuildUnlockItems();
        OnPropertyChanged(nameof(FullName));
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(RecipeAsset.Kind):
                foreach (string name in new[]
                         {
                             nameof(Station), nameof(IsCrafting), nameof(IsShapeless), nameof(IsCooking), nameof(IsStonecutting), nameof(IsSmithing),
                             nameof(HasSingleInput), nameof(HasResultCount), nameof(GridHint), nameof(CategoryChoices), nameof(HasCategory), nameof(Category),
                         })
                {
                    OnPropertyChanged(name);
                }

                break;

            case nameof(RecipeAsset.Grid) or nameof(RecipeAsset.SmeltingInput) or nameof(RecipeAsset.Result):
                RefreshSlots();
                break;

            case nameof(RecipeAsset.UnlockItems):
                RebuildUnlockItems();
                break;

            case nameof(RecipeAsset.ResultCount):
                OnPropertyChanged(nameof(ResultCount));
                break;

            case nameof(RecipeAsset.Experience):
                OnPropertyChanged(nameof(Experience));
                break;

            case nameof(RecipeAsset.CookingTime):
                OnPropertyChanged(nameof(CookingSeconds));
                break;

            case nameof(Asset.ResourceId) or nameof(RecipeAsset.Namespace):
                OnPropertyChanged(nameof(RegistryName));
                OnPropertyChanged(nameof(Namespace));
                OnPropertyChanged(nameof(FullName));
                break;

            default:
                OnPropertyChanged(propertyName);
                break;
        }
    }

    private static string CategoryLabel(string category) => category switch
    {
        "building" => Tr.T("Building blocks"),
        "redstone" => Tr.T("Redstone"),
        "equipment" => Tr.T("Equipment"),
        "food" => Tr.T("Food"),
        "blocks" => Tr.T("Blocks"),
        _ => Tr.T("Miscellaneous"),
    };

    private void RefreshSlots()
    {
        foreach (RecipeSlotViewModel slot in GridSlots.Concat(SmithingSlots).Append(InputSlot).Append(ResultSlot))
        {
            slot.Refresh();
        }
    }

    private void RebuildUnlockItems()
    {
        UnlockItems.Clear();
        for (int i = 0; i < _recipe.UnlockItems.Count; i++)
        {
            int index = i;
            UnlockItems.Add(new RecipeSlotViewModel(Context, Tr.T("Unlocks the recipe"), () => index < _recipe.UnlockItems.Count ? _recipe.UnlockItems[index] : null,
                v => SetUnlockItems(Tr.T("Change an item that unlocks the recipe"), v is null
                    ? [.. _recipe.UnlockItems.Where((_, j) => j != index)]
                    : [.. _recipe.UnlockItems.Select((r, j) => j == index ? v : r)]),
                allowTags: true));
        }

        OnPropertyChanged(nameof(HasUnlockItems));
    }

    private void AddUnlockItem()
    {
        var browser = new AssetBrowserViewModel(Context, null, Tr.T("Unlocks the recipe"), Tr.T("(empty)"), allowTags: true);
        if (Context.Dialogs.ShowAssetBrowser(browser) && browser.ReferenceResult is { } item && !_recipe.UnlockItems.Contains(item))
        {
            SetUnlockItems(Tr.T("Add an item that unlocks the recipe"), [.. _recipe.UnlockItems, item]);
        }
    }

    private void SetUnlockItems(string description, IReadOnlyList<ContentReference> items)
    {
        Change(description, nameof(RecipeAsset.UnlockItems), () => _recipe.UnlockItems, v => _recipe.UnlockItems = v, items);
        Context.History.Seal();
    }

    private void SetGrid(int index, ContentReference? value) =>
        Apply(Tr.T("Change grid"), nameof(RecipeAsset.Grid), () => _recipe.Grid, g => _recipe.Grid = g, _recipe.WithGridSlot(index, value));

    private void Apply<T>(string description, string propertyName, Func<T> getter, Action<T> setter, T value) =>
        ChangeIfDifferent(description, propertyName, getter, setter, value);

    private void SetNumber<T>(string description, string propertyName, double value, Func<T> getter, Action<T> setter, Func<double, T> convert)
    {
        if (double.IsNaN(value))
        {
            // Champ vidé : on réaffiche la valeur actuelle.
            OnPropertyChanged(string.Empty);
            return;
        }

        T converted = convert(value);
        if (!EqualityComparer<T>.Default.Equals(getter(), converted))
        {
            Change(description, propertyName, getter, setter, converted);
        }
    }
}
