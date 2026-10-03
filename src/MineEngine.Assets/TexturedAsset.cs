using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Asset qui possède une texture principale. Depuis la V0.3, la texture est un
/// <see cref="TextureAsset"/> désigné par son identifiant interne : renommer la
/// texture ne casse pas le lien.
/// </summary>
public abstract class TexturedAsset : Asset
{
    private Guid? _textureId;

    protected TexturedAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    /// <summary>Texture principale (asset Texture), ou null.</summary>
    public Guid? TextureId
    {
        get => _textureId;
        set => SetField(ref _textureId, value == Guid.Empty ? null : value);
    }

    public bool HasTexture => TextureId is not null;

    /// <summary>Message affiché quand aucune texture n'est choisie.</summary>
    protected virtual string MissingTextureMessage => "Aucune texture ; une texture de remplacement sera utilisée.";

    public override IEnumerable<AssetReference> GetReferences()
    {
        if (TextureId is { } texture)
        {
            yield return new AssetReference(texture, AssetReferenceKind.Texture, "texture");
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (!HasTexture)
        {
            diagnostics.Warning(MissingTextureMessage, ToString(), Id);
        }
    }
}
