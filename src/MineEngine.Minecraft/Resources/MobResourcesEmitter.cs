using System.Text.Json.Nodes;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Minecraft.Resources;

/// <summary>Texture, modèle de l'œuf d'apparition et butin (loot table) de chaque mob.</summary>
public sealed class MobResourcesEmitter : ResourceEmitter
{
    public MobResourcesEmitter(PlaceholderTexture placeholder)
        : base(placeholder)
    {
    }

    public override string Name => "Ressources des mobs";

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        foreach (IRMob mob in context.Ir.Content.Mobs)
        {
            // Sans texture, le moteur de rendu reprend celle de la créature d'origine.
            if (mob.Texture is not null)
            {
                WriteTexture(context, mob.Texture, paths.EntityTexture(mob.Id));
            }

            if (mob.SpawnEgg is { } egg)
            {
                WriteJson(context, paths.ItemModel(egg.ItemId), new JsonObject { ["parent"] = "minecraft:item/template_spawn_egg" });
            }

            if (mob.Drops.Count > 0)
            {
                WriteJson(context, paths.EntityLootTable(mob.Id), CreateLootTable(mob));
            }
        }
    }

    /// <summary>Un tirage par item laissé, avec une quantité aléatoire entre le minimum et le maximum.</summary>
    private static JsonObject CreateLootTable(IRMob mob)
    {
        var pools = new JsonArray();
        foreach (IRMobDrop drop in mob.Drops)
        {
            var entry = new JsonObject { ["type"] = "minecraft:item", ["name"] = drop.Item.ToString() };
            if (drop.Min != 1 || drop.Max != 1)
            {
                entry["functions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["function"] = "minecraft:set_count",
                        ["count"] = new JsonObject { ["type"] = "minecraft:uniform", ["min"] = drop.Min, ["max"] = drop.Max },
                    },
                };
            }

            pools.Add(new JsonObject { ["rolls"] = 1, ["entries"] = new JsonArray { entry } });
        }

        return new JsonObject { ["type"] = "minecraft:entity", ["pools"] = pools };
    }
}
