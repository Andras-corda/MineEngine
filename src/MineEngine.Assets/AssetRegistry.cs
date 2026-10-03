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

/// <summary>
/// Ensemble des assets d'un projet. Garantit l'unicité des identifiants dans chaque
/// espace (<see cref="AssetIdScope"/>) et connaît les liens entre assets.
/// </summary>
public sealed class AssetRegistry : IReadOnlyCollection<Asset>
{
    private readonly List<Asset> _assets = [];

    public event EventHandler<AssetRegistryChangedEventArgs>? Changed;

    public int Count => _assets.Count;

    public void Add(Asset asset) => Insert(_assets.Count, asset);

    /// <summary>Ajoute un asset à une position donnée de la liste (utilisé pour annuler une suppression).</summary>
    public void Insert(int index, Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (Find(asset.Id) is not null)
        {
            throw new InvalidOperationException($"L'asset {asset} est déjà présent dans le projet.");
        }

        if (!IsResourceIdAvailable(asset.ResourceId, asset.Type))
        {
            throw new InvalidOperationException($"L'identifiant '{asset.ResourceId}' est déjà utilisé.");
        }

        _assets.Insert(Math.Min(index, _assets.Count), asset);
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

    public int IndexOf(Asset asset) => _assets.IndexOf(asset);

    public Asset? Find(Guid id) => _assets.Find(a => a.Id == id);

    /// <summary>Asset du même espace d'identifiants que <paramref name="type"/> qui porte cet identifiant.</summary>
    public Asset? FindByResourceId(ResourceId resourceId, AssetType type) =>
        _assets.Find(a => a.ResourceId == resourceId && a.Type.IdScope() == type.IdScope());

    public IEnumerable<TAsset> OfType<TAsset>() where TAsset : Asset => _assets.OfType<TAsset>();

    /// <summary>
    /// Indique si l'identifiant est libre pour un asset du type donné (en ignorant
    /// éventuellement un asset, celui qu'on renomme).
    /// </summary>
    public bool IsResourceIdAvailable(ResourceId resourceId, AssetType type, Asset? ignoredAsset = null) =>
        !_assets.Exists(a => a.ResourceId == resourceId
                             && a.Type.IdScope() == type.IdScope()
                             && !ReferenceEquals(a, ignoredAsset));

    /// <summary>Construit un identifiant libre à partir d'un texte ("new_item", "new_item_2"...).</summary>
    public ResourceId CreateUniqueResourceId(string baseText, AssetType type)
    {
        string stem = ResourceId.FromText(baseText).Value;
        ResourceId candidate = ResourceId.Parse(stem);
        for (int suffix = 2; !IsResourceIdAvailable(candidate, type); suffix++)
        {
            string suffixText = "_" + suffix;
            int stemLength = Math.Min(stem.Length, ResourceId.MaxLength - suffixText.Length);
            candidate = ResourceId.Parse(stem[..stemLength] + suffixText);
        }

        return candidate;
    }

    /// <summary>Assets qui font référence à l'asset donné, avec le rôle de chaque lien.</summary>
    public IReadOnlyList<(Asset Source, AssetReference Reference)> FindReferencesTo(Guid targetId) =>
        _assets.SelectMany(a => a.GetReferences().Where(r => r.TargetId == targetId).Select(r => (a, r))).ToList();

    public void Validate(DiagnosticBag diagnostics)
    {
        foreach (IGrouping<(AssetIdScope, ResourceId), Asset> group in _assets
                     .GroupBy(a => (a.Type.IdScope(), a.ResourceId))
                     .Where(g => g.Count() > 1))
        {
            ResourceId id = group.Key.Item2;
            diagnostics.Error($"L'identifiant '{id}' est utilisé par {group.Count()} assets.", id.Value, group.First().Id);
        }

        ValidateSpawnEggIds(diagnostics);

        foreach (Asset asset in _assets)
        {
            asset.Validate(diagnostics);
            ValidateReferences(asset, diagnostics);
        }
    }

    /// <summary>Vérifie qu'une référence vise un asset existant du bon type.</summary>
    public string? DescribeReferenceProblem(AssetReference reference)
    {
        Asset? target = Find(reference.TargetId);
        if (target is null)
        {
            return $"{Capitalize(reference.Role)} : l'asset visé a été supprimé.";
        }

        return reference.Kind switch
        {
            AssetReferenceKind.Texture when target is not TextureAsset =>
                $"{Capitalize(reference.Role)} : {target} n'est pas une texture.",
            AssetReferenceKind.Sound when target is not SoundAsset =>
                $"{Capitalize(reference.Role)} : {target} n'est pas un son.",
            AssetReferenceKind.Item when target is BlockAsset { HasItemForm: false } =>
                $"{Capitalize(reference.Role)} : le bloc {target.ResourceId} n'a pas de forme item.",
            AssetReferenceKind.Item when target is not (ItemAsset or BlockAsset) =>
                $"{Capitalize(reference.Role)} : {target} n'est pas un item.",
            _ => null,
        };
    }

    private void ValidateReferences(Asset asset, DiagnosticBag diagnostics)
    {
        foreach (AssetReference reference in asset.GetReferences())
        {
            if (DescribeReferenceProblem(reference) is not { } problem)
            {
                continue;
            }

            // Une texture manquante est remplacée à la génération ; le reste empêche de générer.
            if (reference.Kind == AssetReferenceKind.Texture)
            {
                diagnostics.Warning(problem + " Une texture de remplacement sera utilisée.", asset.ToString(), asset.Id);
            }
            else
            {
                diagnostics.Error(problem, asset.ToString(), asset.Id);
            }
        }
    }

    /// <summary>L'œuf d'un mob ("goblin_spawn_egg") est un item : il ne doit pas en écraser un autre.</summary>
    private void ValidateSpawnEggIds(DiagnosticBag diagnostics)
    {
        foreach (MobAsset mob in _assets.OfType<MobAsset>().Where(m => m.HasSpawnEgg))
        {
            if (_assets.Exists(a => a.Type.IdScope() == AssetIdScope.GameContent && a.ResourceId.Value == mob.SpawnEggId))
            {
                diagnostics.Error($"L'œuf d'apparition '{mob.SpawnEggId}' porte le même identifiant qu'un autre asset.", mob.ToString(), mob.Id);
            }
        }
    }

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

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
