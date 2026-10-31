using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Une créature vivante. Inspiré de l'éditeur "Living entity" de MCreator, refait en V0.7.9
/// (Entity.md) : visuel (modèle, texture, échelle, boîte de collision), variantes et calques,
/// attributs (type, apprivoisement, monture, créature aquatique, immunités et faiblesses), IA,
/// apparition (dimensions, biomes, structures, groupes, œuf), propriétés spéciales (boss,
/// golem), sons et butin.
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
    public const double MaxStepHeight = 10;
    public const int MaxTrackingRange = 32;
    public const double MaxKnockback = 10;
    public const double MaxShadowRadius = 8;
    public const double MaxPassengerOffset = 16;

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
    private bool _persistent;

    private MobScale _visualScale = MobScale.Default;
    private bool _textureTransparent;
    private double _shadowRadius = 0.5;
    private double _passengerOffset = 1.8;
    private bool _noCollision;
    private MobScale _hitboxScale = MobScale.Default;
    private IReadOnlyList<Guid> _variations = [];
    private IReadOnlyList<MobLayer> _layers = [];

    private MobCreatureType _creatureType = MobCreatureType.Undefined;
    private double _stepHeight = 0.6;
    private int _trackingRange = 8;
    private double _knockback;
    private MobTaming _taming = MobTaming.Disabled;
    private MobRiding _riding = MobRiding.Disabled;
    private MobWater _water = MobWater.Disabled;
    private IReadOnlySet<MobImmunity> _immunities = new HashSet<MobImmunity>();
    private IReadOnlySet<Guid> _immuneDamageTypes = new HashSet<Guid>();
    private IReadOnlySet<MobVulnerability> _vulnerabilities = new HashSet<MobVulnerability>();

    private Guid? _despawnFunction;
    private MobGroupMode _groupMode = MobGroupMode.Range;
    private Guid? _groupFunction;
    private IReadOnlySet<MobDimension> _dimensions = new HashSet<MobDimension> { MobDimension.Overworld };
    private bool _inStructures;
    private IReadOnlySet<string> _structures = new HashSet<string>();
    private Guid? _eggCreativeTab;
    private Guid? _eggTextureId;

    private MobBoss _boss = MobBoss.Disabled;
    private MobGolem _golem = MobGolem.Disabled;
    private MobVehicle _vehicle = MobVehicle.None;

    private IReadOnlyList<MobGoal> _goals = MobGoalCatalog.DefaultGoals(MobKind.Monster);
    private Guid? _behaviorTreeId;
    private ContentReference? _breedingItem;

    private ContentReference? _ambientSound;
    private ContentReference? _hurtSound;
    private ContentReference? _deathSound;
    private IReadOnlyList<MobDrop> _drops = [];
    private Guid? _dropTable;

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

    /// <summary>Size of the model on the screen (the collision box does not change).</summary>
    public MobScale VisualScale
    {
        get => _visualScale;
        set => SetField(ref _visualScale, value ?? MobScale.Default);
    }

    /// <summary>The texture has transparent pixels drawn see-through (glass, ghost), not only cut out.</summary>
    public bool TextureTransparent
    {
        get => _textureTransparent;
        set => SetField(ref _textureTransparent, value);
    }

    /// <summary>Radius of the shadow on the ground, in blocks.</summary>
    public double ShadowRadius
    {
        get => _shadowRadius;
        set => SetField(ref _shadowRadius, Clamp(value, 0, MaxShadowRadius, 0.5));
    }

    /// <summary>Height, in blocks, where an entity riding this one sits.</summary>
    public double PassengerOffset
    {
        get => _passengerOffset;
        set => SetField(ref _passengerOffset, Clamp(value, 0, MaxPassengerOffset, 1.8));
    }

    /// <summary>Nothing pushes it and it pushes nothing (the players walk through it).</summary>
    public bool NoCollision
    {
        get => _noCollision;
        set => SetField(ref _noCollision, value);
    }

    /// <summary>Factor of the collision box (and of the shadow).</summary>
    public MobScale HitboxScale
    {
        get => _hitboxScale;
        set => SetField(ref _hitboxScale, value ?? MobScale.Default);
    }

    /// <summary>Textures one of which each new mob takes at random (kept in its NBT data, like a cat's).</summary>
    public IReadOnlyList<Guid> Variations
    {
        get => _variations;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SequenceEqual(_variations))
            {
                _variations = [.. value];
                RaiseChanged(nameof(Variations));
            }
        }
    }

    /// <summary>Layers drawn over the mob with the same model (eyes, armor, saddle...).</summary>
    public IReadOnlyList<MobLayer> Layers
    {
        get => _layers;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SequenceEqual(_layers))
            {
                _layers = [.. value];
                RaiseChanged(nameof(Layers));
            }
        }
    }
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

    /// <summary>Immune to fire (and to lava, which the game treats like fire for a fire-immune mob).</summary>
    public bool FireImmune => _immunities.Contains(MobImmunity.Fire);

    /// <summary>Prend feu au soleil, comme un zombie.</summary>
    public bool BurnsInDaylight => _vulnerabilities.Contains(MobVulnerability.Sun);

    /// <summary>Type of creature for the game (Smite, Bane of Arthropods, raids, potions).</summary>
    public MobCreatureType CreatureType
    {
        get => _creatureType;
        set => SetField(ref _creatureType, value);
    }

    /// <summary>Height, in blocks, it climbs without jumping (0.6: a slab).</summary>
    public double StepHeight
    {
        get => _stepHeight;
        set => SetField(ref _stepHeight, Clamp(value, 0, MaxStepHeight, 0.6));
    }

    /// <summary>Distance, in chunks, at which the players still see it.</summary>
    public int TrackingRange
    {
        get => _trackingRange;
        set => SetField(ref _trackingRange, Math.Clamp(value, 1, MaxTrackingRange));
    }

    /// <summary>Knockback of its attacks.</summary>
    public double Knockback
    {
        get => _knockback;
        set => SetField(ref _knockback, Clamp(value, 0, MaxKnockback, 0));
    }

    public MobTaming Taming
    {
        get => _taming;
        set => SetField(ref _taming, value ?? MobTaming.Disabled);
    }

    public MobRiding Riding
    {
        get => _riding;
        set => SetField(ref _riding, value ?? MobRiding.Disabled);
    }

    public MobWater Water
    {
        get => _water;
        set => SetField(ref _water, value ?? MobWater.Disabled);
    }

    /// <summary>Damage types of the project the mob is immune to (V0.7.10, Entity.md "custom damage type").</summary>
    public IReadOnlySet<Guid> ImmuneDamageTypes
    {
        get => _immuneDamageTypes;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SetEquals(_immuneDamageTypes))
            {
                _immuneDamageTypes = new HashSet<Guid>(value);
                RaiseChanged(nameof(ImmuneDamageTypes));
            }
        }
    }

    public IReadOnlySet<MobImmunity> Immunities
    {
        get => _immunities;
        set => SetField(ref _immunities, new HashSet<MobImmunity>(value ?? new HashSet<MobImmunity>()));
    }

    public IReadOnlySet<MobVulnerability> Vulnerabilities
    {
        get => _vulnerabilities;
        set => SetField(ref _vulnerabilities, new HashSet<MobVulnerability>(value ?? new HashSet<MobVulnerability>()));
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

    /// <summary>Drop table of the project the mob drops instead of <see cref="Drops"/> (V0.7.10), or null.</summary>
    public Guid? DropTable
    {
        get => _dropTable;
        set => SetField(ref _dropTable, value);
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

    // ----- Propriétés spéciales -----

    public MobBoss Boss
    {
        get => _boss;
        set => SetField(ref _boss, value ?? MobBoss.Disabled);
    }

    public MobGolem Golem
    {
        get => _golem;
        set => SetField(ref _golem, value ?? MobGolem.Disabled);
    }

    /// <summary>Boat or minecart (V0.7.9, step 3): placed by its item, it neither spawns nor has an egg.</summary>
    public MobVehicle Vehicle
    {
        get => _vehicle;
        set => SetField(ref _vehicle, value ?? MobVehicle.None);
    }

    /// <summary>Identifier of the item that places the vehicle ("storm_wolf_boat"), or null.</summary>
    public string? VehicleItemId => Vehicle.Kind switch
    {
        MobVehicleKind.Boat => ResourceId.Value + "_boat",
        MobVehicleKind.Minecart => ResourceId.Value + "_minecart",
        _ => null,
    };

    // ----- Apparition -----

    /// <summary>Function (the mob → yes / no) that decides whether it despawns far from the players; null: the rule of the game.</summary>
    public Guid? DespawnFunction
    {
        get => _despawnFunction;
        set => SetField(ref _despawnFunction, value);
    }

    /// <summary>Fixed: <see cref="SpawnMinGroup"/> mobs; range: between the two sizes; dynamic: up to the result of <see cref="GroupFunction"/>.</summary>
    public MobGroupMode GroupMode
    {
        get => _groupMode;
        set => SetField(ref _groupMode, value);
    }

    /// <summary>Function (the mob → number) that gives the largest group.</summary>
    public Guid? GroupFunction
    {
        get => _groupFunction;
        set => SetField(ref _groupFunction, value);
    }

    /// <summary>Dimensions where it spawns (never empty: the Overworld by default).</summary>
    public IReadOnlySet<MobDimension> Dimensions
    {
        get => _dimensions;
        set => SetField(ref _dimensions, value is { Count: > 0 } ? new HashSet<MobDimension>(value) : new HashSet<MobDimension> { MobDimension.Overworld });
    }

    /// <summary>It spawns only inside the chosen structures.</summary>
    public bool InStructures
    {
        get => _inStructures;
        set => SetField(ref _inStructures, value);
    }

    /// <summary>Structures of the game ("minecraft:pillager_outpost").</summary>
    public IReadOnlySet<string> Structures
    {
        get => _structures;
        set => SetField(ref _structures, new HashSet<string>(value ?? new HashSet<string>(), StringComparer.Ordinal));
    }

    /// <summary>Creative tab of the project that shows the egg; null: the tab of the mod and the Spawn Eggs tab.</summary>
    public Guid? EggCreativeTab
    {
        get => _eggCreativeTab;
        set => SetField(ref _eggCreativeTab, value);
    }

    /// <summary>Texture of the egg instead of the two colors; null: the egg of the game, colored.</summary>
    public Guid? EggTextureId
    {
        get => _eggTextureId;
        set => SetField(ref _eggTextureId, value);
    }

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
        if (DropTable is { } table)
        {
            yield return new AssetReference(table, AssetReferenceKind.LootTable, Tr.T("drop table"));
        }

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

        foreach (Guid texture in Variations)
        {
            yield return new AssetReference(texture, AssetReferenceKind.Texture, Tr.T("variation"));
        }

        foreach (MobLayer layer in Layers)
        {
            yield return new AssetReference(layer.TextureId, AssetReferenceKind.Texture, Tr.T("layer"));
        }

        if (EggTextureId is { } egg && !Vehicle.Enabled)
        {
            yield return new AssetReference(egg, AssetReferenceKind.Texture, Tr.T("texture of the spawn egg"));
        }

        if (Vehicle is { Enabled: true, ItemTextureId: { } vehicleTexture })
        {
            yield return new AssetReference(vehicleTexture, AssetReferenceKind.Texture, Tr.T("texture of the vehicle item"));
        }

        foreach (MobTamingItem item in Taming.Enabled ? Taming.Items : [])
        {
            if (item.Item.Kind == ContentReferenceKind.Asset)
            {
                yield return new AssetReference(item.Item.AssetId, AssetReferenceKind.Item, Tr.T("taming item"));
            }
        }
    }

    /// <summary>Functions of the project the mob uses (dynamic sizes, layers, water, spawning, golem).</summary>
    public IEnumerable<(Guid Function, string Role)> FunctionBindings()
    {
        if (VisualScale is { Mode: MobValueMode.Dynamic, Function: { } visual })
        {
            yield return (visual, Tr.T("visual scale"));
        }

        if (HitboxScale is { Mode: MobValueMode.Dynamic, Function: { } hitbox })
        {
            yield return (hitbox, Tr.T("collision box scale"));
        }

        foreach (MobLayer layer in Layers)
        {
            foreach ((Guid? function, string role) in new[] { (layer.VisibleFunction, Tr.T("display of a layer")), (layer.GlowFunction, Tr.T("glow of a layer")), (layer.HurtFunction, Tr.T("hurt flash of a layer")) })
            {
                if (function is { } id)
                {
                    yield return (id, role);
                }
            }
        }

        if (Water.Enabled && Water.BreathesFunction is { } breathes)
        {
            yield return (breathes, Tr.T("breathing in fluids"));
        }

        if (Water.Enabled && Water.PushedFunction is { } pushed)
        {
            yield return (pushed, Tr.T("push of the fluids"));
        }

        if (DespawnFunction is { } despawn)
        {
            yield return (despawn, Tr.T("despawn condition"));
        }

        if (GroupMode == MobGroupMode.Dynamic && GroupFunction is { } group)
        {
            yield return (group, Tr.T("group size"));
        }

        if (Golem.Enabled && Golem.Function is { } golem)
        {
            yield return (golem, Tr.T("golem detection"));
        }
    }

    public override bool UsesCustomModel => Model == MobModel.Custom && CustomModelId is not null;

    protected override AssetReferenceKind CustomModelKind => AssetReferenceKind.EntityModel;

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();
        if (DropTable is not null && Drops.Count > 0)
        {
            diagnostics.Info(Tr.T("The items of the drop list are not used: the mob drops its drop table."), source, Id);
        }

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

        if (VehicleItemId is { } vehicleItem && vehicleItem.Length > ResourceId.MaxLength)
        {
            diagnostics.Error(Tr.F("The identifier is too long to create the item of the vehicle (\"{0}\").", vehicleItem), source, Id);
        }

        if (!Vehicle.Enabled && HasSpawnEgg && ResourceId.Value.Length + SpawnEggSuffixLength > ResourceId.MaxLength)
        {
            diagnostics.Error(Tr.T("The identifier is too long to create the spawn egg (\"_spawn_egg\" is added)."), source, Id);
        }

        if (BurnsInDaylight && FireImmune)
        {
            diagnostics.Info(Tr.T("The mob is fire resistant: it will not burn in sunlight."), source, Id);
        }

        if (Taming.Enabled && Kind != MobKind.Animal)
        {
            diagnostics.Error(Tr.T("Only a mob of the Animal family can be tamed."), source, Id);
        }

        if (Taming.Enabled && Taming.Items.Count == 0)
        {
            diagnostics.Warning(Tr.T("The mob can be tamed but no item tames it."), source, Id);
        }

        if (Taming.Items.Any(i => i.Item.Kind == ContentReferenceKind.Tag))
        {
            diagnostics.Error(Tr.T("The taming items must be specific items, not tags."), source, Id);
        }

        if (Riding.Enabled && Riding.SaddleLayer >= Layers.Count)
        {
            diagnostics.Warning(Tr.T("The layer of the saddle no longer exists."), source, Id);
        }

        if (Riding.Enabled && !Riding.Forward && !Riding.Strafe)
        {
            diagnostics.Info(Tr.T("The mob can be ridden but the rider cannot move it."), source, Id);
        }

        if (Golem.Enabled && Golem.Activator.Kind == ContentReferenceKind.Tag)
        {
            diagnostics.Error(Tr.T("The block that builds the golem must be a specific block, not a tag."), source, Id);
        }

        if (SpawnsNaturally && InStructures && Structures.Count == 0)
        {
            diagnostics.Warning(Tr.T("The mob spawns only in structures but none is chosen."), source, Id);
        }

        foreach ((MobScale scale, string label) in new[] { (VisualScale, Tr.T("visual scale")), (HitboxScale, Tr.T("collision box scale")) })
        {
            if (scale is { Mode: MobValueMode.Dynamic, Function: null })
            {
                diagnostics.Warning(Tr.F("The {0} is dynamic but no function gives it: it stays at 1.", label), source, Id);
            }
        }

        if (SpawnsNaturally && GroupMode == MobGroupMode.Dynamic && GroupFunction is null)
        {
            diagnostics.Warning(Tr.T("The group size is dynamic but no function gives it."), source, Id);
        }
    }

    /// <summary>Comportements réellement utilisés : aucun quand un arbre Puppeteer pilote le mob.</summary>
    private IEnumerable<MobGoal> ActiveGoals => UsesBehaviorTree ? [] : Goals;

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private static float ClampHitbox(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, MinHitbox, MaxHitbox) : 1f;
}
