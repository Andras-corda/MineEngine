using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>Contenu déclaratif du mod : ce qui existe dans le jeu.</summary>
public sealed class IRContent
{
    public IRContent(IEnumerable<IRItem> items, IEnumerable<IRBlock> blocks)
    {
        Items = items.OrderBy(i => i.Id).ToList();
        Blocks = blocks.OrderBy(b => b.Id).ToList();
    }

    public IReadOnlyList<IRItem> Items { get; }

    public IReadOnlyList<IRBlock> Blocks { get; }

    public bool IsEmpty => Items.Count == 0 && Blocks.Count == 0;
}

/// <summary>Élément de contenu qui apparaît en jeu avec un nom et une texture.</summary>
public abstract class IRElement
{
    protected IRElement(ResourceId id, string displayName, IRTexture texture)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Value : displayName;
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
    }

    public ResourceId Id { get; }

    public string DisplayName { get; }

    public IRTexture Texture { get; }
}

public sealed class IRItem : IRElement
{
    public IRItem(ResourceId id, string displayName, IRTexture texture, int maxStackSize)
        : base(id, displayName, texture)
    {
        MaxStackSize = maxStackSize;
    }

    public int MaxStackSize { get; }
}

public sealed class IRBlock : IRElement
{
    public IRBlock(ResourceId id, string displayName, IRTexture texture, float hardness, float resistance)
        : base(id, displayName, texture)
    {
        Hardness = hardness;
        Resistance = resistance;
    }

    public float Hardness { get; }

    public float Resistance { get; }
}

/// <summary>Texture d'un élément : un fichier PNG existant, ou une texture de remplacement.</summary>
public sealed class IRTexture
{
    private IRTexture(string? sourceFile)
    {
        SourceFile = sourceFile;
    }

    public static IRTexture Placeholder { get; } = new(null);

    /// <summary>Chemin absolu du PNG source, ou null pour la texture de remplacement.</summary>
    public string? SourceFile { get; }

    public bool IsPlaceholder => SourceFile is null;

    public static IRTexture FromFile(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        return new IRTexture(absolutePath);
    }
}
