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

    /// <summary>Other textures (number 1, 2...) switched by the appearance nodes of the graphs.</summary>
    public IReadOnlyList<IRAppearance> Appearances { get; init; } = [];

    public MobModel Model { get; init; } = MobModel.Zombie;

    /// <summary>Blockbench model of the project when <see cref="Model"/> is Custom (see <see cref="IRModel"/>).</summary>
    public ResourceId? CustomModel { get; init; }

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

    /// <summary>Arbre Puppeteer qui pilote le mob (il remplace alors <see cref="Goals"/>), ou null.</summary>
    public ResourceId? BehaviorTree { get; init; }

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

    // ----- V0.7.9 (Entity.md) -----

    /// <summary>Size of the model on the screen.</summary>
    public IRMobScale VisualScale { get; init; } = IRMobScale.One;

    /// <summary>The texture is drawn see-through where it is transparent.</summary>
    public bool TextureTransparent { get; init; }

    public float ShadowRadius { get; init; } = 0.5f;

    /// <summary>Height where a rider sits, in blocks.</summary>
    public double PassengerOffset { get; init; } = 1.8;

    public bool NoCollision { get; init; }

    /// <summary>Factor of the collision box (a fixed one is already in <see cref="Width"/> and <see cref="Height"/>).</summary>
    public IRMobScale HitboxScale { get; init; } = IRMobScale.One;

    /// <summary>Textures one of which each new mob takes at random.</summary>
    public IReadOnlyList<IRTexture> Variations { get; init; } = [];

    public IReadOnlyList<IRMobLayer> Layers { get; init; } = [];

    public MobCreatureType CreatureType { get; init; }

    public double StepHeight { get; init; } = 0.6;

    /// <summary>Distance in chunks at which the players see it.</summary>
    public int TrackingRange { get; init; } = 8;

    public double Knockback { get; init; }

    /// <summary>Items that tame it, or null if it cannot be tamed.</summary>
    public IReadOnlyList<IRMobTamingItem>? Taming { get; init; }

    public IRMobRiding? Riding { get; init; }

    /// <summary>Boat or minecart placed by its item (V0.7.9, step 3), or null.</summary>
    public IRMobVehicle? Vehicle { get; init; }

    public IRMobWater? Water { get; init; }

    public IReadOnlySet<MobImmunity> Immunities { get; init; } = new HashSet<MobImmunity>();

    public IReadOnlySet<MobVulnerability> Vulnerabilities { get; init; } = new HashSet<MobVulnerability>();

    /// <summary>Function that decides whether it despawns far from the players; null: the rule of the game.</summary>
    public IRFunctionBinding? DespawnFunction { get; init; }

    public IRMobBoss? Boss { get; init; }

    public IRMobGolem? Golem { get; init; }
}

/// <summary>
/// Function of the project called by a mob or a block (V0.7.9): its graph, and whether it takes an
/// argument (the mob, the position of the golem or of the block). It returns a number or a yes / no, as the binding expects.
/// </summary>
public sealed record IRFunctionBinding(Guid FunctionId, bool TakesArgument);

/// <summary>A size: a fixed factor, or the result of a function (the mob → number).</summary>
public sealed record IRMobScale(double Value, IRFunctionBinding? Function)
{
    public static IRMobScale One { get; } = new(1, null);

    public bool IsOne => Function is null && Math.Abs(Value - 1) < 1e-9;
}

/// <summary>Layer drawn over the mob, with the functions (the mob → yes / no) that show it, light it, flash it.</summary>
public sealed record IRMobLayer(IRTexture Texture, bool Glow, bool NoHurtOverlay)
{
    public IRFunctionBinding? Visible { get; init; }

    public IRFunctionBinding? GlowFunction { get; init; }

    public IRFunctionBinding? HurtFunction { get; init; }
}

/// <summary>Item that tames the mob, with its chance in percent.</summary>
public sealed record IRMobTamingItem(NamespacedId Item, double Chance);

/// <summary>The player rides the mob; <see cref="SaddleLayer"/> is the layer shown once it is saddled (-1: none).</summary>
public sealed record IRMobRiding(bool Forward, bool Strafe, bool NeedsSaddle, int SaddleLayer);

/// <summary>
/// A boat or a minecart: the item that places it ("storm_wolf_boat"), its rarity and its texture
/// (null: the item of the game).
/// </summary>
public sealed record IRMobVehicle(MobVehicleKind Kind, string ItemId, ItemRarity Rarity, IRTexture? Texture);

/// <summary>Breathing in fluids and push of the fluids, fixed or given by a function (the mob → yes / no).</summary>
public sealed record IRMobWater(bool Breathes, IRFunctionBinding? BreathesFunction, bool Pushed, IRFunctionBinding? PushedFunction, NamespacedId? Fluid);

public sealed record IRMobBoss(bool ShowBar, MobBossColor Color, MobBossOverlay Overlay);

/// <summary>The mob is built by placing <see cref="Activator"/> on a column of two identical blocks, or where the function (position → yes / no) says.</summary>
public sealed record IRMobGolem(NamespacedId Activator, IRFunctionBinding? Function);

/// <summary>Un comportement d'IA, avec les items déjà résolus.</summary>
public sealed record IRMobGoal(MobGoalKind Kind, double Speed, double Distance, MobTarget Target, NamespacedId? Item, bool Flag);

public sealed record IRMobDrop(NamespacedId Item, int Min, int Max);

/// <summary>
/// Œuf d'apparition : identifiant de l'item et couleurs (0xRRGGBB). V0.7.9: the creative tab of the
/// project that shows it (null: the tab of the mod and Spawn Eggs), and its own texture (null: colored).
/// </summary>
public sealed record IRSpawnEgg(string ItemId, int PrimaryColor, int SecondaryColor)
{
    public ResourceId? CreativeTab { get; init; }

    public IRTexture? Texture { get; init; }
}

/// <summary>
/// Natural spawning: the biomes of the biome modifiers ("minecraft:plains", "#minecraft:is_nether"),
/// the weight and the groups. V0.7.9: the structures it is limited to, and the function (the mob →
/// number) that gives the largest group.
/// </summary>
public sealed record IRMobSpawning(IReadOnlyList<string> Biomes, int Weight, int MinGroup, int MaxGroup)
{
    public IReadOnlyList<NamespacedId> Structures { get; init; } = [];

    public IRFunctionBinding? GroupFunction { get; init; }
}
