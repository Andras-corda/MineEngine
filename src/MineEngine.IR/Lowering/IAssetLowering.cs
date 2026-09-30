using MineEngine.Core.Assets;

namespace MineEngine.IR.Lowering;

/// <summary>
/// Traduit un type d'asset en éléments du Mod IR. Chaque type d'asset a sa
/// propre traduction ; ajouter un type revient à ajouter une implémentation.
/// </summary>
public interface IAssetLowering
{
    AssetType AssetType { get; }

    void Lower(Asset asset, LoweringContext context);
}

/// <summary>Base typée : vérifie le type de l'asset avant de le traduire.</summary>
public abstract class AssetLowering<TAsset> : IAssetLowering
    where TAsset : Asset
{
    public abstract AssetType AssetType { get; }

    public void Lower(Asset asset, LoweringContext context)
    {
        if (asset is not TAsset typed)
        {
            throw new ArgumentException($"{asset} ne peut pas être traduit par {GetType().Name}.", nameof(asset));
        }

        Lower(typed, context);
    }

    protected abstract void Lower(TAsset asset, LoweringContext context);
}
