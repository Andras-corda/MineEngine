using System.Text.Json.Nodes;
using MineEngine.Core.GameData;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Apparition naturelle des mobs : un "biome modifier" par biome choisi. Le format est
/// le même pour Forge et NeoForge ; seuls le dossier et le type changent.
/// </summary>
public sealed class SpawnBiomeModifierEmitter : IFileEmitter
{
    private readonly string _loaderNamespace;

    /// <param name="loaderNamespace">"forge" ou "neoforge".</param>
    public SpawnBiomeModifierEmitter(string loaderNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loaderNamespace);
        _loaderNamespace = loaderNamespace;
    }

    public string Name => Tr.T("Natural mob spawning");

    public void Emit(GenerationContext context)
    {
        foreach (IRMob mob in context.Ir.Content.Mobs)
        {
            if (mob.Spawning is not { } spawning)
            {
                continue;
            }

            foreach (SpawnBiome biome in spawning.Biomes)
            {
                var modifier = new JsonObject
                {
                    ["type"] = $"{_loaderNamespace}:add_spawns",
                    ["biomes"] = MobJava.Biomes(biome),
                    ["spawners"] = new JsonObject
                    {
                        ["type"] = context.Paths.Reference(mob.Id.Value),
                        ["weight"] = spawning.Weight,
                        ["minCount"] = spawning.MinGroup,
                        ["maxCount"] = spawning.MaxGroup,
                    },
                };

                string name = $"spawn_{mob.Id}_{biome.ToString().ToLowerInvariant()}";
                context.Files.WriteText(context.Paths.BiomeModifier(_loaderNamespace, name), JsonFormatting.ToText(modifier));
            }
        }

        foreach (IRBlock block in context.Ir.Content.Blocks.Where(b => b.Options.Ore.Enabled))
        {
            WriteOre(context, block);
        }
    }

    /// <summary>
    /// Ore generation: a configured feature (the ore and what it replaces), a placed feature (groups
    /// per chunk, heights) and a biome modifier that adds it to the underground ores of the biomes.
    /// </summary>
    private void WriteOre(GenerationContext context, IRBlock block)
    {
        ResourcePaths paths = context.Paths;
        MineEngine.Assets.BlockOreGeneration ore = block.Options.Ore;
        string name = $"{block.Id}_ore";
        var targets = new JsonArray();
        foreach (string replaced in ore.Replace.Where(r => r.Length > 0))
        {
            JsonObject predicate = replaced.StartsWith('#')
                ? new JsonObject { ["predicate_type"] = "minecraft:tag_match", ["tag"] = replaced[1..] }
                : new JsonObject { ["predicate_type"] = "minecraft:block_match", ["block"] = replaced.Contains(':', StringComparison.Ordinal) ? replaced : "minecraft:" + replaced };
            targets.Add(new JsonObject
            {
                ["target"] = predicate,
                ["state"] = new JsonObject { ["Name"] = paths.Reference(block.Id.Value) },
            });
        }

        var configured = new JsonObject
        {
            ["type"] = "minecraft:ore",
            ["config"] = new JsonObject
            {
                ["size"] = Math.Clamp(ore.GroupSize, 1, 64),
                ["discard_chance_on_air_exposure"] = 0.0,
                ["targets"] = targets,
            },
        };
        var placed = new JsonObject
        {
            ["feature"] = paths.Reference(name),
            ["placement"] = new JsonArray
            {
                new JsonObject { ["type"] = "minecraft:count", ["count"] = Math.Clamp(ore.GroupsPerChunk, 1, 256) },
                new JsonObject { ["type"] = "minecraft:in_square" },
                new JsonObject
                {
                    ["type"] = "minecraft:height_range",
                    ["height"] = new JsonObject
                    {
                        ["type"] = ore.Shape == MineEngine.Assets.OreShape.Triangle ? "minecraft:trapezoid" : "minecraft:uniform",
                        ["min_inclusive"] = new JsonObject { ["absolute"] = Math.Min(ore.MinHeight, ore.MaxHeight) },
                        ["max_inclusive"] = new JsonObject { ["absolute"] = Math.Max(ore.MinHeight, ore.MaxHeight) },
                    },
                },
                new JsonObject { ["type"] = "minecraft:biome" },
            },
        };

        // Biomes: a tag alone, or a list of biomes; nothing chosen: the overworld.
        List<string> biomes = [.. ore.Biomes.Where(b => b.Length > 0)];
        JsonNode biomeSelector = biomes.FirstOrDefault(b => b.StartsWith('#')) is { } tag
            ? JsonValue.Create(tag)!
            : biomes.Count == 0
                ? JsonValue.Create("#minecraft:is_overworld")!
                : new JsonArray([.. biomes.Select(b => (JsonNode)JsonValue.Create(b.Contains(':', StringComparison.Ordinal) ? b : "minecraft:" + b)!)]);
        var modifier = new JsonObject
        {
            ["type"] = $"{_loaderNamespace}:add_features",
            ["biomes"] = biomeSelector,
            ["features"] = paths.Reference(name),
            ["step"] = "underground_ores",
        };

        context.Files.WriteText(paths.Worldgen("configured_feature", name), JsonFormatting.ToText(configured));
        context.Files.WriteText(paths.Worldgen("placed_feature", name), JsonFormatting.ToText(placed));
        context.Files.WriteText(paths.BiomeModifier(_loaderNamespace, name), JsonFormatting.ToText(modifier));
    }
}
