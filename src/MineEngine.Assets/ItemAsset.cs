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
    private bool _fastFood;
    private IReadOnlyList<FoodEffect> _foodEffects = [];
    private int _durability;
    private bool _hasGlint;
    private bool _isFood;
    private int _nutrition = 4;
    private float _saturation = 0.3f;
    private bool _alwaysEdible;
    private bool _isMeat;

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

    /// <summary>Se mange deux fois plus vite (comme les baies).</summary>
    public bool FastFood
    {
        get => _fastFood;
        set => SetField(ref _fastFood, value);
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

        if (CraftRemainder is { Kind: ContentReferenceKind.Asset } remainder)
        {
            yield return new AssetReference(remainder.AssetId, AssetReferenceKind.Item, Tr.T("item returned after crafting"));
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

        if (CraftRemainder is { Kind: ContentReferenceKind.Tag })
        {
            diagnostics.Error(Tr.T("The item returned after crafting must be a specific item, not a tag."), source, Id);
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
