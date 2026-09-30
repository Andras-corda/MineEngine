using MineEngine.Assets;

namespace MineEngine.Editor.ViewModels.Assets;

public sealed class ItemAssetViewModel : TexturedAssetViewModel
{
    private readonly ItemAsset _item;
    private int _maxStackSize;

    public ItemAssetViewModel(ItemAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _item = model;
        _maxStackSize = model.MaxStackSize;
    }

    public string StackSizeHint => $"Entre {ItemAsset.MinStackSize} et {ItemAsset.MaxStackSizeLimit} (64 pour la plupart des objets, 1 pour une arme).";

    public int MaxStackSize
    {
        get => _maxStackSize;
        set
        {
            if (!SetProperty(ref _maxStackSize, value))
            {
                return;
            }

            if (ItemAsset.IsValidStackSize(value))
            {
                ClearErrors(nameof(MaxStackSize));
                _item.MaxStackSize = value;
            }
            else
            {
                SetError(nameof(MaxStackSize), StackSizeHint);
            }
        }
    }
}
