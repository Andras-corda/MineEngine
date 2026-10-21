using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Status effect of the mod (V0.6.3), like Speed or Poison: its kind and color, changes to the
/// speed, damage, armor and health of the creature (multiplied by the level of the effect), healing
/// or damage at regular intervals, and an optional potion ("&lt;id&gt;" potion, splash, lingering
/// and tipped arrows). Graphs give and remove it with the effect nodes.
/// </summary>
public sealed class EffectAsset : Asset
{
    private EffectCategory _category = EffectCategory.Beneficial;
    private string _color = "#9B59D0";
    private Guid? _iconTextureId;
    private int _speedPercent;
    private float _attackDamage;
    private float _armor;
    private float _maxHealth;
    private float _healAmount;
    private float _damageAmount;
    private int _interval = 40;
    private bool _hasPotion = true;
    private int _potionSeconds = 60;

    public EffectAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Effect;

    public EffectCategory Category
    {
        get => _category;
        set => SetField(ref _category, value);
    }

    /// <summary>Color of the particles and of the potion, "#RRGGBB".</summary>
    public string Color
    {
        get => _color;
        set => SetField(ref _color, FluidAsset.NormalizeColor(value) ?? _color);
    }

    public int ColorValue => int.Parse(_color[1..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Icon of the effect in the inventory (18 x 18 pixels); null for a placeholder.</summary>
    public Guid? IconTextureId
    {
        get => _iconTextureId;
        set => SetField(ref _iconTextureId, value == Guid.Empty ? null : value);
    }

    /// <summary>Change of the movement speed per level, in percent (Speed: +20, Slowness: -15).</summary>
    public int SpeedPercent
    {
        get => _speedPercent;
        set => SetField(ref _speedPercent, Math.Clamp(value, -100, 500));
    }

    /// <summary>Attack damage added per level (Strength: +3).</summary>
    public float AttackDamage
    {
        get => _attackDamage;
        set => SetField(ref _attackDamage, Math.Clamp(value, -100f, 100f));
    }

    /// <summary>Armor points added per level.</summary>
    public float Armor
    {
        get => _armor;
        set => SetField(ref _armor, Math.Clamp(value, -30f, 30f));
    }

    /// <summary>Maximum health added per level (Health Boost: +4, two hearts).</summary>
    public float MaxHealth
    {
        get => _maxHealth;
        set => SetField(ref _maxHealth, Math.Clamp(value, -100f, 100f));
    }

    /// <summary>Health given back at each interval, per level (Regeneration: 1).</summary>
    public float HealAmount
    {
        get => _healAmount;
        set => SetField(ref _healAmount, Math.Clamp(value, 0f, 100f));
    }

    /// <summary>Magic damage at each interval, per level (Poison: 1).</summary>
    public float DamageAmount
    {
        get => _damageAmount;
        set => SetField(ref _damageAmount, Math.Clamp(value, 0f, 100f));
    }

    /// <summary>Ticks between two heals or damages (20 ticks = 1 second).</summary>
    public int Interval
    {
        get => _interval;
        set => SetField(ref _interval, Math.Clamp(value, 1, 1200));
    }

    public bool HasPeriodicAction => HealAmount > 0 || DamageAmount > 0;

    /// <summary>A potion of the effect is added (with its splash and lingering forms and tipped arrows).</summary>
    public bool HasPotion
    {
        get => _hasPotion;
        set => SetField(ref _hasPotion, value);
    }

    public int PotionSeconds
    {
        get => _potionSeconds;
        set => SetField(ref _potionSeconds, Math.Clamp(value, 1, 3600));
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        if (IconTextureId is { } icon)
        {
            yield return new AssetReference(icon, AssetReferenceKind.Texture, Tr.T("icon of the effect"));
        }
    }
}
