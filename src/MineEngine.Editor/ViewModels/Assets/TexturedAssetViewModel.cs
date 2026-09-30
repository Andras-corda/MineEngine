using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Asset avec texture : ajoute le choix et l'aperçu de la texture.</summary>
public abstract class TexturedAssetViewModel : AssetViewModel
{
    private readonly TexturedAsset _texturedModel;

    protected TexturedAssetViewModel(TexturedAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _texturedModel = model;
        ChooseTextureCommand = new RelayCommand(ChooseTexture);
        ClearTextureCommand = new RelayCommand(ClearTexture, () => _texturedModel.HasTexture);
    }

    public ICommand ChooseTextureCommand { get; }

    public ICommand ClearTextureCommand { get; }

    public string TextureLabel => _texturedModel.TexturePath ?? "Aucune (texture de remplacement)";

    /// <summary>Chemin absolu de la texture pour l'aperçu, ou null.</summary>
    public string? TextureFullPath
    {
        get
        {
            string? path = Context.TextureImporter.GetAbsolutePath(Context.Project, _texturedModel);
            return path is not null && File.Exists(path) ? path : null;
        }
    }

    private void ChooseTexture()
    {
        string? file = Context.Dialogs.AskTextureFile();
        if (file is null)
        {
            return;
        }

        try
        {
            Context.TextureImporter.Import(Context.Project, _texturedModel, file);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Context.Dialogs.ShowError("Texture", exception.Message);
            return;
        }

        RaiseTextureChanged();
    }

    private void ClearTexture()
    {
        _texturedModel.TexturePath = null;
        RaiseTextureChanged();
    }

    private void RaiseTextureChanged()
    {
        OnPropertyChanged(nameof(TextureLabel));
        OnPropertyChanged(nameof(TextureFullPath));
    }
}
