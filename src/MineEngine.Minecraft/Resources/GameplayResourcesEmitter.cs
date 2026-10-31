using System.Text.Json.Nodes;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Resources of V0.6.3: the block and bucket of each fluid with their textures, the icon of each
/// status effect, and, from 1.21, the enchantments as data (with the tags that place them in the
/// enchanting table, the treasures or the curses). V0.7.10: the damage types and their tags.
/// </summary>
public sealed class GameplayResourcesEmitter : ResourceEmitter
{
    public GameplayResourcesEmitter(PlaceholderTexture placeholder)
        : base(placeholder)
    {
    }

    public override string Name => Tr.T("Fluids, effects and enchantments");

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        IRContent content = context.Ir.Content;

        foreach (IRFluid fluid in content.Fluids)
        {
            EmitFluid(context, paths, fluid);
        }

        foreach (IREffect effect in content.Effects)
        {
            WriteTexture(context, effect.Icon, paths.MobEffectTexture(effect.Id));
        }

        if (JavaGameApi.For(context.MinecraftVersion).EnchantmentsAreData && content.Enchantments.Count > 0)
        {
            EmitEnchantments(context, paths, content.Enchantments);
        }

        if (content.DamageTypes.Count > 0)
        {
            EmitDamageTypes(context, paths, content.DamageTypes);
        }
    }

    /// <summary>
    /// Damage types (V0.7.10): one data file each, named by the translation key of its message, then the
    /// tags of the game that give their properties (the game reads them: armor, fire, projectile...).
    /// </summary>
    private void EmitDamageTypes(GenerationContext context, ResourcePaths paths, IReadOnlyList<IRDamageType> damageTypes)
    {
        foreach (IRDamageType damage in damageTypes)
        {
            WriteJson(context, paths.DamageType(damage.Id), new JsonObject
            {
                ["message_id"] = $"{paths.Namespace}.{damage.Id}",
                ["exhaustion"] = damage.Exhaustion,
                ["scaling"] = damage.Scaling switch
                {
                    DamageScaling.Never => "never",
                    DamageScaling.Always => "always",
                    _ => "when_caused_by_living_non_player",
                },
                ["effects"] = damage.Effects.ToString().ToLowerInvariant(),
                ["death_message_type"] = damage.MessageType switch
                {
                    DeathMessageType.FallVariants => "fall_variants",
                    DeathMessageType.IntentionalGameDesign => "intentional_game_design",
                    _ => "default",
                },
            });
        }

        foreach (IGrouping<DamageTypeFlag, IRDamageType> tag in damageTypes
            .SelectMany(d => d.Flags.Select(f => (Flag: f, Damage: d)))
            .GroupBy(x => x.Flag, x => x.Damage)
            .OrderBy(g => g.Key))
        {
            WriteJson(context, paths.VanillaDamageTypeTag(DamageTypeTag(tag.Key)), new JsonObject
            {
                ["replace"] = false,
                ["values"] = new JsonArray([.. tag.Select(d => (JsonNode?)JsonValue.Create(paths.Reference(d.Id.Value)))]),
            });
        }
    }

    /// <summary>Tag of the game of a property ("bypasses_armor", "is_fire").</summary>
    public static string DamageTypeTag(DamageTypeFlag flag) => string.Concat(flag.ToString().Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    private void EmitFluid(GenerationContext context, ResourcePaths paths, IRFluid fluid)
    {
        string still = paths.Reference($"block/{fluid.Id}_still");
        if (fluid.StillTexture is { } stillTexture)
        {
            WriteTexture(context, stillTexture, paths.BlockTexture(fluid.Id, "_still"));
        }

        if (fluid.FlowingTexture is { } flowingTexture)
        {
            WriteTexture(context, flowingTexture, paths.BlockTexture(fluid.Id, "_flow"));
        }

        // The fluid is drawn by the game: the block only gives the particles of its texture.
        string blockModel = paths.Reference($"block/{fluid.Id}");
        WriteJson(context, paths.BlockState(fluid.Id), new JsonObject
        {
            ["variants"] = new JsonObject { [""] = new JsonObject { ["model"] = blockModel } },
        });
        WriteJson(context, paths.BlockModel(fluid.Id), new JsonObject
        {
            ["textures"] = new JsonObject { ["particle"] = fluid.StillTexture is null ? "minecraft:block/water_still" : still },
        });

        if (fluid.BucketId is { } bucket)
        {
            string layer = "minecraft:item/water_bucket";
            if (fluid.BucketTexture is { } bucketTexture)
            {
                WriteTexture(context, bucketTexture, paths.ItemTexture(ResourceId.Parse(bucket)));
                layer = paths.Reference($"item/{bucket}");
            }

            WriteJson(context, paths.ItemModel(bucket), new JsonObject
            {
                ["parent"] = "minecraft:item/generated",
                ["textures"] = new JsonObject { ["layer0"] = layer },
            });
        }
    }

    /// <summary>Enchantments of 1.21: one JSON file each, and their place in the tags of Minecraft.</summary>
    private static void EmitEnchantments(GenerationContext context, ResourcePaths paths, IReadOnlyList<IREnchantment> enchantments)
    {
        foreach (IREnchantment enchantment in enchantments)
        {
            var json = new JsonObject
            {
                ["description"] = new JsonObject { ["translate"] = paths.EnchantmentTranslationKey(enchantment.Id) },
                ["supported_items"] = "#minecraft:enchantable/" + SupportedItems(enchantment.Target),
                ["weight"] = enchantment.Weight,
                ["max_level"] = enchantment.MaxLevel,
                ["min_cost"] = new JsonObject { ["base"] = 1, ["per_level_above_first"] = 10 },
                ["max_cost"] = new JsonObject { ["base"] = 51, ["per_level_above_first"] = 10 },
                ["anvil_cost"] = AnvilCost(enchantment.Rarity),
                ["slots"] = new JsonArray([.. Slots(enchantment.Target).Select(s => (JsonNode)JsonValue.Create(s))]),
            };
            if (enchantment.DamageBonus > 0)
            {
                // Like Sharpness: the bonus grows by the same amount at each level.
                json["effects"] = new JsonObject
                {
                    ["minecraft:damage"] = new JsonArray(new JsonObject
                    {
                        ["effect"] = new JsonObject
                        {
                            ["type"] = "minecraft:add",
                            ["value"] = new JsonObject
                            {
                                ["type"] = "minecraft:linear",
                                ["base"] = enchantment.DamageBonus,
                                ["per_level_above_first"] = enchantment.DamageBonus,
                            },
                        },
                    }),
                };
            }

            WriteJsonFile(context, paths.Enchantment(enchantment.Id), json);
        }

        // Treasures are found in loot and trades; the others also in the enchanting table.
        WriteTag(context, paths, "non_treasure", enchantments.Where(e => !e.Treasure && !e.Curse));
        WriteTag(context, paths, "treasure", enchantments.Where(e => e.Treasure || e.Curse));
        WriteTag(context, paths, "curse", enchantments.Where(e => e.Curse));
        WriteTag(context, paths, "tradeable", enchantments.Where(e => !e.Curse));
        WriteTag(context, paths, "on_random_loot", enchantments);
    }

    private static void WriteTag(GenerationContext context, ResourcePaths paths, string tag, IEnumerable<IREnchantment> enchantments)
    {
        List<IREnchantment> list = [.. enchantments];
        if (list.Count == 0)
        {
            return;
        }

        WriteJsonFile(context, paths.VanillaEnchantmentTag(tag), new JsonObject
        {
            ["replace"] = false,
            ["values"] = new JsonArray([.. list.Select(e => (JsonNode)JsonValue.Create(paths.Reference(e.Id.Value)))]),
        });
    }

    private static void WriteJsonFile(GenerationContext context, string path, JsonNode json) => WriteJson(context, path, json);

    private static string SupportedItems(EnchantmentTarget target) => target switch
    {
        EnchantmentTarget.Sword => "sword",
        EnchantmentTarget.Armor => "armor",
        EnchantmentTarget.Helmet => "head_armor",
        EnchantmentTarget.Chestplate => "chest_armor",
        EnchantmentTarget.Leggings => "leg_armor",
        EnchantmentTarget.Boots => "foot_armor",
        EnchantmentTarget.Tool => "mining",
        EnchantmentTarget.Bow => "bow",
        EnchantmentTarget.Crossbow => "crossbow",
        EnchantmentTarget.Trident => "trident",
        EnchantmentTarget.FishingRod => "fishing",
        EnchantmentTarget.Breakable => "durability",
        _ => "weapon",
    };

    private static IReadOnlyList<string> Slots(EnchantmentTarget target) => target switch
    {
        EnchantmentTarget.Armor => ["armor"],
        EnchantmentTarget.Helmet => ["head"],
        EnchantmentTarget.Chestplate => ["chest"],
        EnchantmentTarget.Leggings => ["legs"],
        EnchantmentTarget.Boots => ["feet"],
        EnchantmentTarget.Breakable => ["any"],
        _ => ["mainhand"],
    };

    private static int AnvilCost(EnchantmentRarity rarity) => rarity switch
    {
        EnchantmentRarity.Common => 1,
        EnchantmentRarity.Uncommon => 2,
        EnchantmentRarity.Rare => 4,
        _ => 8,
    };
}
