using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
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

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (HasDurability && MaxStackSize > 1)
        {
            diagnostics.Warning("Un item avec durabilité ne s'empile pas : la taille de pile sera 1.", ToString(), Id);
        }
    }
}
