using MineEngine.Core.Assets;

namespace MineEngine.Editor.Services;

/// <summary>Demande d'ouverture d'un asset dans un onglet, en montrant éventuellement un nœud.</summary>
public sealed class AssetNavigationEventArgs : EventArgs
{
    public AssetNavigationEventArgs(Asset asset, Guid? nodeId)
    {
        Asset = asset;
        NodeId = nodeId;
    }

    public Asset Asset { get; }

    /// <summary>Nœud à montrer dans un graphe, ou null.</summary>
    public Guid? NodeId { get; }
}

/// <summary>
/// Permet à un ViewModel d'asset de demander l'ouverture d'un autre asset dans un onglet
/// (par exemple le graphe relié à un événement d'item) sans connaître la fenêtre principale.
/// </summary>
public sealed class EditorNavigation
{
    public event EventHandler<AssetNavigationEventArgs>? OpenRequested;

    public void Open(Asset asset, Guid? nodeId = null)
    {
        ArgumentNullException.ThrowIfNull(asset);
        OpenRequested?.Invoke(this, new AssetNavigationEventArgs(asset, nodeId));
    }
}
