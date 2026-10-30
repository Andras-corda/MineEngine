using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Un item : objet d'inventaire, éventuellement à durabilité, brillant ou comestible.
/// Inspiré de l'éditeur d'items de MCreator (pages Visuel, Propriétés, Nourriture).
/// </summary>
public sealed class ItemAsset : InventoryAsset
{
    public const int MaxDurability = 100_000;
    public const int MaxNutrition = 20;
    public const float MaxSaturation = 10f;
    public const float MaxAttack = 100f;
    public const int MaxBurnTime = 72_000;
    public const float DefaultEatSeconds = 1.6f;
    public const float MaxEatSeconds = 60f;

    private ItemKind _kind = ItemKind.Simple;
    private ItemTier _tier = ItemTier.Iron;
    private ArmorMaterialKind _armorMaterial = ArmorMaterialKind.Iron;
    private float _attackDamage = 3f;
    private float _attackSpeed = -2.4f;
    private bool _handheld;
    private ContentReference? _craftRemainder;
    private bool _noRepair;
    private int _burnTime;
    private IReadOnlyList<NamespacedId> _itemTags = [];
    private IReadOnlyList<FoodEffect> _foodEffects = [];
    private int _durability;
    private bool _hasGlint;
    private bool _isFood;
    private int _nutrition = 4;
    private float _saturation = 0.3f;
    private bool _alwaysEdible;
    private bool _isMeat;
    private Guid? _guiTextureId;
    private Guid? _glintCondition;
    private ContentReference? _repairItem;
    private bool _harvestsAnyBlock;
    private ItemRemainderKind _remainder;
    private ContentReference? _eatResult;
    private ItemUseAnimation _useAnimation;
    private float _eatSeconds = DefaultEatSeconds;
    private ItemCustomTier _customTier = new();
    private Guid? _burnTimeFunction;
    private Guid? _burnCondition;
    private int _compostChance;
    private Guid? _compostCondition;
    private bool _piglinCurrency;
    private ItemRanged _ranged = new();
    private IReadOnlyList<ElementMethod> _methods = [];

    public ItemAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Item;

    /// <summary>Nombre d'utilisations avant de casser ; 0 = pas de durabilité.</summary>
    public int Durability
    {
        get => _durability;
        set => SetField(ref _durability, Math.Clamp(value, 0, MaxDurability));
    }

    public bool HasDurability => Durability > 0;

    /// <summary>Reflet brillant, comme un livre enchanté.</summary>
    public bool HasGlint
    {
        get => _hasGlint;
        set => SetField(ref _hasGlint, value);
    }

    public bool IsFood
    {
        get => _isFood;
        set => SetField(ref _isFood, value);
    }

    /// <summary>Demi-cuisses de la barre de faim rendues (0 à 20).</summary>
    public int Nutrition
    {
        get => _nutrition;
        set => SetField(ref _nutrition, Math.Clamp(value, 0, MaxNutrition));
    }

    /// <summary>Saturation : durée pendant laquelle la faim ne baisse pas (0,3 par défaut).</summary>
    public float Saturation
    {
        get => _saturation;
        set => SetField(ref _saturation, float.IsFinite(value) ? Math.Clamp(value, 0f, MaxSaturation) : 0f);
    }

    /// <summary>Peut être mangé même sans faim (comme une pomme dorée).</summary>
    public bool AlwaysEdible
    {
        get => _alwaysEdible;
        set => SetField(ref _alwaysEdible, value);
    }

    /// <summary>Viande : les loups peuvent la manger.</summary>
    public bool IsMeat
    {
        get => _isMeat;
        set => SetField(ref _isMeat, value);
    }

    // ----- Type d'item (V0.4.1) -----

    /// <summary>Classe de l'item : simple, outil, arme ou pièce d'armure.</summary>
    public ItemKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    /// <summary>Matériau d'un outil ou d'une arme.</summary>
    public ItemTier Tier
    {
        get => _tier;
        set => SetField(ref _tier, value);
    }

