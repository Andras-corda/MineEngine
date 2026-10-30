using MineEngine.Core.GameData;

namespace MineEngine.Assets.Mobs;

/// <summary>Biomes of the spawning of a mob as the biome modifiers name them: a biome or a tag of biomes.</summary>
public static class MobBiomes
{
    public static string Reference(SpawnBiome biome) => biome switch
    {
        SpawnBiome.Plains => "minecraft:plains",
        SpawnBiome.Desert => "minecraft:desert",
        SpawnBiome.Swamp => "minecraft:swamp",
        SpawnBiome.Forest => "#minecraft:is_forest",
        SpawnBiome.Taiga => "#minecraft:is_taiga",
        SpawnBiome.Jungle => "#minecraft:is_jungle",
        SpawnBiome.Savanna => "#minecraft:is_savanna",
        SpawnBiome.Mountain => "#minecraft:is_mountain",
        SpawnBiome.Badlands => "#minecraft:is_badlands",
        SpawnBiome.Beach => "#minecraft:is_beach",
        SpawnBiome.Ocean => "#minecraft:is_ocean",
        SpawnBiome.River => "#minecraft:is_river",
        SpawnBiome.Nether => "#minecraft:is_nether",
        SpawnBiome.End => "#minecraft:is_end",
        _ => "#minecraft:is_overworld",
    };
}
