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

    public string Name => "Apparition naturelle des mobs";

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
    }
}
