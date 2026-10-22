using System.Text.Json;
using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>Tag of the Tags tab (V0.7.1).</summary>
public sealed class TagAssetDefinition : AssetDefinition<TagAsset>
{
    public override AssetType Type => AssetType.Tag;

    public override string Label => Tr.T("Tag");

    public override string DefaultResourceIdBase => "new_tag";

    public override string DefaultDisplayName => Tr.T("new tag");

    protected override TagAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(TagAsset asset, JsonPropertyReader reader)
    {
        asset.Registry = reader.GetEnum("registry", TagRegistry.Items);
        asset.Namespace = reader.GetString("namespace") ?? TagAsset.ModNamespace;
        asset.TagPath = reader.GetString("path") ?? asset.ResourceId.Value;
        asset.Replace = reader.GetBoolean("replace", false);
        asset.Values = reader.GetStringList("values");
    }

    protected override void WriteProperties(TagAsset asset, JsonObject properties)
    {
        properties["registry"] = asset.Registry.ToString();
        properties["namespace"] = asset.Namespace;
        properties["path"] = asset.TagPath;
        properties["replace"] = asset.Replace;
        properties["values"] = new JsonArray([.. asset.Values.Select(v => JsonValue.Create(v))]);
    }
}

/// <summary>Plant (V0.7.0).</summary>
public sealed class PlantAssetDefinition : InventoryAssetDefinition<PlantAsset>
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) } };

    public override AssetType Type => AssetType.Plant;

    public override string Label => Tr.T("Plant");

    public override string DefaultResourceIdBase => "new_plant";

    public override string DefaultDisplayName => Tr.T("New plant");

    protected override PlantAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(PlantAsset asset, JsonPropertyReader reader)
    {
        ReadInventory(asset, reader);
        asset.Kind = reader.GetEnum("kind", PlantKind.Flower);
        asset.Soil = reader.GetEnum("soil", PlantSoil.Dirt);
        asset.TopTextureId = reader.GetGuid("topTexture");
        asset.StageTextures = [.. reader.GetStringList("stages").Select(s => Guid.TryParse(s, out Guid id) ? id : Guid.Empty).Where(id => id != Guid.Empty)];
        asset.MaxHeight = reader.GetInt32("maxHeight", 3);
        asset.LightLevel = reader.GetInt32("lightLevel", 0);
        asset.Sound = reader.GetEnum("sound", BlockSoundType.Grass);
        asset.Produce = reader.GetReference("produce");
        asset.ProduceMin = reader.GetInt32("produceMin", 1);
        asset.ProduceMax = reader.GetInt32("produceMax", 1);
        try
        {
            asset.Generation = reader.GetNode("generation")?.Deserialize<PlantGeneration>(Json) ?? new PlantGeneration();
        }
        catch (JsonException)
        {
            asset.Generation = new PlantGeneration();
        }
    }

    protected override void WriteProperties(PlantAsset asset, JsonObject properties)
    {
        WriteInventory(asset, properties);
        properties["kind"] = asset.Kind.ToString();
        properties["soil"] = asset.Soil.ToString();
        properties["topTexture"] = asset.TopTextureId?.ToString("D");
        properties["stages"] = new JsonArray([.. asset.StageTextures.Select(s => JsonValue.Create(s.ToString("D")))]);
        properties["maxHeight"] = asset.MaxHeight;
        properties["lightLevel"] = asset.LightLevel;
        properties["sound"] = asset.Sound.ToString();
        properties["produce"] = asset.Produce?.ToStorageText();
        properties["produceMin"] = asset.ProduceMin;
        properties["produceMax"] = asset.ProduceMax;
        properties["generation"] = JsonSerializer.SerializeToNode(asset.Generation, Json);
    }
}

/// <summary>Biome (V0.7.0).</summary>
public sealed class BiomeAssetDefinition : AssetDefinition<BiomeAsset>
{
    private static readonly JsonSerializerOptions Json = new();

    public override AssetType Type => AssetType.Biome;

    public override string Label => Tr.T("Biome");

    public override string DefaultResourceIdBase => "new_biome";

    public override string DefaultDisplayName => Tr.T("New biome");

    protected override BiomeAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(BiomeAsset asset, JsonPropertyReader reader)
    {
        asset.BaseBiome = reader.GetString("base") ?? "minecraft:plains";
        asset.Temperature = reader.GetDouble("temperature", 0.8);
        asset.Downfall = reader.GetDouble("downfall", 0.4);
        asset.HasPrecipitation = reader.GetBoolean("precipitation", true);
        asset.SkyColor = reader.GetInt32("sky", 0x78A7FF);
        asset.FogColor = reader.GetInt32("fog", 0xC0D8FF);
        asset.WaterColor = reader.GetInt32("water", 0x3F76E4);
        asset.WaterFogColor = reader.GetInt32("waterFog", 0x050533);
        asset.GrassColor = reader.GetNode("grass") is null ? null : reader.GetInt32("grass", 0);
        asset.FoliageColor = reader.GetNode("foliage") is null ? null : reader.GetInt32("foliage", 0);
        asset.TopBlock = reader.GetString("topBlock") ?? string.Empty;
        asset.UnderBlock = reader.GetString("underBlock") ?? string.Empty;
        asset.GeneratesInOverworld = reader.GetBoolean("overworld", true);
        asset.ClimateTemperature = Climate(reader, "climateTemperature", 0.2);
        asset.Humidity = Climate(reader, "humidity", 0);
        asset.Continentalness = Climate(reader, "continentalness", 0.3);
        asset.Erosion = Climate(reader, "erosion", 0);
        asset.Weirdness = Climate(reader, "weirdness", 0);
    }

    private static ClimateRange Climate(JsonPropertyReader reader, string name, double center)
    {
        try
        {
            return reader.GetNode(name)?.Deserialize<ClimateRange>(Json) ?? new ClimateRange(center);
        }
        catch (JsonException)
        {
            return new ClimateRange(center);
        }
    }

    protected override void WriteProperties(BiomeAsset asset, JsonObject properties)
    {
        properties["base"] = asset.BaseBiome;
        properties["temperature"] = asset.Temperature;
        properties["downfall"] = asset.Downfall;
        properties["precipitation"] = asset.HasPrecipitation;
        properties["sky"] = asset.SkyColor;
        properties["fog"] = asset.FogColor;
        properties["water"] = asset.WaterColor;
        properties["waterFog"] = asset.WaterFogColor;
        properties["grass"] = asset.GrassColor;
        properties["foliage"] = asset.FoliageColor;
        properties["topBlock"] = asset.TopBlock;
        properties["underBlock"] = asset.UnderBlock;
        properties["overworld"] = asset.GeneratesInOverworld;
        properties["climateTemperature"] = JsonSerializer.SerializeToNode(asset.ClimateTemperature, Json);
        properties["humidity"] = JsonSerializer.SerializeToNode(asset.Humidity, Json);
        properties["continentalness"] = JsonSerializer.SerializeToNode(asset.Continentalness, Json);
        properties["erosion"] = JsonSerializer.SerializeToNode(asset.Erosion, Json);
        properties["weirdness"] = JsonSerializer.SerializeToNode(asset.Weirdness, Json);
    }
}
