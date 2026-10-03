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
    private readonly AssetFileImporter _importer;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;

    public AssetViewModelFactory(AssetCatalog catalog, AssetFileImporter importer, IDialogService dialogs, IShellService shell)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    public AssetCatalog Catalog => _catalog;

    public AssetFileImporter Importer => _importer;

    /// <param name="selectAsset">Sélection d'un autre asset du projet (navigation depuis l'Inspector).</param>
    public AssetViewModel Create(Asset asset, ModProject project, Func<Guid, bool> selectAsset)
    {
        var context = new AssetEditingContext(project, _importer, _dialogs, _shell, selectAsset);
        string label = _catalog.Get(asset.Type).Label;

        return asset switch
        {
            ItemAsset item => new ItemAssetViewModel(item, context, label),
            BlockAsset block => new BlockAssetViewModel(block, context, label),
            MobAsset mob => new MobAssetViewModel(mob, context, label),
            RecipeAsset recipe => new RecipeAssetViewModel(recipe, context, label),
            TextureAsset texture => new TextureAssetViewModel(texture, context, label),
            SoundAsset sound => new SoundAssetViewModel(sound, context, label),
            _ => throw new NotSupportedException($"L'éditeur ne sait pas encore afficher les assets de type {asset.Type}."),
        };
    }
}
