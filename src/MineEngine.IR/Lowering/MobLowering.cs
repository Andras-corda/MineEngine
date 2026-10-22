using MineEngine.Assets;
using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

public sealed class MobLowering : AssetLowering<MobAsset>
{
    public override AssetType AssetType => AssetType.Mob;

    protected override void Lower(MobAsset asset, LoweringContext context)
    {
        (float width, float height) = asset.Hitbox;
        bool isAnimal = asset.Kind == MobKind.Animal;

        context.AddMob(new IRMob(asset.Id, asset.ResourceId, asset.DisplayName)
        {
            Model = asset.Model,
            CustomModel = context.ResolveCustomModel(asset),
            Appearances = context.ResolveAppearances(asset),
            Kind = asset.Kind,
            Texture = context.ResolveOptionalTexture(asset, asset.TextureId),
            Width = width,
            Height = height,
            Health = asset.Health,
            Armor = asset.Armor,
            MovementSpeed = asset.MovementSpeed,
            AttackDamage = asset.AttackDamage,
            FollowRange = asset.FollowRange,
            KnockbackResistance = asset.KnockbackResistance,
            Experience = asset.Experience,
            FireImmune = asset.FireImmune,
            BurnsInDaylight = asset.BurnsInDaylight,
            Persistent = asset.Persistent,
            BehaviorTree = asset.BehaviorTreeId is { } tree ? context.Project.Assets.Find(tree)?.ResourceId : null,
            Goals = asset.UsesBehaviorTree ? [] : [.. asset.Goals.Where(g => isAnimal || !g.Info.RequiresAnimal).Select(g => LowerGoal(asset, g, context)).OfType<IRMobGoal>()],
            BreedingItem = isAnimal ? context.ResolveItem(asset, asset.BreedingItem, Tr.T("Breeding item")) : null,
            AmbientSound = context.ResolveSound(asset, asset.AmbientSound, Tr.T("Ambient sound")),
            HurtSound = context.ResolveSound(asset, asset.HurtSound, Tr.T("Hurt sound")),
            DeathSound = context.ResolveSound(asset, asset.DeathSound, Tr.T("Death sound")),
            Drops = [.. asset.Drops.Select(d => LowerDrop(asset, d, context)).OfType<IRMobDrop>()],
            SpawnEgg = asset.HasSpawnEgg ? new IRSpawnEgg(asset.SpawnEggId, asset.EggPrimaryColor, asset.EggSecondaryColor) : null,
            Spawning = asset.SpawnsNaturally && asset.SpawnBiomes.Count > 0
                ? new IRMobSpawning(
                    [.. asset.SpawnBiomes.Order()],
                    asset.SpawnWeight,
                    Math.Min(asset.SpawnMinGroup, asset.SpawnMaxGroup),
                    Math.Max(asset.SpawnMinGroup, asset.SpawnMaxGroup))
                : null,
        });
    }

    private static IRMobGoal? LowerGoal(MobAsset asset, MobGoal goal, LoweringContext context)
    {
        NamespacedId? item = null;
        if (goal.Info.UsesItem)
        {
            item = context.ResolveItem(asset, goal.Item, Tr.F("AI \"{0}\"", goal.Info.Label));
            if (item is null)
            {
                return null;
            }
        }

        return new IRMobGoal(goal.Kind, goal.Speed, goal.Distance, goal.Target, item, goal.Flag);
    }

    private static IRMobDrop? LowerDrop(MobAsset asset, MobDrop drop, LoweringContext context) =>
        context.ResolveItem(asset, drop.Item, Tr.T("Loot")) is { } item
            ? new IRMobDrop(item, Math.Min(drop.Min, drop.Max), Math.Max(drop.Min, drop.Max))
            : null;
}
