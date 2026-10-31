using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Damage type of the mod (V0.7.10): the death message, the hunger it costs, how the difficulty
/// scales it, the sound of the hurt, and its properties (ignores armor, fire, projectile...), which
/// are tags of the game. Graphs hurt with it ("Damage of a type"); a mob can be immune to it.
/// </summary>
public sealed class DamageTypeAsset : Asset
{
    public const float MaxExhaustion = 10f;

    private string _deathMessage = "{victim} died";
    private string _killerMessage = "{victim} was killed by {killer}";
    private float _exhaustion = 0.1f;
    private DamageScaling _scaling = DamageScaling.WhenCausedByLivingNonPlayer;
    private DamageEffects _effects = DamageEffects.Hurt;
    private DeathMessageType _messageType = DeathMessageType.Default;
    private IReadOnlySet<DamageTypeFlag> _flags = new HashSet<DamageTypeFlag>();

    public DamageTypeAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.DamageType;

    /// <summary>Message when the victim dies of it alone; {victim} is its name.</summary>
    public string DeathMessage
    {
        get => _deathMessage;
        set => SetField(ref _deathMessage, value?.Trim() ?? string.Empty);
    }

    /// <summary>Message when another entity caused it; {killer} is the name of that entity.</summary>
    public string KillerMessage
    {
        get => _killerMessage;
        set => SetField(ref _killerMessage, value?.Trim() ?? string.Empty);
    }

    /// <summary>Hunger the damage costs (most: 0.1; starving: 0).</summary>
    public float Exhaustion
    {
        get => _exhaustion;
        set => SetField(ref _exhaustion, float.IsFinite(value) ? Math.Clamp(value, 0f, MaxExhaustion) : 0.1f);
    }

    public DamageScaling Scaling
    {
        get => _scaling;
        set => SetField(ref _scaling, value);
    }

    /// <summary>Sound of the hurt (hurt, thorns, drowning, burning, poking, freezing).</summary>
    public DamageEffects Effects
    {
        get => _effects;
        set => SetField(ref _effects, value);
    }

    public DeathMessageType MessageType
    {
        get => _messageType;
        set => SetField(ref _messageType, value);
    }

    /// <summary>Properties of the damage: each is a tag of the game the damage type joins.</summary>
    public IReadOnlySet<DamageTypeFlag> Flags
    {
        get => _flags;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SetEquals(_flags))
            {
                _flags = new HashSet<DamageTypeFlag>(value);
                RaiseChanged(nameof(Flags));
            }
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (DeathMessage.Length == 0)
        {
            diagnostics.Warning(Tr.T("The death message is empty: the game shows its translation key."), ToString(), Id);
        }

        if (KillerMessage.Length > 0 && !KillerMessage.Contains("{killer}", StringComparison.Ordinal))
        {
            diagnostics.Info(Tr.T("The message with a killer does not name it: write {killer} where its name goes."), ToString(), Id);
        }
    }
}
