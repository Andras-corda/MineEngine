using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// The damage types of V0.7.10: a rune zap that ignores armor and counts as lightning, the stone
/// guardian immune to it, and the ruby sword that zaps the creatures it hits. Added with the samples,
/// so that the end-to-end builds compile them.
/// </summary>
internal static class DamageSample
{
    public static DamageTypeAsset AddTo(ModProject project)
    {
        var zap = new DamageTypeAsset(Guid.NewGuid(), ResourceId.Parse("rune_zap"), "Rune zap")
        {
            DeathMessage = "{victim} was zapped by a rune (100%)",
            KillerMessage = "{victim} was zapped by {killer}",
            Exhaustion = 0.3f,
            Scaling = DamageScaling.Never,
            Effects = DamageEffects.Burning,
            Flags = new HashSet<DamageTypeFlag> { DamageTypeFlag.BypassesArmor, DamageTypeFlag.IsLightning },
        };
        project.Assets.Add(zap);

        MobAsset guardian = project.Assets.OfType<MobAsset>().Single(m => m.ResourceId.Value == "stone_guardian");
        guardian.ImmuneDamageTypes = new HashSet<Guid> { zap.Id };

        ItemAsset sword = project.Assets.OfType<ItemAsset>().Single(i => i.ResourceId.Value == "ruby_sword");
        var builder = new GraphBuilder();
        GraphNode hit = builder.Add(GraphNodeKind.OnItemHitEntity, 0, 0, ("item", ContentReference.ToAsset(sword.Id).ToStorageText()));
        GraphNode source = builder.Add(GraphNodeKind.DamageOfType, 300, 120, ("type", "rune_zap"));
        GraphNode damage = builder.Add(GraphNodeKind.DamageWithSource, 600, 0, ("amount", "4"));
        builder.Exec(hit, damage)
            .Data(hit, "player", source, "attacker")
            .Data(hit, "other", damage, "entity")
            .Data(source, "source", damage, "source");
        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("zap_events"), "Zap events") { Graph = builder.Build() });
        return zap;
    }
}
