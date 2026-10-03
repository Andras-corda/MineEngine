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
            Kind = MobGoalKind.Swim, Label = "Nager",
            Description = "Reste à la surface de l'eau au lieu de couler.",
        },
        new()
        {
            Kind = MobGoalKind.Panic, Label = "Paniquer quand il est blessé",
            Description = "Court dans tous les sens après avoir reçu un coup, comme une vache.",
            SpeedLabel = "Vitesse de fuite", DefaultSpeed = 1.25,
        },
        new()
        {
            Kind = MobGoalKind.MeleeAttack, Label = "Attaquer au corps à corps",
            Description = "Poursuit sa cible et la frappe. Il faut aussi un comportement qui choisit une cible.",
            SpeedLabel = "Vitesse de poursuite", DefaultSpeed = 1.2,
            FlagLabel = "Continue la poursuite même sans voir la cible",
        },
        new()
        {
            Kind = MobGoalKind.LeapAtTarget, Label = "Bondir sur la cible",
            Description = "Saute sur sa cible quand elle est proche, comme une araignée.",
            DistanceLabel = "Hauteur du bond", DefaultDistance = 0.4,
        },
        new()
        {
            Kind = MobGoalKind.RandomStroll, Label = "Se promener",
            Description = "Marche au hasard en évitant l'eau.",
            SpeedLabel = "Vitesse de marche", DefaultSpeed = 1,
        },
        new()
        {
            Kind = MobGoalKind.LookAtPlayer, Label = "Regarder les joueurs",
            Description = "Tourne la tête vers les joueurs proches.",
            DistanceLabel = "Distance de vue", DefaultDistance = 8,
        },
        new()
        {
            Kind = MobGoalKind.RandomLookAround, Label = "Regarder autour de lui",
            Description = "Tourne la tête au hasard quand il ne fait rien.",
        },
        new()
        {
            Kind = MobGoalKind.Tempt, Label = "Suivre un joueur qui tient un item",
            Description = "Suit le joueur qui tient l'item choisi, comme une vache attirée par du blé.",
            SpeedLabel = "Vitesse", DefaultSpeed = 1.2,
            ItemLabel = "Item qui l'attire",
        },
        new()
        {
            Kind = MobGoalKind.Breed, Label = "Se reproduire",
            Description = "Cherche un partenaire après avoir été nourri avec son item de reproduction.",
            SpeedLabel = "Vitesse", DefaultSpeed = 1,
            RequiresAnimal = true,
        },
        new()
        {
            Kind = MobGoalKind.FollowParent, Label = "Suivre ses parents",
            Description = "Les bébés suivent un adulte de leur espèce.",
            SpeedLabel = "Vitesse", DefaultSpeed = 1.1,
            RequiresAnimal = true,
        },
        new()
        {
            Kind = MobGoalKind.AvoidEntity, Label = "Fuir une créature",
            Description = "S'éloigne des créatures choisies quand elles approchent.",
            SpeedLabel = "Vitesse de fuite", DefaultSpeed = 1.2,
            DistanceLabel = "Distance d'alerte", DefaultDistance = 8,
            TargetLabel = "Créatures à fuir",
        },
        new()
        {
            Kind = MobGoalKind.FleeSun, Label = "Fuir le soleil",
            Description = "Cherche l'ombre pendant la journée, comme un zombie.",
            SpeedLabel = "Vitesse", DefaultSpeed = 1,
        },
        new()
        {
            Kind = MobGoalKind.MoveTowardsTarget, Label = "Se rapprocher de la cible",
            Description = "Marche vers sa cible tant qu'elle est à portée.",
            SpeedLabel = "Vitesse", DefaultSpeed = 0.9,
            DistanceLabel = "Portée", DefaultDistance = 32,
        },
        new()
        {
            Kind = MobGoalKind.HurtByTarget, Label = "Riposter",
            Description = "Prend pour cible la créature qui l'a frappé.",
            Selector = MobGoalSelector.Target,
            FlagLabel = "Appelle ses semblables à l'aide",
        },
        new()
        {
            Kind = MobGoalKind.AttackNearest, Label = "Cibler les créatures proches",
            Description = "Prend pour cible la créature choisie la plus proche.",
            Selector = MobGoalSelector.Target,
            TargetLabel = "Créatures ciblées",
            FlagLabel = "Doit voir la cible", DefaultFlag = true,
        },
    }.ToDictionary(i => i.Kind);

    public static IReadOnlyCollection<MobGoalInfo> All => Infos.Values;

    public static MobGoalInfo Get(MobGoalKind kind) =>
        Infos.TryGetValue(kind, out MobGoalInfo? info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Comportement inconnu.");

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
