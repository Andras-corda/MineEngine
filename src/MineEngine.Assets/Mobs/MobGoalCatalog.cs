using MineEngine.Core.GameData;

namespace MineEngine.Assets.Mobs;

/// <summary>Sélecteur de Minecraft dans lequel un comportement est rangé.</summary>
public enum MobGoalSelector
{
    /// <summary>Ce que fait le mob (se déplacer, attaquer...).</summary>
    Goal,

    /// <summary>Qui le mob choisit comme cible.</summary>
    Target,
}

/// <summary>
/// Description d'un type de comportement : nom, explication et réglages qu'il
/// utilise. Un libellé null signifie que le réglage n'est pas utilisé.
/// </summary>
public sealed class MobGoalInfo
{
    public required MobGoalKind Kind { get; init; }

    public required string Label { get; init; }

    public required string Description { get; init; }

    public MobGoalSelector Selector { get; init; } = MobGoalSelector.Goal;

    public string? SpeedLabel { get; init; }

    public double DefaultSpeed { get; init; } = 1;

    public string? DistanceLabel { get; init; }

    public double DefaultDistance { get; init; } = 8;

    public string? TargetLabel { get; init; }

    public MobTarget DefaultTarget { get; init; } = MobTarget.Player;

    public string? ItemLabel { get; init; }

    public string? FlagLabel { get; init; }

    public bool DefaultFlag { get; init; }

    /// <summary>Utilisable seulement par un mob de la famille Animal.</summary>
    public bool RequiresAnimal { get; init; }

    public bool UsesSpeed => SpeedLabel is not null;

    public bool UsesDistance => DistanceLabel is not null;

    public bool UsesTarget => TargetLabel is not null;

    public bool UsesItem => ItemLabel is not null;

    public bool UsesFlag => FlagLabel is not null;
}

/// <summary>Catalogue des comportements d'IA proposés, inspiré de l'éditeur d'IA de MCreator.</summary>
public static class MobGoalCatalog
{
    private static readonly Dictionary<MobGoalKind, MobGoalInfo> Infos = new MobGoalInfo[]
    {
        new()
        {
            Kind = MobGoalKind.Swim, Label = Tr.T("Swim"),
            Description = Tr.T("Stays on the surface of water instead of sinking."),
        },
        new()
        {
            Kind = MobGoalKind.Panic, Label = Tr.T("Panic when hurt"),
            Description = Tr.T("Runs around after being hit, like a cow."),
            SpeedLabel = Tr.T("Flee speed"), DefaultSpeed = 1.25,
        },
        new()
        {
            Kind = MobGoalKind.MeleeAttack, Label = Tr.T("Melee attack"),
            Description = Tr.T("Chases its target and hits it. A behavior that picks a target is also needed."),
            SpeedLabel = Tr.T("Chase speed"), DefaultSpeed = 1.2,
            FlagLabel = Tr.T("Keeps chasing even without seeing the target"),
        },
        new()
        {
            Kind = MobGoalKind.LeapAtTarget, Label = Tr.T("Leap at target"),
            Description = Tr.T("Leaps at its target when it is close, like a spider."),
            DistanceLabel = Tr.T("Leap height"), DefaultDistance = 0.4,
        },
        new()
        {
            Kind = MobGoalKind.RandomStroll, Label = Tr.T("Wander"),
            Description = Tr.T("Walks around randomly, avoiding water."),
            SpeedLabel = Tr.T("Walk speed"), DefaultSpeed = 1,
        },
        new()
        {
            Kind = MobGoalKind.LookAtPlayer, Label = Tr.T("Look at players"),
            Description = Tr.T("Turns its head toward nearby players."),
            DistanceLabel = Tr.T("Sight distance"), DefaultDistance = 8,
        },
        new()
        {
            Kind = MobGoalKind.RandomLookAround, Label = Tr.T("Look around"),
            Description = Tr.T("Turns its head randomly when idle."),
        },
        new()
        {
            Kind = MobGoalKind.Tempt, Label = Tr.T("Follow a player holding an item"),
            Description = Tr.T("Follows the player holding the chosen item, like a cow drawn to wheat."),
            SpeedLabel = Tr.T("Speed"), DefaultSpeed = 1.2,
            ItemLabel = Tr.T("Item that attracts it"),
        },
        new()
        {
            Kind = MobGoalKind.Breed, Label = Tr.T("Breed"),
            Description = Tr.T("Looks for a partner after being fed its breeding item."),
            SpeedLabel = Tr.T("Speed"), DefaultSpeed = 1,
            RequiresAnimal = true,
        },
        new()
        {
            Kind = MobGoalKind.FollowParent, Label = Tr.T("Follow parents"),
            Description = Tr.T("Babies follow an adult of their species."),
            SpeedLabel = Tr.T("Speed"), DefaultSpeed = 1.1,
            RequiresAnimal = true,
        },
        new()
        {
            Kind = MobGoalKind.AvoidEntity, Label = Tr.T("Flee from a creature"),
            Description = Tr.T("Moves away from the chosen creatures when they come close."),
            SpeedLabel = Tr.T("Flee speed"), DefaultSpeed = 1.2,
            DistanceLabel = Tr.T("Alert distance"), DefaultDistance = 8,
            TargetLabel = Tr.T("Creatures to flee"),
        },
        new()
        {
            Kind = MobGoalKind.FleeSun, Label = Tr.T("Avoid the sun"),
            Description = Tr.T("Looks for shade during the day, like a zombie."),
            SpeedLabel = Tr.T("Speed"), DefaultSpeed = 1,
        },
        new()
        {
            Kind = MobGoalKind.MoveTowardsTarget, Label = Tr.T("Move toward target"),
            Description = Tr.T("Walks toward its target while it is in range."),
            SpeedLabel = Tr.T("Speed"), DefaultSpeed = 0.9,
            DistanceLabel = Tr.T("Range"), DefaultDistance = 32,
        },
        new()
        {
            Kind = MobGoalKind.HurtByTarget, Label = Tr.T("Fight back"),
            Description = Tr.T("Targets the creature that hit it."),
            Selector = MobGoalSelector.Target,
            FlagLabel = Tr.T("Calls its kind for help"),
        },
        new()
        {
            Kind = MobGoalKind.AttackNearest, Label = Tr.T("Target nearby creatures"),
            Description = Tr.T("Targets the nearest chosen creature."),
            Selector = MobGoalSelector.Target,
            TargetLabel = Tr.T("Targeted creatures"),
            FlagLabel = Tr.T("Must see the target"), DefaultFlag = true,
        },
    }.ToDictionary(i => i.Kind);

