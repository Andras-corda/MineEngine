using System.Globalization;
using MineEngine.Core.GameData;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Java;

/// <summary>Modèle 3D vanilla réutilisé pour afficher un mob.</summary>
/// <param name="ModelClass">Classe du modèle (net.minecraft.client.model).</param>
/// <param name="Layer">Champ de ModelLayers à "cuire".</param>
/// <param name="VanillaTexture">Texture de la créature d'origine, utilisée si le mob n'en a pas.</param>
/// <param name="ShadowRadius">Rayon de l'ombre au sol.</param>
public sealed record MobModelJava(string ModelClass, string Layer, string VanillaTexture, float ShadowRadius);

/// <summary>Correspondance entre les mobs de Mine Engine et le code Java de Minecraft.</summary>
public static class MobJava
{
    public static MobModelJava Model(MobModel model) => model switch
    {
        MobModel.Skeleton => new("HumanoidModel", "SKELETON", "textures/entity/skeleton/skeleton.png", 0.5f),
        MobModel.Villager => new("VillagerModel", "VILLAGER", "textures/entity/villager/villager.png", 0.5f),
        MobModel.Witch => new("WitchModel", "WITCH", "textures/entity/witch.png", 0.5f),
        MobModel.Creeper => new("CreeperModel", "CREEPER", "textures/entity/creeper/creeper.png", 0.5f),
        MobModel.Spider => new("SpiderModel", "SPIDER", "textures/entity/spider/spider.png", 0.8f),
        MobModel.Cow => new("CowModel", "COW", "textures/entity/cow/cow.png", 0.7f),
        MobModel.Pig => new("PigModel", "PIG", "textures/entity/pig/pig.png", 0.7f),
        MobModel.Chicken => new("ChickenModel", "CHICKEN", "textures/entity/chicken.png", 0.3f),
        MobModel.Ocelot => new("OcelotModel", "OCELOT", "textures/entity/cat/ocelot.png", 0.4f),
        MobModel.Blaze => new("BlazeModel", "BLAZE", "textures/entity/blaze.png", 0.5f),
        MobModel.Slime => new("SlimeModel", "SLIME", "textures/entity/slime/slime.png", 0.5f),
        MobModel.Silverfish => new("SilverfishModel", "SILVERFISH", "textures/entity/silverfish.png", 0.3f),
        MobModel.SnowGolem => new("SnowGolemModel", "SNOW_GOLEM", "textures/entity/snow_golem.png", 0.5f),
        _ => new("HumanoidModel", "ZOMBIE", "textures/entity/zombie/zombie.png", 0.5f),
    };

    /// <summary>Classe Java dont hérite le mob.</summary>
    public static string BaseClass(MobKind kind) => kind switch
    {
        MobKind.Animal => "Animal",
        MobKind.Creature => "PathfinderMob",
        _ => "Monster",
    };

    public static string BaseClassImport(MobKind kind) => kind switch
    {
        MobKind.Animal => "net.minecraft.world.entity.animal.Animal",
        MobKind.Creature => "net.minecraft.world.entity.PathfinderMob",
        _ => "net.minecraft.world.entity.monster.Monster",
    };

    /// <summary>Point de départ des attributs (le monstre a déjà dégâts et portée de suivi).</summary>
    public static string AttributesStart(MobKind kind) =>
        kind == MobKind.Monster ? "Monster.createMonsterAttributes()" : "Mob.createMobAttributes()";

    public static string Category(MobKind kind) => "MobCategory." + (kind == MobKind.Monster ? "MONSTER" : "CREATURE");

    /// <summary>Règle d'apparition naturelle de la famille (lumière, sol...).</summary>
    public static string SpawnRule(MobKind kind) => kind switch
    {
        MobKind.Animal => "Animal::checkAnimalSpawnRules",
        MobKind.Creature => "Mob::checkMobSpawnRules",
        _ => "Monster::checkMonsterSpawnRules",
    };

    public static string SpawnRuleImport(MobKind kind) => kind switch
    {
        MobKind.Animal => "net.minecraft.world.entity.animal.Animal",
        MobKind.Creature => "net.minecraft.world.entity.Mob",
        _ => "net.minecraft.world.entity.monster.Monster",
    };

