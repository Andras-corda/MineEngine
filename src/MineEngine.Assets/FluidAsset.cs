using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Fluid of the mod (V0.6.3): a source and a flowing fluid, the block placed in the world and,
/// optionally, a bucket ("&lt;id&gt;_bucket"). It looks like water tinted by its color, unless
/// textures are chosen for the still and the flowing fluid.
/// </summary>
public sealed class FluidAsset : Asset
{
    private string _color = "#3F76E4";
    private int _lightLevel;
    private int _flowSpeed = 5;
    private int _spreadDistance = 4;
    private int _levelDecrease = 1;
    private bool _infinite = true;
    private int _density = 1000;
    private int _viscosity = 1000;
    private bool _hasBucket = true;
    private Guid? _stillTextureId;
    private Guid? _flowingTextureId;
    private Guid? _bucketTextureId;

    public FluidAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Fluid;

    /// <summary>Tint of the fluid, "#RRGGBB" (the blue of water: #3F76E4).</summary>
    public string Color
    {
        get => _color;
        set => SetField(ref _color, NormalizeColor(value) ?? _color);
    }

    /// <summary>Light given by the fluid, from 0 to 15 (lava: 15).</summary>
    public int LightLevel
    {
        get => _lightLevel;
        set => SetField(ref _lightLevel, Math.Clamp(value, 0, 15));
    }

    /// <summary>Ticks between two steps of the flow: water 5, lava 30 (slower).</summary>
    public int FlowSpeed
    {
        get => _flowSpeed;
        set => SetField(ref _flowSpeed, Math.Clamp(value, 1, 100));
    }

    /// <summary>How far the fluid looks for a slope to flow to: water 4, lava 2.</summary>
    public int SpreadDistance
    {
        get => _spreadDistance;
        set => SetField(ref _spreadDistance, Math.Clamp(value, 1, 8));
    }

    /// <summary>Level lost per block: water 1 (flows 7 blocks), lava 2 (flows 3 blocks).</summary>
    public int LevelDecrease
    {
        get => _levelDecrease;
        set => SetField(ref _levelDecrease, Math.Clamp(value, 1, 7));
    }

    /// <summary>Two sources make a third one between them, like water.</summary>
    public bool Infinite
    {
        get => _infinite;
        set => SetField(ref _infinite, value);
    }

    /// <summary>Density in kg/m³ (water 1000, lava 3000); a negative value makes it rise like a gas.</summary>
    public int Density
    {
        get => _density;
        set => SetField(ref _density, Math.Clamp(value, -10000, 10000));
    }

    /// <summary>Viscosity (water 1000, lava 6000): how thick it is.</summary>
    public int Viscosity
    {
        get => _viscosity;
        set => SetField(ref _viscosity, Math.Clamp(value, 0, 100000));
    }

    public bool HasBucket
    {
        get => _hasBucket;
        set => SetField(ref _hasBucket, value);
    }

    /// <summary>Identifier of the bucket item.</summary>
    public string BucketId => ResourceId.Value + "_bucket";

    /// <summary>Texture of the still fluid (block atlas); null to use the water texture tinted by the color.</summary>
    public Guid? StillTextureId
    {
        get => _stillTextureId;
        set => SetField(ref _stillTextureId, value == Guid.Empty ? null : value);
    }

    public Guid? FlowingTextureId
    {
        get => _flowingTextureId;
        set => SetField(ref _flowingTextureId, value == Guid.Empty ? null : value);
    }

    /// <summary>Texture of the bucket; null for the water bucket.</summary>
    public Guid? BucketTextureId
    {
        get => _bucketTextureId;
        set => SetField(ref _bucketTextureId, value == Guid.Empty ? null : value);
    }

    /// <summary>Color as a number 0xRRGGBB.</summary>
    public int ColorValue => int.Parse(_color[1..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);

    public override IEnumerable<AssetReference> GetReferences()
    {
        if (StillTextureId is { } still)
        {
            yield return new AssetReference(still, AssetReferenceKind.Texture, Tr.T("still texture"));
        }

        if (FlowingTextureId is { } flowing)
        {
            yield return new AssetReference(flowing, AssetReferenceKind.Texture, Tr.T("flowing texture"));
        }

        if (BucketTextureId is { } bucket)
        {
            yield return new AssetReference(bucket, AssetReferenceKind.Texture, Tr.T("bucket texture"));
        }
    }

    /// <summary>"#RRGGBB" in upper case, or null if the text is not a color.</summary>
    public static string? NormalizeColor(string? text)
    {
        string value = (text ?? string.Empty).Trim().TrimStart('#');
        return value.Length == 6 && value.All(Uri.IsHexDigit) ? "#" + value.ToUpperInvariant() : null;
    }
}
