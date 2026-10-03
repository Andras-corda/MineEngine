using MineEngine.Assets;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Inspector d'un item : visuel, propriétés et nourriture, comme dans MCreator.</summary>
public sealed class ItemAssetViewModel : InventoryAssetViewModel
{
    private readonly ItemAsset _item;

    public ItemAssetViewModel(ItemAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _item = model;
    }

    public int MaxDurability => ItemAsset.MaxDurability;

    public int MaxNutrition => ItemAsset.MaxNutrition;

    public double MaxSaturation => ItemAsset.MaxSaturation;

    public double Durability
    {
        get => _item.Durability;
        set => ChangeNumber("Modifier la durabilité", nameof(ItemAsset.Durability), value, () => _item.Durability, v => _item.Durability = v,
            v => Math.Clamp((int)Math.Round(v), 0, ItemAsset.MaxDurability));
    }

    /// <summary>La taille de pile n'a pas de sens pour un item à durabilité (toujours 1).</summary>
    public override bool CanEditStackSize => !_item.HasDurability;

    public override string StackSizeHint => _item.HasDurability
        ? "Toujours 1 : un item à durabilité ne s'empile pas."
        : base.StackSizeHint;

    public bool HasGlint
    {
        get => _item.HasGlint;
        set => ChangeIfDifferent("Modifier l'effet brillant", nameof(ItemAsset.HasGlint), () => _item.HasGlint, v => _item.HasGlint = v, value);
    }

    public bool IsFood
    {
        get => _item.IsFood;
        set => ChangeIfDifferent("Rendre comestible", nameof(ItemAsset.IsFood), () => _item.IsFood, v => _item.IsFood = v, value);
    }

    public double Nutrition
    {
        get => _item.Nutrition;
        set => ChangeNumber("Modifier la valeur nutritive", nameof(ItemAsset.Nutrition), value, () => _item.Nutrition, v => _item.Nutrition = v,
            v => Math.Clamp((int)Math.Round(v), 0, ItemAsset.MaxNutrition));
    }

    public double Saturation
    {
        get => _item.Saturation;
        set => ChangeNumber("Modifier la saturation", nameof(ItemAsset.Saturation), value, () => _item.Saturation, v => _item.Saturation = v,
            v => (float)Math.Clamp(v, 0d, ItemAsset.MaxSaturation));
    }

    public bool AlwaysEdible
    {
        get => _item.AlwaysEdible;
        set => ChangeIfDifferent("Modifier « toujours mangeable »", nameof(ItemAsset.AlwaysEdible), () => _item.AlwaysEdible, v => _item.AlwaysEdible = v, value);
    }

    public bool IsMeat
    {
        get => _item.IsMeat;
        set => ChangeIfDifferent("Modifier « viande »", nameof(ItemAsset.IsMeat), () => _item.IsMeat, v => _item.IsMeat = v, value);
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName == nameof(ItemAsset.Durability))
        {
            OnPropertyChanged(nameof(CanEditStackSize));
            OnPropertyChanged(nameof(StackSizeHint));
        }
    }
}
