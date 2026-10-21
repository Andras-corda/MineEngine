using System.Globalization;
using MineEngine.Assets.Puppeteer;
using MineEngine.Core.Assets;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.ViewModels.Puppeteer;

/// <summary>Input field of a setting in the Puppeteer Details panel.</summary>
public enum BtEditorKind
{
    Number,
    Boolean,
    Choice,
    Key,
    Sound,
    Goal,
}

/// <summary>Key offered by a key setting; <see cref="Id"/> null for "none".</summary>
public sealed record BtKeyOption(Guid? Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// A setting of the selected node, decorator or service. Every change goes through the
/// document (one undoable step; successive keystrokes in a field make a single one).
/// </summary>
public sealed class BtParameterViewModel : ObservableObject
{
    private readonly BtItemViewModel _item;
    private string _numberText;

    public BtParameterViewModel(BtItemViewModel item, BtParameterInfo info, AssetEditingContext context)
    {
        _item = item;
        Info = info;
        _numberText = BtText.FormatUserNumber(info, item.GetValue(info.Name));
        Editor = info.Type switch
        {
            BtParameterType.Number or BtParameterType.Integer => BtEditorKind.Number,
            BtParameterType.Boolean => BtEditorKind.Boolean,
            BtParameterType.Choice => BtEditorKind.Choice,
            BtParameterType.Key => BtEditorKind.Key,
            BtParameterType.Goal => BtEditorKind.Goal,
            _ => BtEditorKind.Sound,
        };
        if (Editor == BtEditorKind.Sound)
        {
            Picker = new ReferencePickerViewModel(
                context,
                ReferenceDomain.Sound,
                () => ContentReference.TryParseStorageText(item.GetValue(info.Name), out ContentReference? reference) ? reference : null,
                reference => Set(reference?.ToStorageText() ?? string.Empty));
        }
    }

    public BtParameterInfo Info { get; }

    public string Name => Info.Name;

    public string Label => Info.Label;

    public string? Hint => Info.Hint;

    public bool HasHint => !string.IsNullOrEmpty(Info.Hint);

    public BtEditorKind Editor { get; }

    public ReferencePickerViewModel? Picker { get; }

    /// <summary>Entered number (comma or dot); applied only when valid, clamped to the bounds.</summary>
    public string NumberText
    {
        get => _numberText;
        set
        {
            if (SetProperty(ref _numberText, value ?? string.Empty) && BtText.ParseUserNumber(_numberText) is { } number)
            {
                Set(BtCatalog.Format(Info.ParseNumber(BtCatalog.Format(number)) ?? number));
            }
        }
    }

    public string Range => Info.Min > double.MinValue / 2 && Info.Max < double.MaxValue / 2
        ? string.Create(CultureInfo.CurrentCulture, $"{Info.Min:0.##} – {Info.Max:0.##}")
        : string.Empty;

    public bool BoolValue
    {
        get => _item.GetValue(Name) == "true";
        set => Set(value ? "true" : "false");
    }

    public IReadOnlyList<BtChoice> Choices => Info.Choices;

    public BtChoice? ChoiceValue
    {
        get => Info.Choices.FirstOrDefault(c => c.Value == _item.GetValue(Name));
        set
        {
            if (value is not null)
            {
                Set(value.Value);
            }
        }
    }

    /// <summary>Blackboard keys of an accepted type (and "none" for an optional setting).</summary>
    public IReadOnlyList<BtKeyOption> KeyOptions
    {
        get
        {
            List<BtKeyOption> options = [.. _item.Document.Keys
                .Where(k => Info.AcceptsKey(k.Model.Type))
                .Select(k => new BtKeyOption(k.Id, $"{k.Model.Name} ({BtCatalog.KeyTypeLabel(k.Model.Type)})"))];
            if (Info.Optional)
            {
                options.Insert(0, new BtKeyOption(null, Tr.T("(none)")));
            }

            return options;
        }
    }

    public BtKeyOption? KeyValue
    {
        get
        {
            string text = _item.GetValue(Name);
            Guid? id = Guid.TryParse(text, out Guid parsed) ? parsed : null;
            return KeyOptions.FirstOrDefault(o => o.Id == id);
        }
        set
        {
            if (value is not null)
            {
                Set(value.Id?.ToString("D") ?? string.Empty);
            }
        }
    }

    /// <summary>Public goals that the node can run.</summary>
    public IReadOnlyList<Choice<Guid>> GoalOptions =>
        [.. _item.Document.PublicGoals.Select(g => new Choice<Guid>(g.Id, string.IsNullOrWhiteSpace(g.DisplayName) ? g.ResourceId.Value : g.DisplayName))];

    public Guid GoalValue
    {
        get => Guid.TryParse(_item.GetValue(Name), out Guid id) ? id : Guid.Empty;
        set
        {
            if (value != Guid.Empty)
            {
                Set(value.ToString("D"));
            }
        }
    }

    /// <summary>Help shown when the project has no public goal yet.</summary>
    public bool HasNoGoal => Editor == BtEditorKind.Goal && _item.Document.PublicGoals.Count == 0;

    /// <summary>Help shown when no key fits: one must be created.</summary>
    public bool HasNoCompatibleKey => Editor == BtEditorKind.Key && !_item.Document.Keys.Any(k => Info.AcceptsKey(k.Model.Type));

    public string MissingKeyHint => Tr.F("Add a \"{0}\" key in the Blackboard.", string.Join(" / ", (Info.KeyTypes.Count == 0 ? Enum.GetValues<BlackboardKeyType>() : [.. Info.KeyTypes]).Select(BtCatalog.KeyTypeLabel)));

    /// <summary>The value changed (typing, undo): the fields are updated.</summary>
    internal void Refresh()
    {
        string current = _item.GetValue(Name);
        if (BtText.ParseUserNumber(_numberText) is not { } typed || BtCatalog.Format(Info.ParseNumber(BtCatalog.Format(typed)) ?? typed) != current)
        {
            _numberText = BtText.FormatUserNumber(Info, current);
            OnPropertyChanged(nameof(NumberText));
        }

        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(ChoiceValue));
        OnPropertyChanged(nameof(KeyOptions));
        OnPropertyChanged(nameof(KeyValue));
        OnPropertyChanged(nameof(HasNoCompatibleKey));
        OnPropertyChanged(nameof(GoalOptions));
        OnPropertyChanged(nameof(GoalValue));
        OnPropertyChanged(nameof(HasNoGoal));
        Picker?.Refresh();
    }

    private void Set(string value) => _item.Document.SetValue(_item, Name, value);

    public override string ToString() => $"{_item.Title}.{Label}";
}

