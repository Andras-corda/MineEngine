using MineEngine.Assets;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Case à cocher d'un onglet créatif vanilla.</summary>
public sealed class CreativeTabOptionViewModel : ObservableObject
{
    private readonly InventoryAssetViewModel _owner;

    public CreativeTabOptionViewModel(InventoryAssetViewModel owner, CreativeTab tab, string label)
    {
        _owner = owner;
        Tab = tab;
        Label = label;
    }

    public CreativeTab Tab { get; }

    public string Label { get; }

    public bool IsChecked
    {
        get => _owner.IsInCreativeTab(Tab);
        set => _owner.SetCreativeTab(Tab, value);
    }

    public void Refresh() => OnPropertyChanged(nameof(IsChecked));
}

/// <summary>
/// Propriétés d'inventaire communes aux items et à la forme item des blocs :
/// pile, rareté, résistance au feu, info-bulle et onglets créatifs.
/// </summary>
public abstract class InventoryAssetViewModel : TexturedAssetViewModel
{
    private readonly InventoryAsset _inventory;
    private string _tooltipText;

    protected InventoryAssetViewModel(InventoryAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel, "Aucune (texture de remplacement)")
    {
        _inventory = model;
        _tooltipText = string.Join(Environment.NewLine, model.TooltipLines);
        CreativeTabOptions = [.. GameDataChoices.CreativeTabs.Select(c => new CreativeTabOptionViewModel(this, c.Value, c.Label))];
    }

    public IReadOnlyList<Choice<ItemRarity>> RarityChoices => GameDataChoices.Rarities;

    public IReadOnlyList<CreativeTabOptionViewModel> CreativeTabOptions { get; }

    public int MinStackSize => InventoryAsset.MinStackSize;

    public int MaxStackSizeLimit => InventoryAsset.MaxStackSizeLimit;

    /// <summary>Faux quand la taille de pile est imposée (item à durabilité).</summary>
    public virtual bool CanEditStackSize => true;

    public virtual string StackSizeHint => $"Entre {InventoryAsset.MinStackSize} et {InventoryAsset.MaxStackSizeLimit} (64 pour la plupart des objets).";

    /// <summary>Taille de pile (double pour le champ numérique ; arrondie et bornée à l'application).</summary>
    public double MaxStackSize
    {
        get => _inventory.MaxStackSize;
        set
        {
            if (double.IsNaN(value))
            {
                OnPropertyChanged();
                return;
            }

            int stackSize = Math.Clamp((int)Math.Round(value), InventoryAsset.MinStackSize, InventoryAsset.MaxStackSizeLimit);
            if (stackSize != _inventory.MaxStackSize)
            {
                Change("Modifier la taille de pile", nameof(InventoryAsset.MaxStackSize), () => _inventory.MaxStackSize, v => _inventory.MaxStackSize = v, stackSize);
            }
        }
    }

    public ItemRarity Rarity
    {
        get => _inventory.Rarity;
        set => ChangeIfDifferent("Modifier la rareté", nameof(InventoryAsset.Rarity), () => _inventory.Rarity, v => _inventory.Rarity = v, value);
    }

    public bool FireResistant
    {
        get => _inventory.FireResistant;
        set => ChangeIfDifferent("Modifier la résistance au feu", nameof(InventoryAsset.FireResistant), () => _inventory.FireResistant, v => _inventory.FireResistant = v, value);
    }

    public bool InModCreativeTab
    {
        get => _inventory.InModCreativeTab;
        set => ChangeIfDifferent("Modifier l'onglet du mod", nameof(InventoryAsset.InModCreativeTab), () => _inventory.InModCreativeTab, v => _inventory.InModCreativeTab = v, value);
    }

    /// <summary>Info-bulle, une ligne par ligne de texte ; les lignes vides sont ignorées.</summary>
    public string TooltipText
    {
        get => _tooltipText;
        set
        {
            if (!SetProperty(ref _tooltipText, value ?? string.Empty))
            {
                return;
            }

            IReadOnlyList<string> lines = ParseLines(_tooltipText);
            if (!lines.SequenceEqual(_inventory.TooltipLines))
            {
                Change("Modifier l'info-bulle", nameof(InventoryAsset.TooltipLines), () => _inventory.TooltipLines, v => _inventory.TooltipLines = v, lines);
            }
        }
    }

    public bool IsInCreativeTab(CreativeTab tab) => _inventory.CreativeTabs.Contains(tab);

    public void SetCreativeTab(CreativeTab tab, bool included)
    {
        if (IsInCreativeTab(tab) == included)
        {
            return;
        }

        var tabs = new HashSet<CreativeTab>(_inventory.CreativeTabs);
        if (included)
        {
            tabs.Add(tab);
        }
        else
        {
            tabs.Remove(tab);
        }

        Change("Modifier les onglets créatifs", nameof(InventoryAsset.CreativeTabs), () => _inventory.CreativeTabs, v => _inventory.CreativeTabs = v, tabs);
        Context.History.Seal();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(InventoryAsset.TooltipLines):
                if (!ParseLines(_tooltipText).SequenceEqual(_inventory.TooltipLines))
                {
                    _tooltipText = string.Join(Environment.NewLine, _inventory.TooltipLines);
                    OnPropertyChanged(nameof(TooltipText));
                }

                break;

            case nameof(InventoryAsset.CreativeTabs):
                foreach (CreativeTabOptionViewModel option in CreativeTabOptions)
                {
                    option.Refresh();
                }

                break;

            default:
                OnPropertyChanged(propertyName);
                break;
        }
    }

    private static IReadOnlyList<string> ParseLines(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
}
