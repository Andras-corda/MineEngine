using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>Kinds of the components of a GUI (V0.8.0).</summary>
public enum GuiComponentKind
{
    /// <summary>A line of text.</summary>
    Text,

    /// <summary>A texture of the project, stretched to the size of the component.</summary>
    Image,

    /// <summary>A button with a text; clicking it is an event of the GUI.</summary>
    Button,

    /// <summary>A slot of 18 x 18 pixels where an item is put or taken.</summary>
    Slot,

    /// <summary>A box with a text, ticked or not.</summary>
    Checkbox,

    /// <summary>A field where the player types a text.</summary>
    TextInput,
}

/// <summary>What the player can do with a slot.</summary>
public enum GuiSlotMode
{
    /// <summary>The player puts an item in it and takes it back.</summary>
    Input,

    /// <summary>The player only takes the item out (the result of a machine).</summary>
    Output,

    /// <summary>Empty for the player: shown only, he can neither put nor take; the graphs fill it (V0.8.1).</summary>
    Display,
}

/// <summary>Place of a content in its box, or of the lines of a text between them (V0.8.1).</summary>
public enum GuiAlign
{
    Start,
    Center,
    End,
}

/// <summary>How an image fills its component (V0.8.1).</summary>
public enum GuiImageSize
{
    /// <summary>Stretched to the size of the component.</summary>
    Stretch,

    /// <summary>As big as possible without being cut nor deformed.</summary>
    Fit,

    /// <summary>The size of the file, placed by the alignment.</summary>
    Original,
}

/// <summary>What changes when the mouse is over an image or a button (V0.8.1).</summary>
public enum GuiHoverEffect
{
    None,
    Highlight,
    Blur,
    Tint,
}

/// <summary>
/// How a component looks (V0.8.1, Gui.md): the place of its content (alignment, lines, padding), its
/// opacity and its rotation; for a text its size, its lines and its look under the mouse; for an image
/// or a button its size, tint, tiling, border, blur, highlight and effect under the mouse.
/// </summary>
public sealed record GuiStyle
{
    public static GuiStyle Default { get; } = new();

    /// <summary>Default look of a kind: a button or a checkbox centers its content.</summary>
    public static GuiStyle For(GuiComponentKind kind) => kind is GuiComponentKind.Button
        ? new GuiStyle { Horizontal = GuiAlign.Center, Vertical = GuiAlign.Center, Justify = GuiAlign.Center }
        : kind is GuiComponentKind.Checkbox ? new GuiStyle { Vertical = GuiAlign.Center } : Default;

    public GuiAlign Horizontal { get; init; }

    public GuiAlign Vertical { get; init; }

    /// <summary>Place of the lines of a text between them (justify content).</summary>
    public GuiAlign Justify { get; init; }

    /// <summary>Space between the border of the component and its content, in pixels.</summary>
    public int Padding { get; init; }

    /// <summary>0 (invisible) to 100 (opaque).</summary>
    public int Opacity { get; init; } = 100;

    /// <summary>Degrees, clockwise.</summary>
    public int Rotation { get; init; }

    /// <summary>Size of a text, in percent of the font of the game (8 pixels).</summary>
    public int FontScale { get; init; } = 100;

    /// <summary>A text goes to the next line at the end of the component.</summary>
    public bool Wrap { get; init; }

    /// <summary>Text shown while the mouse is over the component; empty: the same.</summary>
    public string HoverText { get; init; } = string.Empty;

    /// <summary>Color of the text while the mouse is over it ("#RRGGBB"); empty: the same.</summary>
    public string HoverColor { get; init; } = string.Empty;

    public GuiImageSize ImageSize { get; init; }

    /// <summary>Color the image is multiplied by ("#FFFFFF": unchanged).</summary>
    public string Tint { get; init; } = "#FFFFFF";

    /// <summary>The image repeats to fill the component instead of being stretched.</summary>
    public bool Tiling { get; init; }

    /// <summary>Thickness of the border, in pixels (0: none).</summary>
    public int Border { get; init; }

    public string BorderColor { get; init; } = "#000000";

    public bool Blur { get; init; }

    /// <summary>A light veil over the image.</summary>
    public bool Highlight { get; init; }

    public GuiHoverEffect HoverEffect { get; init; }

    /// <summary>Tint while the mouse is over the image (effect Tint).</summary>
    public string HoverTint { get; init; } = "#FFFF80";

    /// <summary>Values brought back into their limits.</summary>
    public GuiStyle Normalized() => this with
    {
        Padding = Math.Clamp(Padding, 0, 64),
        Opacity = Math.Clamp(Opacity, 0, 100),
        Rotation = ((Rotation % 360) + 360) % 360,
        FontScale = Math.Clamp(FontScale, 25, 800),
        Border = Math.Clamp(Border, 0, 16),
    };
}

/// <summary>
/// A component of a GUI: its kind, its name (used by the code and the nodes), its place and size in
/// pixels of the GUI (the top left corner of the GUI is 0, 0), and the settings of its kind.
/// </summary>
public sealed record GuiComponent(Guid Id, GuiComponentKind Kind, string Name, int X, int Y, int Width, int Height)
{
    /// <summary>Size of a slot, the one of the game.</summary>
    public const int SlotSize = 18;