    /// <summary>Matériau d'une pièce d'armure (défense, durabilité, apparence portée).</summary>
    public ArmorMaterialKind ArmorMaterial
    {
        get => _armorMaterial;
        set => SetField(ref _armorMaterial, value);
    }

    /// <summary>Dégâts ajoutés à ceux du matériau (épée en fer : 3).</summary>
    public float AttackDamage
    {
        get => _attackDamage;
        set => SetField(ref _attackDamage, float.IsFinite(value) ? Math.Clamp(value, -MaxAttack, MaxAttack) : 0f);
    }

    /// <summary>Vitesse d'attaque ajoutée à celle de la main (épée : -2,4 ; plus c'est bas, plus c'est lent).</summary>
    public float AttackSpeed
    {
        get => _attackSpeed;
        set => SetField(ref _attackSpeed, float.IsFinite(value) ? Math.Clamp(value, -4f, MaxAttack) : 0f);
    }

    /// <summary>Tenu comme un outil en main (modèle "handheld") plutôt qu'à plat.</summary>
    public bool Handheld
    {
        get => _handheld;
        set => SetField(ref _handheld, value);
    }

    // ----- Propriétés avancées (V0.4.1) -----

    /// <summary>Item rendu dans la grille après une fabrication (comme le seau vide après un gâteau).</summary>
    public ContentReference? CraftRemainder
    {
        get => _craftRemainder;
        set => SetField(ref _craftRemainder, value);
    }

    /// <summary>Ne peut pas être réparé à l'enclume avec un matériau.</summary>
    public bool NoRepair
    {
        get => _noRepair;
        set => SetField(ref _noRepair, value);
    }

    /// <summary>Durée de combustion dans un four, en ticks (charbon : 1600) ; 0 = pas un combustible.</summary>
    public int BurnTime
    {
        get => _burnTime;
        set => SetField(ref _burnTime, Math.Clamp(value, 0, MaxBurnTime));
    }