    public static IReadOnlyCollection<MobGoalInfo> All => Infos.Values;

    public static MobGoalInfo Get(MobGoalKind kind) =>
        Infos.TryGetValue(kind, out MobGoalInfo? info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, Tr.T("Unknown behavior."));

    /// <summary>IA de départ d'un nouveau mob de la famille donnée.</summary>
    public static IReadOnlyList<MobGoal> DefaultGoals(MobKind kind) => kind switch
    {
        MobKind.Monster =>
        [
            MobGoal.Create(MobGoalKind.Swim),
            MobGoal.Create(MobGoalKind.MeleeAttack),
            MobGoal.Create(MobGoalKind.RandomStroll),
            MobGoal.Create(MobGoalKind.LookAtPlayer),
            MobGoal.Create(MobGoalKind.RandomLookAround),
            MobGoal.Create(MobGoalKind.HurtByTarget),
            MobGoal.Create(MobGoalKind.AttackNearest),
        ],
        MobKind.Animal =>
        [
            MobGoal.Create(MobGoalKind.Swim),
            MobGoal.Create(MobGoalKind.Panic),
            MobGoal.Create(MobGoalKind.Breed),
            MobGoal.Create(MobGoalKind.FollowParent),
            MobGoal.Create(MobGoalKind.RandomStroll),
            MobGoal.Create(MobGoalKind.LookAtPlayer),
            MobGoal.Create(MobGoalKind.RandomLookAround),
        ],
        _ =>
        [
            MobGoal.Create(MobGoalKind.Swim),
            MobGoal.Create(MobGoalKind.Panic),
            MobGoal.Create(MobGoalKind.RandomStroll),
            MobGoal.Create(MobGoalKind.LookAtPlayer),
            MobGoal.Create(MobGoalKind.RandomLookAround),
        ],
    };
}
