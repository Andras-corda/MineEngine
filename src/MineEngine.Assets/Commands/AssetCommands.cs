using MineEngine.Core.Assets;
using MineEngine.Core.Commands;

namespace MineEngine.Assets.Commands;

/// <summary>Ajout d'un asset au projet.</summary>
public sealed class AddAssetCommand : IUndoableCommand
{
    private readonly AssetRegistry _registry;
    private readonly Asset _asset;

    public AddAssetCommand(AssetRegistry registry, Asset asset, string typeLabel)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        Description = $"Ajouter {typeLabel.ToLowerInvariant()} '{asset.ResourceId}'";
    }

    public string Description { get; }

    public void Execute() => _registry.Add(_asset);

    public void Undo() => _registry.Remove(_asset);

    public bool TryMerge(IUndoableCommand next) => false;
}

/// <summary>Suppression d'un asset ; l'annulation le remet à sa place dans la liste.</summary>
public sealed class RemoveAssetCommand : IUndoableCommand
{
    private readonly AssetRegistry _registry;
    private readonly Asset _asset;
    private int _index = -1;

    public RemoveAssetCommand(AssetRegistry registry, Asset asset, string typeLabel)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        Description = $"Supprimer {typeLabel.ToLowerInvariant()} '{asset.ResourceId}'";
    }

    public string Description { get; }

    public void Execute()
    {
        _index = _registry.IndexOf(_asset);
        _registry.Remove(_asset);
    }

    public void Undo() => _registry.Insert(Math.Max(_index, 0), _asset);

    public bool TryMerge(IUndoableCommand next) => false;
}
