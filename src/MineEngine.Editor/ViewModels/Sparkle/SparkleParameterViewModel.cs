using System.Globalization;
using MineEngine.Assets;
using MineEngine.Assets.Sparkle;
using MineEngine.Core.Assets;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Puppeteer;

namespace MineEngine.Editor.ViewModels.Sparkle;

/// <summary>Input field of a module setting.</summary>
public enum SparkleEditorKind
{
    Number,
    Boolean,
    Choice,
    Vector,
    Texture,
    Color,
    EmitterId,

    /// <summary>Several textures, each one a line of the list.</summary>
    Textures,
}

/// <summary>
/// A setting of a module in the body of an emitter. Every change goes through the document (one
/// undoable step; successive keystrokes in a field make a single one).
/// </summary>
public sealed class SparkleParameterViewModel : ObservableObject
{
    /// <summary>Particle textures of Minecraft offered in the texture field.</summary>
    public static IReadOnlyList<string> VanillaSprites { get; } =
    [
        .. Enumerable.Range(0, 8).Select(i => $"minecraft:glitter_{i}"),
        .. Enumerable.Range(0, 8).Select(i => $"minecraft:generic_{i}"),
        .. Enumerable.Range(0, 8).Select(i => $"minecraft:spark_{i}"),
        .. Enumerable.Range(0, 8).Select(i => $"minecraft:effect_{i}"),
        .. Enumerable.Range(0, 8).Select(i => $"minecraft:spell_{i}"),
        "minecraft:flame", "minecraft:soul_fire_flame", "minecraft:flash", "minecraft:glow", "minecraft:heart", "minecraft:angry",
        "minecraft:critical_hit", "minecraft:enchanted_hit", "minecraft:note", "minecraft:bubble", "minecraft:lava", "minecraft:damage",
        "minecraft:nautilus", "minecraft:drip_hang", "minecraft:drip_fall", "minecraft:drip_land",
        .. Enumerable.Range(0, 12).Select(i => $"minecraft:big_smoke_{i}"),
        .. Enumerable.Range(0, 16).Select(i => $"minecraft:explosion_{i}"),
        .. Enumerable.Range(0, 12).Select(i => $"minecraft:cherry_{i}"),
    ];

    private readonly SparkleModuleViewModel _module;
    private string _numberText = string.Empty;
    private string _x = string.Empty;
    private string _y = string.Empty;
    private string _z = string.Empty;
    private string _colorText = string.Empty;
    private string _textureText = string.Empty;

    public SparkleParameterViewModel(SparkleModuleViewModel module, SparkleParameterInfo info)
    {
        _module = module;
        Info = info;
        Editor = info.Type switch
        {
            SparkleParameterType.Number or SparkleParameterType.Integer => SparkleEditorKind.Number,
            SparkleParameterType.Boolean => SparkleEditorKind.Boolean,
            SparkleParameterType.Choice => SparkleEditorKind.Choice,
            SparkleParameterType.Vector => SparkleEditorKind.Vector,
            SparkleParameterType.Texture => SparkleEditorKind.Texture,
            SparkleParameterType.Color => SparkleEditorKind.Color,
            SparkleParameterType.TextureList => SparkleEditorKind.Textures,
            _ => SparkleEditorKind.EmitterId,
        };
        AddTextureCommand = new RelayCommand(() => Set(SparkleCatalog.JoinTextures([.. CurrentTextures, SparkleCatalog.DefaultSprite])));
        LoadTexts();
    }

    public SparkleParameterInfo Info { get; }

    public string Name => Info.Name;

    public string Label => Info.Label;

    public string? Hint => Info.Hint;

    public bool HasHint => !string.IsNullOrEmpty(Info.Hint);

    public SparkleEditorKind Editor { get; }

    private string Value => _module.Model.GetValue(Name);

    // ----- Link to a user parameter -----

    /// <summary>Numbers, vectors, colors and boxes can be linked to a user parameter of the system.</summary>
    public bool CanBind => Info.Type is SparkleParameterType.Number or SparkleParameterType.Integer or SparkleParameterType.Vector
        or SparkleParameterType.Color or SparkleParameterType.Boolean;

    /// <summary>Parameters of the system of the type of the setting, after "No link".</summary>
    public IReadOnlyList<Choice<string>> BindingOptions =>
    [
        new(string.Empty, Tr.T("No link")),
        .. _module.Emitter.Document.Asset.UserParameters.Where(p => p.Accepts(Info.Type)).Select(p => new Choice<string>(p.Binding, "→ " + p.Name)),
    ];

