using MineEngine.Core.Identifiers;

namespace MineEngine.Core.Assets;

public enum ContentReferenceKind
{
    /// <summary>Asset du projet, désigné par son identifiant interne.</summary>
    Asset,

    /// <summary>Élément existant du jeu ou d'un autre mod ("minecraft:diamond").</summary>
    Id,

    /// <summary>Tag d'items ("#minecraft:planks"), accepté seulement comme ingrédient.</summary>
    Tag,
}

/// <summary>
/// Désigne un item (ou un son) : soit un asset du projet, suivi par son guid pour
/// survivre aux renommages, soit un identifiant du jeu, soit un tag. Immuable.
/// </summary>
public sealed class ContentReference : IEquatable<ContentReference>
{
    private const string AssetPrefix = "asset:";

    private ContentReference(ContentReferenceKind kind, Guid assetId, NamespacedId? id)
    {
        Kind = kind;
        AssetId = assetId;
        Id = id;
    }

    public ContentReferenceKind Kind { get; }

    /// <summary>Asset visé quand <see cref="Kind"/> vaut Asset.</summary>
    public Guid AssetId { get; }

    /// <summary>Identifiant ou tag visé, sinon null.</summary>
    public NamespacedId? Id { get; }

    public static ContentReference ToAsset(Guid assetId) =>
        assetId == Guid.Empty
            ? throw new ArgumentException("L'identifiant de l'asset ne peut pas être vide.", nameof(assetId))
            : new ContentReference(ContentReferenceKind.Asset, assetId, null);

    public static ContentReference ToId(NamespacedId id) =>
        new(ContentReferenceKind.Id, Guid.Empty, id ?? throw new ArgumentNullException(nameof(id)));

    public static ContentReference ToTag(NamespacedId tag) =>
        new(ContentReferenceKind.Tag, Guid.Empty, tag ?? throw new ArgumentNullException(nameof(tag)));

    /// <summary>Texte enregistré dans les fichiers d'assets : "asset:guid", "#espace:tag" ou "espace:chemin".</summary>
    public string ToStorageText() => Kind switch
    {
        ContentReferenceKind.Asset => AssetPrefix + AssetId.ToString("D"),
        ContentReferenceKind.Tag => "#" + Id,
        _ => Id!.ToString(),
    };

    public static bool TryParseStorageText(string? text, out ContentReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        if (trimmed.StartsWith(AssetPrefix, StringComparison.Ordinal))
        {
            if (Guid.TryParse(trimmed[AssetPrefix.Length..], out Guid assetId) && assetId != Guid.Empty)
            {
                reference = ToAsset(assetId);
            }

            return reference is not null;
        }

        bool isTag = trimmed.StartsWith('#');
        if (!NamespacedId.TryParse(isTag ? trimmed[1..] : trimmed, out NamespacedId? id))
        {
            return false;
        }

        reference = isTag ? ToTag(id!) : ToId(id!);
        return true;
    }

    public bool Equals(ContentReference? other) =>
        other is not null && other.Kind == Kind && other.AssetId == AssetId && Equals(other.Id, Id);

    public override bool Equals(object? obj) => Equals(obj as ContentReference);

    public override int GetHashCode() => HashCode.Combine(Kind, AssetId, Id);

    public override string ToString() => ToStorageText();
}
