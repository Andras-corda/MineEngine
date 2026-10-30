using MineEngine.Assets;
using MineEngine.Assets.Graphs;
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
        // A fixed factor of the collision box is applied here; a dynamic one by the mob while it lives.
        (float width, float height) = asset.Hitbox;
        if (asset.HitboxScale.Mode == MobValueMode.Fixed)
        {
            width = (float)Math.Clamp(width * asset.HitboxScale.Value, MobAsset.MinHitbox, MobAsset.MaxHitbox);
            height = (float)Math.Clamp(height * asset.HitboxScale.Value, MobAsset.MinHitbox, MobAsset.MaxHitbox);
        }
        bool isAnimal = asset.Kind == MobKind.Animal;
        bool vehicle = asset.Vehicle.Enabled;

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
            Persistent = asset.Persistent || vehicle,
            VisualScale = Scale(asset, asset.VisualScale, context, Tr.T("visual scale")),
            TextureTransparent = asset.TextureTransparent,
            ShadowRadius = (float)(asset.ShadowRadius * (asset.HitboxScale.Mode == MobValueMode.Fixed ? asset.HitboxScale.Value : 1)),
            PassengerOffset = asset.PassengerOffset,
            NoCollision = asset.NoCollision,
            HitboxScale = asset.HitboxScale.Mode == MobValueMode.Fixed ? IRMobScale.One : Scale(asset, asset.HitboxScale, context, Tr.T("collision box scale")),
            Variations = [.. asset.Variations.Select(v => context.ResolveOptionalTexture(asset, v)).OfType<IRTexture>()],
            Layers = [.. asset.Layers.Select(l => LowerLayer(asset, l, context)).OfType<IRMobLayer>()],
            CreatureType = asset.CreatureType,
            // A minecart climbs the rails of a slope, one block high.
            StepHeight = asset.Vehicle.Kind == MobVehicleKind.Minecart ? Math.Max(1.0, asset.StepHeight) : asset.StepHeight,
            TrackingRange = asset.TrackingRange,
            Knockback = asset.Knockback,
            Taming = asset.Taming.Enabled && isAnimal
                ? [.. asset.Taming.Items.Select(i => context.ResolveItem(asset, i.Item, Tr.T("Taming item")) is { } item ? new IRMobTamingItem(item, i.Chance) : null).OfType<IRMobTamingItem>()]
                : null,
            // A vehicle is always ridden: without the section, it goes forward and needs no saddle.
            Riding = asset.Riding.Enabled
                ? new IRMobRiding(asset.Riding.Forward, asset.Riding.Strafe, asset.Riding.NeedsSaddle, asset.Riding.SaddleLayer < asset.Layers.Count ? asset.Riding.SaddleLayer : -1)
                : vehicle ? new IRMobRiding(true, false, false, -1) : null,
            Vehicle = vehicle
                ? new IRMobVehicle(asset.Vehicle.Kind, asset.VehicleItemId!, asset.Vehicle.Rarity, context.ResolveOptionalTexture(asset, asset.Vehicle.ItemTextureId))
                : null,
            Water = asset.Water.Enabled
                ? new IRMobWater(
                    asset.Water.Breathes, Function(asset, asset.Water.BreathesFunction, context, Tr.T("breathing in fluids"), FunctionResult.Boolean, PinType.Entity),
                    asset.Water.PushedByFluid, Function(asset, asset.Water.PushedFunction, context, Tr.T("push of the fluids"), FunctionResult.Boolean, PinType.Entity),
                    asset.Water.Fluid is { Kind: ContentReferenceKind.Id } fluid ? fluid.Id : null)
                : null,
            Immunities = asset.Vehicle.Kind == MobVehicleKind.Boat ? new HashSet<MobImmunity>(asset.Immunities) { MobImmunity.Drowning } : asset.Immunities,
            Vulnerabilities = asset.Vulnerabilities,
            DespawnFunction = Function(asset, asset.DespawnFunction, context, Tr.T("despawn condition"), FunctionResult.Boolean, PinType.Entity),
            Boss = asset.Boss.Enabled ? new IRMobBoss(asset.Boss.ShowBar, asset.Boss.Color, asset.Boss.Overlay) : null,
            Golem = asset.Golem.Enabled && context.ResolveBlock(asset, asset.Golem.Activator, Tr.T("Golem activator")) is { } activator
                ? new IRMobGolem(activator, Function(asset, asset.Golem.Function, context, Tr.T("golem detection"), FunctionResult.Boolean, PinType.Position))
                : null,
            // A vehicle has no AI: it only moves when its rider drives it.
            BehaviorTree = !vehicle && asset.BehaviorTreeId is { } tree ? context.Project.Assets.Find(tree)?.ResourceId : null,
            Goals = asset.UsesBehaviorTree || vehicle ? [] : [.. asset.Goals.Where(g => isAnimal || !g.Info.RequiresAnimal).Select(g => LowerGoal(asset, g, context)).OfType<IRMobGoal>()],
            BreedingItem = isAnimal ? context.ResolveItem(asset, asset.BreedingItem, Tr.T("Breeding item")) : null,
            AmbientSound = context.ResolveSound(asset, asset.AmbientSound, Tr.T("Ambient sound")),
            HurtSound = context.ResolveSound(asset, asset.HurtSound, Tr.T("Hurt sound")),
            DeathSound = context.ResolveSound(asset, asset.DeathSound, Tr.T("Death sound")),
            Drops = [.. asset.Drops.Select(d => LowerDrop(asset, d, context)).OfType<IRMobDrop>()],
            SpawnEgg = asset.HasSpawnEgg && !vehicle
                ? new IRSpawnEgg(asset.SpawnEggId, asset.EggPrimaryColor, asset.EggSecondaryColor)
                {
                    CreativeTab = asset.EggCreativeTab is { } tab ? context.Project.Assets.Find(tab) is CreativeTabAsset found ? found.ResourceId : null : null,
                    Texture = context.ResolveOptionalTexture(asset, asset.EggTextureId),
                }
                : null,
            Spawning = asset.SpawnsNaturally && !vehicle ? LowerSpawning(asset, context) : null,
        });
    }

    /// <summary>
    /// The biomes of the spawning: in the Overworld the chosen biomes (all of it when none is chosen),
    /// the whole Nether and the whole End. A fixed group has its minimum as its size.
    /// </summary>
    private static IRMobSpawning LowerSpawning(MobAsset asset, LoweringContext context)
    {
        var biomes = new List<string>();
        foreach (MobDimension dimension in asset.Dimensions.Order())
        {
            switch (dimension)
            {
                case MobDimension.Overworld when asset.SpawnBiomes.Count > 0:
                    biomes.AddRange(asset.SpawnBiomes.Order().Select(MobBiomes.Reference));
                    break;
                case MobDimension.Overworld:
                    biomes.Add("#minecraft:is_overworld");
                    break;
                case MobDimension.Nether:
                    biomes.Add("#minecraft:is_nether");
                    break;
                default:
                    biomes.Add("#minecraft:is_end");
                    break;
            }
        }

        (int min, int max) = asset.GroupMode == MobGroupMode.Fixed
            ? (asset.SpawnMinGroup, asset.SpawnMinGroup)
            : (Math.Min(asset.SpawnMinGroup, asset.SpawnMaxGroup), Math.Max(asset.SpawnMinGroup, asset.SpawnMaxGroup));
        return new IRMobSpawning(biomes, asset.SpawnWeight, min, max)
        {
            Structures = asset.InStructures
                ? [.. asset.Structures.Order(StringComparer.Ordinal).Select(s => NamespacedId.TryParse(s, out NamespacedId? id) ? id : null).OfType<NamespacedId>()]
                : [],
            GroupFunction = asset.GroupMode == MobGroupMode.Dynamic
                ? Function(asset, asset.GroupFunction, context, Tr.T("group size"), FunctionResult.Number, PinType.Entity)
                : null,
        };
    }

    private static IRMobScale Scale(MobAsset asset, MobScale scale, LoweringContext context, string role) =>
        scale.Mode == MobValueMode.Dynamic
            ? new IRMobScale(1, Function(asset, scale.Function, context, role, FunctionResult.Number, PinType.Entity))
            : new IRMobScale(scale.Value, null);

    private static IRMobLayer? LowerLayer(MobAsset asset, MobLayer layer, LoweringContext context) =>
        context.ResolveOptionalTexture(asset, layer.TextureId) is { } texture
            ? new IRMobLayer(texture, layer.Glow, layer.NoHurtOverlay)
            {
                Visible = Function(asset, layer.VisibleFunction, context, Tr.T("display of a layer"), FunctionResult.Boolean, PinType.Entity),
                GlowFunction = Function(asset, layer.GlowFunction, context, Tr.T("glow of a layer"), FunctionResult.Boolean, PinType.Entity),
                HurtFunction = Function(asset, layer.HurtFunction, context, Tr.T("hurt flash of a layer"), FunctionResult.Boolean, PinType.Entity),
            }
            : null;

    private enum FunctionResult
    {
        Boolean,
        Number,
    }

    /// <summary>
    /// A function of the project bound to the mob: it must be a function graph that returns one value
    /// of the expected kind and takes nothing or the argument (the mob, or a position).
    /// </summary>
    private static IRFunctionBinding? Function(MobAsset asset, Guid? id, LoweringContext context, string role, FunctionResult result, PinType argument) =>
        context.ResolveFunction(asset, id, role, result == FunctionResult.Boolean, argument);

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
