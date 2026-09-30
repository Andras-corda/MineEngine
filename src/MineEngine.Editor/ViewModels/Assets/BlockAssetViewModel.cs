using System.Runtime.CompilerServices;
using MineEngine.Assets;

namespace MineEngine.Editor.ViewModels.Assets;

public sealed class BlockAssetViewModel : TexturedAssetViewModel
{
    private readonly BlockAsset _block;
    private float _hardness;
    private float _resistance;

    public BlockAssetViewModel(BlockAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _block = model;
        _hardness = model.Hardness;
        _resistance = model.Resistance;
    }

    public string HardnessHint => "Temps pour casser le bloc. Terre : 0.5, pierre : 1.5, minerai : 3.";

    public string ResistanceHint => "Résistance aux explosions. Pierre : 6, obsidienne : 1200.";

    public float Hardness
    {
        get => _hardness;
        set => UpdateStrength(ref _hardness, value, v => _block.Hardness = v);
    }

    public float Resistance
    {
        get => _resistance;
        set => UpdateStrength(ref _resistance, value, v => _block.Resistance = v);
    }

    private void UpdateStrength(ref float field, float value, Action<float> apply, [CallerMemberName] string propertyName = "")
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return;
        }

        if (BlockAsset.IsValidStrength(value))
        {
            ClearErrors(propertyName);
            apply(value);
        }
        else
        {
            SetError(propertyName, $"La valeur doit être comprise entre 0 et {BlockAsset.MaxStrength}.");
        }
    }
}