    /// <summary>Text of a text, a button or a checkbox; hint shown in an empty text input.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Color of a text ("#RRGGBB").</summary>
    public string Color { get; init; } = "#404040";

    /// <summary>Texture of an image.</summary>
    public Guid? TextureId { get; init; }

    /// <summary>Text shown when the mouse is over the component; empty: none.</summary>
    public string Tooltip { get; init; } = string.Empty;

    public GuiSlotMode SlotMode { get; init; } = GuiSlotMode.Input;

    /// <summary>A checkbox ticked when the GUI opens.</summary>
    public bool Checked { get; init; }

    /// <summary>Longest text of a text input.</summary>
    public int MaxLength { get; init; } = 32;

    /// <summary>How it looks (V0.8.1).</summary>
    public GuiStyle Style { get; init; } = GuiStyle.Default;

    /// <summary>
    /// The component is a variable of the graph of the GUI (V0.8.1): its node gives it to the GUI nodes, and
    /// it keeps following it when it is renamed.
    /// </summary>
    public bool IsVariable { get; init; }

    /// <summary>Default size of a new component of this kind.</summary>
    public static (int Width, int Height) DefaultSize(GuiComponentKind kind) => kind switch
    {
        GuiComponentKind.Text => (60, 10),
        GuiComponentKind.Image => (32, 32),
        GuiComponentKind.Button => (60, 20),
        GuiComponentKind.Slot => (SlotSize, SlotSize),
        GuiComponentKind.Checkbox => (80, 20),
        _ => (100, 20),
    };

    /// <summary>A new component of this kind, with its default size and text.</summary>
    public static GuiComponent Create(GuiComponentKind kind, string name, int x, int y)
    {
        (int width, int height) = DefaultSize(kind);
        return new GuiComponent(Guid.NewGuid(), kind, name, x, y, width, height)
        {
            Style = GuiStyle.For(kind),
            Text = kind switch
            {
                GuiComponentKind.Text => Tr.T("Text"),
                GuiComponentKind.Button => Tr.T("Button"),
                GuiComponentKind.Checkbox => Tr.T("Option"),
                GuiComponentKind.TextInput => Tr.T("Type here..."),
                _ => string.Empty,
            },
        };
    }

    /// <summary>A slot keeps the size of the game; the others are at least 1 x 1.</summary>
    public GuiComponent Normalized() => Kind == GuiComponentKind.Slot
        ? this with { Width = SlotSize, Height = SlotSize }
        : this with { Width = Math.Max(1, Width), Height = Math.Max(1, Height), MaxLength = Math.Clamp(MaxLength, 1, 32767), Style = Style.Normalized() };

    /// <summary>True when the name can be a Java field and a node choice (letters, digits, _, not starting with a digit).</summary>
    public static bool IsValidName(string name) =>
        name.Length > 0 && (char.IsAsciiLetter(name[0]) || name[0] == '_') && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}

/// <summary>
/// GUI of the mod (V0.8.0, like the GUI editor of MCreator): a window of the given size with its
/// components placed with the mouse, with or without the slots of the player's inventory, that can
/// pause the game. The editor snaps the components on a grid chosen here.
/// </summary>
public sealed class GuiAsset : Asset, IHasMethods
{
    public const int MinSize = 16;
    public const int MaxSize = 512;

    /// <summary>Width of the player's inventory (9 slots).</summary>
    public const int InventoryWidth = 162;

    /// <summary>Height of the player's inventory: 3 rows, a gap of 4 pixels, the hotbar.</summary>
    public const int InventoryHeight = 76;

    private int _width = 176;
    private int _height = 166;
    private bool _hasInventory;
    private int _inventoryOffsetX;
    private int _inventoryOffsetY;
    private bool _pauseGame;
    private bool _renderBackground = true;
    private IReadOnlyList<GuiComponent> _components = [];
    private IReadOnlyList<ElementMethod> _methods = [];
    private bool _snapToGrid = true;
    private int _gridX = 2;
    private int _gridY = 2;
    private int _gridOffsetX;
    private int _gridOffsetY;

    public GuiAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Gui;

    public int Width
    {
        get => _width;
        set => SetField(ref _width, Math.Clamp(value, MinSize, MaxSize));
    }

    public int Height
    {
        get => _height;
        set => SetField(ref _height, Math.Clamp(value, MinSize, MaxSize));
    }

    /// <summary>The slots of the player's inventory are shown at the bottom of the GUI (a GUI with slots).</summary>
    public bool HasInventory
    {
        get => _hasInventory;
        set => SetField(ref _hasInventory, value);
    }

    /// <summary>Move of the player's inventory from its usual place, in pixels.</summary>
    public int InventoryOffsetX
    {
        get => _inventoryOffsetX;
        set => SetField(ref _inventoryOffsetX, Math.Clamp(value, -MaxSize, MaxSize));
    }