    /// <summary>Classe Java visée par un comportement (cible, créature à fuir).</summary>
    public static (string Class, string Import) Target(MobTarget target) => target switch
    {
        MobTarget.Villager => ("AbstractVillager", "net.minecraft.world.entity.npc.AbstractVillager"),
        MobTarget.IronGolem => ("IronGolem", "net.minecraft.world.entity.animal.IronGolem"),
        MobTarget.Animal => ("Animal", "net.minecraft.world.entity.animal.Animal"),
        MobTarget.Monster => ("Monster", "net.minecraft.world.entity.monster.Monster"),
        _ => ("Player", "net.minecraft.world.entity.player.Player"),
    };

    /// <summary>Tag ou biome visé par l'apparition naturelle.</summary>
    public static string Biomes(SpawnBiome biome) => biome switch
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

    /// <summary>
    /// Expression Java qui crée le comportement, et les imports nécessaires. Les items
    /// passent par la méthode item("espace:chemin") de la classe générée.
    /// </summary>
    public static (string Expression, IReadOnlyList<string> Imports) Goal(IRMobGoal goal)
    {
        const string Goals = "net.minecraft.world.entity.ai.goal.";
        string speed = Double(goal.Speed);
        string distance = JavaNames.FloatLiteral((float)goal.Distance);
        (string targetClass, string targetImport) = Target(goal.Target);
        string Flag() => goal.Flag ? "true" : "false";

        return goal.Kind switch
        {
            MobGoalKind.Swim => ("new FloatGoal(this)", [Goals + "FloatGoal"]),
            MobGoalKind.Panic => ($"new PanicGoal(this, {speed})", [Goals + "PanicGoal"]),
            MobGoalKind.MeleeAttack => ($"new MeleeAttackGoal(this, {speed}, {Flag()})", [Goals + "MeleeAttackGoal"]),
            MobGoalKind.LeapAtTarget => ($"new LeapAtTargetGoal(this, {distance})", [Goals + "LeapAtTargetGoal"]),
            MobGoalKind.RandomStroll => ($"new WaterAvoidingRandomStrollGoal(this, {speed})", [Goals + "WaterAvoidingRandomStrollGoal"]),
            MobGoalKind.LookAtPlayer => (
                $"new LookAtPlayerGoal(this, Player.class, {distance})",
                [Goals + "LookAtPlayerGoal", "net.minecraft.world.entity.player.Player"]),
            MobGoalKind.RandomLookAround => ("new RandomLookAroundGoal(this)", [Goals + "RandomLookAroundGoal"]),
            MobGoalKind.Tempt => (
                $"new TemptGoal(this, {speed}, Ingredient.of(item({JavaNames.StringLiteral(goal.Item!.ToString())})), false)",
                [Goals + "TemptGoal", "net.minecraft.world.item.crafting.Ingredient"]),
            MobGoalKind.Breed => ($"new BreedGoal(this, {speed})", [Goals + "BreedGoal"]),
            MobGoalKind.FollowParent => ($"new FollowParentGoal(this, {speed})", [Goals + "FollowParentGoal"]),
            MobGoalKind.AvoidEntity => (
                $"new AvoidEntityGoal<>(this, {targetClass}.class, {distance}, {speed}, {Double(goal.Speed * 1.25)})",
                [Goals + "AvoidEntityGoal", targetImport]),
            MobGoalKind.FleeSun => ($"new FleeSunGoal(this, {speed})", [Goals + "FleeSunGoal"]),
            MobGoalKind.MoveTowardsTarget => (
                $"new MoveTowardsTargetGoal(this, {speed}, {distance})", [Goals + "MoveTowardsTargetGoal"]),
            MobGoalKind.HurtByTarget => (
                goal.Flag ? "new HurtByTargetGoal(this).setAlertOthers()" : "new HurtByTargetGoal(this)",
                ["net.minecraft.world.entity.ai.goal.target.HurtByTargetGoal"]),
            MobGoalKind.AttackNearest => (
                $"new NearestAttackableTargetGoal<>(this, {targetClass}.class, {Flag()})",
                ["net.minecraft.world.entity.ai.goal.target.NearestAttackableTargetGoal", targetImport]),
            _ => throw new ArgumentOutOfRangeException(nameof(goal), goal.Kind, "Comportement inconnu."),
        };
    }

    /// <summary>Littéral double Java ("1.25").</summary>
    public static string Double(double value) =>
        value.ToString("0.0###", CultureInfo.InvariantCulture);
}
