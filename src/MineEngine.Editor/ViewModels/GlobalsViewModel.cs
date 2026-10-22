using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Graphs;

namespace MineEngine.Editor.ViewModels;

/// <summary>A kind of container offered in the Global tab: a single value or a collection.</summary>
public sealed record ContainerChoice(PinType? Kind, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A scope offered in the Global tab.</summary>
public sealed record ScopeChoice(GlobalScope Scope, string Label, string Description)
{
    public override string ToString() => Label;
}

/// <summary>
/// A global variable in the table of the Global tab. Each change is one undoable step on the
/// asset; the row reads the asset again after an undo.
/// </summary>
public sealed class GlobalRowViewModel : ObservableObject
{
    private readonly GlobalsViewModel _owner;

    public GlobalRowViewModel(GlobalsViewModel owner, GlobalVariableAsset asset)
    {
        _owner = owner;
        Asset = asset;
        RemoveCommand = new RelayCommand(() => _owner.Remove(this), () => _owner.CanEdit);
    }

    public GlobalVariableAsset Asset { get; }

    public ICommand RemoveCommand { get; }

    public string Name
    {
        get => Asset.VariableName;
        set
        {
            string name = value?.Trim() ?? string.Empty;
            if (name.Length > 0 && name != Asset.DisplayName)
            {
                _owner.Change(Tr.T("Rename a global variable"), Asset, nameof(Asset.DisplayName), () => Asset.DisplayName, v => Asset.DisplayName = v, name);
            }
        }
    }

    /// <summary>Single value, array, list, set or dictionary.</summary>
    public ContainerChoice Container
    {
        get => _owner.ContainerChoices.First(c => c.Kind == (Asset.ValueType.IsCollection ? Asset.ValueType.Kind : null));
        set
        {
            if (value is not null && value != Container)
            {
                DataType element = Element.Type;
                SetType(value.Kind is { } kind
                    ? DataType.Collection(kind, element, kind == PinType.Map ? KeyType.Type : null)
                    : element);
            }
        }
    }

    /// <summary>Type of the value, or of the values of the collection.</summary>
    public DataTypeChoice Element
    {
        get => _owner.ChoiceFor(Asset.ValueType.IsCollection ? Asset.ValueType.Element ?? DataType.Number : Asset.ValueType);
        set
        {
            if (value is not null && value.Type != Element.Type)
            {
                SetType(Asset.ValueType.IsCollection
                    ? DataType.Collection(Asset.ValueType.Kind, value.Type, Asset.ValueType.Key)
                    : value.Type);
            }
        }
    }

    /// <summary>Type of the keys of a dictionary.</summary>
    public DataTypeChoice KeyType
    {
        get => _owner.ChoiceFor(Asset.ValueType.Key ?? DataType.Text);
        set
        {
            if (value is not null && Asset.ValueType.Kind == PinType.Map && value.Type != KeyType.Type)
            {
                SetType(DataType.Collection(PinType.Map, Element.Type, value.Type));
            }
        }
    }

    public bool IsDictionary => Asset.ValueType.Kind == PinType.Map;

    public ScopeChoice Scope
    {
        get => _owner.ScopeChoices.First(c => c.Scope == Asset.Scope);
        set
        {
            if (value is not null && value.Scope != Asset.Scope)
            {
                _owner.Change(Tr.T("Change the scope of a global variable"), Asset, nameof(Asset.Scope), () => Asset.Scope, v => Asset.Scope = v, value.Scope);
            }
        }
    }

    public string DefaultValue
    {
        get => Asset.DefaultValue;
        set
        {
            string text = value?.Trim() ?? string.Empty;
            if (text != Asset.DefaultValue)
            {
                _owner.Change(Tr.T("Change the default value of a global variable"), Asset, nameof(Asset.DefaultValue),
                    () => Asset.DefaultValue, v => Asset.DefaultValue = v, text);
            }
        }
    }

    /// <summary>Only a number, a decimal, a text, true/false or a direction takes a starting value.</summary>
    public bool HasDefaultValue => Asset.ValueType.HasInlineValue && !Asset.ValueType.IsContentReference && Asset.ValueType.Kind != PinType.Enum;

    /// <summary>"0", "north"...: what the variable holds when nothing is typed.</summary>
    public string DefaultHint => HasDefaultValue ? _owner.DefaultFor(Asset.ValueType) : Tr.T("(empty)");

    /// <summary>Warning shown on the row: the value cannot be saved with this scope.</summary>
    public string? Warning => Asset.IsSaved && !GlobalVariableAsset.CanBeSaved(Asset.ValueType)
        ? Tr.T("Not saved: an entity, a player or an object is kept only until the server stops.")
        : null;

    public bool HasWarning => Warning is not null;

    /// <summary>"List of Number", "Dictionary Text → Item"...</summary>
    public string TypeSummary => _owner.Label(Asset.ValueType);

    /// <summary>Reads the asset again (change, undo, other variable renamed).</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);

    private void SetType(DataType type)
    {
        _owner.Change(Tr.T("Change the type of a global variable"), Asset, nameof(Asset.ValueType), () => Asset.ValueType, v => Asset.ValueType = v, type);
        if (!HasDefaultValue && Asset.DefaultValue.Length > 0)
        {
            DefaultValue = string.Empty;
        }
    }
}

/// <summary>
/// Global tab (V0.6.7), between Resources and Languages, like the global variables of MCreator:
/// variables readable and changeable from every graph (Get / Set nodes), with a type (a simple
/// value, an object, a structure or an enumeration of the project, or a collection of them) and a
/// scope that says how long the value is kept. The search of the toolbar filters the table.
/// DataContext of GlobalsPanel: MainViewModel.Globals.
/// </summary>
public sealed class GlobalsViewModel : ObservableObject, IDisposable
{
    private readonly ProjectViewModel _project;
    private readonly Func<bool> _canEdit;
    private readonly Dictionary<Guid, GlobalRowViewModel> _rows = [];
    private string _searchText = string.Empty;
    private GlobalRowViewModel? _selectedRow;
    private IReadOnlyList<DataTypeChoice> _typeChoices = [];
    private IReadOnlyList<DataTypeChoice> _keyChoices = [];

    public GlobalsViewModel(ProjectViewModel project, Func<bool> canEdit)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
        ContainerChoices =
        [
            new ContainerChoice(null, Tr.T("Single value")),
            .. DataType.CollectionKinds.Select(k => new ContainerChoice(k, DataType.CollectionLabel(k))),
        ];
        ScopeChoices = [.. GlobalVariableAsset.OrderedScopes.Select(s => new ScopeChoice(s, GlobalVariableAsset.ScopeLabel(s), GlobalVariableAsset.ScopeDescription(s)))];
        AddCommand = new RelayCommand(Add, () => CanEdit);
        _project.Model.Assets.Changed += OnAssetsChanged;
        _project.History.Changed += OnHistoryChanged;
        Refresh();
    }

    /// <summary>Rows shown (filtered by the search), ordered by name.</summary>
    public ObservableCollection<GlobalRowViewModel> Rows { get; } = [];

    public IReadOnlyList<ContainerChoice> ContainerChoices { get; }

    public IReadOnlyList<ScopeChoice> ScopeChoices { get; }

    /// <summary>Simple types, then the classes, structures and enumerations of the project.</summary>
    public IReadOnlyList<DataTypeChoice> TypeChoices
    {
        get => _typeChoices;
        private set => SetProperty(ref _typeChoices, value);
    }

    /// <summary>Types that can be keys of a dictionary (values that compare well).</summary>
    public IReadOnlyList<DataTypeChoice> KeyChoices
    {
        get => _keyChoices;
        private set => SetProperty(ref _keyChoices, value);
    }

    public ICommand AddCommand { get; }

    public bool CanEdit => _canEdit();

    public GlobalRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    /// <summary>Search of the toolbar: name or type.</summary>
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

    /// <summary>"3 global variables".</summary>
    public string Summary => _rows.Count == 1 ? Tr.T("1 global variable") : Tr.F("{0} global variables", _rows.Count);

    public DataTypeChoice ChoiceFor(DataType type) =>
        TypeChoices.Concat(KeyChoices).FirstOrDefault(c => c.Type == type) ?? new DataTypeChoice(type, Label(type));

    public string Label(DataType type) => _project.Model.Assets.Program.TypeLabel(type);

    public string DefaultFor(DataType type) => _project.Model.Assets.Program.DefaultValueFor(type) ?? string.Empty;

    /// <summary>A new variable "variable", "variable2"... (a number kept with the save), selected.</summary>
    public void Add()
    {
        AssetRegistry assets = _project.Model.Assets;
        string name = UniqueName(Tr.T("variable"));
        ResourceId id = assets.CreateUniqueResourceId(name, AssetType.GlobalVariable);
        var variable = new GlobalVariableAsset(Guid.NewGuid(), id, name);
        _project.History.Execute(new AddAssetCommand(assets, variable, Tr.T("Global variable")));
        SelectedRow = _rows.GetValueOrDefault(variable.Id);
    }

    /// <summary>Deletes the variable; its Get / Set nodes are reported by the diagnostics.</summary>
    public void Remove(GlobalRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _project.History.Execute(new RemoveAssetCommand(_project.Model.Assets, row.Asset, Tr.T("Global variable")));
    }

    internal void Change<T>(string description, GlobalVariableAsset asset, string property, Func<T> getter, Action<T> setter, T value)
    {
        if (CanEdit)
        {
            _project.History.Execute(new PropertyChangeCommand<T>(description, asset, property, getter, setter, value));
        }
    }

    public void Dispose()
    {
        _project.Model.Assets.Changed -= OnAssetsChanged;
        _project.History.Changed -= OnHistoryChanged;
    }

    private string UniqueName(string baseName)
    {
        var used = new HashSet<string>(_project.Model.Assets.OfType<GlobalVariableAsset>().Select(g => g.VariableName), StringComparer.OrdinalIgnoreCase);
        string name = baseName;
        for (int i = 2; used.Contains(name); i++)
        {
            name = baseName + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return name;
    }

    private void OnAssetsChanged(object? sender, AssetRegistryChangedEventArgs e)
    {
        if (e.Asset is GlobalVariableAsset or GraphAsset)
        {
            Refresh();
        }
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        foreach (GlobalRowViewModel row in _rows.Values)
        {
            row.Refresh();
        }

        Refresh();
    }

    private void Refresh()
    {
        ProgramModel program = _project.Model.Assets.Program;
        DataTypeChoice[] projectTypes =
        [
            .. program.Definitions.Where(d => d.Kind.IsType())
                .OrderBy(ProgramModel.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(d => new DataTypeChoice(ProgramModel.TypeOf(d), ProgramModel.Name(d))),
        ];
        DataTypeChoice[] types = [.. DataType.BuiltIn.Select(t => new DataTypeChoice(t, t.BuiltInLabel)), .. projectTypes];
        if (!types.SequenceEqual(TypeChoices))
        {
            TypeChoices = types;
            KeyChoices =
            [
                .. types.Where(c => c.Type.Kind is PinType.Boolean or PinType.Number or PinType.Text or PinType.Direction or PinType.Position
                    or PinType.Item or PinType.Block or PinType.Enum or PinType.Char or PinType.Long or PinType.Byte or PinType.Uuid
                    or PinType.Identifier or PinType.EquipmentSlot or PinType.Hand or PinType.Rarity),
            ];
        }

        var assets = _project.Model.Assets.OfType<GlobalVariableAsset>().ToList();
        foreach (Guid removed in _rows.Keys.Except(assets.Select(a => a.Id)).ToList())
        {
            _rows.Remove(removed);
        }

        foreach (GlobalVariableAsset asset in assets.Where(a => !_rows.ContainsKey(a.Id)))
        {
            _rows[asset.Id] = new GlobalRowViewModel(this, asset);
        }

        string search = _searchText.Trim();
        List<GlobalRowViewModel> shown =
        [
            .. _rows.Values
                .Where(r => search.Length == 0
                            || r.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                            || r.TypeSummary.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
        if (!shown.SequenceEqual(Rows))
        {
            Rows.Clear();
            foreach (GlobalRowViewModel row in shown)
            {
                Rows.Add(row);
            }
        }

        if (_selectedRow is not null && !_rows.ContainsValue(_selectedRow))
        {
            SelectedRow = null;
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Summary));
    }
}