    public int InventoryOffsetY
    {
        get => _inventoryOffsetY;
        set => SetField(ref _inventoryOffsetY, Math.Clamp(value, -MaxSize, MaxSize));
    }

    /// <summary>The game is paused while the GUI is open (in single player).</summary>
    public bool PauseGame
    {
        get => _pauseGame;
        set => SetField(ref _pauseGame, value);
    }

    /// <summary>The world behind the GUI is darkened, like the menus of the game.</summary>
    public bool RenderBackground
    {
        get => _renderBackground;
        set => SetField(ref _renderBackground, value);
    }

    /// <summary>Components, from the back to the front.</summary>
    public IReadOnlyList<GuiComponent> Components
    {
        get => _components;
        set => SetField(ref _components, value is null ? [] : [.. value.Select(c => c.Normalized())]);
    }

    /// <summary>Events of the GUI and of its components (V0.8.0, see <see cref="GuiMethodSlots"/>), in Java or as graphs.</summary>
    public IReadOnlyList<ElementMethod> Methods
    {
        get => _methods;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SequenceEqual(_methods))
            {
                _methods = [.. value];
                RaiseChanged(nameof(Methods));
            }
        }
    }

    /// <summary>The method of a place, or null when it is not created.</summary>
    public ElementMethod? FindMethod(string slot) => _methods.FirstOrDefault(m => m.Slot == slot);

    // ----- Editor options: they change the editor, not the GUI. -----

    public bool SnapToGrid
    {
        get => _snapToGrid;
        set => SetField(ref _snapToGrid, value);
    }

    public int GridX
    {
        get => _gridX;
        set => SetField(ref _gridX, Math.Clamp(value, 1, 64));
    }

    public int GridY
    {
        get => _gridY;
        set => SetField(ref _gridY, Math.Clamp(value, 1, 64));
    }

    public int GridOffsetX
    {
        get => _gridOffsetX;
        set => SetField(ref _gridOffsetX, Math.Clamp(value, 0, 63));
    }

    public int GridOffsetY
    {
        get => _gridOffsetY;
        set => SetField(ref _gridOffsetY, Math.Clamp(value, 0, 63));
    }

    /// <summary>Top left corner of the player's inventory in the GUI: centered, at the bottom, then moved by the offset.</summary>
    public (int X, int Y) InventoryOrigin =>
        ((Width - InventoryWidth) / 2 + InventoryOffsetX, Height - InventoryHeight - 7 + InventoryOffsetY);

    public bool HasSlots => HasInventory || Components.Any(c => c.Kind == GuiComponentKind.Slot);

    public GuiComponent? FindComponent(Guid id) => _components.FirstOrDefault(c => c.Id == id);

    /// <summary>A free name "button1", "button2"... for a new component of this kind.</summary>
    public string NewComponentName(GuiComponentKind kind)
    {
        string prefix = kind switch
        {
            GuiComponentKind.TextInput => "input",
            _ => kind.ToString().ToLowerInvariant(),
        };
        for (int i = 1; ; i++)
        {
            string name = prefix + i;
            if (_components.All(c => !string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }
    }

    /// <summary>Snaps a position on the grid of the editor (when the snap is on).</summary>
    public (int X, int Y) Snap(double x, double y)
    {
        if (!SnapToGrid)
        {
            return ((int)Math.Round(x), (int)Math.Round(y));
        }

        return ((int)(Math.Round((x - GridOffsetX) / GridX) * GridX + GridOffsetX), (int)(Math.Round((y - GridOffsetY) / GridY) * GridY + GridOffsetY));
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (GuiComponent image in _components.Where(c => c.Kind is GuiComponentKind.Image or GuiComponentKind.Button && c.TextureId is not null))
        {
            yield return new AssetReference(image.TextureId!.Value, AssetReferenceKind.Texture, Tr.F("image \"{0}\"", image.Name));
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();
        foreach (GuiComponent component in _components)
        {
            if (!GuiComponent.IsValidName(component.Name))
            {
                diagnostics.Error(Tr.F("\"{0}\" is not a valid component name (letters, digits and _, not starting with a digit).", component.Name), source, Id);
            }

            if (component.X < 0 || component.Y < 0 || component.X + component.Width > Width || component.Y + component.Height > Height)
            {
                diagnostics.Warning(Tr.F("The component \"{0}\" goes out of the GUI.", component.Name), source, Id);
            }

            if (component.Kind == GuiComponentKind.Image && component.TextureId is null)
            {
                diagnostics.Warning(Tr.F("The image \"{0}\" has no texture.", component.Name), source, Id);
            }
        }

        foreach (IGrouping<string, GuiComponent> twins in _components.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            diagnostics.Error(Tr.F("Several components are named \"{0}\": each one needs its own name.", twins.Key), source, Id);
        }

        if (HasInventory && (Width < InventoryWidth + 14 || Height < InventoryHeight + 14))
        {
            diagnostics.Warning(Tr.F("The GUI is too small for the player's inventory ({0} x {1} at least).", InventoryWidth + 14, InventoryHeight + 14), source, Id);
        }
    }
}
