using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Core.Assets;
using MineEngine.Editor.Services;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Crée le ViewModel d'Inspector adapté à chaque type d'asset.</summary>
public sealed class AssetViewModelFactory
{
    private readonly AssetCatalog _catalog;
    private readonly TextureImporter _textureImporter;
    private readonly IDialogService _dialogs;

    public AssetViewModelFactory(AssetCatalog catalog, TextureImporter textureImporter, IDialogService dialogs)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _textureImporter = textureImporter ?? throw new ArgumentNullException(nameof(textureImporter));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public AssetCatalog Catalog => _catalog;

    public AssetViewModel Create(Asset asset, ModProject project)
    {
        var context = new AssetEditingContext(project, _textureImporter, _dialogs);
        string label = _catalog.Get(asset.Type).Label;

        return asset switch
        {
            ItemAsset item => new ItemAssetViewModel(item, context, label),
            BlockAsset block => new BlockAssetViewModel(block, context, label),
            _ => throw new NotSupportedException($"L'éditeur ne sait pas encore afficher les assets de type {asset.Type}."),
        };
    }
}
