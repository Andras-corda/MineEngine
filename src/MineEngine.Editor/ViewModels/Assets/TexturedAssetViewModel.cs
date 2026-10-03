using MineEngine.Assets;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Asset avec texture : ajoute l'emplacement de texture principal.</summary>
public abstract class TexturedAssetViewModel : AssetViewModel
{
    protected TexturedAssetViewModel(TexturedAsset model, AssetEditingContext context, string typeLabel, string emptyTextureLabel)
        : base(model, context, typeLabel)
    {
        MainTexture = new TextureSlotViewModel(
            model, context, nameof(TexturedAsset.TextureId), "Texture", emptyTextureLabel,
            () => model.TextureId, v => model.TextureId = v);
    }

    public TextureSlotViewModel MainTexture { get; }

    public override string? IconPath => MainTexture.FullPath;

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        MainTexture.Refresh();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName == nameof(TexturedAsset.TextureId))
        {
            MainTexture.Refresh();
            OnPropertyChanged(nameof(IconPath));
            OnPropertyChanged(nameof(HasIcon));
        }
    }
}
