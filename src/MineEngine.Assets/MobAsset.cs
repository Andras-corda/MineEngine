using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Une créature vivante. Inspiré de l'éditeur "Living entity" de MCreator :
/// apparence (modèle, texture, taille), attributs (vie, dégâts, vitesse...), IA
/// (liste ordonnée de comportements), apparition naturelle, œuf, sons et butin.
/// </summary>
public sealed class MobAsset : TexturedAsset
{
    /// <summary>The geometry of a mob stays: its appearances are textures.</summary>
    public override bool AppearancesCanUseModels => false;

    public const double MaxHealth = 1024;
    public const double MaxArmor = 30;
    public const double MaxMovementSpeed = 2;
    public const double MaxAttackDamage = 2048;
    public const double MaxFollowRange = 128;
    public const int MaxExperience = 1000;
    public const float MinHitbox = 0.1f;
    public const float MaxHitbox = 16f;
    public const int MaxSpawnWeight = 1000;
    public const int MaxGroupSize = 16;
    public const int SpawnEggSuffixLength = 10;

    private MobModel _model = MobModel.Zombie;
    private MobKind _kind = MobKind.Monster;
    private bool _useCustomHitbox;
    private float _width = 0.6f;
    private float _height = 1.8f;

    private double _health = 20;
    private double _armor;
    private double _movementSpeed = 0.25;
    private double _attackDamage = 3;
    private double _followRange = 16;
    private double _knockbackResistance;
    private int _experience = 5;
    private bool _fireImmune;
    private bool _burnsInDaylight;
    private bool _persistent;

    private IReadOnlyList<MobGoal> _goals = MobGoalCatalog.DefaultGoals(MobKind.Monster);
    private Guid? _behaviorTreeId;
    private ContentReference? _breedingItem;

    private ContentReference? _ambientSound;
    private ContentReference? _hurtSound;
    private ContentReference? _deathSound;
    private IReadOnlyList<MobDrop> _drops = [];

    private bool _hasSpawnEgg = true;
    private int _eggPrimaryColor = 0x4C7A2A;
    private int _eggSecondaryColor = 0x1F2E14;
    private bool _spawnsNaturally;
    private IReadOnlySet<SpawnBiome> _spawnBiomes = new HashSet<SpawnBiome> { SpawnBiome.Overworld };
    private int _spawnWeight = 10;
    private int _spawnMinGroup = 1;
    private int _spawnMaxGroup = 3;

    public MobAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Mob;

    protected override string MissingTextureMessage =>
        Tr.T("No texture; the texture of the model's original creature will be used.");

    // ----- Apparence -----

    public MobModel Model
    {
        get => _model;
        set => SetField(ref _model, value);
    }

    /// <summary>Faux : la boîte de collision est celle de la créature d'origine du modèle.</summary>
    public bool UseCustomHitbox
    {
        get => _useCustomHitbox;
        set => SetField(ref _useCustomHitbox, value);
    }

    public float Width
    {
        get => _width;
        set => SetField(ref _width, ClampHitbox(value));
    }

    public float Height
    {
        get => _height;
        set => SetField(ref _height, ClampHitbox(value));
    }

    /// <summary>Taille réellement utilisée en jeu.</summary>
    public (float Width, float Height) Hitbox => UseCustomHitbox ? (Width, Height) : MobModelInfo.DefaultHitbox(Model);

    // ----- Attributs -----

    public MobKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    /// <summary>Points de vie (20 = 10 cœurs, comme un zombie).</summary>
    public double Health
    {
        get => _health;
        set => SetField(ref _health, Clamp(value, 1, MaxHealth, 20));
    }

    public double Armor
    {
        get => _armor;
        set => SetField(ref _armor, Clamp(value, 0, MaxArmor, 0));
    }

    /// <summary>Vitesse de déplacement (zombie : 0,23 ; vache : 0,2 ; joueur : 0,1 en interne).</summary>
    public double MovementSpeed
    {
        get => _movementSpeed;
        set => SetField(ref _movementSpeed, Clamp(value, 0, MaxMovementSpeed, 0.25));
    }

    /// <summary>Dégâts d'une attaque au corps à corps (zombie : 3).</summary>
    public double AttackDamage
    {
        get => _attackDamage;
        set => SetField(ref _attackDamage, Clamp(value, 0, MaxAttackDamage, 3));
    }

    /// <summary>Distance à laquelle le mob repère et suit sa cible.</summary>
    public double FollowRange
    {
        get => _followRange;
        set => SetField(ref _followRange, Clamp(value, 1, MaxFollowRange, 16));
    }

    /// <summary>Résistance au recul, de 0 (aucune) à 1 (inébranlable).</summary>
    public double KnockbackResistance
    {
        get => _knockbackResistance;
        set => SetField(ref _knockbackResistance, Clamp(value, 0, 1, 0));
    }

    /// <summary>Expérience donnée quand un joueur le tue.</summary>
    public int Experience
    {
        get => _experience;
        set => SetField(ref _experience, Math.Clamp(value, 0, MaxExperience));
    }