    /// <summary>Tags d'items auxquels l'item appartient ("c:gems/ruby", "minecraft:piglin_loved"...).</summary>
    public IReadOnlyList<NamespacedId> ItemTags
    {
        get => _itemTags;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SequenceEqual(_itemTags))
            {
                _itemTags = [.. value.Distinct()];
                RaiseChanged(nameof(ItemTags));
            }
        }
    }

    // ----- Nourriture (compléments V0.4.1) -----

    /// <summary>Time to eat the item, in seconds (1.6 by default, 0.8 for berries).</summary>
    public float EatSeconds
    {
        get => _eatSeconds;
        set => SetField(ref _eatSeconds, float.IsFinite(value) ? Math.Clamp(value, 0.05f, MaxEatSeconds) : DefaultEatSeconds);
    }

    /// <summary>Item given back once eaten (the bowl of a stew), or null.</summary>
    public ContentReference? EatResult
    {
        get => _eatResult;
        set => SetField(ref _eatResult, value);
    }

    /// <summary>Animation of the player while eating (eat, drink...).</summary>
    public ItemUseAnimation UseAnimation
    {
        get => _useAnimation;
        set => SetField(ref _useAnimation, value);
    }

    // ----- Visual and properties of item.md (V0.7.12) -----

    /// <summary>Texture shown in the inventories when the item has a 3D model; null: the model there too.</summary>
    public Guid? GuiTextureId
    {
        get => _guiTextureId;
        set => SetField(ref _guiTextureId, value);
    }

    /// <summary>Function (the holder → yes / no) that turns the glint on; null: always on.</summary>
    public Guid? GlintCondition
    {
        get => _glintCondition;
        set => SetField(ref _glintCondition, value);
    }

    /// <summary>Item or tag that repairs this item on an anvil, or null (the material of a tool).</summary>
    public ContentReference? RepairItem
    {
        get => _repairItem;
        set => SetField(ref _repairItem, value);
    }

    /// <summary>Every block mined with this item drops (like the right tool for all of them).</summary>
    public bool HarvestsAnyBlock
    {
        get => _harvestsAnyBlock;
        set => SetField(ref _harvestsAnyBlock, value);
    }

    /// <summary>What stays in the crafting grid (and in the fuel slot) once the item is used.</summary>
    public ItemRemainderKind Remainder
    {
        get => _remainder;
        set => SetField(ref _remainder, value);
    }

    /// <summary>Properties of a tool of the custom material.</summary>
    public ItemCustomTier CustomTier
    {
        get => _customTier;
        set => SetField(ref _customTier, value ?? new ItemCustomTier());
    }

    public bool UsesCustomTier => Kind.IsTool() && Tier == ItemTier.Custom;

    // ----- Advanced properties of item.md (V0.7.12) -----

    /// <summary>Function (→ seconds) that gives the burn time in a furnace, or null (<see cref="BurnTime"/>).</summary>
    public Guid? BurnTimeFunction
    {
        get => _burnTimeFunction;
        set => SetField(ref _burnTimeFunction, value);
    }

    /// <summary>Function (→ yes / no) that lets the item burn, or null (always).</summary>
    public Guid? BurnCondition
    {
        get => _burnCondition;
        set => SetField(ref _burnCondition, value);
    }

    /// <summary>Chance, in percent, that the item raises the level of a composter; 0 = not compostable.</summary>
    public int CompostChance
    {
        get => _compostChance;
        set => SetField(ref _compostChance, Math.Clamp(value, 0, 100));
    }

    /// <summary>Function (the player → yes / no) that lets the item go into a composter, or null (always).</summary>
    public Guid? CompostCondition
    {
        get => _compostCondition;
        set => SetField(ref _compostCondition, value);
    }

    /// <summary>Piglins take the item to barter, like gold ingots.</summary>
    public bool PiglinCurrency
    {
        get => _piglinCurrency;
        set => SetField(ref _piglinCurrency, value);
    }

    /// <summary>Methods of the class of the item (V0.7.13): bindings and events, created on demand.</summary>
    public IReadOnlyList<ElementMethod> Methods
    {
        get => _methods;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SequenceEqual(_methods))
            {
                _methods = [.. value];
                RaiseChanged(nameof(Methods));
            }
        }
    }

    /// <summary>The method of a place of the class, or null when it is not created.</summary>
    public ElementMethod? FindMethod(string slot) => Methods.FirstOrDefault(m => m.Slot == slot);

    /// <summary>The item shoots (bow, crossbow, thrown item, fishing rod); None by default.</summary>
    public ItemRanged Ranged
    {
        get => _ranged;
        set => SetField(ref _ranged, value ?? new ItemRanged());
    }

    /// <summary>Effets donnés en mangeant (pomme dorée : régénération et absorption).</summary>
    public IReadOnlyList<FoodEffect> FoodEffects
    {
        get => _foodEffects;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SequenceEqual(_foodEffects))
            {
                _foodEffects = [.. value];
                RaiseChanged(nameof(FoodEffects));
            }
        }
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (AssetReference reference in base.GetReferences())
        {
            yield return reference;
        }

        if (Remainder == ItemRemainderKind.OtherItem && CraftRemainder is { Kind: ContentReferenceKind.Asset } remainder)
        {
            yield return new AssetReference(remainder.AssetId, AssetReferenceKind.Item, Tr.T("item returned after crafting"));
        }

        if (UsesCustomModel && GuiTextureId is { } gui)
        {
            yield return new AssetReference(gui, AssetReferenceKind.Texture, Tr.T("texture in the inventories"));
        }

        if (!NoRepair && RepairItem is { Kind: ContentReferenceKind.Asset } repair)
        {
            yield return new AssetReference(repair.AssetId, AssetReferenceKind.Item, Tr.T("repair item"));
        }

        if (Ranged.Kind is not (RangedItemKind.None or RangedItemKind.FishingRod) && Ranged.Ammo is { Kind: ContentReferenceKind.Asset } ammo)
        {
            yield return new AssetReference(ammo.AssetId, AssetReferenceKind.Item, Tr.T("ammunition"));
        }

        foreach (ElementMethod method in Methods.Where(m => m is { Language: MethodLanguage.Nodal, GraphId: not null }))
        {
            yield return new AssetReference(method.GraphId!.Value, AssetReferenceKind.Definition, Tr.F("method {0}", method.Slot));
        }

        if (IsFood && EatResult is { Kind: ContentReferenceKind.Asset } result)
        {
            yield return new AssetReference(result.AssetId, AssetReferenceKind.Item, Tr.T("item left once eaten"));
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();
        if (Kind.IsEquipment())
        {
            if (HasDurability)
            {
                diagnostics.Info(Tr.T("The durability of a tool or armor comes from its material: the value entered is ignored."), source, Id);
            }

            if (MaxStackSize > 1)
            {
                diagnostics.Info(Tr.T("A tool or armor does not stack: the stack size will be 1."), source, Id);
            }
        }
        else if (HasDurability && MaxStackSize > 1)
        {
            diagnostics.Warning(Tr.T("An item with durability does not stack: the stack size will be 1."), source, Id);
        }

        if (Remainder == ItemRemainderKind.OtherItem && CraftRemainder is { Kind: ContentReferenceKind.Tag })
        {
            diagnostics.Error(Tr.T("The item returned after crafting must be a specific item, not a tag."), source, Id);
        }

        if (Remainder == ItemRemainderKind.OtherItem && CraftRemainder is null)
        {
            diagnostics.Warning(Tr.T("No item is chosen to stay in the crafting grid: the item is used up."), source, Id);
        }

        if (Remainder == ItemRemainderKind.ItselfDamaged && !HasDurability && !Kind.IsEquipment())
        {
            diagnostics.Warning(Tr.T("The item has no durability to lose in the crafting grid: it stays as it is."), source, Id);
        }

        if (IsFood && EatResult is { Kind: ContentReferenceKind.Tag })
        {
            diagnostics.Error(Tr.T("The item left once eaten must be a specific item, not a tag."), source, Id);
        }

        if (Ranged.Kind != RangedItemKind.None && Kind != ItemKind.Simple)
        {
            diagnostics.Error(Tr.T("A tool or an armor cannot also be a ranged item."), source, Id);
        }

        if (Ranged.Kind != RangedItemKind.None && IsFood)
        {
            diagnostics.Error(Tr.T("A food cannot also be a ranged item: both are used by holding the right click."), source, Id);
        }

        if (Ranged.Ammo is { Kind: ContentReferenceKind.Tag })
        {
            diagnostics.Error(Tr.T("The ammunition of a ranged item must be a specific item, not a tag."), source, Id);
        }

        if (BurnTime == 0 && (BurnTimeFunction is not null || BurnCondition is not null))
        {
            diagnostics.Info(Tr.T("The functions of the fuel are not used: the item is not a fuel."), source, Id);
        }

        foreach (ElementMethod method in Methods.Where(m => ItemMethodSlots.Find(m.Slot) is null))
        {
            diagnostics.Warning(Tr.F("The method \"{0}\" is not a method of an item: it is ignored.", method.Slot), source, Id);
        }

        foreach (IGrouping<string, ElementMethod> twice in Methods.GroupBy(m => m.Slot).Where(g => g.Count() > 1))
        {
            diagnostics.Error(Tr.F("The method \"{0}\" is created twice.", twice.Key), source, Id);
        }

        if (UsesCustomTier && CustomTier.MiningLevel == ItemTier.Custom)
        {
            diagnostics.Error(Tr.T("The mining level of the custom material must be one of the game."), source, Id);
        }

        if (IsFood && FoodEffects.Count > 0 && FoodEffects.GroupBy(e => e.Effect).Any(g => g.Count() > 1))
        {
            diagnostics.Info(Tr.T("The same effect is given several times when eaten; Minecraft only applies the strongest."), source, Id);
        }
    }
}

