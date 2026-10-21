using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Commands;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.ViewModels.Graphs;

/// <summary>Un type proposé pour une variable, un paramètre ou un champ.</summary>
public sealed record DataTypeChoice(DataType Type, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Une définition proposée (classe parente, interface...), ou "aucune" (<see cref="Id"/> null).</summary>
public sealed record DefinitionChoice(Guid? Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Panneau "Définition" d'un graphe (comme "My Blueprint" dans l'Unreal Engine) : nom,
/// accessibilité, paramètres, valeurs renvoyées, variables ou champs, héritage et interfaces
/// d'une classe, méthodes et types qui lui sont intégrés, valeurs d'une énumération, méthodes
/// d'une interface. Chaque modification passe par l'historique du projet (annulable) ; une
/// saisie au clavier forme une seule étape.
/// </summary>
public sealed class DefinitionViewModel : ObservableObject
{
    private readonly GraphAsset _asset;
    private readonly AssetEditingContext _context;
    private readonly Func<ProgramModel> _program;
    private readonly Action<GraphKind, Guid> _createMember;
    private IReadOnlyList<DataTypeChoice> _typeChoices = [];

    /// <param name="createMember">Crée une définition intégrée à la classe (méthode, type imbriqué) et l'ouvre.</param>
    public DefinitionViewModel(GraphAsset asset, AssetEditingContext context, Func<ProgramModel> program, Action<GraphKind, Guid> createMember)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _program = program ?? throw new ArgumentNullException(nameof(program));
        _createMember = createMember ?? throw new ArgumentNullException(nameof(createMember));

        AddParameterCommand = new RelayCommand(() => AddVariable(VariableList.Parameters, Tr.T("parameter")));
        AddReturnCommand = new RelayCommand(() => AddVariable(VariableList.Returns, Tr.T("value")));
        AddVariableCommand = new RelayCommand(() => AddVariable(VariableList.Variables, IsFieldList ? "champ" : "variable"));
        AddConstantCommand = new RelayCommand(AddConstant);
        AddMethodCommand = new RelayCommand(AddMethod);
        AddMemberCommand = new RelayCommand<GraphKind>(kind => _createMember(kind, _asset.Id));
        OpenOwnerCommand = new RelayCommand(() => _context.Navigation.Open(_program().Find(_asset.OwnerId)!), () => _asset.OwnerId is not null);
        Refresh();
    }

    public GraphKind Kind => _asset.Kind;

    public string KindLabel => _asset.Kind.Label();

    // ----- Nom et accessibilité -----

    public string Name
    {
        get => _asset.DisplayName;
        set => Change(nameof(GraphAsset.DisplayName), Tr.T("Rename"), () => _asset.DisplayName, v => _asset.DisplayName = v, value ?? string.Empty);
    }

    /// <summary>A library and its functions are always public: no choice to make.</summary>
    public bool ShowAccess => _asset.Kind is not (GraphKind.Events or GraphKind.Library) && !IsInLibrary;

    /// <summary>Function or procedure of a function library.</summary>
    public bool IsInLibrary => _program().Find(_asset.OwnerId) is { Kind: GraphKind.Library };

    public bool IsLibrary => _asset.Kind == GraphKind.Library;

    public IReadOnlyList<AccessLevel> AccessChoices { get; } = Enum.GetValues<AccessLevel>();

    public AccessLevel Access
    {
        get => _asset.Access;
        set => Change(nameof(GraphAsset.Access), Tr.T("Change accessibility"), () => _asset.Access, v => _asset.Access = v, value);
    }

    /// <summary>Explication de l'accessibilité choisie, selon que la définition est intégrée à une classe ou non.</summary>
    public string AccessHint => (_asset.Access, _asset.OwnerId is not null) switch
    {
        (AccessLevel.Public, _) => Tr.T("Usable in the whole project."),
        (AccessLevel.Private, true) => Tr.T("Usable only in its class."),
        (AccessLevel.Protected, true) => Tr.T("Usable in its class and in the classes that inherit from it."),
        _ => Tr.T("Usable only from the graphs in its Dashboard folder."),
    };

    public bool IsOwned => _asset.OwnerId is not null;

    public string OwnerLabel => _program().Find(_asset.OwnerId) switch
    {
        { Kind: GraphKind.Library } library => Tr.F("In the function library {0}: public, usable in every graph", ProgramModel.Name(library)),
        { } owner => Tr.F("Nested in class {0}", ProgramModel.Name(owner)),
        _ => string.Empty,
    };

    public ICommand OpenOwnerCommand { get; }

    public bool ShowPure => _asset.Kind == GraphKind.Function;

    public bool IsPure
    {
        get => _asset.IsPure;
        set => Change(nameof(GraphAsset.IsPure), Tr.T("Pure function"), () => _asset.IsPure, v => _asset.IsPure = v, value);
    }

    public bool ShowStatic => _asset.Kind.IsCallable() && _asset.OwnerId is not null && !IsInLibrary;

    public bool IsStatic
    {
        get => _asset.IsStatic;
        set => Change(nameof(GraphAsset.IsStatic), Tr.T("Static method"), () => _asset.IsStatic, v => _asset.IsStatic = v, value);
    }

    // ----- Paramètres, valeurs renvoyées, variables -----

    public bool ShowParameters => _asset.Kind is GraphKind.Function or GraphKind.Procedure or GraphKind.Class;

    public string ParametersTitle => _asset.Kind == GraphKind.Class ? Tr.T("CONSTRUCTOR PARAMETERS") : Tr.T("PARAMETERS");

    public bool ShowReturns => _asset.Kind == GraphKind.Function;

    public bool ShowVariables => _asset.Kind is GraphKind.Events or GraphKind.Function or GraphKind.Procedure or GraphKind.Class or GraphKind.Struct;

    public bool IsFieldList => _asset.Kind is GraphKind.Class or GraphKind.Struct;

    public string VariablesTitle => _asset.Kind switch
    {
        GraphKind.Class or GraphKind.Struct => "CHAMPS",
        GraphKind.Events => Tr.T("GRAPH VARIABLES"),
        _ => "VARIABLES",
    };

    public string VariablesHint => _asset.Kind switch
    {
        GraphKind.Events => Tr.T("Kept while the game runs, shared by all the events of the graph."),
        GraphKind.Function or GraphKind.Procedure => Tr.T("Local: reset to their value at each call. Global: kept from one call to the next."),
        GraphKind.Struct => Tr.T("Each field is given when the struct is created."),
        _ => Tr.T("Each object of the class has its own values."),
    };

    public ObservableCollection<VariableItemViewModel> Parameters { get; } = [];

    public ObservableCollection<VariableItemViewModel> Returns { get; } = [];

    public ObservableCollection<VariableItemViewModel> Variables { get; } = [];

    public ICommand AddParameterCommand { get; }

    public ICommand AddReturnCommand { get; }

    public ICommand AddVariableCommand { get; }

    /// <summary>Types proposés : les types intégrés, puis ceux du projet visibles depuis ce graphe.</summary>
    public IReadOnlyList<DataTypeChoice> TypeChoices
    {
        get => _typeChoices;
        private set => SetProperty(ref _typeChoices, value);
    }

    // ----- Classe : héritage, interfaces, membres -----

    public bool IsClass => _asset.Kind == GraphKind.Class;

    public IReadOnlyList<DefinitionChoice> BaseClassChoices
    {
        get
        {
            ProgramModel program = _program();
            return
            [
                new DefinitionChoice(null, Tr.T("(none)")),
                .. program.Definitions
                    .Where(d => d.Kind == GraphKind.Class && d.Id != _asset.Id && program.IsVisible(d, _asset)
                                && !program.Ancestors(d).Any(a => a.Id == _asset.Id))
                    .OrderBy(ProgramModel.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(d => new DefinitionChoice(d.Id, ProgramModel.Name(d))),
            ];
        }
    }

    public DefinitionChoice? BaseClass
    {
        get => BaseClassChoices.FirstOrDefault(c => c.Id == _asset.BaseClassId);
        set
        {
            if (value is not null && value.Id != _asset.BaseClassId)
            {
                Change(nameof(GraphAsset.BaseClassId), Tr.T("Change parent class"), () => _asset.BaseClassId, v => _asset.BaseClassId = v, value.Id);
            }
        }
    }

    public ObservableCollection<InterfaceChoiceViewModel> Interfaces { get; } = [];

    public ObservableCollection<MemberItemViewModel> Members { get; } = [];

    /// <summary>Crée une définition intégrée à la classe (paramètre : ce qu'elle définit).</summary>
    public ICommand AddMemberCommand { get; }

    // ----- Énumération, interface -----

    public bool IsEnum => _asset.Kind == GraphKind.Enum;

    public ObservableCollection<ConstantItemViewModel> Constants { get; } = [];

    public ICommand AddConstantCommand { get; }

    public bool IsInterface => _asset.Kind == GraphKind.Interface;

    public ObservableCollection<MethodSignatureItemViewModel> Methods { get; } = [];

    public ICommand AddMethodCommand { get; }

    /// <summary>Relit la définition (modification, annulation, autre graphe modifié) en gardant les lignes en cours de saisie.</summary>
    public void Refresh()
    {
        ProgramModel program = _program();
        TypeChoices =
        [
            .. DataType.BuiltIn.Select(t => new DataTypeChoice(t, t.BuiltInLabel)),
            .. program.VisibleTypes(_asset).Select(d => new DataTypeChoice(ProgramModel.TypeOf(d), ProgramModel.Name(d))),
        ];

        Sync(Parameters, _asset.Parameters, v => new VariableItemViewModel(this, VariableList.Parameters, v));
        Sync(Returns, _asset.Returns, v => new VariableItemViewModel(this, VariableList.Returns, v));
        Sync(Variables, _asset.Variables, v => new VariableItemViewModel(this, VariableList.Variables, v));
        Sync(Constants, _asset.Constants, c => new ConstantItemViewModel(this, c), c => c.Id, (item, c) => item.Update(c));
        Sync(Methods, _asset.Methods, m => new MethodSignatureItemViewModel(this, m), m => m.Id, (item, m) => item.Update(m));

        Interfaces.Clear();
        foreach (GraphAsset contract in program.Definitions.Where(d => d.Kind == GraphKind.Interface && program.IsVisible(d, _asset))
                     .OrderBy(ProgramModel.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Interfaces.Add(new InterfaceChoiceViewModel(this, contract.Id, ProgramModel.Name(contract), _asset.InterfaceIds.Contains(contract.Id)));
        }

        Members.Clear();
        foreach (GraphAsset member in program.Members(_asset).OrderBy(m => m.Kind).ThenBy(ProgramModel.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Members.Add(new MemberItemViewModel(member, () => _context.Navigation.Open(member)));
        }

        foreach (string property in new[]
                 {
                     nameof(Name), nameof(Access), nameof(AccessHint), nameof(IsPure), nameof(IsStatic), nameof(OwnerLabel), nameof(IsOwned),
                     nameof(BaseClassChoices), nameof(BaseClass), nameof(ShowStatic), nameof(ShowAccess), nameof(IsInLibrary),
                 })
        {
            OnPropertyChanged(property);
        }
    }

    // ----- Modifications (appelées par les lignes) -----

    internal ProgramModel Program => _program();

    internal void UpdateVariable(VariableList list, Guid id, Func<VariableDefinition, VariableDefinition> change, string description) =>
        SetList(list, [.. GetList(list).Select(v => v.Id == id ? change(v) : v)], description);

    internal void RemoveVariable(VariableList list, Guid id) =>
        Structural(() => SetList(list, [.. GetList(list).Where(v => v.Id != id)], Tr.T("Delete a row")));

    internal void MoveVariable(VariableList list, Guid id, int delta)
    {
        List<VariableDefinition> items = [.. GetList(list)];
        int index = items.FindIndex(v => v.Id == id);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= items.Count)
        {
            return;
        }

        (items[index], items[target]) = (items[target], items[index]);
        Structural(() => SetList(list, items, Tr.T("Move a row")));
    }

    internal void UpdateConstant(Guid id, string name) =>
        Change(nameof(GraphAsset.Constants), Tr.T("Rename a value"), () => _asset.Constants, v => _asset.Constants = v,
            [.. _asset.Constants.Select(c => c.Id == id ? c with { Name = name } : c)]);

    internal void RemoveConstant(Guid id) => Structural(() =>
        Change(nameof(GraphAsset.Constants), Tr.T("Delete a value"), () => _asset.Constants, v => _asset.Constants = v,
            [.. _asset.Constants.Where(c => c.Id != id)]));

    internal void UpdateMethod(Guid id, Func<MethodSignature, MethodSignature> change, string description) =>
        Change(nameof(GraphAsset.Methods), description, () => _asset.Methods, v => _asset.Methods = v,
            [.. _asset.Methods.Select(m => m.Id == id ? change(m) : m)]);

    internal void RemoveMethod(Guid id) => Structural(() =>
        Change(nameof(GraphAsset.Methods), Tr.T("Delete a method"), () => _asset.Methods, v => _asset.Methods = v,
            [.. _asset.Methods.Where(m => m.Id != id)]));

    internal void SetInterface(Guid id, bool implemented)
    {
        IReadOnlyList<Guid> interfaces = implemented
            ? [.. _asset.InterfaceIds.Append(id).Distinct()]
            : [.. _asset.InterfaceIds.Where(i => i != id)];
        Structural(() => Change(nameof(GraphAsset.InterfaceIds), implemented ? Tr.T("Implement an interface") : Tr.T("Remove an interface"),
            () => _asset.InterfaceIds, v => _asset.InterfaceIds = v, interfaces));
    }

    /// <summary>Valeurs d'une énumération du projet (pour la valeur par défaut d'une variable).</summary>
    internal IReadOnlyList<EnumConstant> ConstantsOf(DataType type) =>
        type.Kind == PinType.Enum ? _program().Find(type.Definition)?.Constants ?? [] : [];

    private void AddVariable(VariableList list, string baseName)
    {
        IReadOnlyList<VariableDefinition> items = GetList(list);
        string name = UniqueName(baseName, items.Select(v => v.Name));
        Structural(() => SetList(list, [.. items, VariableDefinition.Create(name, DataType.Number) with { DefaultValue = list == VariableList.Returns ? string.Empty : "0" }],
            Tr.T("Add a row")));
    }

    private void AddConstant() => Structural(() =>
        Change(nameof(GraphAsset.Constants), Tr.T("Add a value"), () => _asset.Constants, v => _asset.Constants = v,
            [.. _asset.Constants, EnumConstant.Create(UniqueName(Tr.T("Value"), _asset.Constants.Select(c => c.Name)))]));

    private void AddMethod() => Structural(() =>
        Change(nameof(GraphAsset.Methods), Tr.T("Add a method"), () => _asset.Methods, v => _asset.Methods = v,
            [.. _asset.Methods, MethodSignature.Create(UniqueName("methode", _asset.Methods.Select(m => m.Name)))]));

    private static string UniqueName(string baseName, IEnumerable<string> existing)
    {
        var taken = existing.Select(n => ProgramNames.Identifier(n)).ToHashSet(StringComparer.Ordinal);
        if (!taken.Contains(ProgramNames.Identifier(baseName)))
        {
            return baseName;
        }

        for (int i = 2; ; i++)
        {
            string candidate = $"{baseName} {i}";
            if (!taken.Contains(ProgramNames.Identifier(candidate)))
            {
                return candidate;
            }
        }
    }

    private IReadOnlyList<VariableDefinition> GetList(VariableList list) => list switch
    {
        VariableList.Parameters => _asset.Parameters,
        VariableList.Returns => _asset.Returns,
        _ => _asset.Variables,
    };

    private void SetList(VariableList list, IReadOnlyList<VariableDefinition> value, string description)
    {
        switch (list)
        {
            case VariableList.Parameters:
                Change(nameof(GraphAsset.Parameters), description, () => _asset.Parameters, v => _asset.Parameters = v, value);
                break;
            case VariableList.Returns:
                Change(nameof(GraphAsset.Returns), description, () => _asset.Returns, v => _asset.Returns = v, value);
                break;
            default:
                Change(nameof(GraphAsset.Variables), description, () => _asset.Variables, v => _asset.Variables = v, value);
                break;
        }
    }

    /// <summary>Ajout, suppression ou déplacement : une étape à part, jamais fusionnée avec une saisie.</summary>
    private void Structural(Action change)
    {
        _context.History.Seal();
        change();
        _context.History.Seal();
    }

    private void Change<T>(string property, string description, Func<T> getter, Action<T> setter, T value)
    {
        if (EqualityComparer<T>.Default.Equals(getter(), value))
        {
            return;
        }

        _context.History.Execute(new PropertyChangeCommand<T>($"{description} ({ProgramModel.Name(_asset)})", _asset, property, getter, setter, value));
    }

    /// <summary>Met une liste de lignes en accord avec le modèle, en gardant les lignes existantes (et la saisie en cours).</summary>
    private static void Sync<TItem, TModel>(
        ObservableCollection<TItem> items, IReadOnlyList<TModel> models, Func<TModel, TItem> create, Func<TModel, Guid> id, Action<TItem, TModel> update)
        where TItem : IIdentifiedItem
    {
        if (items.Select(i => i.Id).SequenceEqual(models.Select(id)))
        {
            for (int i = 0; i < models.Count; i++)
            {
                update(items[i], models[i]);
            }

            return;
        }

        items.Clear();
        foreach (TModel model in models)
        {
            items.Add(create(model));
        }
    }

    private static void Sync(ObservableCollection<VariableItemViewModel> items, IReadOnlyList<VariableDefinition> models, Func<VariableDefinition, VariableItemViewModel> create) =>
        Sync(items, models, create, v => v.Id, (item, v) => item.Update(v));
}

/// <summary>Liste de variables d'une définition.</summary>
public enum VariableList
{
    Parameters,
    Returns,
    Variables,
}

/// <summary>Ligne d'une liste identifiée par l'identifiant de son modèle.</summary>
public interface IIdentifiedItem
{
    Guid Id { get; }
}

/// <summary>Paramètre, valeur renvoyée, variable ou champ : nom, type, valeur par défaut, accessibilité, portée.</summary>
public sealed class VariableItemViewModel : ObservableObject, IIdentifiedItem
{
    private readonly DefinitionViewModel _owner;
    private readonly VariableList _list;
    private VariableDefinition _model;

    public VariableItemViewModel(DefinitionViewModel owner, VariableList list, VariableDefinition model)
    {
        _owner = owner;
        _list = list;
        _model = model;
        RemoveCommand = new RelayCommand(() => _owner.RemoveVariable(_list, _model.Id));
        MoveUpCommand = new RelayCommand(() => _owner.MoveVariable(_list, _model.Id, -1));
        MoveDownCommand = new RelayCommand(() => _owner.MoveVariable(_list, _model.Id, 1));
    }

    public Guid Id => _model.Id;

    public string Name
    {
        get => _model.Name;
        set => _owner.UpdateVariable(_list, _model.Id, v => v with { Name = value ?? string.Empty }, Tr.T("Rename"));
    }

    public IReadOnlyList<DataTypeChoice> TypeChoices => _owner.TypeChoices;

    public DataTypeChoice? Type
    {
        get => _owner.TypeChoices.FirstOrDefault(c => c.Type == _model.Type) ?? new DataTypeChoice(_model.Type, _owner.Program.TypeLabel(_model.Type));
        set
        {
            if (value is not null && value.Type != _model.Type)
            {
                // Une valeur par défaut n'a de sens que pour son type : elle repart de zéro.
                string reset = _list == VariableList.Returns ? string.Empty : _owner.Program.DefaultValueFor(value.Type) ?? string.Empty;
                _owner.UpdateVariable(_list, _model.Id, v => v with { Type = value.Type, DefaultValue = reset }, Tr.T("Change type"));
            }
        }
    }

    /// <summary>Les valeurs renvoyées n'ont pas de valeur par défaut ; les types objet non plus (aucun objet).</summary>
    public bool HasDefault => _list != VariableList.Returns && _model.Type.HasInlineValue && !_model.Type.IsContentReference;

    public bool IsTextDefault => HasDefault && _model.Type.Kind is PinType.Number or PinType.Text;

    public bool IsBooleanDefault => HasDefault && _model.Type.Kind == PinType.Boolean;

    public bool IsChoiceDefault => HasDefault && _model.Type.Kind == PinType.Enum;

    public string DefaultText
    {
        get => _model.DefaultValue;
        set => _owner.UpdateVariable(_list, _model.Id, v => v with { DefaultValue = value ?? string.Empty }, Tr.T("Default value"));
    }

    public bool DefaultBool
    {
        get => bool.TryParse(_model.DefaultValue, out bool flag) && flag;
        set => _owner.UpdateVariable(_list, _model.Id, v => v with { DefaultValue = value ? "true" : "false" }, Tr.T("Default value"));
    }

    public IReadOnlyList<EnumConstant> DefaultChoices => _owner.ConstantsOf(_model.Type);

    public EnumConstant? DefaultChoice
    {
        get => DefaultChoices.FirstOrDefault(c => c.Id.ToString("D") == _model.DefaultValue) ?? DefaultChoices.FirstOrDefault();
        set
        {
            if (value is not null)
            {
                _owner.UpdateVariable(_list, _model.Id, v => v with { DefaultValue = value.Id.ToString("D") }, Tr.T("Default value"));
            }
        }
    }

    public bool ShowAccess => _list == VariableList.Variables && _owner.Kind == GraphKind.Class;

    public IReadOnlyList<AccessLevel> AccessChoices => _owner.AccessChoices;

    public AccessLevel Access
    {
        get => _model.Access;
        set => _owner.UpdateVariable(_list, _model.Id, v => v with { Access = value }, Tr.T("Change accessibility"));
    }

    public bool ShowGlobal => _list == VariableList.Variables && _owner.Kind is GraphKind.Function or GraphKind.Procedure;

    public bool IsGlobal
    {
        get => _model.IsGlobal;
        set => _owner.UpdateVariable(_list, _model.Id, v => v with { IsGlobal = value }, Tr.T("Global variable"));
    }

    public ICommand RemoveCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    internal void Update(VariableDefinition model)
    {
        bool typeChanged = model.Type != _model.Type;
        _model = model;
        foreach (string property in new[]
                 {
                     nameof(Name), nameof(Type), nameof(TypeChoices), nameof(DefaultText), nameof(DefaultBool), nameof(DefaultChoice),
                     nameof(Access), nameof(IsGlobal),
                 })
        {
            OnPropertyChanged(property);
        }

        if (typeChanged)
        {
            foreach (string property in new[] { nameof(HasDefault), nameof(IsTextDefault), nameof(IsBooleanDefault), nameof(IsChoiceDefault), nameof(DefaultChoices) })
            {
                OnPropertyChanged(property);
            }
        }
    }
}

/// <summary>Valeur d'une énumération.</summary>
public sealed class ConstantItemViewModel : ObservableObject, IIdentifiedItem
{
    private readonly DefinitionViewModel _owner;
    private EnumConstant _model;

    public ConstantItemViewModel(DefinitionViewModel owner, EnumConstant model)
    {
        _owner = owner;
        _model = model;
        RemoveCommand = new RelayCommand(() => _owner.RemoveConstant(_model.Id));
    }

    public Guid Id => _model.Id;

    public string Name
    {
        get => _model.Name;
        set => _owner.UpdateConstant(_model.Id, value ?? string.Empty);
    }

    /// <summary>Nom dans le code Java ("TRES_RARE").</summary>
    public string JavaName => ProgramNames.Constant(_model.Name);

    public ICommand RemoveCommand { get; }

    internal void Update(EnumConstant model)
    {
        _model = model;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(JavaName));
    }
}

/// <summary>Méthode d'une interface : nom, paramètres (texte "nom : type") et type renvoyé (ou rien).</summary>
public sealed class MethodSignatureItemViewModel : ObservableObject, IIdentifiedItem
{
    private readonly DefinitionViewModel _owner;
    private MethodSignature _model;

    public MethodSignatureItemViewModel(DefinitionViewModel owner, MethodSignature model)
    {
        _owner = owner;
        _model = model;
        RemoveCommand = new RelayCommand(() => _owner.RemoveMethod(_model.Id));
        AddParameterCommand = new RelayCommand(() => _owner.UpdateMethod(_model.Id,
            m => m with { Parameters = [.. m.Parameters, VariableDefinition.Create(Tr.F("parameter {0}", m.Parameters.Count + 1), DataType.Number)] },
            Tr.T("Add a parameter")));
    }

    public Guid Id => _model.Id;

    public string Name
    {
        get => _model.Name;
        set => _owner.UpdateMethod(_model.Id, m => m with { Name = value ?? string.Empty }, Tr.T("Rename a method"));
    }

    public IReadOnlyList<DataTypeChoice> TypeChoices => _owner.TypeChoices;

    /// <summary>Type de la valeur renvoyée (si la méthode en renvoie une).</summary>
    public DataTypeChoice? ReturnType
    {
        get => _model.Returns.Count == 0 ? null : _owner.TypeChoices.FirstOrDefault(c => c.Type == _model.Returns[0].Type);
        set
        {
            if (value is not null)
            {
                _owner.UpdateMethod(_model.Id,
                    m => m with { Returns = [m.Returns.FirstOrDefault() is { } r ? r with { Type = value.Type } : VariableDefinition.Create(Tr.T("value"), value.Type)] },
                    Tr.T("Change return type"));
            }
        }
    }

    public bool ReturnsValue
    {
        get => _model.Returns.Count > 0;
        set => _owner.UpdateMethod(_model.Id,
            m => m with { Returns = value ? [VariableDefinition.Create(Tr.T("value"), DataType.Number)] : [] },
            Tr.T("Return value"));
    }

    public IReadOnlyList<MethodParameterViewModel> Parameters => [.. _model.Parameters.Select(p => new MethodParameterViewModel(this, p))];

    public ICommand RemoveCommand { get; }

    public ICommand AddParameterCommand { get; }

    internal void UpdateParameter(Guid id, Func<VariableDefinition, VariableDefinition> change, string description) =>
        _owner.UpdateMethod(_model.Id, m => m with { Parameters = [.. m.Parameters.Select(p => p.Id == id ? change(p) : p)] }, description);

    internal void RemoveParameter(Guid id) =>
        _owner.UpdateMethod(_model.Id, m => m with { Parameters = [.. m.Parameters.Where(p => p.Id != id)] }, Tr.T("Delete a parameter"));

    internal void Update(MethodSignature model)
    {
        bool structure = !model.Parameters.Select(p => p.Id).SequenceEqual(_model.Parameters.Select(p => p.Id));
        _model = model;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ReturnType));
        OnPropertyChanged(nameof(ReturnsValue));
        if (structure)
        {
            OnPropertyChanged(nameof(Parameters));
        }
    }
}

