using System.Text.Json.Nodes;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Texture, modèle de l'œuf d'apparition et butin (loot table) de chaque mob ; V0.7.9 : textures
/// des variations et des calques, texture propre de l'œuf, type de créature en tag (depuis 1.20.5).
/// </summary>
public sealed class MobResourcesEmitter : ResourceEmitter
{
    public MobResourcesEmitter(PlaceholderTexture placeholder)
        : base(placeholder)
    {
    }

    public override string Name => Tr.T("Mob resources");

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        foreach (IRMob mob in context.Ir.Content.Mobs)
        {
            // Sans texture, le moteur de rendu reprend celle de la créature d'origine ; un modèle 3D
            // donne la sienne, ou la texture de remplacement s'il n'en a aucune.
            if (mob.Texture is not null)
            {
                WriteTexture(context, mob.Texture, paths.EntityTexture(mob.Id));
            }
            else if (mob.CustomModel is { } custom && context.Ir.Content.Models.FirstOrDefault(m => m.Id == custom) is { Textures.Count: 0 })
            {
                WriteTexture(context, IRTexture.Placeholder, paths.EntityTexture(mob.Id));
            }

            for (int number = 1; number <= mob.Appearances.Count; number++)
            {
                if (mob.Appearances[number - 1].Texture is { } texture)
                {
                    WriteTexture(context, texture, paths.EntityTexture(IRAppearance.FileId(mob.Id, number)));
                }
            }

            for (int i = 0; i < mob.Variations.Count; i++)
            {
                WriteTexture(context, mob.Variations[i], paths.EntityTexture(ResourceId.Parse($"{mob.Id}_variation_{i + 1}")));
            }

            for (int i = 0; i < mob.Layers.Count; i++)
            {
                WriteTexture(context, mob.Layers[i].Texture, paths.EntityTexture(ResourceId.Parse($"{mob.Id}_layer_{i + 1}")));
            }

            if (mob.SpawnEgg is { Texture: { } eggTexture } texturedEgg)
            {
                // Its own image instead of the colored egg of the game.
                WriteTexture(context, eggTexture, paths.ItemTexture(ResourceId.Parse(texturedEgg.ItemId)));
                WriteJson(context, paths.ItemModel(texturedEgg.ItemId), new JsonObject
                {
                    ["parent"] = "minecraft:item/generated",
                    ["textures"] = new JsonObject { ["layer0"] = paths.Reference("item/" + texturedEgg.ItemId) },
                });
            }
            else if (mob.SpawnEgg is { } egg)
            {
                WriteJson(context, paths.ItemModel(egg.ItemId), new JsonObject { ["parent"] = "minecraft:item/template_spawn_egg" });
            }

            if (mob.Vehicle is { } vehicle)
            {
                // Its own image, or the one of the oak boat or the minecart of the game.
                string texture = vehicle.Kind == MobVehicleKind.Boat ? "minecraft:item/oak_boat" : "minecraft:item/minecart";
                if (vehicle.Texture is { } image)
                {
                    WriteTexture(context, image, paths.ItemTexture(ResourceId.Parse(vehicle.ItemId)));
                    texture = paths.Reference("item/" + vehicle.ItemId);
                }

                WriteJson(context, paths.ItemModel(vehicle.ItemId), new JsonObject
                {
                    ["parent"] = "minecraft:item/generated",
                    ["textures"] = new JsonObject { ["layer0"] = texture },
                });
            }

            if (mob.Drops.Count > 0)
            {
                WriteJson(context, paths.EntityLootTable(mob.Id), CreateLootTable(mob));
            }
        }

        WriteCreatureTypeTags(context);
    }

    /// <summary>
    /// Since 1.20.5 the type of a creature (undead, arthropod, illager, aquatic) is a tag of entity types
    /// of the game; before, the mob says it itself (getMobType).
    /// </summary>
    private void WriteCreatureTypeTags(GenerationContext context)
    {
        if (!JavaGameApi.For(context.MinecraftVersion).CreatureTypeIsTag)
        {
            return;
        }

        foreach (var group in context.Ir.Content.Mobs.Where(m => m.CreatureType != MobCreatureType.Undefined).GroupBy(m => m.CreatureType))
        {
            string tag = group.Key switch
            {
                MobCreatureType.Undead => "undead",
                MobCreatureType.Arthropod => "arthropod",
                MobCreatureType.Illager => "illager",
                _ => "aquatic",
            };
            WriteJson(context, context.Paths.VanillaEntityTypeTag(tag), new JsonObject
            {
                ["replace"] = false,
                ["values"] = new JsonArray([.. group.Select(m => (JsonNode?)JsonValue.Create(context.Paths.Reference(m.Id.Value)))]),
            });
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
