using MineEngine.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>
/// Tag of the Tags tab (V0.7.1): <paramref name="Namespace"/> is the one of the file (the id of the
/// mod for its own tags); <paramref name="Values"/> are full entries ("minecraft:stone", "#c:ores",
/// "othermod:thing?" for an optional one).
/// </summary>
public sealed record IRTag(Guid SourceAssetId, TagRegistry Registry, string Namespace, string Path, bool Replace, IReadOnlyList<string> Values);

/// <summary>What makes a block a plant (V0.7.0): its kind, its soil, its other textures, its crop and its patches.</summary>
public sealed record IRPlant(PlantKind Kind, PlantSoil Soil)
{
    /// <summary>Top half of a tall plant.</summary>
    public IRTexture? TopTexture { get; init; }

    /// <summary>Stages of a crop, from the seed to the ripe plant.</summary>
    public IReadOnlyList<IRTexture> Stages { get; init; } = [];

    /// <summary>Highest stack of a growing plant.</summary>
    public int MaxHeight { get; init; } = 3;

    /// <summary>Item dropped by a ripe crop, or null.</summary>
    public NamespacedId? Produce { get; init; }

    public int ProduceMin { get; init; } = 1;

    public int ProduceMax { get; init; } = 1;

    public PlantGeneration Generation { get; init; } = new();
}

/// <summary>Biome of the mod (V0.7.0).</summary>
public sealed record IRBiome(Guid SourceAssetId, ResourceId Id, string DisplayName)
{
    public string BaseBiome { get; init; } = "minecraft:plains";

    public double Temperature { get; init; } = 0.8;

    public double Downfall { get; init; } = 0.4;

    public bool HasPrecipitation { get; init; } = true;

    public int SkyColor { get; init; }

    public int FogColor { get; init; }

    public int WaterColor { get; init; }

    public int WaterFogColor { get; init; }

    public int? GrassColor { get; init; }

    public int? FoliageColor { get; init; }

    /// <summary>Block of the surface ("minecraft:grass_block"), or null.</summary>
    public string? TopBlock { get; init; }

    /// <summary>Block under the surface, or null.</summary>
    public string? UnderBlock { get; init; }

    public bool GeneratesInOverworld { get; init; } = true;

    public ClimateRange ClimateTemperature { get; init; } = new();

    public ClimateRange Humidity { get; init; } = new();

    public ClimateRange Continentalness { get; init; } = new();

    public ClimateRange Erosion { get; init; } = new();

    public ClimateRange Weirdness { get; init; } = new();
}
