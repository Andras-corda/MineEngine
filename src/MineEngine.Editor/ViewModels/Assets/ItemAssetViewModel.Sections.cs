using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.GameData;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Item tab of V0.7.12 (item.md): the texture of the inventories and the glint condition, the
/// boxes that turn a group of fields on (durability, tool, armor), the repair item, what stays in
/// the crafting grid, the end of eating, and the material of a tool chosen in the editor.
/// </summary>
public sealed partial class ItemAssetViewModel
{
    private const int DefaultDurability = 250;

    // ----- Visual -----

    /// <summary>Flat texture of the inventories when the item has a 3D model.</summary>
    public TextureSlotViewModel GuiTextureSlot { get; }

    public Guid? GlintCondition
    {
        get => _item.GlintCondition;
        set => ChangeIfDifferent(Tr.T("Change the glint condition"), nameof(ItemAsset.GlintCondition), () => _item.GlintCondition, v => _item.GlintCondition = v, value);
    }

    /// <summary>Functions of the project (the holder → yes / no).</summary>
    public IReadOnlyList<Choice<Guid?>> FunctionChoices =>
    [
        new(null, Tr.T("None (always)")),
        .. Context.Project.Assets.OfType<GraphAsset>().Where(g => g.Kind == GraphKind.Function && g.ElementId is null)
            .OrderBy(g => g.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new Choice<Guid?>(g.Id, DisplayLabel(g))),
    ];

    // ----- Item properties -----

    /// <summary>The item wears out; unchecked, it has no durability (and stacks again).</summary>
    public bool HasDurability
    {
        get => _item.HasDurability;
        set => Durability = value ? (_item.HasDurability ? _item.Durability : DefaultDurability) : 0;
    }

    public ReferencePickerViewModel RepairItemPicker { get; }

    public bool CanChooseRepairItem => !_item.NoRepair;

    public bool HarvestsAnyBlock
    {
        get => _item.HarvestsAnyBlock;
        set => ChangeIfDifferent(Tr.T("Change \"harvests any block\""), nameof(ItemAsset.HarvestsAnyBlock), () => _item.HarvestsAnyBlock, v => _item.HarvestsAnyBlock = v, value);
    }

    public IReadOnlyList<Choice<ItemRemainderKind>> RemainderChoices => GameDataChoices.ItemRemainders;

    public ItemRemainderKind Remainder
    {
        get => _item.Remainder;
        set => ChangeIfDifferent(Tr.T("Change what stays in the crafting grid"), nameof(ItemAsset.Remainder), () => _item.Remainder, v => _item.Remainder = v, value);
    }

    public bool UsesOtherRemainder => _item.Remainder == ItemRemainderKind.OtherItem;

    // ----- Food -----

    public ReferencePickerViewModel EatResultPicker { get; }

    public IReadOnlyList<Choice<ItemUseAnimation>> UseAnimationChoices => GameDataChoices.ItemUseAnimations;

    public ItemUseAnimation UseAnimation
    {
        get => _item.UseAnimation;
        set => ChangeIfDifferent(Tr.T("Change the eating animation"), nameof(ItemAsset.UseAnimation), () => _item.UseAnimation, v => _item.UseAnimation = v, value);
    }

    public double EatSeconds
    {
        get => Math.Round(_item.EatSeconds, 2);
        set => ChangeNumber(Tr.T("Change the eating time"), nameof(ItemAsset.EatSeconds), value, () => _item.EatSeconds, v => _item.EatSeconds = v,
            v => (float)Math.Clamp(v, 0.05, ItemAsset.MaxEatSeconds));
    }

    public double MaxEatSeconds => ItemAsset.MaxEatSeconds;

    // ----- Tool and armor -----

    public IReadOnlyList<Choice<ItemKind>> ToolKindChoices => [.. GameDataChoices.ItemKinds.Where(c => c.Value.IsTool())];

    public IReadOnlyList<Choice<ItemKind>> ArmorKindChoices => [.. GameDataChoices.ItemKinds.Where(c => c.Value.IsArmor())];

    /// <summary>The item is a tool or a weapon (a pickaxe at first); unchecked, a simple item.</summary>
    public bool IsToolItem
    {
        get => _item.Kind.IsTool();
        set
        {
            if (value != _item.Kind.IsTool())
            {
                Kind = value ? ItemKind.Pickaxe : ItemKind.Simple;
            }
        }
    }

