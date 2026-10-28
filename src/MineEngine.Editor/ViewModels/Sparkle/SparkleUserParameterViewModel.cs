using System.Globalization;
using System.Windows.Input;
using MineEngine.Assets.Sparkle;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Puppeteer;

namespace MineEngine.Editor.ViewModels.Sparkle;

/// <summary>
/// A user parameter of the system, in the list under the preview: its name, its type and its value
/// (number, vector, color or box). Module settings linked to it take its value.
/// </summary>
public sealed class SparkleUserParameterViewModel : ObservableObject
{
    public static IReadOnlyList<Choice<SparkleUserParameterType>> Types { get; } =
    [
        new(SparkleUserParameterType.Number, Tr.T("Number")),
        new(SparkleUserParameterType.Vector, Tr.T("Vector")),
        new(SparkleUserParameterType.Color, Tr.T("Color")),
        new(SparkleUserParameterType.Boolean, Tr.T("Yes / no")),
    ];

    private readonly SparkleDocumentViewModel _document;
    private SparkleUserParameter _model;
    private string _name;
    private string _numberText = string.Empty;
    private string _x = string.Empty;
    private string _y = string.Empty;
    private string _z = string.Empty;
    private string _colorText = string.Empty;

    public SparkleUserParameterViewModel(SparkleDocumentViewModel document, SparkleUserParameter model)
    {
        _document = document;
        _model = model;
        _name = model.Name;
        DeleteCommand = new RelayCommand(() => _document.DeleteUserParameter(Id));
        LoadTexts();
    }

    public Guid Id => _model.Id;

    public SparkleUserParameter Model => _model;

    /// <summary>Name shown in the settings linked to it; typing makes a single undoable change.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                _document.ChangeUserParameter(Id, p => p with { Name = _name }, Tr.T("Rename a user parameter"), "name");
            }
        }
    }

    public IReadOnlyList<Choice<SparkleUserParameterType>> TypeChoices => Types;

    /// <summary>Changing the type gives the default value of the new type (the linked settings of another type fall back to their default).</summary>
    public Choice<SparkleUserParameterType>? Type
    {
        get => Types.FirstOrDefault(t => t.Value == _model.Type);
        set
        {
            if (value is not null && value.Value != _model.Type)
            {
                _document.ChangeUserParameter(Id, p => p with { Type = value.Value, Value = SparkleUserParameter.DefaultValue(value.Value) },
                    Tr.T("Change the type of a user parameter"));
            }
        }
    }

    public bool IsNumber => _model.Type == SparkleUserParameterType.Number;

    public bool IsVector => _model.Type == SparkleUserParameterType.Vector;

    public bool IsColor => _model.Type == SparkleUserParameterType.Color;

    public bool IsBoolean => _model.Type == SparkleUserParameterType.Boolean;

    public string NumberText
    {
        get => _numberText;
        set
        {
            if (SetProperty(ref _numberText, value ?? string.Empty) && BtText.ParseUserNumber(_numberText) is { } number)
            {
                SetValue(number.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    public string X { get => _x; set => SetAxis(ref _x, value); }

    public string Y { get => _y; set => SetAxis(ref _y, value); }

    public string Z { get => _z; set => SetAxis(ref _z, value); }

    public string ColorText
    {
        get => _colorText;
        set
        {
            if (!SetProperty(ref _colorText, value ?? string.Empty))
            {
                return;
            }

            string text = "#" + _colorText.Trim().TrimStart('#');
            if (text.Length == 7 && int.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                SetValue(text.ToUpperInvariant());
            }
        }
    }

    public string ColorPreview => _model.Type == SparkleUserParameterType.Color ? _model.Value : "#FFFFFF";

    public bool BoolValue
    {
        get => _model.Value == "true";
        set => SetValue(value ? "true" : "false");
    }

    public ICommand DeleteCommand { get; }

    /// <summary>The parameter changed (edit, undo): the fields that do not hold the typed text are updated.</summary>
    internal void Update(SparkleUserParameter model)
    {
        bool typeChanged = model.Type != _model.Type;
        _model = model;
        if (_name != model.Name)
        {
            _name = model.Name;
            OnPropertyChanged(nameof(Name));
        }

        if (typeChanged)
        {
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(IsNumber));
            OnPropertyChanged(nameof(IsVector));
            OnPropertyChanged(nameof(IsColor));
            OnPropertyChanged(nameof(IsBoolean));
        }

        LoadTexts();
        OnPropertyChanged(nameof(ColorPreview));
        OnPropertyChanged(nameof(BoolValue));
    }

    private void SetAxis(ref string field, string value)
    {
        if (SetProperty(ref field, value ?? string.Empty)
            && BtText.ParseUserNumber(_x) is { } x && BtText.ParseUserNumber(_y) is { } y && BtText.ParseUserNumber(_z) is { } z)
        {
            SetValue(SparkleVector.Format(new SparkleVector(x, y, z)));
        }
    }

    private void SetValue(string value)
    {
        if (_model.Value != value)
        {
            _document.ChangeUserParameter(Id, p => p with { Value = value }, Tr.F("Change \"{0}\"", _model.Name), "value");
        }
    }

    /// <summary>Reloads the texts of the fields, unless the one being typed already means the stored value.</summary>
    private void LoadTexts()
    {
        switch (_model.Type)
        {
            case SparkleUserParameterType.Number:
                double stored = double.TryParse(_model.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) ? n : 0;
                if (BtText.ParseUserNumber(_numberText) is not { } typed || Math.Abs(typed - stored) > 1e-9)
                {
                    _numberText = stored.ToString("0.###", CultureInfo.CurrentCulture);
                    OnPropertyChanged(nameof(NumberText));
                }

                break;
            case SparkleUserParameterType.Vector:
                SparkleVector vector = SparkleVector.Parse(_model.Value) ?? SparkleVector.Zero;
                LoadAxis(ref _x, vector.X, nameof(X));
                LoadAxis(ref _y, vector.Y, nameof(Y));
                LoadAxis(ref _z, vector.Z, nameof(Z));
                break;
            case SparkleUserParameterType.Color:
                if (!string.Equals(_colorText.Trim().TrimStart('#'), _model.Value.TrimStart('#'), StringComparison.OrdinalIgnoreCase))
                {
                    _colorText = _model.Value;
                    OnPropertyChanged(nameof(ColorText));
                }

                break;
        }
    }

    private void LoadAxis(ref string field, double value, string propertyName)
    {
        if (BtText.ParseUserNumber(field) is { } typed && Math.Abs(typed - value) < 1e-9)
        {
            return;
        }

        field = value.ToString("0.###", CultureInfo.CurrentCulture);
        OnPropertyChanged(propertyName);
    }

    public override string ToString() => _name;
}
