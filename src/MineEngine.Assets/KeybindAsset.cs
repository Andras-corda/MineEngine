using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>A key of the keyboard that a keybind can use by default: its name, its label and its GLFW code.</summary>
public sealed record KeyboardKey(string Name, string Label, int Code);

/// <summary>
/// Key of the controls of the mod (V0.6.2): its name in the Controls screen (the display name),
/// its default key, which the player can change. Pressing it runs the "On key pressed" event of
/// the event graphs, on the server, with the player who pressed it.
/// </summary>
public sealed class KeybindAsset : Asset
{
    private string _defaultKey = "R";

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

    /// <summary>Keys proposed as default: letters, digits, function keys, arrows and modifiers.</summary>
    public static IReadOnlyList<KeyboardKey> Keys { get; } =
    [
        .. Enumerable.Range('A', 26).Select(c => new KeyboardKey(((char)c).ToString(), ((char)c).ToString(), c)),
        .. Enumerable.Range(0, 10).Select(d => new KeyboardKey(d.ToString(System.Globalization.CultureInfo.InvariantCulture), d.ToString(System.Globalization.CultureInfo.InvariantCulture), '0' + d)),
        .. Enumerable.Range(1, 12).Select(f => new KeyboardKey($"F{f}", $"F{f}", 289 + f)),
        new KeyboardKey("SPACE", Tr.T("Space"), 32),
        new KeyboardKey("TAB", Tr.T("Tab"), 258),
        new KeyboardKey("BACKSPACE", Tr.T("Backspace"), 259),
        new KeyboardKey("ENTER", Tr.T("Enter"), 257),
        new KeyboardKey("UP", Tr.T("Up arrow"), 265),
        new KeyboardKey("DOWN", Tr.T("Down arrow"), 264),
        new KeyboardKey("LEFT", Tr.T("Left arrow"), 263),
        new KeyboardKey("RIGHT", Tr.T("Right arrow"), 262),
        new KeyboardKey("LEFT_SHIFT", Tr.T("Left Shift"), 340),
        new KeyboardKey("LEFT_CONTROL", Tr.T("Left Ctrl"), 341),
        new KeyboardKey("LEFT_ALT", Tr.T("Left Alt"), 342),
        new KeyboardKey("CAPS_LOCK", Tr.T("Caps Lock"), 280),
        .. Enumerable.Range(0, 10).Select(d => new KeyboardKey($"KP_{d}", Tr.F("Numpad {0}", d), 320 + d)),
    ];

    public static KeyboardKey? Find(string? name) => Keys.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));
}
