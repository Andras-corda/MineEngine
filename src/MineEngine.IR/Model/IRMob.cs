using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>Créature du mod, prête à être générée.</summary>
public sealed class IRMob
{
    public IRMob(Guid sourceAssetId, ResourceId id, string displayName)
    {
        SourceAssetId = sourceAssetId;
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Value : displayName;
    }

    public Guid SourceAssetId { get; }

    public ResourceId Id { get; }

    public string DisplayName { get; }

    public MobModel Model { get; init; } = MobModel.Zombie;

    public MobKind Kind { get; init; } = MobKind.Monster;

    /// <summary>Texture du mod, ou null pour reprendre celle de la créature d'origine du modèle.</summary>
    public IRTexture? Texture { get; init; }

    public float Width { get; init; } = 0.6f;

    public float Height { get; init; } = 1.8f;

    public double Health { get; init; } = 20;

    public double Armor { get; init; }

    public double MovementSpeed { get; init; } = 0.25;

    public double AttackDamage { get; init; } = 3;

    public double FollowRange { get; init; } = 16;

    public double KnockbackResistance { get; init; }

    public int Experience { get; init; }

    public bool FireImmune { get; init; }

    public bool BurnsInDaylight { get; init; }

    public bool Persistent { get; init; }

    /// <summary>Comportements par ordre de priorité.</summary>
    public IReadOnlyList<IRMobGoal> Goals { get; init; } = [];

    /// <summary>Item de reproduction (Animal), ou null.</summary>
    public NamespacedId? BreedingItem { get; init; }

    public NamespacedId? AmbientSound { get; init; }

    public NamespacedId? HurtSound { get; init; }

    public NamespacedId? DeathSound { get; init; }

    public IReadOnlyList<IRMobDrop> Drops { get; init; } = [];

    public IRSpawnEgg? SpawnEgg { get; init; }

    /// <summary>Apparition naturelle, ou null si le mob n'apparaît que par œuf ou commande.</summary>
    public IRMobSpawning? Spawning { get; init; }
}

/// <summary>Un comportement d'IA, avec les items déjà résolus.</summary>
public sealed record IRMobGoal(MobGoalKind Kind, double Speed, double Distance, MobTarget Target, NamespacedId? Item, bool Flag);

public sealed record IRMobDrop(NamespacedId Item, int Min, int Max);

/// <summary>Œuf d'apparition : identifiant de l'item et couleurs (0xRRGGBB).</summary>
public sealed record IRSpawnEgg(string ItemId, int PrimaryColor, int SecondaryColor);

public sealed record IRMobSpawning(IReadOnlyList<SpawnBiome> Biomes, int Weight, int MinGroup, int MaxGroup);
