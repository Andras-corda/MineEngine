using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Asset qui existe sous forme d'item dans l'inventaire : propriétés communes aux
/// items et à la forme item des blocs (pile, rareté, info-bulle, onglets créatifs).
/// </summary>
public abstract class InventoryAsset : TexturedAsset
{
    public const int MinStackSize = 1;
    public const int MaxStackSizeLimit = 99;
    public const int DefaultStackSize = 64;

    private int _maxStackSize = DefaultStackSize;
    private ItemRarity _rarity = ItemRarity.Common;
    private bool _fireResistant;
    private IReadOnlyList<string> _tooltipLines = [];
    private bool _inModCreativeTab = true;
    private IReadOnlySet<CreativeTab> _creativeTabs = new HashSet<CreativeTab>();

    protected InventoryAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    /// <summary>Nombre maximal d'exemplaires par case d'inventaire (1 à 99).</summary>
    public int MaxStackSize
    {
        get => _maxStackSize;
        set
        {
            if (!IsValidStackSize(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, $"La taille de pile doit être comprise entre {MinStackSize} et {MaxStackSizeLimit}.");
            }

            SetField(ref _maxStackSize, value);
        }
    }

    /// <summary>Rareté : ne change que la couleur du nom.</summary>
    public ItemRarity Rarity
    {
        get => _rarity;
        set => SetField(ref _rarity, value);
    }

    /// <summary>L'item ne brûle pas dans la lave ou le feu (comme la netherite).</summary>
    public bool FireResistant
    {
        get => _fireResistant;
        set => SetField(ref _fireResistant, value);
    }

    /// <summary>Lignes ajoutées sous le nom dans l'info-bulle ("Special information" de MCreator).</summary>
    public IReadOnlyList<string> TooltipLines
    {
        get => _tooltipLines;
        set => SetField(ref _tooltipLines, (value ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList());
    }

    /// <summary>Affiché dans l'onglet créatif propre au mod.</summary>
    public bool InModCreativeTab
    {
        get => _inModCreativeTab;
        set => SetField(ref _inModCreativeTab, value);
    }

    /// <summary>Onglets créatifs vanilla dans lesquels l'item apparaît aussi.</summary>
    public IReadOnlySet<CreativeTab> CreativeTabs
    {
        get => _creativeTabs;
        set => SetField(ref _creativeTabs, new HashSet<CreativeTab>(value ?? new HashSet<CreativeTab>()));
    }

    public static bool IsValidStackSize(int value) => value is >= MinStackSize and <= MaxStackSizeLimit;

    /// <summary>Vrai si la forme item existe (toujours pour un item, optionnel pour un bloc).</summary>
    public virtual bool HasItemForm => true;

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (HasItemForm && !InModCreativeTab && CreativeTabs.Count == 0)
        {
            diagnostics.Info("N'apparaît dans aucun onglet créatif : il ne sera accessible que par commande ou recette.", ToString(), Id);
        }
    }
}
