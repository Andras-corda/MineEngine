using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Enchantment of the mod (V0.6.3): the items it goes on, its maximum level, how often it is
/// offered, whether it is a treasure or a curse, and a bonus to the damage of weapons. Its level
/// on an item is read in graphs with the enchantment nodes ("Enchantment level"...).
/// </summary>
public sealed class EnchantmentAsset : Asset
{
    public const int MaxLevelLimit = 10;

    private EnchantmentTarget _target = EnchantmentTarget.Weapon;
    private EnchantmentRarity _rarity = EnchantmentRarity.Uncommon;
    private int _maxLevel = 3;
    private bool _treasure;
    private bool _curse;
    private float _damageBonus;

    public EnchantmentAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Enchantment;

    public EnchantmentTarget Target
    {
        get => _target;
        set => SetField(ref _target, value);
    }

    public EnchantmentRarity Rarity
    {
        get => _rarity;
        set => SetField(ref _rarity, value);
    }

    public int MaxLevel
    {
        get => _maxLevel;
        set => SetField(ref _maxLevel, Math.Clamp(value, 1, MaxLevelLimit));
    }

    /// <summary>Only found in loot and trades, never on the enchanting table (like Mending).</summary>
    public bool Treasure
    {
        get => _treasure;
        set => SetField(ref _treasure, value);
    }

    /// <summary>A curse: shown in red and cannot be removed by a grindstone.</summary>
    public bool Curse
    {
        get => _curse;
        set => SetField(ref _curse, value);
    }

    /// <summary>Extra damage of a weapon per level of the enchantment (Sharpness: about 0.5 to 1); 0 for none.</summary>
    public float DamageBonus
    {
        get => _damageBonus;
        set => SetField(ref _damageBonus, Math.Clamp(value, 0f, 100f));
    }

    /// <summary>Weight in the enchanting table: Common 10, Uncommon 5, Rare 2, Very rare 1.</summary>
    public static int Weight(EnchantmentRarity rarity) => rarity switch
    {
        EnchantmentRarity.Common => 10,
        EnchantmentRarity.Uncommon => 5,
        EnchantmentRarity.Rare => 2,
        _ => 1,
    };

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (DamageBonus > 0 && Target is not (EnchantmentTarget.Weapon or EnchantmentTarget.Sword or EnchantmentTarget.Trident))
        {
            diagnostics.Warning(Tr.T("The damage bonus only applies to weapons (sword, axe, trident)."), ToString(), Id);
        }
    }
}
