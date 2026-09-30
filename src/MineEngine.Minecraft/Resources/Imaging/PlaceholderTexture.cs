namespace MineEngine.Minecraft.Resources.Imaging;

/// <summary>
/// Texture 16x16 en damier magenta et noir, reconnaissable en jeu comme
/// "texture manquante", utilisée quand un asset n'a pas encore de texture.
/// </summary>
public sealed class PlaceholderTexture
{
    public const int Size = 16;

    private readonly Lazy<byte[]> _png;

    public PlaceholderTexture(PngEncoder encoder)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        _png = new Lazy<byte[]>(() => encoder.Encode(CreateImage()));
    }

    public byte[] GetPngBytes() => _png.Value;

    private static RgbaImage CreateImage()
    {
        var image = new RgbaImage(Size, Size);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                bool magenta = ((x / 8) + (y / 8)) % 2 == 0;
                if (magenta)
                {
                    image.SetPixel(x, y, 248, 0, 248);
                }
                else
                {
                    image.SetPixel(x, y, 0, 0, 0);
                }
            }
        }

        return image;
    }
}