    public bool FireImmune
    {
        get => _fireImmune;
        set => SetField(ref _fireImmune, value);
    }

    /// <summary>Prend feu au soleil, comme un zombie.</summary>
    public bool BurnsInDaylight
    {
        get => _burnsInDaylight;
        set => SetField(ref _burnsInDaylight, value);
    }

    /// <summary>Ne disparaît jamais quand le joueur s'éloigne.</summary>
    public bool Persistent
    {
        get => _persistent;
        set => SetField(ref _persistent, value);
    }

    // ----- IA -----

    /// <summary>Comportements par ordre de priorité : le premier l'emporte sur les suivants.</summary>
    public IReadOnlyList<MobGoal> Goals
    {
        get => _goals;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SequenceEqual(_goals))
            {
                _goals = [.. value];
                RaiseChanged(nameof(Goals));
            }
        }
    }

    /// <summary>
    /// Arbre de comportement Puppeteer qui pilote le mob, ou null. Quand il est choisi, il
    /// remplace la liste de comportements (<see cref="Goals"/>, gardée pour y revenir).
    /// </summary>
    public Guid? BehaviorTreeId
    {
        get => _behaviorTreeId;
        set => SetField(ref _behaviorTreeId, value);
    }

    /// <summary>Vrai si l'IA du mob est un arbre Puppeteer plutôt que la liste de comportements.</summary>
    public bool UsesBehaviorTree => _behaviorTreeId is not null;

    /// <summary>Item qui déclenche la reproduction (famille Animal).</summary>
    public ContentReference? BreedingItem
    {
        get => _breedingItem;
        set => SetField(ref _breedingItem, value);
    }

    // ----- Sons et butin -----

    /// <summary>Son joué de temps en temps ; null pour le son par défaut de la famille.</summary>
    public ContentReference? AmbientSound
    {
        get => _ambientSound;
        set => SetField(ref _ambientSound, value);
    }

    public ContentReference? HurtSound
    {
        get => _hurtSound;
        set => SetField(ref _hurtSound, value);
    }

    public ContentReference? DeathSound
    {
        get => _deathSound;
        set => SetField(ref _deathSound, value);
    }

    public IReadOnlyList<MobDrop> Drops
    {
        get => _drops;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SequenceEqual(_drops))
            {
                _drops = [.. value];
                RaiseChanged(nameof(Drops));
            }
        }
    }

    // ----- Apparition -----

    public bool HasSpawnEgg
    {
        get => _hasSpawnEgg;
        set => SetField(ref _hasSpawnEgg, value);
    }

    /// <summary>Couleur de fond de l'œuf, en RGB (0xRRGGBB).</summary>
    public int EggPrimaryColor
    {
        get => _eggPrimaryColor;
        set => SetField(ref _eggPrimaryColor, value & 0xFFFFFF);
    }

    /// <summary>Couleur des taches de l'œuf, en RGB (0xRRGGBB).</summary>
    public int EggSecondaryColor
    {
        get => _eggSecondaryColor;
        set => SetField(ref _eggSecondaryColor, value & 0xFFFFFF);
    }

    public bool SpawnsNaturally
    {
        get => _spawnsNaturally;
        set => SetField(ref _spawnsNaturally, value);
    }

    public IReadOnlySet<SpawnBiome> SpawnBiomes
    {
        get => _spawnBiomes;
        set => SetField(ref _spawnBiomes, new HashSet<SpawnBiome>(value ?? new HashSet<SpawnBiome>()));
    }

    /// <summary>Fréquence d'apparition comparée aux autres créatures (zombie : 100 ; vache : 8).</summary>
    public int SpawnWeight
    {
        get => _spawnWeight;
        set => SetField(ref _spawnWeight, Math.Clamp(value, 1, MaxSpawnWeight));
    }

    public int SpawnMinGroup
    {
        get => _spawnMinGroup;
        set => SetField(ref _spawnMinGroup, Math.Clamp(value, 1, MaxGroupSize));
    }

    public int SpawnMaxGroup
    {
        get => _spawnMaxGroup;
        set => SetField(ref _spawnMaxGroup, Math.Clamp(value, 1, MaxGroupSize));
    }

    /// <summary>Identifiant de l'item œuf d'apparition ("goblin_spawn_egg").</summary>
    public string SpawnEggId => ResourceId.Value + "_spawn_egg";

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (AssetReference reference in base.GetReferences())
        {
            yield return reference;
        }

        if (BehaviorTreeId is { } tree)
        {
            yield return new AssetReference(tree, AssetReferenceKind.BehaviorTree, Tr.T("behavior tree"));
        }

        foreach (MobGoal goal in ActiveGoals.Where(g => g.Info.UsesItem && g.Item is { Kind: ContentReferenceKind.Asset }))
        {
            yield return new AssetReference(goal.Item!.AssetId, AssetReferenceKind.Item, "IA : " + goal.Info.Label.ToLowerInvariant());
        }

        if (Kind == MobKind.Animal && BreedingItem is { Kind: ContentReferenceKind.Asset } breeding)
        {
            yield return new AssetReference(breeding.AssetId, AssetReferenceKind.Item, Tr.T("breeding item"));
        }

        foreach ((ContentReference? sound, string role) in new[]
                 {
                     (AmbientSound, Tr.T("ambient sound")), (HurtSound, Tr.T("hurt sound")), (DeathSound, Tr.T("death sound")),
                 })
        {
            if (sound is { Kind: ContentReferenceKind.Asset })
            {
                yield return new AssetReference(sound.AssetId, AssetReferenceKind.Sound, role);
            }
        }

        foreach (MobDrop drop in Drops.Where(d => d.Item.Kind == ContentReferenceKind.Asset))
        {
            yield return new AssetReference(drop.Item.AssetId, AssetReferenceKind.Item, "butin");
        }
    }

    public override bool UsesCustomModel => Model == MobModel.Custom && CustomModelId is not null;

    protected override AssetReferenceKind CustomModelKind => AssetReferenceKind.EntityModel;

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();

        if (Model == MobModel.Custom && CustomModelId is null)
        {
            diagnostics.Error(Tr.T("Model \"3D model\": choose the Blockbench model of the mob."), source, Id);
        }

        foreach (MobGoal goal in ActiveGoals)
        {
            MobGoalInfo info = goal.Info;
            if (info.RequiresAnimal && Kind != MobKind.Animal)
            {
                diagnostics.Error(Tr.F("The behavior \"{0}\" is only possible for a mob of the Animal family.", info.Label), source, Id);
            }

            if (info.UsesItem && goal.Item is null)
            {
                diagnostics.Error(Tr.F("The behavior \"{0}\" has no item.", info.Label), source, Id);
            }

            if (info.UsesItem && goal.Item is { Kind: ContentReferenceKind.Tag })
            {
                diagnostics.Error(Tr.F("The behavior \"{0}\" must use a specific item, not a tag.", info.Label), source, Id);
            }
        }

        foreach (MobGoalKind kind in ActiveGoals.GroupBy(g => g.Kind).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            diagnostics.Info(Tr.F("The behavior \"{0}\" appears several times.", MobGoalCatalog.Get(kind).Label), source, Id);
        }

        bool attacks = ActiveGoals.Any(g => g.Kind == MobGoalKind.MeleeAttack);
        bool targets = ActiveGoals.Any(g => g.Info.Selector == MobGoalSelector.Target);
        if (attacks && !targets)
        {
            diagnostics.Warning(
                Tr.T("The mob can attack but never picks a target: add \"Fight back\" or \"Target nearby creatures\"."),
                source,
                Id);
        }

        if (targets && !attacks && Kind == MobKind.Monster)
        {
            diagnostics.Info(Tr.T("The mob picks targets but has no attack behavior."), source, Id);
        }

        if (Kind == MobKind.Animal && BreedingItem is null && ActiveGoals.Any(g => g.Kind == MobGoalKind.Breed))
        {
            diagnostics.Warning(Tr.T("Without a breeding item, the \"Breed\" behavior will never trigger."), source, Id);
        }

        if (Kind == MobKind.Animal && BreedingItem is { Kind: ContentReferenceKind.Tag })
        {
            diagnostics.Error(Tr.T("The breeding item must be a specific item, not a tag."), source, Id);
        }

        foreach ((ContentReference? sound, string label) in new[]
                 {
                     (AmbientSound, "ambiant"), (HurtSound, Tr.T("hurt")), (DeathSound, Tr.T("death")),
                 })
        {
            if (sound is { Kind: ContentReferenceKind.Tag })
            {
                diagnostics.Error(Tr.F("The {0} sound cannot be a tag.", label), source, Id);
            }
        }

        if (Drops.Any(d => d.Item.Kind == ContentReferenceKind.Tag))
        {
            diagnostics.Error(Tr.T("Loot must be made of specific items, not tags."), source, Id);
        }

        if (SpawnsNaturally && SpawnBiomes.Count == 0)
        {
            diagnostics.Warning(Tr.T("Natural spawning is enabled but no biome is chosen."), source, Id);
        }

        if (SpawnMinGroup > SpawnMaxGroup)
        {
            diagnostics.Warning(Tr.T("The minimum group size is greater than the maximum: they will be swapped."), source, Id);
        }

        if (HasSpawnEgg && ResourceId.Value.Length + SpawnEggSuffixLength > ResourceId.MaxLength)
        {
            diagnostics.Error(Tr.T("The identifier is too long to create the spawn egg (\"_spawn_egg\" is added)."), source, Id);
        }

        if (BurnsInDaylight && FireImmune)
        {
            diagnostics.Info(Tr.T("The mob is fire resistant: it will not burn in sunlight."), source, Id);
        }
    }

    /// <summary>Comportements réellement utilisés : aucun quand un arbre Puppeteer pilote le mob.</summary>
    private IEnumerable<MobGoal> ActiveGoals => UsesBehaviorTree ? [] : Goals;

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private static float ClampHitbox(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, MinHitbox, MaxHitbox) : 1f;
}
