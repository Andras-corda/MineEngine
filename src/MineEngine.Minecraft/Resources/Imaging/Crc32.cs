namespace MineEngine.Minecraft.Resources.Imaging;

/// <summary>Somme de contrôle CRC-32 (polynôme 0xEDB88320) utilisée par le format PNG.</summary>
public sealed class Crc32
{
    private static readonly uint[] Table = CreateTable();

    private uint _value = 0xFFFFFFFFu;

    public uint Value => _value ^ 0xFFFFFFFFu;

    public void Append(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
        {
            _value = Table[(_value ^ b) & 0xFF] ^ (_value >> 8);
        }
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
