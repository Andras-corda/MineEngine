using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.GameData;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Advanced and ranged properties of the item tab (V0.7.12, item.md): fuel and compost with their
/// functions, the currency of the piglins, and the items that shoot.
/// </summary>
public sealed partial class ItemAssetViewModel
{
    private const double DefaultBurnSeconds = 10;
    private const int DefaultCompostChance = 65;

    // ----- Fuel -----

    /// <summary>The item burns in a furnace (10 s at first); unchecked, it does not.</summary>
    public bool IsFuel
    {
        get => _item.BurnTime > 0;
        set => BurnSeconds = value ? (_item.BurnTime > 0 ? BurnSeconds : DefaultBurnSeconds) : 0;
    }

    public Guid? BurnTimeFunction
    {
        get => _item.BurnTimeFunction;
        set => ChangeIfDifferent(Tr.T("Change the function of the burn time"), nameof(ItemAsset.BurnTimeFunction), () => _item.BurnTimeFunction, v => _item.BurnTimeFunction = v, value);
    }

    public Guid? BurnCondition
    {
        get => _item.BurnCondition;
        set => ChangeIfDifferent(Tr.T("Change the burning condition"), nameof(ItemAsset.BurnCondition), () => _item.BurnCondition, v => _item.BurnCondition = v, value);
    }

    /// <summary>Functions of the project, for a value: none keeps the value entered.</summary>
    public IReadOnlyList<Choice<Guid?>> ValueFunctionChoices =>
    [
        new(null, Tr.T("None (the value above)")),
        .. FunctionChoices.Skip(1),
    ];

    // ----- Compost -----

    /// <summary>The item goes into a composter (65 % at first, like an apple); unchecked, it does not.</summary>
    public bool IsCompostable
    {
        get => _item.CompostChance > 0;
        set => CompostChance = value ? (_item.CompostChance > 0 ? _item.CompostChance : DefaultCompostChance) : 0;
    }

    public double CompostChance
    {
        get => _item.CompostChance;
        set => ChangeNumber(Tr.T("Change the compost chance"), nameof(ItemAsset.CompostChance), value, () => _item.CompostChance, v => _item.CompostChance = v,
            v => Math.Clamp((int)Math.Round(v), 0, 100));
    }

    public Guid? CompostCondition
    {
        get => _item.CompostCondition;
        set => ChangeIfDifferent(Tr.T("Change the composting condition"), nameof(ItemAsset.CompostCondition), () => _item.CompostCondition, v => _item.CompostCondition = v, value);
    }

    // ----- Piglins -----

    public bool PiglinCurrency
    {
        get => _item.PiglinCurrency;
        set => ChangeIfDifferent(Tr.T("Change the trade with the piglins"), nameof(ItemAsset.PiglinCurrency), () => _item.PiglinCurrency, v => _item.PiglinCurrency = v, value);
    }

    // ----- Ranged item -----

    public IReadOnlyList<Choice<RangedItemKind>> RangedKindChoices => [.. GameDataChoices.RangedKinds.Where(c => c.Value != RangedItemKind.None)];

    public IReadOnlyList<Choice<RangedProjectile>> ProjectileChoices => GameDataChoices.RangedProjectiles;

    public IReadOnlyList<Choice<RangedFireMode>> FireModeChoices => GameDataChoices.RangedFireModes;

    /// <summary>The item shoots (a bow at first); unchecked, it does not.</summary>
    public bool IsRanged
    {
        get => _item.Ranged.Kind != RangedItemKind.None;
        set => RangedKind = value ? (IsRanged ? _item.Ranged.Kind : RangedItemKind.Bow) : RangedItemKind.None;
    }

    public RangedItemKind RangedKind
    {
        get => _item.Ranged.Kind;
        set => ChangeRanged(Tr.T("Change the kind of ranged item"), r => r with { Kind = value });
    }

    /// <summary>A fishing rod is the one of the game: the other settings are for the items that shoot.</summary>
    public bool IsShooter => _item.Ranged.Kind is RangedItemKind.Bow or RangedItemKind.Crossbow or RangedItemKind.Throw;

    public RangedProjectile Projectile
    {
        get => _item.Ranged.Projectile;
        set => ChangeRanged(Tr.T("Change the projectile"), r => r with { Projectile = value });
    }

    public ReferencePickerViewModel AmmoPicker { get; }

    public bool NeedsAmmo
    {
        get => _item.Ranged.NeedsAmmo;
        set => ChangeRanged(Tr.T("Change the ammunition check"), r => r with { NeedsAmmo = value });
    }

    public RangedFireMode FireMode
    {
        get => _item.Ranged.Mode;
        set => ChangeRanged(Tr.T("Change how the item shoots"), r => r with { Mode = value });
    }

    public bool UsesFireRate => _item.Ranged.Mode != RangedFireMode.Charge;

    public double FireRate
    {
        get => _item.Ranged.RateTicks;
        set => ChangeRanged(Tr.T("Change the time between two shots"), r => r with { RateTicks = (int)Math.Round(value) });
    }

    public double ProjectileSpeed
    {
        get => Math.Round(_item.Ranged.Speed, 2);
        set => ChangeRanged(Tr.T("Change the speed of the projectile"), r => r with { Speed = (float)value });
    }

    public double ProjectileDamage
    {
        get => Math.Round(_item.Ranged.Damage, 2);
        set => ChangeRanged(Tr.T("Change the damage of the arrows"), r => r with { Damage = (float)value });
    }

    public int MaxFireRate => ItemRanged.MaxRateTicks;

    public double MaxProjectileSpeed => ItemRanged.MaxSpeed;

    public double MaxProjectileDamage => ItemRanged.MaxDamage;

    private void ChangeRanged(string description, Func<ItemRanged, ItemRanged> update) =>
        ChangeIfDifferent(description, nameof(ItemAsset.Ranged), () => _item.Ranged, v => _item.Ranged = v, update(_item.Ranged));

    private void OnAdvancedPropertyChanged(string propertyName)
    {
        string[] derived = propertyName switch
        {
            nameof(ItemAsset.BurnTime) => [nameof(IsFuel)],
            nameof(ItemAsset.BurnTimeFunction) => [nameof(BurnTimeFunction)],
            nameof(ItemAsset.BurnCondition) => [nameof(BurnCondition)],
            nameof(ItemAsset.CompostChance) => [nameof(IsCompostable), nameof(CompostChance)],
            nameof(ItemAsset.CompostCondition) => [nameof(CompostCondition)],
            nameof(ItemAsset.PiglinCurrency) => [nameof(PiglinCurrency)],
            nameof(ItemAsset.Ranged) =>
            [
                nameof(IsRanged), nameof(RangedKind), nameof(IsShooter), nameof(Projectile), nameof(NeedsAmmo), nameof(FireMode),
                nameof(UsesFireRate), nameof(FireRate), nameof(ProjectileSpeed), nameof(ProjectileDamage), nameof(RangedEventsHint),
            ],
            _ => [],
        };
        foreach (string name in derived)
        {
            OnPropertyChanged(name);
        }

        if (propertyName == nameof(ItemAsset.Ranged))
        {
            AmmoPicker.Refresh();
        }
    }
}
