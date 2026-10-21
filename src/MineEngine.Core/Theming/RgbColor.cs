using System.Globalization;

namespace MineEngine.Core.Theming;

/// <summary>Couleur opaque écrite #RRGGBB dans les fichiers de thème et les préférences.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    /// <summary>Lit une couleur #RRGGBB (le # est facultatif).</summary>
    public static bool TryParse(string? text, out RgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string hex = text.Trim().TrimStart('#');
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
        {
            return false;
        }

        color = new RgbColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    public static RgbColor Parse(string text) =>
        TryParse(text, out RgbColor color) ? color : throw new FormatException(Tr.F("Invalid color: {0} (expected: #RRGGBB).", text));

    /// <summary>Mélange avec une autre couleur : 0 donne cette couleur, 1 l'autre.</summary>
    public RgbColor Mix(RgbColor other, double amount)
    {
        double t = Math.Clamp(amount, 0, 1);
        return new RgbColor(Channel(R, other.R, t), Channel(G, other.G, t), Channel(B, other.B, t));
    }

    public RgbColor Lighten(double amount) => Mix(new RgbColor(255, 255, 255), amount);

    public RgbColor Darken(double amount) => Mix(new RgbColor(0, 0, 0), amount);

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    private static byte Channel(byte from, byte to, double t) => (byte)Math.Round(from + ((to - from) * t));
}
