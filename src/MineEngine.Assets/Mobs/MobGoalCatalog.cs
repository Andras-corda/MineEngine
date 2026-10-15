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
            Kind = MobGoalKind.Swim, Label = Tr.T("Nager"),
            Description = Tr.T("Reste à la surface de l'eau au lieu de couler."),
        },
        new()
        {
            Kind = MobGoalKind.Panic, Label = Tr.T("Paniquer quand il est blessé"),
            Description = Tr.T("Court dans tous les sens après avoir reçu un coup, comme une vache."),
            SpeedLabel = Tr.T("Vitesse de fuite"), DefaultSpeed = 1.25,
        },
        new()
        {
            Kind = MobGoalKind.MeleeAttack, Label = Tr.T("Attaquer au corps à corps"),
            Description = Tr.T("Poursuit sa cible et la frappe. Il faut aussi un comportement qui choisit une cible."),
            SpeedLabel = Tr.T("Vitesse de poursuite"), DefaultSpeed = 1.2,
            FlagLabel = Tr.T("Continue la poursuite même sans voir la cible"),
        },
        new()
        {
            Kind = MobGoalKind.LeapAtTarget, Label = Tr.T("Bondir sur la cible"),
            Description = Tr.T("Saute sur sa cible quand elle est proche, comme une araignée."),
            DistanceLabel = Tr.T("Hauteur du bond"), DefaultDistance = 0.4,
        },
        new()
        {
            Kind = MobGoalKind.RandomStroll, Label = Tr.T("Se promener"),
            Description = Tr.T("Marche au hasard en évitant l'eau."),
            SpeedLabel = Tr.T("Vitesse de marche"), DefaultSpeed = 1,
        },
        new()
        {
            Kind = MobGoalKind.LookAtPlayer, Label = Tr.T("Regarder les joueurs"),
            Description = Tr.T("Tourne la tête vers les joueurs proches."),
            DistanceLabel = Tr.T("Distance de vue"), DefaultDistance = 8,
        },
        new()
        {
            Kind = MobGoalKind.RandomLookAround, Label = Tr.T("Regarder autour de lui"),
            Description = Tr.T("Tourne la tête au hasard quand il ne fait rien."),
        },
        new()
        {
            Kind = MobGoalKind.Tempt, Label = Tr.T("Suivre un joueur qui tient un item"),
            Description = Tr.T("Suit le joueur qui tient l'item choisi, comme une vache attirée par du blé."),
            SpeedLabel = Tr.T("Vitesse"), DefaultSpeed = 1.2,
            ItemLabel = Tr.T("Item qui l'attire"),
        },
        new()
        {
            Kind = MobGoalKind.Breed, Label = Tr.T("Se reproduire"),
            Description = Tr.T("Cherche un partenaire après avoir été nourri avec son item de reproduction."),
            SpeedLabel = Tr.T("Vitesse"), DefaultSpeed = 1,
            RequiresAnimal = true,
        },
        new()
        {
            Kind = MobGoalKind.FollowParent, Label = Tr.T("Suivre ses parents"),
            Description = Tr.T("Les bébés suivent un adulte de leur espèce."),
            SpeedLabel = Tr.T("Vitesse"), DefaultSpeed = 1.1,
            RequiresAnimal = true,
        },
        new()
        {
            Kind = MobGoalKind.AvoidEntity, Label = Tr.T("Fuir une créature"),
            Description = Tr.T("S'éloigne des créatures choisies quand elles approchent."),
            SpeedLabel = Tr.T("Vitesse de fuite"), DefaultSpeed = 1.2,
            DistanceLabel = Tr.T("Distance d'alerte"), DefaultDistance = 8,
            TargetLabel = Tr.T("Créatures à fuir"),
        },
        new()
        {
            Kind = MobGoalKind.FleeSun, Label = Tr.T("Fuir le soleil"),
            Description = Tr.T("Cherche l'ombre pendant la journée, comme un zombie."),
            SpeedLabel = Tr.T("Vitesse"), DefaultSpeed = 1,
        },
        new()
        {
            Kind = MobGoalKind.MoveTowardsTarget, Label = Tr.T("Se rapprocher de la cible"),
            Description = Tr.T("Marche vers sa cible tant qu'elle est à portée."),
            SpeedLabel = Tr.T("Vitesse"), DefaultSpeed = 0.9,
            DistanceLabel = Tr.T("Portée"), DefaultDistance = 32,
        },
        new()
        {
            Kind = MobGoalKind.HurtByTarget, Label = Tr.T("Riposter"),
            Description = Tr.T("Prend pour cible la créature qui l'a frappé."),
            Selector = MobGoalSelector.Target,
            FlagLabel = Tr.T("Appelle ses semblables à l'aide"),
        },
        new()
        {
            Kind = MobGoalKind.AttackNearest, Label = Tr.T("Cibler les créatures proches"),
            Description = Tr.T("Prend pour cible la créature choisie la plus proche."),
            Selector = MobGoalSelector.Target,
            TargetLabel = Tr.T("Créatures ciblées"),
            FlagLabel = Tr.T("Doit voir la cible"), DefaultFlag = true,
        },
    }.ToDictionary(i => i.Kind);

    public static IReadOnlyCollection<MobGoalInfo> All => Infos.Values;

    public static MobGoalInfo Get(MobGoalKind kind) =>
        Infos.TryGetValue(kind, out MobGoalInfo? info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, Tr.T("Comportement inconnu."));

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
