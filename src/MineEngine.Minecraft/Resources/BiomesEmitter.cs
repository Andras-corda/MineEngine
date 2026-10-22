using System.Text.Json.Nodes;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Project.Vanilla;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Biomes of the mod (V0.7.0), data/&lt;mod&gt;/worldgen/biome: the biome of Minecraft chosen as a base
/// (read from the game of this computer, so that its trees, ores, caves and mobs keep the order the
/// game expects), with the climate and the colors of the biome of the mod. Without the files of the
/// game, the biome has no vegetation of its own.
/// </summary>
public sealed class BiomesEmitter : IFileEmitter
{
    private readonly Func<string, string?> _readVanillaBiome;

    public BiomesEmitter()
        : this(null)
    {
    }

    /// <param name="readVanillaBiome">Reads "data/minecraft/worldgen/biome/plains.json" of the game (tests); null: the game of this computer.</param>
    public BiomesEmitter(Func<string, string?>? readVanillaBiome)
    {
        _readVanillaBiome = readVanillaBiome ?? (_ => null);
    }

    public string Name => Tr.T("Biomes");

    public void Emit(GenerationContext context)
    {
        IReadOnlyList<IRBiome> biomes = context.Ir.Content.Biomes;
        if (biomes.Count == 0)
        {
            return;
        }

        VanillaAssetLibrary? library = null;
        string? Vanilla(string entry)
        {
            if (_readVanillaBiome(entry) is { } text)
            {
                return text;
            }

            string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MineEngine", "vanilla");
            library ??= VanillaAssetLibrary.For(context.MinecraftVersion.ToString(), cache);
            return library?.ReadEntry(entry);
        }

        foreach (IRBiome biome in biomes)
        {
            string baseId = biome.BaseBiome.Contains(':', StringComparison.Ordinal) ? biome.BaseBiome : "minecraft:" + biome.BaseBiome;
            string[] parts = baseId.Split(':', 2);
            JsonObject json = (Vanilla($"data/{parts[0]}/worldgen/biome/{parts[1]}.json") is { } text ? JsonNode.Parse(text) as JsonObject : null)
                              ?? Fallback();
            json["has_precipitation"] = biome.HasPrecipitation;
            json["temperature"] = biome.Temperature;
            json["downfall"] = biome.Downfall;
            JsonObject effects = json["effects"] as JsonObject ?? [];
            effects["sky_color"] = biome.SkyColor;
            effects["fog_color"] = biome.FogColor;
            effects["water_color"] = biome.WaterColor;
            effects["water_fog_color"] = biome.WaterFogColor;
            SetOrRemove(effects, "grass_color", biome.GrassColor);
            SetOrRemove(effects, "foliage_color", biome.FoliageColor);
            effects.Remove("grass_color_modifier");
            json["effects"] = effects;
            context.Files.WriteText(context.Paths.Worldgen("biome", biome.Id.Value), JsonFormatting.ToText(json));
        }
    }

    private static void SetOrRemove(JsonObject json, string key, int? value)
    {
        if (value is { } color)
        {
            json[key] = color;
        }
        else
        {
            json.Remove(key);
        }
    }

    /// <summary>A biome without features (the base biome of the game was not found).</summary>
    private static JsonObject Fallback()
    {
        var features = new JsonArray();
        for (int step = 0; step < 11; step++)
        {
            features.Add(new JsonArray());
        }

        return new JsonObject
        {
            ["effects"] = new JsonObject
            {
                ["mood_sound"] = new JsonObject
                {
                    ["block_search_extent"] = 8,
                    ["offset"] = 2.0,
                    ["sound"] = "minecraft:ambient.cave",
                    ["tick_delay"] = 6000,
                },
            },
            ["spawners"] = new JsonObject(),
            ["spawn_costs"] = new JsonObject(),
            ["carvers"] = new JsonObject { ["air"] = new JsonArray() },
            ["features"] = features,
        };
    }
}
