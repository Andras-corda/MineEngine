using MineEngine.Editor.Services;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Services dont un ViewModel d'asset a besoin pour éditer son asset.</summary>
public sealed class AssetEditingContext
{
    public AssetEditingContext(ModProject project, TextureImporter textureImporter, IDialogService dialogs)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        TextureImporter = textureImporter ?? throw new ArgumentNullException(nameof(textureImporter));
        Dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public ModProject Project { get; }

    public TextureImporter TextureImporter { get; }

    public IDialogService Dialogs { get; }
}