    /// <summary>The setting takes the value of a user parameter: its field is replaced by the name of the parameter.</summary>
    public bool IsBound => SparkleUserParameter.BoundId(Value) is not null;

    public bool HasBindingOptions => CanBind && (IsBound || _module.Emitter.Document.Asset.UserParameters.Any(p => p.Accepts(Info.Type)));

    /// <summary>Linked parameter ("@id"), or "" for a plain value; unlinking keeps the value the parameter had.</summary>
    public string BindingValue
    {
        get => IsBound ? Value : string.Empty;
        set
        {
            value ??= string.Empty;
            if (value == BindingValue)
            {
                return;
            }

            if (value.Length > 0)
            {
                Set(value);
            }
            else
            {
                SparkleUserParameter? linked = BoundParameter;
                Set(linked is not null && linked.Accepts(Info.Type) ? linked.Value : Info.DefaultValue);
            }
        }
    }

    private SparkleUserParameter? BoundParameter =>
        SparkleUserParameter.BoundId(Value) is { } id ? _module.Emitter.Document.Asset.UserParameters.FirstOrDefault(p => p.Id == id) : null;

    /// <summary>The user parameters changed: the choices and the name shown follow.</summary>
    internal void RefreshBinding()
    {
        OnPropertyChanged(nameof(BindingOptions));
        OnPropertyChanged(nameof(BindingValue));
        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(HasBindingOptions));
        OnPropertyChanged(nameof(ShortValue));
        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(BindingValue));
        OnPropertyChanged(nameof(HasBindingOptions));
    }

    /// <summary>Shown only when the choice it depends on has the right value (radius of a sphere...).</summary>
    public bool IsVisible => Info.IsVisible(_module.Model.GetValue);

    // ----- Number -----

    /// <summary>Entered number (comma or dot); applied only when valid, clamped to the bounds.</summary>
    public string NumberText
    {
        get => _numberText;
        set
        {
            if (SetProperty(ref _numberText, value ?? string.Empty) && BtText.ParseUserNumber(_numberText) is { } number)
            {
                Set(Format(Info.ParseNumber(Format(number)) ?? number));
            }
        }
    }

    public string Range => Info.Min > double.MinValue / 2 && Info.Max < double.MaxValue / 2 && Editor == SparkleEditorKind.Number
        ? string.Create(CultureInfo.CurrentCulture, $"{Info.Min:0.##} – {Info.Max:0.##}")
        : string.Empty;

    // ----- Boolean and choice -----

    public bool BoolValue
    {
        get => Value == "true";
        set => Set(value ? "true" : "false");
    }

    public IReadOnlyList<SparkleChoice> Choices => Info.Choices;

    public SparkleChoice? ChoiceValue
    {
        get => Info.Choices.FirstOrDefault(c => c.Value == _module.Model.Choice(Name));
        set
        {
            if (value is not null)
            {
                Set(value.Value);
            }
        }
    }

    // ----- Vector -----

    public string X { get => _x; set => SetAxis(ref _x, value); }

    public string Y { get => _y; set => SetAxis(ref _y, value); }

    public string Z { get => _z; set => SetAxis(ref _z, value); }

    private void SetAxis(ref string field, string value)
    {
        if (!SetProperty(ref field, value ?? string.Empty))
        {
            return;
        }

        if (BtText.ParseUserNumber(_x) is { } x && BtText.ParseUserNumber(_y) is { } y && BtText.ParseUserNumber(_z) is { } z)
        {
            double Clamp(double v) => Math.Clamp(v, Info.Min, Info.Max);
            Set(SparkleVector.Format(new SparkleVector(Clamp(x), Clamp(y), Clamp(z))));
        }
    }

    // ----- Color -----

    /// <summary>"#RRGGBB"; applied only when valid.</summary>
    public string ColorText
    {
        get => _colorText;
        set
        {
            if (SetProperty(ref _colorText, value ?? string.Empty))
            {
                string text = _colorText.Trim();
                if (!text.StartsWith('#'))
                {
                    text = "#" + text;
                }

                if (text.Length == 7 && int.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                {
                    Set(text.ToUpperInvariant());
                }

                OnPropertyChanged(nameof(ColorPreview));
            }
        }
    }

    /// <summary>Color shown next to the field.</summary>
    public string ColorPreview => "#" + _module.Model.Color(Name).ToString("X6", CultureInfo.InvariantCulture);

    // ----- Texture -----

    /// <summary>Textures of the project (by name), then the particle textures of Minecraft.</summary>
    public IReadOnlyList<string> TextureSuggestions =>
        [.. _module.Emitter.Document.Assets.OfType<TextureAsset>().Select(TextureName).Order(StringComparer.Ordinal), .. VanillaSprites];

    /// <summary>
    /// Name of a texture of the project ("ruby_spark") or texture of the game ("minecraft:flame").
    /// A name without namespace is a texture of the project.
    /// </summary>
    public string TextureText
    {
        get => _textureText;
        set
        {
            if (!SetProperty(ref _textureText, value ?? string.Empty))
            {
                return;
            }

            string text = _textureText.Trim();
            if (_module.Emitter.Document.Assets.OfType<TextureAsset>().FirstOrDefault(t => TextureName(t) == text) is { } texture)
            {
                Set(ContentReference.ToAsset(texture.Id).ToStorageText());
            }
            else if (text.Contains(':', StringComparison.Ordinal) && ContentReference.TryParseStorageText(text, out ContentReference? reference)
                     && reference!.Kind == ContentReferenceKind.Id)
            {
                Set(reference.ToStorageText());
            }
        }
    }

    private static string TextureName(TextureAsset texture) => texture.ResourceId.Value;

    private string DisplayTexture(string value)
    {
        if (ContentReference.TryParseStorageText(value, out ContentReference? reference) && reference!.Kind == ContentReferenceKind.Asset)
        {
            return _module.Emitter.Document.Assets.Find(reference.AssetId) is TextureAsset texture ? TextureName(texture) : string.Empty;
        }

        return value;
    }

    // ----- Emitter ID -----

    /// <summary>The other emitters of the system, by ID.</summary>
    public IReadOnlyList<Choice<int>> EmitterOptions =>
        [.. _module.Emitter.Document.Emitters.Where(e => e != _module.Emitter).Select(e => new Choice<int>(e.EventId, $"{e.EventId} – {e.Name}"))];

    public int EmitterValue
    {
        get => _module.Model.Integer(Name);
        set => Set(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Value in a few characters, for the line of the module on the node (empty: not shown).</summary>
    public string ShortValue => IsBound ? "@" + (BoundParameter?.Name ?? "?") : Editor switch
    {
        SparkleEditorKind.Number => Info.ParseNumber(Value) is { } number ? number.ToString("0.##", CultureInfo.CurrentCulture) : Value,
        SparkleEditorKind.Boolean => BoolValue ? Label : string.Empty,
        SparkleEditorKind.Choice => ChoiceValue?.Label ?? Value,
        SparkleEditorKind.Vector => string.Join(" ", new[] { _module.Model.Vector(Name).X, _module.Model.Vector(Name).Y, _module.Model.Vector(Name).Z }
            .Select(v => v.ToString("0.##", CultureInfo.CurrentCulture))),
        SparkleEditorKind.Texture => DisplayTexture(Value).Replace("minecraft:", string.Empty, StringComparison.Ordinal),
        SparkleEditorKind.Color => Value,
        SparkleEditorKind.Textures => string.Join(" ", SparkleCatalog.SplitTextures(Value).Select(DisplayTexture)).Replace("minecraft:", string.Empty, StringComparison.Ordinal),
        _ => Tr.F("ID {0}", EmitterValue),
    };

    // ----- Texture list -----

    /// <summary>One line per texture: a name of the project or a texture of the game, removed with its button.</summary>
    public System.Collections.ObjectModel.ObservableCollection<SparkleTextureItemViewModel> Textures { get; } = [];

    public System.Windows.Input.ICommand AddTextureCommand { get; }

    private IReadOnlyList<string> CurrentTextures => SparkleCatalog.SplitTextures(Value);

    /// <summary>A line of the list was typed: the stored list is written again (only the lines that mean a texture).</summary>
    internal void TextureTyped()
    {
        IReadOnlyList<string> current = CurrentTextures;
        Set(SparkleCatalog.JoinTextures(Textures.Select((t, i) => ToStorage(t.Text) ?? (i < current.Count ? current[i] : string.Empty))));
    }

    internal void RemoveTexture(SparkleTextureItemViewModel item)
    {
        int index = Textures.IndexOf(item);
        IReadOnlyList<string> current = CurrentTextures;
        if (index >= 0 && current.Count > 1)
        {
            Set(SparkleCatalog.JoinTextures(current.Where((_, i) => i != index)));
        }
    }

    /// <summary>Storage text of a typed texture: a texture of the project by its name, or a "namespace:path" of the game; null if it means nothing yet.</summary>
    private string? ToStorage(string typed)
    {
        string text = typed.Trim();
        if (_module.Emitter.Document.Assets.OfType<TextureAsset>().FirstOrDefault(t => TextureName(t) == text) is { } texture)
        {
            return ContentReference.ToAsset(texture.Id).ToStorageText();
        }

        return text.Contains(':', StringComparison.Ordinal) && ContentReference.TryParseStorageText(text, out ContentReference? reference)
               && reference!.Kind == ContentReferenceKind.Id
            ? reference.ToStorageText()
            : null;
    }

    /// <summary>The value changed (typing, undo): the fields are updated.</summary>
    internal void Refresh()
    {
        LoadTexts();
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(ChoiceValue));
        OnPropertyChanged(nameof(ColorPreview));
        OnPropertyChanged(nameof(EmitterOptions));
        OnPropertyChanged(nameof(EmitterValue));
        OnPropertyChanged(nameof(ShortValue));
    }

    /// <summary>Reloads the texts of the fields, unless the one being typed already means the stored value.</summary>
    private void LoadTexts()
    {
        string value = Value;
        switch (Editor)
        {
            case SparkleEditorKind.Number:
                if (BtText.ParseUserNumber(_numberText) is not { } typed || Format(Info.ParseNumber(Format(typed)) ?? typed) != value)
                {
                    _numberText = Info.ParseNumber(value) is { } number ? number.ToString("0.###", CultureInfo.CurrentCulture) : value;
                    OnPropertyChanged(nameof(NumberText));
                }

                break;
            case SparkleEditorKind.Vector:
                SparkleVector vector = _module.Model.Vector(Name);
                LoadAxis(ref _x, vector.X, nameof(X));
                LoadAxis(ref _y, vector.Y, nameof(Y));
                LoadAxis(ref _z, vector.Z, nameof(Z));
                break;
            case SparkleEditorKind.Color:
                if (!string.Equals(_colorText.Trim().TrimStart('#'), value.TrimStart('#'), StringComparison.OrdinalIgnoreCase))
                {
                    _colorText = value;
                    OnPropertyChanged(nameof(ColorText));
                }

                break;
            case SparkleEditorKind.Textures:
                IReadOnlyList<string> textures = CurrentTextures;
                if (Textures.Count != textures.Count || Textures.Select(t => ToStorage(t.Text)).Where((s, i) => s != textures[i]).Any())
                {
                    Textures.Clear();
                    foreach (string texture in textures)
                    {
                        Textures.Add(new SparkleTextureItemViewModel(this, DisplayTexture(texture)));
                    }
                }

                foreach (SparkleTextureItemViewModel item in Textures)
                {
                    item.CanRemove = Textures.Count > 1;
                }

                break;
            case SparkleEditorKind.Texture:
                string shown = DisplayTexture(value);
                if (_textureText.Trim() != shown)
                {
                    _textureText = shown;
                    OnPropertyChanged(nameof(TextureText));
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

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private void Set(string value) => _module.SetValue(Name, value);

    public override string ToString() => $"{_module.Title}.{Label}";
}

/// <summary>A line of a texture list: the typed name, with the suggestions of the texture field.</summary>
public sealed class SparkleTextureItemViewModel : ObservableObject
{
    private readonly SparkleParameterViewModel _parameter;
    private string _text;
    private bool _canRemove;

    public SparkleTextureItemViewModel(SparkleParameterViewModel parameter, string text)
    {
        _parameter = parameter;
        _text = text;
        RemoveCommand = new RelayCommand(() => _parameter.RemoveTexture(this), () => _canRemove);
    }

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty))
            {
                _parameter.TextureTyped();
            }
        }
    }

    public IReadOnlyList<string> Suggestions => _parameter.TextureSuggestions;

    public bool CanRemove
    {
        get => _canRemove;
        internal set => SetProperty(ref _canRemove, value);
    }

    public System.Windows.Input.ICommand RemoveCommand { get; }
}
