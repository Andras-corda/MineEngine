using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.IR.Model;
using MineEngine.Project;

namespace MineEngine.IR.Lowering;

/// <summary>
/// Les textures ne produisent rien par elles-mêmes : chaque item, bloc ou mob copie
/// celles qu'il utilise. Une texture inutilisée n'est donc pas incluse dans le mod.
/// </summary>
public sealed class TextureLowering : AssetLowering<TextureAsset>
{
    public override AssetType AssetType => AssetType.Texture;

    protected override void Lower(TextureAsset asset, LoweringContext context)
    {
    }
}

/// <summary>Un son devient un événement sonore du mod, avec son fichier OGG.</summary>
public sealed class SoundLowering : AssetLowering<SoundAsset>
{
    public override AssetType AssetType => AssetType.Sound;

    protected override void Lower(SoundAsset asset, LoweringContext context)
    {
        string? file = AssetFileImporter.GetExistingFile(context.Project, asset);
        if (file is null)
        {
            context.Diagnostics.Error($"Le fichier du son ({asset.FilePath}) est introuvable.", asset.ToString(), asset.Id);
            return;
        }

        context.AddSound(new IRSound(asset.Id, asset.ResourceId, file, asset.Subtitle));
    }
}
