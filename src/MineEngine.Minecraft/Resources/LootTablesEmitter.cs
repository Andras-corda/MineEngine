using System.Text.Json.Nodes;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Drop tables of the project (V0.7.10), as loot tables of the game. Looting and silk touch are written
/// in the form of each version: an enchantment function and an item predicate changed in 1.20.5.
/// </summary>
public sealed class LootTablesEmitter : IFileEmitter
{
    public string Name => Tr.T("Drop tables");

    public void Emit(GenerationContext context)
    {
        bool modern = JavaGameApi.For(context.MinecraftVersion).UsesDataComponents;
        foreach (IRLootTable table in context.Ir.Content.LootTables)
        {
            var json = new JsonObject
            {
                ["type"] = "minecraft:" + table.Kind.ToString().ToLowerInvariant(),
                ["pools"] = new JsonArray([.. table.Pools.Select(p => (JsonNode)Pool(p, modern))]),
            };
            context.Files.WriteText(context.Paths.LootTable(table.Id), MineEngine.Core.Json.JsonFormatting.ToText(json));
        }
    }

    /// <summary>
    /// A loot table of the game made of the pools of a drop table of the project (a block, a mob): its type
    /// is the one of what drops it, and each pool takes the conditions of that element (its tool...).
    /// </summary>
    public static JsonObject? FromProjectTable(GenerationContext context, ResourceId? tableId, string type, Func<JsonArray?>? conditions = null)
    {
        if (tableId is null || context.Ir.Content.LootTables.FirstOrDefault(t => t.Id == tableId) is not { } table)
        {
            return null;
        }

        bool modern = JavaGameApi.For(context.MinecraftVersion).UsesDataComponents;
        var pools = new JsonArray();
        foreach (IRLootPool pool in table.Pools)
        {
            JsonObject json = Pool(pool, modern);
            if (conditions?.Invoke() is { Count: > 0 } extra)
            {
                json["conditions"] = extra;
            }

            pools.Add(json);
        }

        return new JsonObject { ["type"] = type, ["pools"] = pools };
    }

    public static JsonObject Pool(IRLootPool pool, bool modern)
    {
        var json = new JsonObject
        {
            ["rolls"] = Number(pool.RollsMin, pool.RollsMax),
            ["entries"] = new JsonArray([.. pool.Entries.Select(e => (JsonNode)Entry(e, modern))]),
        };
        if (pool.BonusRolls > 0)
        {
            json["bonus_rolls"] = pool.BonusRolls;
        }

        return json;
    }

    private static JsonObject Entry(IRLootEntry entry, bool modern)
    {
        JsonObject json = entry.Item switch
        {
            null => new JsonObject { ["type"] = "minecraft:empty" },
            { IsTag: true } tag => new JsonObject { ["type"] = "minecraft:tag", ["name"] = tag.Id.ToString(), ["expand"] = true },
            { } item => new JsonObject { ["type"] = "minecraft:item", ["name"] = item.Id.ToString() },
        };
        if (entry.Weight != 1)
        {
            json["weight"] = entry.Weight;
        }

        var conditions = new JsonArray();
        if (entry.Chance < 100)
        {
            conditions.Add(new JsonObject { ["condition"] = "minecraft:random_chance", ["chance"] = entry.Chance / 100.0 });
        }

        if (entry.KilledByPlayer)
        {
            conditions.Add(new JsonObject { ["condition"] = "minecraft:killed_by_player" });
        }

        if (entry.SurvivesExplosion)
        {
            conditions.Add(new JsonObject { ["condition"] = "minecraft:survives_explosion" });
        }

        if (entry.SilkTouch != LootSilkTouch.Any)
        {
            JsonObject silk = SilkTouch(modern);
            conditions.Add(entry.SilkTouch == LootSilkTouch.Required ? silk : new JsonObject { ["condition"] = "minecraft:inverted", ["term"] = silk });
        }

        if (conditions.Count > 0)
        {
            json["conditions"] = conditions;
        }

        var functions = new JsonArray();
        if (entry.Item is not null && (entry.CountMin != 1 || entry.CountMax != 1))
        {
            functions.Add(new JsonObject { ["function"] = "minecraft:set_count", ["count"] = Number(entry.CountMin, entry.CountMax) });
        }

        if (entry.Item is not null && entry.LootingBonus > 0)
        {
            functions.Add(modern
                ? new JsonObject
                {
                    ["function"] = "minecraft:enchanted_count_increase",
                    ["enchantment"] = "minecraft:looting",
                    ["count"] = Uniform(0, entry.LootingBonus),
                }
                : new JsonObject { ["function"] = "minecraft:looting_enchant", ["count"] = Uniform(0, entry.LootingBonus) });
        }

        if (entry.Item is not null && entry.SmeltIfOnFire)
        {
            functions.Add(new JsonObject
            {
                ["function"] = "minecraft:furnace_smelt",
                ["conditions"] = new JsonArray(new JsonObject
                {
                    ["condition"] = "minecraft:entity_properties",
                    ["entity"] = "this",
                    ["predicate"] = new JsonObject { ["flags"] = new JsonObject { ["is_on_fire"] = true } },
                }),
            });
        }

        if (entry.Item is not null && entry.EnchantRandomly)
        {
            functions.Add(new JsonObject { ["function"] = "minecraft:enchant_randomly" });
        }

        if (functions.Count > 0)
        {
            json["functions"] = functions;
        }

        return json;
    }

    /// <summary>The tool has silk touch: the predicate of an item changed in 1.20.5 (enchantments became a component).</summary>
    private static JsonObject SilkTouch(bool modern) => new()
    {
        ["condition"] = "minecraft:match_tool",
        ["predicate"] = modern
            ? new JsonObject
            {
                ["predicates"] = new JsonObject
                {
                    ["minecraft:enchantments"] = new JsonArray(new JsonObject
                    {
                        ["enchantments"] = "minecraft:silk_touch",
                        ["levels"] = new JsonObject { ["min"] = 1 },
                    }),
                },
            }
            : new JsonObject
            {
                ["enchantments"] = new JsonArray(new JsonObject
                {
                    ["enchantment"] = "minecraft:silk_touch",
                    ["levels"] = new JsonObject { ["min"] = 1 },
                }),
            },
    };

    /// <summary>A fixed number, or a uniform number between two values.</summary>
    private static JsonNode Number(int min, int max) => min == max ? JsonValue.Create(min) : Uniform(min, max);

    private static JsonObject Uniform(int min, int max) => new() { ["type"] = "minecraft:uniform", ["min"] = min, ["max"] = max };
}
