using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace MineEngine.Minecraft.Resources.Imaging;

/// <summary>
/// Encodeur PNG minimal (RGBA 8 bits, sans filtre). Suffisant pour produire
/// les textures de remplacement sans dépendre d'une bibliothèque graphique.
/// </summary>
public sealed class PngEncoder
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public byte[] Encode(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR", CreateHeader(image));
        WriteChunk(output, "IDAT", CompressPixels(image));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] CreateHeader(RgbaImage image)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
        header[8] = 8;  // bits par canal
        header[9] = 6;  // type de couleur : RGBA
        header[10] = 0; // compression
        header[11] = 0; // filtre
        header[12] = 0; // pas d'entrelacement
        return header;
    }

    private static byte[] CompressPixels(RgbaImage image)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int y = 0; y < image.Height; y++)
            {
                zlib.WriteByte(0); // filtre "None" pour chaque ligne
                zlib.Write(image.GetRow(y));
            }
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crc = new Crc32();
        crc.Append(typeBytes);
        crc.Append(data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc.Value);
        output.Write(crcBytes);
    }
}
