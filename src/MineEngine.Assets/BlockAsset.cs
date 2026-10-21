using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Un bloc. Inspiré de l'éditeur de blocs de MCreator : visuel (forme, textures par
/// face, transparence, lumière), propriétés (dureté, son, glissance...), récolte
/// (outil, drops, expérience) et forme item.
/// </summary>
public sealed class BlockAsset : InventoryAsset
{
    public const float MaxStrength = 3_600_000f;
    public const float DefaultHardness = 1.5f;
    public const float DefaultResistance = 6f;
    public const int MaxLightLevel = 15;
    public const float DefaultFriction = 0.6f;
    public const int MaxDropCount = 64;
    public const int MaxExperience = 1000;

    private readonly Dictionary<BlockTextureSlot, Guid> _extraTextures = [];

    private BlockModelKind _model = BlockModelKind.Cube;
    private BlockRenderType _renderType = BlockRenderType.Solid;
    private int _lightLevel;
    private float _hardness = DefaultHardness;
    private float _resistance = DefaultResistance;
    private bool _unbreakable;
    private BlockSoundType _sound = BlockSoundType.Stone;
    private float _friction = DefaultFriction;
    private float _speedFactor = 1f;
    private float _jumpFactor = 1f;
    private HarvestTool _harvestTool = HarvestTool.None;
    private ToolTier _toolTier = ToolTier.Any;
    private bool _requiresCorrectTool;
    private BlockDropKind _dropKind = BlockDropKind.Self;
    private ContentReference? _dropItem;
    private int _dropMin = 1;
    private int _dropMax = 1;
    private int _experienceMin;
    private int _experienceMax;
    private bool _hasItemForm = true;

    public BlockAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Block;

    // ----- Visuel -----

    public BlockModelKind Model
    {
        get => _model;
        set => SetField(ref _model, value);
    }

    public BlockRenderType RenderType
    {
        get => _renderType;
        set => SetField(ref _renderType, value);
    }

    /// <summary>Lumière émise, de 0 (aucune) à 15 (comme la glowstone).</summary>
    public int LightLevel
    {
        get => _lightLevel;
        set => SetField(ref _lightLevel, Math.Clamp(value, 0, MaxLightLevel));
    }

    /// <summary>
    /// Texture d'un emplacement ; l'emplacement principal est <see cref="TexturedAsset.TextureId"/>.
    /// Un emplacement vide reprend la texture principale.
    /// </summary>
    public Guid? GetTexture(BlockTextureSlot slot) =>
        slot == BlockTextureSlot.Main ? TextureId : _extraTextures.TryGetValue(slot, out Guid id) ? (Guid?)id : null;

    public void SetTexture(BlockTextureSlot slot, Guid? textureId)
    {
        if (slot == BlockTextureSlot.Main)
        {
            TextureId = textureId;
            return;
        }

        Guid? normalized = textureId == Guid.Empty ? null : textureId;
        if (GetTexture(slot) == normalized)
        {
            return;
        }

        if (normalized is { } id)
        {
            _extraTextures[slot] = id;
        }
        else
        {
            _extraTextures.Remove(slot);
        }

        RaiseChanged(TexturePropertyName(slot));
    }

    /// <summary>Emplacements secondaires renseignés.</summary>
    public IReadOnlyDictionary<BlockTextureSlot, Guid> ExtraTextures => _extraTextures;

    public static string TexturePropertyName(BlockTextureSlot slot) => "Texture." + slot;

    // ----- Propriétés -----

    /// <summary>Temps nécessaire pour casser le bloc (pierre : 1,5 ; terre : 0,5).</summary>
    public float Hardness
    {
        get => _hardness;
        set => SetField(ref _hardness, EnsureValidStrength(value));
    }

    /// <summary>Résistance aux explosions (pierre : 6 ; obsidienne : 1200).</summary>
    public float Resistance
    {
        get => _resistance;
        set => SetField(ref _resistance, EnsureValidStrength(value));
    }

    /// <summary>Impossible à casser, comme la bedrock.</summary>
    public bool Unbreakable
    {
        get => _unbreakable;
        set => SetField(ref _unbreakable, value);
    }

    public BlockSoundType Sound
    {
        get => _sound;
        set => SetField(ref _sound, value);
    }

    /// <summary>Glissance (0,6 normal ; glace : 0,98).</summary>
    public float Friction
    {
        get => _friction;
        set => SetField(ref _friction, ClampFactor(value, 0.1f, 1.2f, DefaultFriction));
    }

    /// <summary>Multiplicateur de vitesse de marche (1 normal ; sable des âmes : 0,4).</summary>
    public float SpeedFactor
    {
        get => _speedFactor;
        set => SetField(ref _speedFactor, ClampFactor(value, 0.1f, 5f, 1f));
    }

    /// <summary>Multiplicateur de hauteur de saut (1 normal ; bloc de miel : 0,5).</summary>
    public float JumpFactor
    {
        get => _jumpFactor;
        set => SetField(ref _jumpFactor, ClampFactor(value, 0.1f, 5f, 1f));
    }

    // ----- Récolte -----

