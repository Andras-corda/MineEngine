using MineEngine.Core.Assets;
using MineEngine.Core.GameData;

namespace MineEngine.Assets.Mobs;

/// <summary>
/// Un comportement de l'IA d'un mob, avec ses réglages. Immuable : modifier un
/// réglage produit un nouveau comportement (ce qui rend l'annulation triviale).
/// Seuls les réglages utiles au type de comportement sont pris en compte
/// (voir <see cref="MobGoalInfo"/>).
/// </summary>
/// <param name="Kind">Type de comportement.</param>
/// <param name="Speed">Multiplicateur de vitesse pendant le comportement (1 = vitesse normale).</param>
/// <param name="Distance">Distance en blocs (ou hauteur du bond).</param>
/// <param name="Target">Famille de créatures visée.</param>
/// <param name="Item">Item qui attire le mob (comportement "Suivre un item").</param>
/// <param name="Flag">Option propre au comportement (voir <see cref="MobGoalInfo.FlagLabel"/>).</param>
public sealed record MobGoal(MobGoalKind Kind, double Speed, double Distance, MobTarget Target, ContentReference? Item, bool Flag)
{
    public const double MaxSpeed = 3;
    public const double MaxDistance = 64;

    /// <summary>Comportement avec les réglages par défaut de son type.</summary>
    public static MobGoal Create(MobGoalKind kind)
    {
        MobGoalInfo info = MobGoalCatalog.Get(kind);
        return new MobGoal(kind, info.DefaultSpeed, info.DefaultDistance, info.DefaultTarget, null, info.DefaultFlag);
    }

    public MobGoalInfo Info => MobGoalCatalog.Get(Kind);
}

/// <summary>Item laissé par un mob à sa mort.</summary>
public sealed record MobDrop(ContentReference Item, int Min, int Max)
{
    public const int MaxCount = 64;
}