    /// <summary>The item is a piece of armor (a chestplate at first); unchecked, a simple item.</summary>
    public bool IsArmorItem
    {
        get => _item.Kind.IsArmor();
        set
        {
            if (value != _item.Kind.IsArmor())
            {
                Kind = value ? ItemKind.Chestplate : ItemKind.Simple;
            }
        }
    }

    public bool UsesCustomTier => _item.UsesCustomTier;

    /// <summary>Materials of the game a custom material harvests like.</summary>
    public IReadOnlyList<Choice<ItemTier>> MiningLevelChoices => [.. GameDataChoices.ItemTiers.Where(c => c.Value != ItemTier.Custom)];

    public double CustomUses
    {
        get => _item.CustomTier.Uses;
        set => ChangeTier(Tr.T("Change the durability of the material"), t => t with { Uses = (int)Math.Round(value) });
    }

    public double CustomSpeed
    {
        get => Math.Round(_item.CustomTier.Speed, 2);
        set => ChangeTier(Tr.T("Change the efficiency of the material"), t => t with { Speed = (float)value });
    }

    public double CustomEnchantability
    {
        get => _item.CustomTier.Enchantability;
        set => ChangeTier(Tr.T("Change the enchantability of the material"), t => t with { Enchantability = (int)Math.Round(value) });
    }

    public ItemTier CustomMiningLevel
    {
        get => _item.CustomTier.MiningLevel;
        set => ChangeTier(Tr.T("Change the mining level of the material"), t => t with { MiningLevel = value });
    }

    public int MaxCustomUses => ItemCustomTier.MaxUses;

    public double MaxCustomSpeed => ItemCustomTier.MaxSpeed;

    public int MaxEnchantability => ItemCustomTier.MaxEnchantability;

    // ----- Methods of the class (V0.7.13) -----

    /// <summary>The places of methods of the class of the item, by key.</summary>
    public IReadOnlyDictionary<string, ElementMethodViewModel> MethodSlots { get; }

    public ElementMethodViewModel GlintMethod => MethodSlots[ItemMethodSlots.GlintCondition];

    public ElementMethodViewModel BurnTimeMethod => MethodSlots[ItemMethodSlots.BurnTime];

    public ElementMethodViewModel BurnConditionMethod => MethodSlots[ItemMethodSlots.BurnCondition];

    public ElementMethodViewModel CompostConditionMethod => MethodSlots[ItemMethodSlots.CompostCondition];

    private void RefreshMethods()
    {
        foreach (ElementMethodViewModel slot in MethodSlots.Values)
        {
            slot.Refresh();
        }
    }

    private void ChangeTier(string description, Func<ItemCustomTier, ItemCustomTier> update) =>
        ChangeIfDifferent(description, nameof(ItemAsset.CustomTier), () => _item.CustomTier, v => _item.CustomTier = v, update(_item.CustomTier));

    private void OnSectionPropertyChanged(string propertyName)
    {
        string[] derived = propertyName switch
        {
            nameof(ItemAsset.Durability) => [nameof(HasDurability)],
            nameof(ItemAsset.GlintCondition) => [nameof(GlintCondition)],
            nameof(ItemAsset.NoRepair) => [nameof(CanChooseRepairItem)],
            nameof(ItemAsset.Remainder) => [nameof(UsesOtherRemainder)],
            nameof(ItemAsset.Kind) => [nameof(IsToolItem), nameof(IsArmorItem), nameof(UsesCustomTier)],
            nameof(ItemAsset.Tier) => [nameof(UsesCustomTier)],
            nameof(ItemAsset.CustomTier) => [nameof(CustomUses), nameof(CustomSpeed), nameof(CustomEnchantability), nameof(CustomMiningLevel)],
            _ => [],
        };
        foreach (string name in derived)
        {
            OnPropertyChanged(name);
        }

        switch (propertyName)
        {
            case nameof(ItemAsset.Methods):
                RefreshMethods();
                break;
            case nameof(ItemAsset.GuiTextureId):
                GuiTextureSlot.Refresh();
                break;
            case nameof(ItemAsset.RepairItem):
                RepairItemPicker.Refresh();
                break;
            case nameof(ItemAsset.EatResult):
                EatResultPicker.Refresh();
                break;
        }
    }
}