    public HarvestTool HarvestTool
    {
        get => _harvestTool;
        set => SetField(ref _harvestTool, value);
    }

    public ToolTier ToolTier
    {
        get => _toolTier;
        set => SetField(ref _toolTier, value);
    }

    /// <summary>Ne laisse rien tomber s'il n'est pas cassé avec le bon outil.</summary>
    public bool RequiresCorrectTool
    {
        get => _requiresCorrectTool;
        set => SetField(ref _requiresCorrectTool, value);
    }

    public BlockDropKind DropKind
    {
        get => _dropKind;
        set => SetField(ref _dropKind, value);
    }

    /// <summary>Item laissé quand <see cref="DropKind"/> vaut OtherItem (item du projet ou du jeu).</summary>
    public ContentReference? DropItem
    {
        get => _dropItem;
        set => SetField(ref _dropItem, value);
    }

    public int DropMin
    {
        get => _dropMin;
        set => SetField(ref _dropMin, Math.Clamp(value, 1, MaxDropCount));
    }

    public int DropMax
    {
        get => _dropMax;
        set => SetField(ref _dropMax, Math.Clamp(value, 1, MaxDropCount));
    }

    /// <summary>Expérience donnée quand le bloc est cassé (minerais).</summary>
    public int ExperienceMin
    {
        get => _experienceMin;
        set => SetField(ref _experienceMin, Math.Clamp(value, 0, MaxExperience));
    }

    public int ExperienceMax
    {
        get => _experienceMax;
        set => SetField(ref _experienceMax, Math.Clamp(value, 0, MaxExperience));
    }

    public bool DropsExperience => ExperienceMax > 0;

    // ----- Forme item -----

    /// <summary>Le bloc existe aussi comme item (sinon : comme un portail, rien en inventaire).</summary>
    public bool HasItemFormEnabled
    {
        get => _hasItemForm;
        set => SetField(ref _hasItemForm, value);
    }

    public override bool HasItemForm => _hasItemForm;

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (AssetReference reference in base.GetReferences())
        {
            yield return reference;
        }

        foreach ((BlockTextureSlot slot, Guid texture) in _extraTextures.OrderBy(t => t.Key))
        {
            yield return new AssetReference(texture, AssetReferenceKind.Texture, Tr.F("{0} texture", slot.ToString().ToLowerInvariant()));
        }

        if (DropKind == BlockDropKind.OtherItem && DropItem is { Kind: ContentReferenceKind.Asset } drop)
        {
            yield return new AssetReference(drop.AssetId, AssetReferenceKind.Item, Tr.T("dropped item"));
        }
    }

    public static bool IsValidStrength(float value) => float.IsFinite(value) && value is >= 0f and <= MaxStrength;

    public override bool UsesCustomModel => Model == BlockModelKind.Custom && CustomModelId is not null;

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();

        if (Model == BlockModelKind.Custom && CustomModelId is null)
        {
            diagnostics.Error(Tr.T("Shape \"3D model\": choose the model of the block."), source, Id);
        }

        if (RequiresCorrectTool && HarvestTool == HarvestTool.None)
        {
            diagnostics.Warning(Tr.T("\"Requires tool\" is checked but no tool is chosen: the block will never drop anything."), source, Id);
        }

        if (ToolTier != ToolTier.Any && HarvestTool == HarvestTool.None)
        {
            diagnostics.Warning(Tr.T("A tool level is chosen without a tool: it will be ignored."), source, Id);
        }

        if (DropKind == BlockDropKind.OtherItem && DropItem is null)
        {
            diagnostics.Error(Tr.T("Choose the item dropped by the block (an item from the project or the game, such as \"diamond\")."), source, Id);
        }

        if (DropKind == BlockDropKind.OtherItem && DropItem is { Kind: ContentReferenceKind.Tag })
        {
            diagnostics.Error(Tr.T("A block cannot drop a tag: choose a specific item."), source, Id);
        }

        if (DropKind == BlockDropKind.Self && !HasItemForm)
        {
            diagnostics.Warning(Tr.T("The block drops itself but has no item form: it will drop nothing."), source, Id);
        }

        if (DropMin > DropMax)
        {
            diagnostics.Warning(Tr.T("The minimum dropped quantity is greater than the maximum: they will be swapped."), source, Id);
        }

        if (ExperienceMin > ExperienceMax)
        {
            diagnostics.Warning(Tr.T("The minimum experience is greater than the maximum: they will be swapped."), source, Id);
        }

        if (DropsExperience && Model == BlockModelKind.Column)
        {
            diagnostics.Warning(Tr.T("A pillar block cannot give experience: it will be ignored."), source, Id);
        }

        if (Model == BlockModelKind.Cross && RenderType == BlockRenderType.Solid)
        {
            diagnostics.Info(Tr.T("A cross block is almost always transparent: choose \"Cutout\" for its texture."), source, Id);
        }
    }

    private static float EnsureValidStrength(float value)
    {
        if (!IsValidStrength(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, Tr.F("The value must be between 0 and {0}.", MaxStrength));
        }

        return value;
    }

    private static float ClampFactor(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
