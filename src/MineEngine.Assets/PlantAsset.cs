using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>Kind of plant (like the plants of MCreator).</summary>
public enum PlantKind
{
    /// <summary>A small plant drawn as a cross, like a poppy or a fern.</summary>
    Flower,

    /// <summary>A plant two blocks high (bottom and top textures), like a sunflower.</summary>
    Tall,

    /// <summary>A plant that grows upward block by block, like sugar cane.</summary>
    Growing,

    /// <summary>A crop planted on farmland, with growth stages, like wheat (its item is the seeds).</summary>
    Crop,
}

/// <summary>Where a plant can be placed.</summary>
public enum PlantSoil
{
    /// <summary>Grass, dirt, podzol, farmland...</summary>
    Dirt,

    /// <summary>Sand and red sand.</summary>
    Sand,

    /// <summary>Farmland only (crops).</summary>
    Farmland,

    /// <summary>The top of any solid block.</summary>
    AnySolid,
}

/// <summary>Natural generation of a plant: patches in the biomes.</summary>
public sealed record PlantGeneration
{
    public bool Enabled { get; init; }

    /// <summary>Biomes or a tag; empty: every biome of the overworld.</summary>
    public IReadOnlyList<string> Biomes { get; init; } = [];

    /// <summary>One patch every this number of chunks (1: in every chunk).</summary>
    public int Rarity { get; init; } = 8;

    /// <summary>Plants tried in a patch.</summary>
    public int PatchSize { get; init; } = 32;
}

/// <summary>
/// Plant of the mod (V0.7.0): a block drawn as a cross (flower), two blocks high, growing upward
/// or a crop with stages, with its item, where it can be placed, and its patches in the world.
/// </summary>
public sealed class PlantAsset : InventoryAsset
{
    public const int MaxStages = 8;
    public const int MaxGrowingHeight = 16;

    private PlantKind _kind = PlantKind.Flower;
    private PlantSoil _soil = PlantSoil.Dirt;
    private Guid? _topTextureId;
    private IReadOnlyList<Guid> _stageTextures = [];
    private int _maxHeight = 3;
    private int _lightLevel;
    private BlockSoundType _sound = BlockSoundType.Grass;
    private ContentReference? _produce;
    private int _produceMin = 1;
    private int _produceMax = 1;
    private PlantGeneration _generation = new();

    public PlantAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Plant;

    public PlantKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    public PlantSoil Soil
    {
        get => _soil;
        set => SetField(ref _soil, value);
    }

    /// <summary>Texture of the top half of a tall plant.</summary>
    public Guid? TopTextureId
    {
        get => _topTextureId;
        set => SetField(ref _topTextureId, value == Guid.Empty ? null : value);
    }

    /// <summary>Textures of the stages of a crop, from the seed to the ripe plant (1 to 8).</summary>
    public IReadOnlyList<Guid> StageTextures
    {
        get => _stageTextures;
        set => SetField(ref _stageTextures, value ?? []);
    }

    /// <summary>Highest stack of a growing plant (sugar cane: 3).</summary>
    public int MaxHeight
    {
        get => _maxHeight;
        set => SetField(ref _maxHeight, Math.Clamp(value, 1, MaxGrowingHeight));
    }

    public int LightLevel
    {
        get => _lightLevel;
        set => SetField(ref _lightLevel, Math.Clamp(value, 0, BlockAsset.MaxLightLevel));
    }

    public BlockSoundType Sound
    {
        get => _sound;
        set => SetField(ref _sound, value);
    }

    /// <summary>What a ripe crop drops besides its seeds (wheat), or null.</summary>
    public ContentReference? Produce
    {
        get => _produce;
        set => SetField(ref _produce, value);
    }

    public int ProduceMin
    {
        get => _produceMin;
        set => SetField(ref _produceMin, Math.Clamp(value, 1, BlockAsset.MaxDropCount));
    }

    public int ProduceMax
    {
        get => _produceMax;
        set => SetField(ref _produceMax, Math.Clamp(value, 1, BlockAsset.MaxDropCount));
    }

    public PlantGeneration Generation
    {
        get => _generation;
        set => SetField(ref _generation, value ?? new PlantGeneration());
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (AssetReference reference in base.GetReferences())
        {
            yield return reference;
        }

        if (Kind == PlantKind.Tall && TopTextureId is { } top)
        {
            yield return new AssetReference(top, AssetReferenceKind.Texture, Tr.T("top texture"));
        }

        if (Kind == PlantKind.Crop)
        {
            for (int i = 0; i < StageTextures.Count; i++)
            {
                yield return new AssetReference(StageTextures[i], AssetReferenceKind.Texture, Tr.F("stage {0}", i + 1));
            }

            if (Produce is { Kind: ContentReferenceKind.Asset } produce)
            {
                yield return new AssetReference(produce.AssetId, AssetReferenceKind.Item, Tr.T("produce"));
            }
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();
        if (Kind == PlantKind.Crop && StageTextures.Count == 0)
        {
            diagnostics.Warning(Tr.T("A crop needs the textures of its growth stages: the texture of its seeds is used for every stage."), source, Id);
        }

        if (Kind == PlantKind.Crop && StageTextures.Count > MaxStages)
        {
            diagnostics.Error(Tr.F("A crop has at most {0} stages.", MaxStages), source, Id);
        }

        if (Kind == PlantKind.Tall && TopTextureId is null)
        {
            diagnostics.Warning(Tr.T("A tall plant has no top texture: the bottom texture is used for both halves."), source, Id);
        }

        if (Kind == PlantKind.Crop && Produce is { Kind: ContentReferenceKind.Tag })
        {
            diagnostics.Error(Tr.T("A crop cannot drop a tag: choose a specific item."), source, Id);
        }

        if (Kind == PlantKind.Crop && Generation.Enabled)
        {
            diagnostics.Info(Tr.T("Crops are not generated in the world: the generation is ignored."), source, Id);
        }

        if (Generation.Enabled && (Generation.Rarity < 1 || Generation.PatchSize < 1))
        {
            diagnostics.Error(Tr.T("Generation: the rarity and the size of the patches must be at least 1."), source, Id);
        }
    }
}
