using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>Un item simple (objet d'inventaire sans comportement particulier).</summary>
public sealed class ItemAsset : TexturedAsset
{
    public const int MinStackSize = 1;
    public const int MaxStackSizeLimit = 99;
    public const int DefaultStackSize = 64;

    private int _maxStackSize = DefaultStackSize;

    public ItemAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Item;

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

    public static bool IsValidStackSize(int value) => value is >= MinStackSize and <= MaxStackSizeLimit;
}
