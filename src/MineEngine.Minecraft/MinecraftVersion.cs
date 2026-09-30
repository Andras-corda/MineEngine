namespace MineEngine.Minecraft;

/// <summary>Numéro de version de Minecraft ("1.20.1"), comparable numériquement.</summary>
public sealed class MinecraftVersion : IComparable<MinecraftVersion>, IEquatable<MinecraftVersion>
{
    private readonly int[] _parts;

    private MinecraftVersion(string id, int[] parts)
    {
        Id = id;
        _parts = parts;
    }

    public string Id { get; }

    /// <summary>Version de Java imposée par Mojang pour cette version du jeu.</summary>
    public int RequiredJavaVersion => IsAtLeast("1.20.5") ? 21 : IsAtLeast("1.18") ? 17 : IsAtLeast("1.17") ? 16 : 8;

    /// <summary>Depuis 1.21, les dossiers de données sont au singulier (loot_tables devient loot_table).</summary>
    public bool UsesSingularDataFolders => IsAtLeast("1.21");

    public static MinecraftVersion Parse(string id) =>
        TryParse(id, out MinecraftVersion? version)
            ? version!
            : throw new FormatException($"Version de Minecraft invalide : '{id}'.");

    public static bool TryParse(string? id, out MinecraftVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        string[] texts = id.Trim().Split('.');
        var parts = new int[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            if (!int.TryParse(texts[i], out parts[i]) || parts[i] < 0)
            {
                return false;
            }
        }

        version = new MinecraftVersion(id.Trim(), parts);
        return true;
    }

    public bool IsAtLeast(string other) => CompareTo(Parse(other)) >= 0;

    public int CompareTo(MinecraftVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int length = Math.Max(_parts.Length, other._parts.Length);
        for (int i = 0; i < length; i++)
        {
            int left = i < _parts.Length ? _parts[i] : 0;
            int right = i < other._parts.Length ? other._parts[i] : 0;
            if (left != right)
            {
                return left.CompareTo(right);
            }
        }

        return 0;
    }

    public bool Equals(MinecraftVersion? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => Equals(obj as MinecraftVersion);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (int part in _parts.Reverse().SkipWhile(p => p == 0))
        {
            hash.Add(part);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => Id;
}