/// <summary>Effet donné en mangeant : type, durée, niveau (0 = I) et probabilité (0 à 1).</summary>
public sealed record FoodEffect
{
    public const int MaxDurationSeconds = 3600;
    public const int MaxAmplifier = 9;

    public FoodEffect(FoodEffectKind effect, int durationSeconds, int amplifier, float probability)
    {
        Effect = effect;
        DurationSeconds = Math.Clamp(durationSeconds, 1, MaxDurationSeconds);
        Amplifier = Math.Clamp(amplifier, 0, MaxAmplifier);
        Probability = float.IsFinite(probability) ? Math.Clamp(probability, 0f, 1f) : 1f;
    }

    public FoodEffectKind Effect { get; init; }

    public int DurationSeconds { get; init; }

    public int Amplifier { get; init; }

    public float Probability { get; init; }
}

/// <summary>
/// Material of a tool chosen in the editor (V0.7.12): uses, mining speed, enchantability, and the
/// material of the game whose blocks it can harvest (iron: diamond ore, not obsidian).
/// </summary>
public sealed record ItemCustomTier
{
    public const int MaxUses = 100_000;
    public const float MaxSpeed = 100f;
    public const int MaxEnchantability = 100;

    private readonly int _uses = 500;
    private readonly float _speed = 6f;
    private readonly int _enchantability = 14;

