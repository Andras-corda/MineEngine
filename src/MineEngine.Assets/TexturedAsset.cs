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
    private Guid? _customModelId;

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

    /// <summary>3D model (asset Model) shown instead of the generated shape, or null.</summary>
    public Guid? CustomModelId
    {
        get => _customModelId;
        set => SetField(ref _customModelId, value == Guid.Empty ? null : value);
    }

    /// <summary>True when the element is shown with its 3D model (its textures then come from the model).</summary>
    public virtual bool UsesCustomModel => CustomModelId is not null;

    /// <summary>What the model must be able to do: be used by the game, or become an entity model.</summary>
    protected virtual AssetReferenceKind CustomModelKind => AssetReferenceKind.Model;

    /// <summary>Message affiché quand aucune texture n'est choisie.</summary>
    protected virtual string MissingTextureMessage => Tr.T("No texture; a placeholder texture will be used.");

    public override IEnumerable<AssetReference> GetReferences()
    {
        if (TextureId is { } texture)
        {
            yield return new AssetReference(texture, AssetReferenceKind.Texture, "texture");
        }

        if (UsesCustomModel && CustomModelId is { } model)
        {
            yield return new AssetReference(model, CustomModelKind, Tr.T("3D model"));
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (!HasTexture && !UsesCustomModel)
        {
            diagnostics.Warning(MissingTextureMessage, ToString(), Id);
        }
    }
}
