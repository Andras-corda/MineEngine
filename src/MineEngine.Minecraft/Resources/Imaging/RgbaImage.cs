namespace MineEngine.Minecraft.Resources.Imaging;

/// <summary>Image en mémoire, 8 bits par canal (rouge, vert, bleu, alpha).</summary>
public sealed class RgbaImage
{
    private readonly byte[] _pixels;

    public RgbaImage(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _pixels = new byte[width * height * 4];
    }

    public int Width { get; }

    public int Height { get; }

    public void SetPixel(int x, int y, byte red, byte green, byte blue, byte alpha = 255)
    {
        int offset = ((y * Width) + x) * 4;
        _pixels[offset] = red;
        _pixels[offset + 1] = green;
        _pixels[offset + 2] = blue;
        _pixels[offset + 3] = alpha;
    }

    public ReadOnlySpan<byte> GetRow(int y) => _pixels.AsSpan(y * Width * 4, Width * 4);
}
