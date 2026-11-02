using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// A key of the keyboard or a button of the mouse that a keybind can use: its name, its label and its
/// GLFW code. A key of the keyboard is named after its place on a US keyboard, like in Minecraft.
/// </summary>
public sealed record KeyboardKey(string Name, string Label, int Code, bool IsMouse = false);

/// <summary>
/// Key of the controls of the mod (V0.6.2): its name in the Controls screen (the display name),
/// its default key or mouse button, which the player can change. The client of the player watches
/// it and runs, on the server, the key events of the event graphs: pressed, released, held, pulse,
/// held then released, combo. The hold time, the pulse interval and the combo key are set here.
/// </summary>
public sealed class KeybindAsset : Asset
{
    /// <summary>Shortest and longest hold time or pulse interval, in seconds.</summary>
    public const double MinSeconds = 0.05;

    public const double MaxSeconds = 60;

    private string _defaultKey = "R";
    private double _holdSeconds = 1;
    private double _pulseSeconds = 0.5;
    private string _comboKey = "LEFT_SHIFT";
    private Guid? _graphId;

    public KeybindAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Keybind;

    /// <summary>Name of the default key (see <see cref="Keys"/>).</summary>
    public string DefaultKey
    {
        get => _defaultKey;
        set => SetField(ref _defaultKey, Find(value)?.Name ?? "R");
    }

    public KeyboardKey Key => Find(_defaultKey) ?? Keys[0];

    /// <summary>Time the key is held before "On key held" (and "held then released" counts), in seconds.</summary>
    public double HoldSeconds
    {
        get => _holdSeconds;
        set => SetField(ref _holdSeconds, Math.Clamp(value, MinSeconds, MaxSeconds));
    }

    /// <summary>Time between two "On key pulse" while the key is held, in seconds.</summary>
    public double PulseSeconds
    {
        get => _pulseSeconds;
        set => SetField(ref _pulseSeconds, Math.Clamp(value, MinSeconds, MaxSeconds));
    }

    /// <summary>Key held with this one for "On key combo" (see <see cref="Keys"/>).</summary>
    public string ComboKey
    {
        get => _comboKey;
        set => SetField(ref _comboKey, Find(value)?.Name ?? "LEFT_SHIFT");
    }

    public KeyboardKey Combo => Find(_comboKey) ?? Keys[0];

    /// <summary>Event graph shown in the editor of the keybind (created from it), or null.</summary>
    public Guid? GraphId
    {
        get => _graphId;
        set => SetField(ref _graphId, value);
    }

    /// <summary>Hold time in ticks of the game (20 per second).</summary>
    public int HoldTicks => Ticks(_holdSeconds);

    public int PulseTicks => Ticks(_pulseSeconds);

    /// <summary>Keys and buttons proposed: letters, digits, signs, function keys, editing keys, arrows, modifiers, numpad, mouse.</summary>
    public static IReadOnlyList<KeyboardKey> Keys { get; } =
    [
        .. Enumerable.Range('A', 26).Select(c => new KeyboardKey(((char)c).ToString(), ((char)c).ToString(), c)),
        .. Enumerable.Range(0, 10).Select(d => new KeyboardKey(d.ToString(System.Globalization.CultureInfo.InvariantCulture), d.ToString(System.Globalization.CultureInfo.InvariantCulture), '0' + d)),
        .. Enumerable.Range(1, 12).Select(f => new KeyboardKey($"F{f}", $"F{f}", 289 + f)),
        new KeyboardKey("SPACE", Tr.T("Space"), 32),
        new KeyboardKey("TAB", Tr.T("Tab"), 258),
        new KeyboardKey("BACKSPACE", Tr.T("Backspace"), 259),
        new KeyboardKey("ENTER", Tr.T("Enter"), 257),
        new KeyboardKey("INSERT", Tr.T("Insert"), 260),
        new KeyboardKey("DELETE", Tr.T("Delete"), 261),
        new KeyboardKey("HOME", Tr.T("Home"), 268),
        new KeyboardKey("END", Tr.T("End"), 269),
        new KeyboardKey("PAGE_UP", Tr.T("Page up"), 266),
        new KeyboardKey("PAGE_DOWN", Tr.T("Page down"), 267),
        new KeyboardKey("UP", Tr.T("Up arrow"), 265),
        new KeyboardKey("DOWN", Tr.T("Down arrow"), 264),
        new KeyboardKey("LEFT", Tr.T("Left arrow"), 263),
        new KeyboardKey("RIGHT", Tr.T("Right arrow"), 262),
        new KeyboardKey("LEFT_SHIFT", Tr.T("Left Shift"), 340),
        new KeyboardKey("LEFT_CONTROL", Tr.T("Left Ctrl"), 341),
        new KeyboardKey("LEFT_ALT", Tr.T("Left Alt"), 342),
        new KeyboardKey("RIGHT_SHIFT", Tr.T("Right Shift"), 344),
        new KeyboardKey("RIGHT_CONTROL", Tr.T("Right Ctrl"), 345),
        new KeyboardKey("RIGHT_ALT", Tr.T("Right Alt"), 346),
        new KeyboardKey("CAPS_LOCK", Tr.T("Caps Lock"), 280),
        new KeyboardKey("GRAVE_ACCENT", "`", 96),
        new KeyboardKey("MINUS", "-", 45),
        new KeyboardKey("EQUAL", "=", 61),
        new KeyboardKey("LEFT_BRACKET", "[", 91),
        new KeyboardKey("RIGHT_BRACKET", "]", 93),
        new KeyboardKey("BACKSLASH", "\\", 92),
        new KeyboardKey("SEMICOLON", ";", 59),
        new KeyboardKey("APOSTROPHE", "'", 39),
        new KeyboardKey("COMMA", ",", 44),
        new KeyboardKey("PERIOD", ".", 46),
        new KeyboardKey("SLASH", "/", 47),
        new KeyboardKey("WORLD_2", "<", 162),
        .. Enumerable.Range(0, 10).Select(d => new KeyboardKey($"KP_{d}", Tr.F("Numpad {0}", d), 320 + d)),
        new KeyboardKey("KP_DECIMAL", Tr.F("Numpad {0}", "."), 330),
        new KeyboardKey("KP_DIVIDE", Tr.F("Numpad {0}", "/"), 331),
        new KeyboardKey("KP_MULTIPLY", Tr.F("Numpad {0}", "*"), 332),
        new KeyboardKey("KP_SUBTRACT", Tr.F("Numpad {0}", "-"), 333),
        new KeyboardKey("KP_ADD", Tr.F("Numpad {0}", "+"), 334),
        new KeyboardKey("KP_ENTER", Tr.F("Numpad {0}", Tr.T("Enter")), 335),
        new KeyboardKey("MOUSE_LEFT", Tr.T("Left mouse button"), 0, IsMouse: true),
        new KeyboardKey("MOUSE_RIGHT", Tr.T("Right mouse button"), 1, IsMouse: true),
        new KeyboardKey("MOUSE_MIDDLE", Tr.T("Middle mouse button"), 2, IsMouse: true),
        new KeyboardKey("MOUSE_4", Tr.F("Mouse button {0}", 4), 3, IsMouse: true),
        new KeyboardKey("MOUSE_5", Tr.F("Mouse button {0}", 5), 4, IsMouse: true),
    ];

    public static KeyboardKey? Find(string? name) => Keys.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));

    private static int Ticks(double seconds) => Math.Max(1, (int)Math.Round(seconds * 20));
}