    /// <summary>Uses before breaking (iron: 250, diamond: 1561).</summary>
    public int Uses
    {
        get => _uses;
        init => _uses = Math.Clamp(value, 1, MaxUses);
    }

    /// <summary>Mining speed (iron: 6, diamond: 8, gold: 12).</summary>
    public float Speed
    {
        get => _speed;
        init => _speed = float.IsFinite(value) ? Math.Clamp(value, 0f, MaxSpeed) : 6f;
    }

    /// <summary>Quality of the enchantments of the table (iron: 14, gold: 22).</summary>
    public int Enchantability
    {
        get => _enchantability;
        init => _enchantability = Math.Clamp(value, 0, MaxEnchantability);
    }

    /// <summary>Material of the game whose blocks the tool can harvest.</summary>
    public ItemTier MiningLevel { get; init; } = ItemTier.Iron;
}

/// <summary>
/// A ranged item (V0.7.12): what it shoots and with what ammunition, how it shoots (on release,
/// at once, while held), the time between two shots, the speed and the damage of the arrows.
/// </summary>
public sealed record ItemRanged
{
    public const int MaxRateTicks = 1200;
    public const float MaxSpeed = 10f;
    public const float MaxDamage = 100f;

    private readonly int _rateTicks = 10;
    private readonly float _speed = 3f;
    private readonly float _damage = 2f;

    public RangedItemKind Kind { get; init; }

    public RangedProjectile Projectile { get; init; }

    /// <summary>Item used up by each shot; null: the one of the projectile (arrow, snowball...).</summary>
    public ContentReference? Ammo { get; init; }

    /// <summary>False: shoots without ammunition, like a bow with Infinity.</summary>
    public bool NeedsAmmo { get; init; } = true;

    public RangedFireMode Mode { get; init; }

    /// <summary>Ticks between two shots while held, or cooldown after a shot at once.</summary>
    public int RateTicks
    {
        get => _rateTicks;
        init => _rateTicks = Math.Clamp(value, 1, MaxRateTicks);
    }

    /// <summary>Speed of the projectile at full power (bow: 3, snowball: 1.5).</summary>
    public float Speed
    {
        get => _speed;
        init => _speed = float.IsFinite(value) ? Math.Clamp(value, 0.1f, MaxSpeed) : 3f;
    }

    /// <summary>Base damage of an arrow (2 like the game; multiplied by its speed).</summary>
    public float Damage
    {
        get => _damage;
        init => _damage = float.IsFinite(value) ? Math.Clamp(value, 0f, MaxDamage) : 2f;
    }
}
