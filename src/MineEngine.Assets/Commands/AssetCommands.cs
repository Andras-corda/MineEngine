using MineEngine.Core.Assets;
using MineEngine.Core.Commands;

namespace MineEngine.Assets.Commands;

/// <summary>
/// Add asset to the registry & undo removes it from the registry.
/// </summary>
public sealed class AddAssetCommand : IUndoableCommand
{
    private readonly AssetRegistry _registry; // objet représentant le registre des assets, où les assets sont ajoutés et supprimés
    private readonly Asset _asset; // objet représentant l'asset à ajouter ou supprimer du registre

    /// <summary>
    /// Initializes a new instance of the AddAssetCommand class, which adds an asset to the registry and allows undoing the action by removing it from the registry.
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="asset"></param>
    /// <param name="typeLabel"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public AddAssetCommand(AssetRegistry registry, Asset asset, string typeLabel)
    {
        if (registry == null)
            throw new ArgumentNullException(nameof(registry));

        if (asset == null)
            throw new ArgumentNullException(nameof(asset));

        _registry = registry;
        _asset = asset;

        Description = Tr.F("Add {0} '{1}'", typeLabel.ToLowerInvariant(), asset.ResourceId);
    }

    public string Description { get; } // decription de la commande, affichée dans l'interface utilisateur pour indiquer l'action effectuée

    public void Execute() => _registry.Add(_asset); // exécute la commande en ajoutant l'asset au registre

    public void Undo() => _registry.Remove(_asset); // annule la commande en supprimant l'asset du registre

    public bool TryMerge(IUndoableCommand next) => false; // indique que cette commande ne peut pas être fusionnée avec une autre commande pour créer une seule commande composite
}

/// <summary>
/// Remove asset from the registry & undo adds it back to the registry.
/// </summary>
public sealed class RemoveAssetCommand : IUndoableCommand
{
    private readonly AssetRegistry _registry; // objet représentant le registre des assets, où les assets sont ajoutés et supprimés
    private readonly Asset _asset; // objet représentant l'asset à supprimer du registre
    private int _index = -1; // index de l'asset dans le registre avant sa suppression, utilisé pour restaurer l'asset à sa position d'origine lors de l'annulation de la commande

    /// <summary>
    /// Initializes a new instance of the RemoveAssetCommand class, which removes an asset from the registry and allows undoing the action by adding it back to the registry.
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="asset"></param>
    /// <param name="typeLabel"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public RemoveAssetCommand(AssetRegistry registry, Asset asset, string typeLabel)
    {
        if (registry == null)
            throw new ArgumentNullException(nameof(registry));

        if (asset == null)
            throw new ArgumentNullException(nameof(asset));

        _registry = registry;
        _asset = asset;

        Description = Tr.F("Delete {0} '{1}'", typeLabel.ToLowerInvariant(), asset.ResourceId);
    }

    public string Description { get; } // description de la commande, affichée dans l'interface utilisateur pour indiquer l'action effectuée

    /// <summary>
    /// Executes the command by removing the asset from the registry and storing its index for potential undo.
    /// </summary>
    public void Execute()
    {
        _index = _registry.IndexOf(_asset);
        _registry.Remove(_asset);
    }

    public void Undo() => _registry.Insert(Math.Max(_index, 0), _asset); // procédure pour annuler la commande en réinsérant l'asset à son index d'origine dans le registre, ou à l'index 0 si l'index d'origine est invalide

    public bool TryMerge(IUndoableCommand next) => false; // procédure pour indiquer que cette commande ne peut pas être fusionnée avec une autre commande pour créer une seule commande composite
}
