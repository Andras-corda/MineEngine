using System.Collections;
using MineEngine.Assets.Graphs;
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
/// <remarks>
/// Intermediate results are cached so that a change does not recompute the whole project:
/// an index by identifier, the index of the references to each asset (rebuilt once after a
/// change instead of once per asset that asks), the program model of the graphs (rebuilt only
/// when a graph is added or removed: it reads the graphs live), and the validation result of
/// each asset, kept until the asset itself changes (or, for a graph, until a definition of the
/// program changes; for a behavior tree, until another tree changes).
/// </remarks>
public sealed class AssetRegistry : IReadOnlyCollection<Asset>
{
    private readonly List<Asset> _assets = [];
    private readonly Dictionary<Guid, Asset> _byId = [];
    private readonly Dictionary<Guid, int> _revisions = [];
    private readonly Dictionary<Guid, CachedValidation> _validations = [];
    private ILookup<Guid, (Asset Source, AssetReference Reference)>? _referencesTo;
    private ProgramModel? _program;
    private int _definitionsVersion;
    private int _treesVersion;

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
            throw new InvalidOperationException(Tr.F("Asset {0} is already in the project.", asset));
        }

        if (!IsResourceIdAvailable(asset.ResourceId, asset.Type))
        {
            throw new InvalidOperationException(Tr.F("The identifier '{0}' is already in use.", asset.ResourceId));
        }

        _assets.Insert(Math.Min(index, _assets.Count), asset);
        _byId[asset.Id] = asset;
        _revisions[asset.Id] = 0;
        OnStructureChanged(asset);
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

        _byId.Remove(asset.Id);
        _revisions.Remove(asset.Id);
        _validations.Remove(asset.Id);
        OnStructureChanged(asset);
        asset.Changed -= OnAssetChanged;
        Changed?.Invoke(this, new AssetRegistryChangedEventArgs(AssetRegistryChange.Removed, asset));
        return true;
    }

    public int IndexOf(Asset asset) => _assets.IndexOf(asset);

    public Asset? Find(Guid id) => _byId.GetValueOrDefault(id);

    /// <summary>Program definitions of the graphs (functions, classes...), shared and kept until a graph is added or removed.</summary>
    public ProgramModel Program => _program ??= new ProgramModel(_assets.OfType<GraphAsset>());

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
    public IReadOnlyList<(Asset Source, AssetReference Reference)> FindReferencesTo(Guid targetId)
    {
        _referencesTo ??= _assets.SelectMany(a => a.GetReferences().Select(r => (Source: a, Reference: r))).ToLookup(p => p.Reference.TargetId);
        return [.. _referencesTo[targetId]];
    }

    public void Validate(DiagnosticBag diagnostics)
    {
        foreach (IGrouping<(AssetIdScope, ResourceId), Asset> group in _assets
                     .GroupBy(a => (a.Type.IdScope(), a.ResourceId))
                     .Where(g => g.Count() > 1))
        {
            ResourceId id = group.Key.Item2;
            diagnostics.Error(Tr.F("The identifier '{0}' is used by {1} assets.", id, group.Count()), id.Value, group.First().Id);
        }

        ValidateSpawnEggIds(diagnostics);
        ValidateGameplayIds(diagnostics);

        // Les graphes se vérifient avec toutes les définitions du projet (fonctions appelées, types...).
        foreach (Asset asset in _assets)
        {
            diagnostics.AddRange(ValidateAsset(asset));
            ValidateReferences(asset, diagnostics);
        }
    }

    /// <summary>Problems of one asset, from the cache when nothing it depends on has changed.</summary>
    private IReadOnlyList<Diagnostic> ValidateAsset(Asset asset)
    {
        int revision = _revisions.GetValueOrDefault(asset.Id);
        int dependencies = asset switch
        {
            GraphAsset => _definitionsVersion,
            BehaviorTreeAsset => _treesVersion,
            _ => 0,
        };
        if (_validations.TryGetValue(asset.Id, out CachedValidation? cached) && cached.Revision == revision && cached.Dependencies == dependencies)
        {
            return cached.Diagnostics;
        }

        var bag = new DiagnosticBag();
        switch (asset)
        {
            case GraphAsset graph:
                graph.Validate(bag, Program);
                break;
            case BehaviorTreeAsset tree:
                tree.Validate(bag, id => Find(id) as BehaviorTreeAsset);
                break;
            default:
                asset.Validate(bag);
                break;
        }

        IReadOnlyList<Diagnostic> result = [.. bag];
        _validations[asset.Id] = new CachedValidation(revision, dependencies, result);
        return result;
    }

    /// <summary>An asset was added or removed: indexes and the caches that depend on the set of assets are refreshed.</summary>
    private void OnStructureChanged(Asset asset)
    {
        _referencesTo = null;
        if (asset is GraphAsset)
        {
            _program = null;
            _definitionsVersion++;
        }
        else if (asset is BehaviorTreeAsset)
        {
            _treesVersion++;
        }
    }

    /// <summary>State of the caches, for the debug mode: assets, revisions, validations kept, indexes built.</summary>
    public string DebugSummary() =>
        Tr.F("Registry: {0} assets, {1} validations cached, reference index {2}, program model {3}, versions {4}/{5}",
            _assets.Count, _validations.Count, _referencesTo is null ? Tr.T("to rebuild") : Tr.T("ready"),
            _program is null ? Tr.T("to rebuild") : Tr.T("ready"), _definitionsVersion, _treesVersion);

    private sealed record CachedValidation(int Revision, int Dependencies, IReadOnlyList<Diagnostic> Diagnostics);

    /// <summary>Vérifie qu'une référence vise un asset existant du bon type.</summary>
    public string? DescribeReferenceProblem(AssetReference reference)
    {
        Asset? target = Find(reference.TargetId);
        if (target is null)
        {
            return Tr.F("{0}: the target asset was deleted.", Capitalize(reference.Role));
        }

        return reference.Kind switch
        {
            AssetReferenceKind.Texture when target is not TextureAsset =>
                Tr.F("{0}: {1} is not a texture.", Capitalize(reference.Role), target),
            AssetReferenceKind.Sound when target is not SoundAsset =>
                Tr.F("{0}: {1} is not a sound.", Capitalize(reference.Role), target),
            AssetReferenceKind.Block when target is not BlockAsset =>
                Tr.F("{0}: {1} is not a block.", Capitalize(reference.Role), target),
            AssetReferenceKind.Item when target is BlockAsset { HasItemForm: false } =>
                Tr.F("{0}: block {1} has no item form.", Capitalize(reference.Role), target.ResourceId),
            AssetReferenceKind.Definition when target is not GraphAsset =>
                Tr.F("{0}: {1} is not a definition.", Capitalize(reference.Role), target),
            AssetReferenceKind.BehaviorTree when target is not BehaviorTreeAsset =>
                Tr.F("{0}: {1} is not a behavior tree.", Capitalize(reference.Role), target),
            AssetReferenceKind.Model or AssetReferenceKind.EntityModel when target is not ModelAsset =>
                Tr.F("{0}: {1} is not a 3D model.", Capitalize(reference.Role), target),
            AssetReferenceKind.Model when target is ModelAsset { IsUsedByTheGame: false } =>
                Tr.F("{0}: {1} cannot be used by the game (only Blockbench and Minecraft Java JSON models can).", Capitalize(reference.Role), target),
            AssetReferenceKind.EntityModel when target is ModelAsset { Format: not ModelFormat.Blockbench } =>
                Tr.F("{0}: a mob needs a Blockbench model (.bbmodel), {1} is not one.", Capitalize(reference.Role), target),
            AssetReferenceKind.Item when target is not (ItemAsset or BlockAsset) =>
                Tr.F("{0}: {1} is not an item.", Capitalize(reference.Role), target),
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
                diagnostics.Warning(problem + Tr.T(" A placeholder texture will be used."), asset.ToString(), asset.Id);
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
                diagnostics.Error(Tr.F("The spawn egg '{0}' has the same identifier as another asset.", mob.SpawnEggId), mob.ToString(), mob.Id);
            }
        }
    }

    /// <summary>The bucket of a fluid is an item ("mana_bucket"); the tab "main" is the tab of the mod.</summary>
    private void ValidateGameplayIds(DiagnosticBag diagnostics)
    {
        foreach (FluidAsset fluid in _assets.OfType<FluidAsset>().Where(f => f.HasBucket))
        {
            if (_assets.Exists(a => a.Type.IdScope() == AssetIdScope.GameContent && a.ResourceId.Value == fluid.BucketId))
            {
                diagnostics.Error(Tr.F("The bucket '{0}' has the same identifier as another asset.", fluid.BucketId), fluid.ToString(), fluid.Id);
            }
        }

        foreach (CreativeTabAsset tab in _assets.OfType<CreativeTabAsset>().Where(t => t.ResourceId.Value == "main"))
        {
            diagnostics.Error(Tr.T("The identifier 'main' is the tab of the mod: choose another one."), tab.ToString(), tab.Id);
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
            _revisions[asset.Id] = _revisions.GetValueOrDefault(asset.Id) + 1;
            _referencesTo = null;
            switch (asset)
            {
                // The body of a graph only matters to itself; its signature, variables or name matter to the other graphs.
                case GraphAsset when e.PropertyName != nameof(GraphAsset.Graph):
                    _definitionsVersion++;
                    break;
                case BehaviorTreeAsset:
                    _treesVersion++;
                    break;
            }

            Changed?.Invoke(this, new AssetRegistryChangedEventArgs(AssetRegistryChange.Modified, asset));
        }
    }
}
