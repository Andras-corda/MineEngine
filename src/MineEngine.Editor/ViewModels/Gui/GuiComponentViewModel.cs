using MineEngine.Assets;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.ViewModels.Gui;

/// <summary>
/// A component on the canvas of the GUI editor and in its list. Its fields are those of the
/// <see cref="GuiComponent"/>; every change goes through the editor, as an undoable step.
/// </summary>
public sealed class GuiComponentViewModel : ObservableObject
{
    private readonly GuiDocumentViewModel _owner;
    private GuiComponent _model;
    private bool _isSelected;
    private string? _nameError;
    private bool _isEditing;
    private string _editText = string.Empty;

    public GuiComponentViewModel(GuiDocumentViewModel owner, GuiComponent model)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public GuiComponent Model
    {
        get => _model;
        set
        {
            if (Equals(_model, value))
            {
                return;
            }

            _model = value;
            OnPropertyChanged(string.Empty);
        }
    }

    public Guid Id => _model.Id;

    public GuiComponentKind Kind => _model.Kind;

    public string KindLabel => LabelOf(Kind);

    public string Icon => IconOf(Kind);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Name
    {
        get => _model.Name;
        set
        {
            string name = (value ?? string.Empty).Trim();
            NameError = !GuiComponent.IsValidName(name)
                ? Tr.T("Letters, digits and _, not starting with a digit.")
                : _owner.IsNameUsed(name, Id) ? Tr.T("Another component already has this name.") : null;
            if (NameError is null)
            {
                _owner.Update(Id, c => c with { Name = name }, Tr.T("Rename the component"));
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Why the typed name was refused, or null.</summary>
    public string? NameError
    {
        get => _nameError;
        private set
        {
            if (SetProperty(ref _nameError, value))
            {
                OnPropertyChanged(nameof(HasNameError));
            }
        }
    }

    public bool HasNameError => _nameError is not null;

    public int X
    {
        get => _model.X;
        set => _owner.Update(Id, c => c with { X = value }, Tr.T("Move the component"));
    }

    public int Y
    {
        get => _model.Y;
        set => _owner.Update(Id, c => c with { Y = value }, Tr.T("Move the component"));
    }

    public int Width
    {
        get => _model.Width;
        set => _owner.Update(Id, c => c with { Width = value }, Tr.T("Resize the component"));
    }

    public int Height
    {
        get => _model.Height;
        set => _owner.Update(Id, c => c with { Height = value }, Tr.T("Resize the component"));
    }

    public bool CanResize => Kind != GuiComponentKind.Slot;

    public string Text
    {
        get => _model.Text;
        set => _owner.Update(Id, c => c with { Text = value ?? string.Empty }, Tr.T("Change the text"));
    }

    public string Color
    {
        get => _model.Color;
        set => _owner.Update(Id, c => c with { Color = value ?? "#404040" }, Tr.T("Change the color"));
    }

    /// <summary>The color of the text, to draw it.</summary>
    public System.Windows.Media.Brush TextBrush => MineEngine.Core.Theming.RgbColor.TryParse(_model.Color, out MineEngine.Core.Theming.RgbColor color)
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B))
        : System.Windows.Media.Brushes.DimGray;

    public string Tooltip
    {
        get => _model.Tooltip;
        set => _owner.Update(Id, c => c with { Tooltip = value ?? string.Empty }, Tr.T("Change the tooltip"));
    }

    public bool HasTooltip => _model.Tooltip.Length > 0;

    /// <summary>The text is typed on the canvas (double-click on a text, a button, a checkbox or a text input).</summary>
    public bool IsEditing
    {
        get => _isEditing;
        private set => SetProperty(ref _isEditing, value);
    }

    /// <summary>The text being typed on the canvas.</summary>
    public string EditText
    {
        get => _editText;
        set => SetProperty(ref _editText, value ?? string.Empty);
    }

    public void BeginEdit()
    {
        if (HasText)
        {
            EditText = Text;
            IsEditing = true;
        }
    }

    /// <summary>Ends the typing on the canvas: the text is kept (Enter, click elsewhere) or dropped (Escape).</summary>
    public void EndEdit(bool keep)
    {
        if (!IsEditing)
        {
            return;
        }

        IsEditing = false;
        if (keep)
        {
            Text = EditText;
        }
    }

    public Guid? TextureId
    {
        get => _model.TextureId;
        set => _owner.Update(Id, c => c with { TextureId = value }, Tr.T("Change the image"));
    }

    /// <summary>File of the texture of an image, to draw it; null without one.</summary>
    public string? ImagePath => _owner.TexturePath(_model.TextureId);

    public GuiSlotMode SlotMode
    {
        get => _model.SlotMode;
        set => _owner.Update(Id, c => c with { SlotMode = value }, Tr.T("Change the slot"));
    }

    public bool IsChecked
    {
        get => _model.Checked;
        set => _owner.Update(Id, c => c with { Checked = value }, Tr.T("Change the checkbox"));
    }

    /// <summary>The component is a variable of the graph of the GUI: its node gives it to the GUI nodes (V0.8.1).</summary>
    public bool IsVariable
    {
        get => _model.IsVariable;
        set => _owner.Update(Id, c => c with { IsVariable = value }, Tr.T("Make the component a variable"));
    }

    public int MaxLength
    {
        get => _model.MaxLength;
        set => _owner.Update(Id, c => c with { MaxLength = value }, Tr.T("Change the text input"));
    }

    // ----- Style (V0.8.1) -----

    public GuiStyle Style => _model.Style;

    private void SetStyle(Func<GuiStyle, GuiStyle> change, string description) =>
        _owner.Update(Id, c => c with { Style = change(c.Style) }, description);

    public GuiAlign Horizontal
    {
        get => Style.Horizontal;
        set => SetStyle(s => s with { Horizontal = value }, Tr.T("Change the alignment"));
    }

    public GuiAlign Vertical
    {
        get => Style.Vertical;
        set => SetStyle(s => s with { Vertical = value }, Tr.T("Change the alignment"));
    }

    public GuiAlign Justify
    {
        get => Style.Justify;
        set => SetStyle(s => s with { Justify = value }, Tr.T("Change the alignment"));
    }

    public int Padding
    {
        get => Style.Padding;
        set => SetStyle(s => s with { Padding = value }, Tr.T("Change the padding"));
    }

    public int Opacity
    {
        get => Style.Opacity;
        set => SetStyle(s => s with { Opacity = value }, Tr.T("Change the opacity"));
    }

    public int Rotation
    {
        get => Style.Rotation;
        set => SetStyle(s => s with { Rotation = value }, Tr.T("Change the rotation"));
    }

    public int FontScale
    {
        get => Style.FontScale;
        set => SetStyle(s => s with { FontScale = value }, Tr.T("Change the size of the text"));
    }

    public bool Wrap
    {
        get => Style.Wrap;
        set => SetStyle(s => s with { Wrap = value }, Tr.T("Change the lines of the text"));
    }

    public string HoverText
    {
        get => Style.HoverText;
        set => SetStyle(s => s with { HoverText = value ?? string.Empty }, Tr.T("Change the text under the mouse"));
    }

    /// <summary>A color under the mouse: the text then takes it.</summary>
    public bool HasHoverColor
    {
        get => Style.HoverColor.Length > 0;
        set => SetStyle(s => s with { HoverColor = value ? (s.HoverColor.Length > 0 ? s.HoverColor : "#FFFFA0") : string.Empty }, Tr.T("Change the color under the mouse"));
    }

    public string HoverColor
    {
        get => Style.HoverColor.Length > 0 ? Style.HoverColor : "#FFFFA0";
        set => SetStyle(s => s with { HoverColor = value ?? string.Empty }, Tr.T("Change the color under the mouse"));
    }

    public GuiImageSize ImageSize
    {
        get => Style.ImageSize;
        set => SetStyle(s => s with { ImageSize = value }, Tr.T("Change the size of the image"));
    }

    public string Tint
    {
        get => Style.Tint;
        set => SetStyle(s => s with { Tint = value ?? "#FFFFFF" }, Tr.T("Change the tint"));
    }

    public bool Tiling
    {
        get => Style.Tiling;
        set => SetStyle(s => s with { Tiling = value }, Tr.T("Change the tiling"));
    }

    public int Border
    {
        get => Style.Border;
        set => SetStyle(s => s with { Border = value }, Tr.T("Change the border"));
    }

    public string BorderColor
    {
        get => Style.BorderColor;
        set => SetStyle(s => s with { BorderColor = value ?? "#000000" }, Tr.T("Change the border"));
    }

    public bool Blur
    {
        get => Style.Blur;
        set => SetStyle(s => s with { Blur = value }, Tr.T("Change the blur"));
    }

    public bool Highlight
    {
        get => Style.Highlight;
        set => SetStyle(s => s with { Highlight = value }, Tr.T("Change the highlight"));
    }

    public GuiHoverEffect HoverEffect
    {
        get => Style.HoverEffect;
        set => SetStyle(s => s with { HoverEffect = value }, Tr.T("Change the effect under the mouse"));
    }

    public string HoverTint
    {
        get => Style.HoverTint;
        set => SetStyle(s => s with { HoverTint = value ?? "#FFFF80" }, Tr.T("Change the effect under the mouse"));
    }

    public bool HasHoverTint => Style.HoverEffect == GuiHoverEffect.Tint;

    // ----- Preview on the canvas -----

    public System.Windows.HorizontalAlignment PreviewHorizontal => Style.Horizontal switch
    {
        GuiAlign.Center => System.Windows.HorizontalAlignment.Center,
        GuiAlign.End => System.Windows.HorizontalAlignment.Right,
        _ => System.Windows.HorizontalAlignment.Left,
    };

    public System.Windows.VerticalAlignment PreviewVertical => Style.Vertical switch
    {
        GuiAlign.Center => System.Windows.VerticalAlignment.Center,
        GuiAlign.End => System.Windows.VerticalAlignment.Bottom,
        _ => System.Windows.VerticalAlignment.Top,
    };

    public System.Windows.TextAlignment PreviewJustify => Style.Justify switch
    {
        GuiAlign.Center => System.Windows.TextAlignment.Center,
        GuiAlign.End => System.Windows.TextAlignment.Right,
        _ => System.Windows.TextAlignment.Left,
    };

    public System.Windows.Media.AlignmentX PreviewImageX => Style.Horizontal switch
    {
        GuiAlign.Center => System.Windows.Media.AlignmentX.Center,
        GuiAlign.End => System.Windows.Media.AlignmentX.Right,
        _ => System.Windows.Media.AlignmentX.Left,
    };

    public System.Windows.Media.AlignmentY PreviewImageY => Style.Vertical switch
    {
        GuiAlign.Center => System.Windows.Media.AlignmentY.Center,
        GuiAlign.End => System.Windows.Media.AlignmentY.Bottom,
        _ => System.Windows.Media.AlignmentY.Top,
    };

    public System.Windows.Thickness PreviewPadding => new(Style.Padding);

    public double PreviewOpacity => Style.Opacity / 100.0;

    public double PreviewFontSize => 8 * Style.FontScale / 100.0;

    public System.Windows.TextWrapping PreviewWrap => Style.Wrap ? System.Windows.TextWrapping.Wrap : System.Windows.TextWrapping.NoWrap;

    public System.Windows.Media.Stretch PreviewStretch => Style.Tiling ? System.Windows.Media.Stretch.None : Style.ImageSize switch
    {
        GuiImageSize.Fit => System.Windows.Media.Stretch.Uniform,
        GuiImageSize.Original => System.Windows.Media.Stretch.None,
        _ => System.Windows.Media.Stretch.Fill,
    };

    public System.Windows.Media.TileMode PreviewTileMode => Style.Tiling ? System.Windows.Media.TileMode.Tile : System.Windows.Media.TileMode.None;

    /// <summary>One tile of the image (its file size, in pixels of the GUI) when it repeats or keeps its size; the whole component otherwise.</summary>
    public System.Windows.Rect PreviewBrushViewport
    {
        get
        {
            if (!Style.Tiling && Style.ImageSize != GuiImageSize.Original)
            {
                return new System.Windows.Rect(0, 0, 1, 1);
            }

            (int width, int height) = _owner.TextureSize(_model.TextureId);
            return new System.Windows.Rect(0, 0, width, height);
        }
    }

    public System.Windows.Media.BrushMappingMode PreviewViewportUnits =>
        Style.Tiling || Style.ImageSize == GuiImageSize.Original ? System.Windows.Media.BrushMappingMode.Absolute : System.Windows.Media.BrushMappingMode.RelativeToBoundingBox;

    public System.Windows.Media.Brush PreviewTint => Brush(Style.Tint, System.Windows.Media.Brushes.White);

    public bool HasTint => !string.Equals(Style.Tint, "#FFFFFF", StringComparison.OrdinalIgnoreCase);

    public System.Windows.Media.Brush PreviewBorderBrush => Brush(Style.BorderColor, System.Windows.Media.Brushes.Black);

    public System.Windows.Thickness PreviewBorder => new(Style.Border);

    /// <summary>An image, or a button that is an image.</summary>
    public bool HasImage => Kind == GuiComponentKind.Image || Kind == GuiComponentKind.Button && _model.TextureId is not null;

    public bool IsPlainButton => Kind == GuiComponentKind.Button && _model.TextureId is null;

    public bool IsImageButton => Kind == GuiComponentKind.Button && _model.TextureId is not null;

    /// <summary>The kinds whose content is placed in its box (all but a slot and a text input).</summary>
    public bool HasAlignment => Kind is not (GuiComponentKind.Slot or GuiComponentKind.TextInput);

    public bool HasImageLook => Kind is GuiComponentKind.Image or GuiComponentKind.Button;

    public bool HasRotation => Kind is GuiComponentKind.Text or GuiComponentKind.Image;

    private static System.Windows.Media.Brush Brush(string hex, System.Windows.Media.Brush fallback) =>
        MineEngine.Core.Theming.RgbColor.TryParse(hex, out MineEngine.Core.Theming.RgbColor color)
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B))
            : fallback;

    public static IReadOnlyList<Choice<GuiAlign>> HorizontalChoices { get; } =
        [new(GuiAlign.Start, Tr.T("Left")), new(GuiAlign.Center, Tr.T("Center")), new(GuiAlign.End, Tr.T("Right"))];

    public static IReadOnlyList<Choice<GuiAlign>> VerticalChoices { get; } =
        [new(GuiAlign.Start, Tr.T("Top")), new(GuiAlign.Center, Tr.T("Middle")), new(GuiAlign.End, Tr.T("Bottom"))];

    public static IReadOnlyList<Choice<GuiImageSize>> ImageSizeChoices { get; } =
    [
        new(GuiImageSize.Stretch, Tr.T("Stretched to the component")),
        new(GuiImageSize.Fit, Tr.T("Fitted (not deformed)")),
        new(GuiImageSize.Original, Tr.T("Size of the file")),
    ];

    public static IReadOnlyList<Choice<GuiHoverEffect>> HoverEffectChoices { get; } =
    [
        new(GuiHoverEffect.None, Tr.T("None")),
        new(GuiHoverEffect.Highlight, Tr.T("Highlight")),
        new(GuiHoverEffect.Blur, Tr.T("Blur")),
        new(GuiHoverEffect.Tint, Tr.T("Tint")),
    ];

    // Which settings the kind has, for the Properties of the editor.
    public bool HasText => Kind is GuiComponentKind.Text or GuiComponentKind.Button or GuiComponentKind.Checkbox or GuiComponentKind.TextInput;

    public bool HasColor => Kind is GuiComponentKind.Text or GuiComponentKind.Checkbox;

    public bool IsImage => Kind == GuiComponentKind.Image;

    public bool IsSlot => Kind == GuiComponentKind.Slot;

    public bool IsCheckbox => Kind == GuiComponentKind.Checkbox;

    public bool IsTextInput => Kind == GuiComponentKind.TextInput;

    public bool IsText => Kind == GuiComponentKind.Text;

    public bool IsButton => Kind == GuiComponentKind.Button;

    /// <summary>Label of the text field in Properties: a placeholder for a text input.</summary>
    public string TextLabel => Kind == GuiComponentKind.TextInput ? Tr.T("Hint (empty field)") : Tr.T("Text");

    public static string LabelOf(GuiComponentKind kind) => kind switch
    {
        GuiComponentKind.Text => Tr.T("Text"),
        GuiComponentKind.Image => Tr.T("Image"),
        GuiComponentKind.Button => Tr.T("Button"),
        GuiComponentKind.Slot => Tr.T("Slot"),
        GuiComponentKind.Checkbox => Tr.T("Checkbox"),
        _ => Tr.T("Text input"),
    };

    public static string IconOf(GuiComponentKind kind) => kind switch
    {
        GuiComponentKind.Text => "Glyph.Format",
        GuiComponentKind.Image => "Glyph.Texture",
        GuiComponentKind.Button => "Glyph.Run",
        GuiComponentKind.Slot => "Glyph.Package",
        GuiComponentKind.Checkbox => "Glyph.Check",
        _ => "Glyph.Rename",
    };

    public static IReadOnlyList<Choice<GuiSlotMode>> SlotModes { get; } =
    [
        new(GuiSlotMode.Input, Tr.T("Input: the player puts an item and takes it back")),
        new(GuiSlotMode.Output, Tr.T("Output: the player only takes the item")),
        new(GuiSlotMode.Display, Tr.T("Empty: shown only, the graphs fill it")),
    ];

    public override string ToString() => $"{_model.Name} ({KindLabel})";
}
