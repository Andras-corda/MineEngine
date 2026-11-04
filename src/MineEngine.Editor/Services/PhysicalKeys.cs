using System.Runtime.InteropServices;
using System.Windows.Input;
using MineEngine.Assets;

namespace MineEngine.Editor.Services;

/// <summary>
/// Minecraft names the keys of letters, digits and signs after their place on a US keyboard (GLFW):
/// on an AZERTY keyboard, the key "A" is the key "Q" of the game. A captured key is therefore read
/// by its scan code (its place), and shown with the letter of the keyboard of the user.
/// </summary>
public static class PhysicalKeys
{
    private const uint VirtualKeyToScanCode = 0;
    private const uint ScanCodeToVirtualKey = 1;
    private const uint VirtualKeyToChar = 2;

    /// <summary>Keys of letters, digits and signs by scan code (set 1), named like <see cref="KeybindAsset.Keys"/>.</summary>
    private static readonly Dictionary<uint, string> Printable = new()
    {
        [0x02] = "1", [0x03] = "2", [0x04] = "3", [0x05] = "4", [0x06] = "5", [0x07] = "6", [0x08] = "7", [0x09] = "8", [0x0A] = "9", [0x0B] = "0",
        [0x0C] = "MINUS", [0x0D] = "EQUAL",
        [0x10] = "Q", [0x11] = "W", [0x12] = "E", [0x13] = "R", [0x14] = "T", [0x15] = "Y", [0x16] = "U", [0x17] = "I", [0x18] = "O", [0x19] = "P",
        [0x1A] = "LEFT_BRACKET", [0x1B] = "RIGHT_BRACKET",
        [0x1E] = "A", [0x1F] = "S", [0x20] = "D", [0x21] = "F", [0x22] = "G", [0x23] = "H", [0x24] = "J", [0x25] = "K", [0x26] = "L",
        [0x27] = "SEMICOLON", [0x28] = "APOSTROPHE", [0x29] = "GRAVE_ACCENT", [0x2B] = "BACKSLASH",
        [0x2C] = "Z", [0x2D] = "X", [0x2E] = "C", [0x2F] = "V", [0x30] = "B", [0x31] = "N", [0x32] = "M",
        [0x33] = "COMMA", [0x34] = "PERIOD", [0x35] = "SLASH", [0x56] = "WORLD_2",
    };

    /// <summary>Other keys: the same on every keyboard.</summary>
    private static readonly Dictionary<Key, string> Named = new()
    {
        [Key.Space] = "SPACE", [Key.Tab] = "TAB", [Key.Back] = "BACKSPACE", [Key.Return] = "ENTER",
        [Key.Insert] = "INSERT", [Key.Delete] = "DELETE", [Key.Home] = "HOME", [Key.End] = "END", [Key.PageUp] = "PAGE_UP", [Key.PageDown] = "PAGE_DOWN",
        [Key.Up] = "UP", [Key.Down] = "DOWN", [Key.Left] = "LEFT", [Key.Right] = "RIGHT",
        [Key.LeftShift] = "LEFT_SHIFT", [Key.LeftCtrl] = "LEFT_CONTROL", [Key.LeftAlt] = "LEFT_ALT",
        [Key.RightShift] = "RIGHT_SHIFT", [Key.RightCtrl] = "RIGHT_CONTROL", [Key.RightAlt] = "RIGHT_ALT", [Key.Capital] = "CAPS_LOCK",
        [Key.Decimal] = "KP_DECIMAL", [Key.Divide] = "KP_DIVIDE", [Key.Multiply] = "KP_MULTIPLY", [Key.Subtract] = "KP_SUBTRACT", [Key.Add] = "KP_ADD",
    };

    /// <summary>Name of the key of the game for a key pressed in the editor, or null if the game has no such key.</summary>
    public static string? FromKey(Key key)
    {
        if (Named.TryGetValue(key, out string? named))
        {
            return named;
        }

        if (key is >= Key.F1 and <= Key.F12)
        {
            return $"F{key - Key.F1 + 1}";
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return $"KP_{key - Key.NumPad0}";
        }

        uint scanCode = MapVirtualKey((uint)KeyInterop.VirtualKeyFromKey(key), VirtualKeyToScanCode);
        return Printable.GetValueOrDefault(scanCode);
    }

    /// <summary>Name of the button of the mouse in the game.</summary>
    public static string FromMouse(MouseButton button) => button switch
    {
        MouseButton.Right => "MOUSE_RIGHT",
        MouseButton.Middle => "MOUSE_MIDDLE",
        MouseButton.XButton1 => "MOUSE_4",
        MouseButton.XButton2 => "MOUSE_5",
        _ => "MOUSE_LEFT",
    };

    /// <summary>Label of a key on the keyboard of the user: "A" for the key "Q" of the game on an AZERTY keyboard.</summary>
    public static string Label(KeyboardKey key)
    {
        if (Printable.FirstOrDefault(p => p.Value == key.Name) is { Key: not 0 } place && !char.IsDigit(key.Name[0]))
        {
            uint virtualKey = MapVirtualKey(place.Key, ScanCodeToVirtualKey);
            uint character = MapVirtualKey(virtualKey, VirtualKeyToChar) & 0x7FFF;
            if (character > ' ')
            {
                return char.ToUpperInvariant((char)character).ToString();
            }
        }

        return key.Label;
    }

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);
}