/// <summary>A blackboard key in the Blackboard panel: editable name and type.</summary>
public sealed class BlackboardKeyViewModel : ObservableObject
{
    private readonly BehaviorTreeDocumentViewModel _document;
    private BlackboardKey _model;
    private string _name;
    private int _usages;

    public BlackboardKeyViewModel(BehaviorTreeDocumentViewModel document, BlackboardKey model)
    {
        _document = document;
        _model = model;
        _name = model.Name;
    }

    public BlackboardKey Model => _model;

    public Guid Id => _model.Id;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                _document.RenameKey(this, _name);
            }
        }
    }

    public static IReadOnlyList<Choice<BlackboardKeyType>> Types { get; } =
        [.. Enum.GetValues<BlackboardKeyType>().Select(t => new Choice<BlackboardKeyType>(t, BtCatalog.KeyTypeLabel(t)))];

    public Choice<BlackboardKeyType>? Type
    {
        get => Types.FirstOrDefault(t => t.Value == _model.Type);
        set
        {
            if (value is not null && value.Value != _model.Type)
            {
                _document.ChangeKeyType(this, value.Value);
            }
        }
    }

    /// <summary>Number of settings that use the key.</summary>
    public int Usages
    {
        get => _usages;
        set
        {
            if (SetProperty(ref _usages, value))
            {
                OnPropertyChanged(nameof(UsageLabel));
            }
        }
    }

    public string UsageLabel => _usages switch
    {
        0 => Tr.T("unused"),
        1 => Tr.T("1 use"),
        _ => Tr.F("{0} uses", _usages),
    };

    internal void Update(BlackboardKey model)
    {
        _model = model;
        if (_name != model.Name)
        {
            _name = model.Name;
            OnPropertyChanged(nameof(Name));
        }

        OnPropertyChanged(nameof(Type));
    }

    public override string ToString() => _name;
}