/// <summary>Paramètre d'une méthode d'interface.</summary>
public sealed class MethodParameterViewModel : ObservableObject
{
    private readonly MethodSignatureItemViewModel _method;
    private readonly VariableDefinition _model;

    public MethodParameterViewModel(MethodSignatureItemViewModel method, VariableDefinition model)
    {
        _method = method;
        _model = model;
        RemoveCommand = new RelayCommand(() => _method.RemoveParameter(_model.Id));
    }

    public string Name
    {
        get => _model.Name;
        set => _method.UpdateParameter(_model.Id, p => p with { Name = value ?? string.Empty }, Tr.T("Rename a parameter"));
    }

    public IReadOnlyList<DataTypeChoice> TypeChoices => _method.TypeChoices;

    public DataTypeChoice? Type
    {
        get => _method.TypeChoices.FirstOrDefault(c => c.Type == _model.Type);
        set
        {
            if (value is not null)
            {
                _method.UpdateParameter(_model.Id, p => p with { Type = value.Type }, Tr.T("Change type"));
            }
        }
    }

    public ICommand RemoveCommand { get; }
}

/// <summary>Interface que la classe peut implémenter (case cochée).</summary>
public sealed class InterfaceChoiceViewModel : ObservableObject
{
    private readonly DefinitionViewModel _owner;
    private readonly Guid _id;
    private bool _isImplemented;

    public InterfaceChoiceViewModel(DefinitionViewModel owner, Guid id, string name, bool isImplemented)
    {
        _owner = owner;
        _id = id;
        Name = name;
        _isImplemented = isImplemented;
    }

    public string Name { get; }

    public bool IsImplemented
    {
        get => _isImplemented;
        set
        {
            if (SetProperty(ref _isImplemented, value))
            {
                _owner.SetInterface(_id, value);
            }
        }
    }
}

/// <summary>Définition intégrée à une classe (méthode, type imbriqué), avec un bouton pour l'ouvrir.</summary>
public sealed class MemberItemViewModel
{
    public MemberItemViewModel(GraphAsset member, Action open)
    {
        Name = ProgramModel.Name(member);
        KindLabel = member.Kind.Label() + (member.IsStatic && member.Kind.IsCallable() ? Tr.T(" static") : string.Empty) + (member.IsPure ? Tr.T(" (pure)") : string.Empty);
        AccessLabel = member.Access.Label();
        OpenCommand = new RelayCommand(open);
    }

    public string Name { get; }

    public string KindLabel { get; }

    public string AccessLabel { get; }

    public ICommand OpenCommand { get; }
}
