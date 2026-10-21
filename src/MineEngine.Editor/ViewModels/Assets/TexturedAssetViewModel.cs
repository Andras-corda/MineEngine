using MineEngine.Assets;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Asset avec texture : ajoute l'emplacement de texture principal.</summary>
public abstract class TexturedAssetViewModel : AssetViewModel
{
    protected TexturedAssetViewModel(TexturedAsset model, AssetEditingContext context, string typeLabel, string emptyTextureLabel)
        : base(model, context, typeLabel)
    {
        MainTexture = new TextureSlotViewModel(
            model, context, nameof(TexturedAsset.TextureId), Tr.T("Texture"), emptyTextureLabel,
            () => model.TextureId, v => model.TextureId = v);
        CustomModel = new ModelPickerViewModel(model, context, entityModel: model is MobAsset);
    }

    public TextureSlotViewModel MainTexture { get; }

    /// <summary>3D model shown instead of the generated shape (Blockbench or Java JSON; Blockbench for a mob).</summary>
    public ModelPickerViewModel CustomModel { get; }

    public bool UsesCustomModel => ((TexturedAsset)Model).UsesCustomModel;

    /// <summary>True when the choice of the 3D model is shown (always for an item; with the 3D model shape for a block or a mob).</summary>
    public virtual bool ShowsModelPicker => true;

    public override string? IconPath => MainTexture.FullPath;

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        MainTexture.Refresh();
        CustomModel.Refresh();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName is nameof(TexturedAsset.CustomModelId) or "Model")
        {
            CustomModel.Refresh();
            OnPropertyChanged(nameof(UsesCustomModel));
            OnPropertyChanged(nameof(ShowsModelPicker));
        }

        if (propertyName == nameof(TexturedAsset.TextureId))
        {
            MainTexture.Refresh();
            OnPropertyChanged(nameof(IconPath));
            OnPropertyChanged(nameof(HasIcon));
        }
    }
}
