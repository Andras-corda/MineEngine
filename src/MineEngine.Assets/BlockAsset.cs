using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>Un bloc plein simple, qui se récupère lui-même quand on le casse.</summary>
public sealed class BlockAsset : TexturedAsset
{
    public const float MaxStrength = 3_600_000f;
    public const float DefaultHardness = 1.5f;
    public const float DefaultResistance = 6f;

    private float _hardness = DefaultHardness;
    private float _resistance = DefaultResistance;

    public BlockAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Block;

    /// <summary>Temps nécessaire pour casser le bloc (pierre : 1,5 ; terre : 0,5).</summary>
    public float Hardness
    {
        get => _hardness;
        set => SetField(ref _hardness, EnsureValidStrength(value));
    }

    /// <summary>Résistance aux explosions (pierre : 6 ; obsidienne : 1200).</summary>
    public float Resistance
    {
        get => _resistance;
        set => SetField(ref _resistance, EnsureValidStrength(value));
    }

    public static bool IsValidStrength(float value) => float.IsFinite(value) && value is >= 0f and <= MaxStrength;

    private static float EnsureValidStrength(float value)
    {
        if (!IsValidStrength(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, $"La valeur doit être comprise entre 0 et {MaxStrength}.");
        }

        return value;
    }
}
