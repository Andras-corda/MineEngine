using System.Collections;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

public enum AssetRegistryChange
{
    Added,
    Removed,
    Modified,
}

public sealed class AssetRegistryChangedEventArgs : EventArgs
{
    public AssetRegistryChangedEventArgs(AssetRegistryChange change, Asset asset)
    {
        Change = change;
        Asset = asset;
    }

    public AssetRegistryChange Change { get; }

    public Asset Asset { get; }
}

/// <summary>Ensemble des assets d'un projet. Garantit l'unicité des identifiants.</summary>
public sealed class AssetRegistry : IReadOnlyCollection<Asset>
{
    private readonly List<Asset> _assets = [];

    public event EventHandler<AssetRegistryChangedEventArgs>? Changed;

    public int Count => _assets.Count;

    public void Add(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (Find(asset.Id) is not null)
        {
            throw new InvalidOperationException($"L'asset {asset} est déjà présent dans le projet.");
        }

        if (!IsResourceIdAvailable(asset.ResourceId))
        {
            throw new InvalidOperationException($"L'identifiant '{asset.ResourceId}' est déjà utilisé.");
        }

        _assets.Add(asset);
        asset.Changed += OnAssetChanged;
        Changed?.Invoke(this, new AssetRegistryChangedEventArgs(AssetRegistryChange.Added, asset));
    }

    public bool Remove(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (!_assets.Remove(asset))
        {
            return false;
        }

        asset.Changed -= OnAssetChanged;
        Changed?.Invoke(this, new AssetRegistryChangedEventArgs(AssetRegistryChange.Removed, asset));
        return true;
    }

    public Asset? Find(Guid id) => _assets.Find(a => a.Id == id);

    public Asset? FindByResourceId(ResourceId resourceId) => _assets.Find(a => a.ResourceId == resourceId);

    public IEnumerable<TAsset> OfType<TAsset>() where TAsset : Asset => _assets.OfType<TAsset>();

    /// <summary>Indique si l'identifiant est libre (en ignorant éventuellement un asset donné).</summary>
    public bool IsResourceIdAvailable(ResourceId resourceId, Asset? ignoredAsset = null) =>
        !_assets.Exists(a => a.ResourceId == resourceId && !ReferenceEquals(a, ignoredAsset));

    /// <summary>Construit un identifiant libre à partir d'un texte ("new_item", "new_item_2"...).</summary>
    public ResourceId CreateUniqueResourceId(string baseText)
    {
        string stem = ResourceId.FromText(baseText).Value;
        ResourceId candidate = ResourceId.Parse(stem);
        for (int suffix = 2; !IsResourceIdAvailable(candidate); suffix++)
        {
            string suffixText = "_" + suffix;
            int stemLength = Math.Min(stem.Length, ResourceId.MaxLength - suffixText.Length);
            candidate = ResourceId.Parse(stem[..stemLength] + suffixText);
        }

        return candidate;
    }

    public void Validate(DiagnosticBag diagnostics)
    {
        foreach (IGrouping<ResourceId, Asset> group in _assets.GroupBy(a => a.ResourceId).Where(g => g.Count() > 1))
        {
            diagnostics.Error($"L'identifiant '{group.Key}' est utilisé par {group.Count()} assets.");
        }

        foreach (Asset asset in _assets)
        {
            asset.Validate(diagnostics);
        }
    }

    public IEnumerator<Asset> GetEnumerator() => _assets.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void OnAssetChanged(object? sender, AssetChangedEventArgs e)
    {
        if (sender is Asset asset)
        {
            Changed?.Invoke(this, new AssetRegistryChangedEventArgs(AssetRegistryChange.Modified, asset));
        }
    }
}
