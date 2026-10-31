using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// The drop tables of V0.7.10: a rune cache (a chest: rubies, a gem of a tag or nothing, then a bonus
/// pool), the spoils of a goblin (looting, smelted on fire, killed by a player, a chance) and the drops
/// of an ore (silk touch or not, survives the explosion). Step 3: a rune vault that drops the ore
/// drops, the stone guardian that drops the goblin spoils, and a graph that drops the rune cache where
/// the vault is placed. Added with the samples, so that the end-to-end builds load them.
/// </summary>
internal static class LootSample
{
    public static void AddTo(ModProject project, ItemAsset ruby)
    {
        ContentReference Id(string id) => ContentReference.ToId(NamespacedId.TryParse(id, out NamespacedId? parsed) ? parsed! : throw new ArgumentException(id));
        ContentReference Tag(string id) => ContentReference.ToTag(NamespacedId.TryParse(id, out NamespacedId? parsed) ? parsed! : throw new ArgumentException(id));

        project.Assets.Add(new LootTableAsset(Guid.NewGuid(), ResourceId.Parse("rune_cache"), "Rune cache")
        {
            Kind = LootTableKind.Chest,
            Pools =
            [
                new LootPool
                {
                    RollsMin = 1, RollsMax = 3, BonusRolls = 1f,
                    Entries =
                    [
                        new LootEntry { Item = ContentReference.ToAsset(ruby.Id), Weight = 3, CountMin = 1, CountMax = 4, EnchantRandomly = false },
                        new LootEntry { Item = Tag("c:gems"), Weight = 1 },
                        new LootEntry { Kind = LootEntryKind.Empty, Weight = 2 },
                    ],
                },
                new LootPool { Entries = [new LootEntry { Item = Id("minecraft:diamond_sword"), EnchantRandomly = true, Chance = 25 }] },
            ],
        });
        var spoils = new LootTableAsset(Guid.NewGuid(), ResourceId.Parse("goblin_spoils"), "Goblin spoils")
        {
            Kind = LootTableKind.Entity,
            Pools =
            [
                new LootPool
                {
                    Entries = [new LootEntry { Item = Id("minecraft:beef"), CountMin = 0, CountMax = 2, LootingBonus = 2, SmeltIfOnFire = true, KilledByPlayer = true, Chance = 50 }],
                },
            ],
        };
        project.Assets.Add(spoils);
        var ore = new LootTableAsset(Guid.NewGuid(), ResourceId.Parse("ore_drops"), "Ore drops")
        {
            Kind = LootTableKind.Block,
            Pools =
            [
                new LootPool
                {
                    Entries =
                    [
                        new LootEntry { Item = Id("minecraft:stone"), SilkTouch = LootSilkTouch.Required },
                        new LootEntry { Item = ContentReference.ToAsset(ruby.Id), SilkTouch = LootSilkTouch.Forbidden, SurvivesExplosion = true },
                    ],
                },
            ],
        };
        project.Assets.Add(ore);

        // Step 3: the tables used by a block, a mob and a graph.
        Guid texture = project.Assets.OfType<TextureAsset>().First().Id;
        project.Assets.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("rune_vault"), "Rune vault")
        {
            TextureId = texture,
            DropKind = BlockDropKind.Table,
            DropTable = ore.Id,
            RequiresCorrectTool = true,
            HarvestTool = HarvestTool.Pickaxe,
        });
        project.Assets.OfType<MobAsset>().Single(m => m.ResourceId.Value == "stone_guardian").DropTable = spoils.Id;

        var builder = new GraphBuilder();
        GraphNode placed = builder.Add(GraphNodeKind.OnBlockPlaced, 0, 0, ("block", "rune_vault"));
        GraphNode drop = builder.Add(GraphNodeKind.DropLootTable, 300, 0, ("table", "rune_cache"));
        builder.Exec(placed, drop).Data(placed, "position", drop, "position");
        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("cache_events"), "Cache events") { Graph = builder.Build() });
    }
}
