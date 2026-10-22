using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Other look of an element, chosen while the game runs by the appearance nodes: a texture, or a
/// 3D model (items and blocks; a mob only changes its texture). Number 0 is the normal look, the
/// appearances of the list are numbered from 1.
/// </summary>
public sealed record AssetAppearance(Guid Id, string Name, Guid? TextureId, Guid? ModelId)
{
    public static AssetAppearance Create(string name) => new(Guid.NewGuid(), name, null, null);

    public bool IsEmpty => TextureId is null && ModelId is null;
}

/// <summary>
/// Asset qui possède une texture principale. Depuis la V0.3, la texture est un
/// <see cref="TextureAsset"/> désigné par son identifiant interne : renommer la
/// texture ne casse pas le lien.
/// </summary>
public abstract class TexturedAsset : Asset
{
    private Guid? _textureId;
    private Guid? _customModelId;
    private IReadOnlyList<AssetAppearance> _appearances = [];

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

    /// <summary>Other looks of the element (number 1, 2...), switched by the appearance nodes of the graphs.</summary>
    public IReadOnlyList<AssetAppearance> Appearances
    {
        get => _appearances;
        set => SetField(ref _appearances, value ?? []);
    }

    /// <summary>A mob only changes its texture; items and blocks can also take another 3D model.</summary>
    public virtual bool AppearancesCanUseModels => true;

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

        foreach ((AssetAppearance appearance, int index) in Appearances.Select((a, i) => (a, i + 1)))
        {
            if (appearance.TextureId is { } appearanceTexture)
            {
                yield return new AssetReference(appearanceTexture, AssetReferenceKind.Texture, Tr.F("appearance {0}", index));
            }

            if (AppearancesCanUseModels && appearance.ModelId is { } appearanceModel)
            {
                yield return new AssetReference(appearanceModel, AssetReferenceKind.Model, Tr.F("appearance {0}", index));
            }
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (!HasTexture && !UsesCustomModel)
        {
            diagnostics.Warning(MissingTextureMessage, ToString(), Id);
        }

        foreach ((AssetAppearance appearance, int index) in Appearances.Select((a, i) => (a, i + 1)))
        {
            if (appearance.TextureId is null && (!AppearancesCanUseModels || appearance.ModelId is null))
            {
                diagnostics.Warning(Tr.F("Appearance {0} (\"{1}\") has no texture: the element keeps its normal look.", index, appearance.Name), ToString(), Id);
            }
        }
    }
}
