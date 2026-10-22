using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>A value of the climate where a biome appears, between -1 and 1: its center and its spread.</summary>
public sealed record ClimateRange(double Center = 0, double Spread = 0.2)
{
    public double Min => Math.Clamp(Center - Spread, -2, 2);

    public double Max => Math.Clamp(Center + Spread, -2, 2);
}

/// <summary>
/// Biome of the mod (V0.7.0): its climate, its colors, the blocks of its surface, and where it
/// appears in the overworld. It starts from a biome of Minecraft (plains, forest...) whose
/// vegetation, ores, caves and mobs it keeps.
/// </summary>
public sealed class BiomeAsset : Asset
{
    private string _baseBiome = "minecraft:plains";
    private double _temperature = 0.8;
    private double _downfall = 0.4;
    private bool _hasPrecipitation = true;
    private int _skyColor = 0x78A7FF;
    private int _fogColor = 0xC0D8FF;
    private int _waterColor = 0x3F76E4;
    private int _waterFogColor = 0x050533;
    private int? _grassColor;
    private int? _foliageColor;
    private string _topBlock = string.Empty;
    private string _underBlock = string.Empty;
    private bool _generatesInOverworld = true;
    private ClimateRange _climateTemperature = new(0.2);
    private ClimateRange _humidity = new(0);
    private ClimateRange _continentalness = new(0.3);
    private ClimateRange _erosion = new(0);
    private ClimateRange _weirdness = new(0);

    public BiomeAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Biome;

    /// <summary>Biome of Minecraft whose features (trees, ores, caves) and mobs are kept.</summary>
    public string BaseBiome
    {
        get => _baseBiome;
        set => SetField(ref _baseBiome, string.IsNullOrWhiteSpace(value) ? "minecraft:plains" : value.Trim().ToLowerInvariant());
    }

    /// <summary>Temperature of the biome (snow below 0.15; desert: 2).</summary>
    public double Temperature
    {
        get => _temperature;
        set => SetField(ref _temperature, Math.Clamp(value, -2, 2));
    }

    /// <summary>Humidity (rain), from 0 to 1.</summary>
    public double Downfall
    {
        get => _downfall;
        set => SetField(ref _downfall, Math.Clamp(value, 0, 1));
    }

    public bool HasPrecipitation
    {
        get => _hasPrecipitation;
        set => SetField(ref _hasPrecipitation, value);
    }

    public int SkyColor { get => _skyColor; set => SetField(ref _skyColor, value & 0xFFFFFF); }

    public int FogColor { get => _fogColor; set => SetField(ref _fogColor, value & 0xFFFFFF); }

    public int WaterColor { get => _waterColor; set => SetField(ref _waterColor, value & 0xFFFFFF); }

    public int WaterFogColor { get => _waterFogColor; set => SetField(ref _waterFogColor, value & 0xFFFFFF); }

    /// <summary>Color of the grass; null: computed from the temperature and the humidity.</summary>
    public int? GrassColor { get => _grassColor; set => SetField(ref _grassColor, value & 0xFFFFFF); }

    /// <summary>Color of the leaves; null: computed from the temperature and the humidity.</summary>
    public int? FoliageColor { get => _foliageColor; set => SetField(ref _foliageColor, value & 0xFFFFFF); }

    /// <summary>Block of the surface (grass block); empty: the one of the overworld.</summary>
    public string TopBlock
    {
        get => _topBlock;
        set => SetField(ref _topBlock, (value ?? string.Empty).Trim().ToLowerInvariant());
    }

    /// <summary>Blocks under the surface (dirt); empty: the ones of the overworld.</summary>
    public string UnderBlock
    {
        get => _underBlock;
        set => SetField(ref _underBlock, (value ?? string.Empty).Trim().ToLowerInvariant());
    }

    /// <summary>The biome appears in the overworld, where the climate matches.</summary>
    public bool GeneratesInOverworld
    {
        get => _generatesInOverworld;
        set => SetField(ref _generatesInOverworld, value);
    }

    /// <summary>Climate where the biome appears: temperature (-1 frozen, 1 hot).</summary>
    public ClimateRange ClimateTemperature { get => _climateTemperature; set => SetField(ref _climateTemperature, value ?? new()); }

    /// <summary>Climate: humidity (-1 dry, 1 humid).</summary>
    public ClimateRange Humidity { get => _humidity; set => SetField(ref _humidity, value ?? new()); }

    /// <summary>Climate: continentalness (-1 ocean, 1 far inland).</summary>
    public ClimateRange Continentalness { get => _continentalness; set => SetField(ref _continentalness, value ?? new()); }

    /// <summary>Climate: erosion (-1 mountains, 1 flat).</summary>
    public ClimateRange Erosion { get => _erosion; set => SetField(ref _erosion, value ?? new()); }

    /// <summary>Climate: weirdness (variants of the biomes).</summary>
    public ClimateRange Weirdness { get => _weirdness; set => SetField(ref _weirdness, value ?? new()); }

    /// <summary>Biomes of Minecraft offered as a base.</summary>
    public static IReadOnlyList<string> BaseBiomes { get; } =
    [
        "minecraft:plains", "minecraft:sunflower_plains", "minecraft:forest", "minecraft:flower_forest", "minecraft:birch_forest",
        "minecraft:dark_forest", "minecraft:taiga", "minecraft:snowy_plains", "minecraft:snowy_taiga", "minecraft:savanna", "minecraft:desert",
        "minecraft:badlands", "minecraft:jungle", "minecraft:swamp", "minecraft:meadow", "minecraft:cherry_grove", "minecraft:mushroom_fields",
        "minecraft:windswept_hills", "minecraft:beach", "minecraft:river", "minecraft:ocean",
    ];

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();
        if (!System.Text.RegularExpressions.Regex.IsMatch(BaseBiome, "^[a-z0-9_.-]+:[a-z0-9_./-]+$"))
        {
            diagnostics.Error(Tr.T("Base biome: an identifier of a biome of Minecraft (minecraft:plains)."), source, Id);
        }

        foreach ((string block, string label) in new[] { (TopBlock, Tr.T("surface block")), (UnderBlock, Tr.T("block under the surface")) })
        {
            if (block.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(block, "^([a-z0-9_.-]+:)?[a-z0-9_./-]+$"))
            {
                diagnostics.Error(Tr.F("{0}: an identifier of a block (minecraft:grass_block).", label), source, Id);
            }
        }

        if (!GeneratesInOverworld)
        {
            diagnostics.Info(Tr.T("The biome does not appear in the overworld: it is only used by commands (/locate, /fillbiome) and by dimensions."), source, Id);
        }
    }
}
