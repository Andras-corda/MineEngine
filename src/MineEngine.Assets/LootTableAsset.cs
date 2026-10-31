using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Drop table of the mod (V0.7.10, like the loot tables of the game): pools drawn one after the other,
/// each a number of rolls among weighted entries. An entry gives an item (or an item of a tag), with its
/// count, its conditions (chance, killed by a player, silk touch...) and its functions (looting, smelted
/// when on fire, enchanted). Blocks, mobs and graphs use it (step 3).
/// </summary>
public sealed class LootTableAsset : Asset
{
    private LootTableKind _kind = LootTableKind.Generic;
    private IReadOnlyList<LootPool> _pools = [new LootPool()];

    public LootTableAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.LootTable;

    /// <summary>What the table is for: it sets which conditions the game can check.</summary>
    public LootTableKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    public IReadOnlyList<LootPool> Pools
    {
        get => _pools;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.SequenceEqual(_pools))
            {
                _pools = [.. value];
                RaiseChanged(nameof(Pools));
            }
        }
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (LootEntry entry in Pools.SelectMany(p => p.Entries))
        {
            if (entry is { Kind: LootEntryKind.Item, Item: { Kind: ContentReferenceKind.Asset } item })
            {
                yield return new AssetReference(item.AssetId, AssetReferenceKind.Item, Tr.T("drop table entry"));
            }
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();
        if (Pools.Count == 0 || Pools.All(p => p.Entries.Count == 0))
        {
            diagnostics.Warning(Tr.T("The drop table has no entry: it gives nothing."), source, Id);
        }

        if (Pools.SelectMany(p => p.Entries).Any(e => e is { Kind: LootEntryKind.Item, Item: null }))
        {
            diagnostics.Error(Tr.T("An entry of the drop table has no item."), source, Id);
        }

        IEnumerable<LootEntry> entries = Pools.SelectMany(p => p.Entries);
        if (Kind != LootTableKind.Entity && entries.Any(e => e.KilledByPlayer || e.LootingBonus > 0 || e.SmeltIfOnFire))
        {
            diagnostics.Info(Tr.T("\"Killed by a player\", looting and \"smelted when on fire\" only work in a drop table of a mob."), source, Id);
        }

        if (Kind != LootTableKind.Block && entries.Any(e => e.SilkTouch != LootSilkTouch.Any || e.SurvivesExplosion))
        {
            diagnostics.Info(Tr.T("Silk touch and \"survives the explosion\" only work in a drop table of a block."), source, Id);
        }
    }
}

/// <summary>A pool of a drop table: rolled a number of times (between two values), each roll draws one entry by weight.</summary>
public sealed record LootPool
{
    public const int MaxRolls = 64;

    private readonly int _rollsMin = 1;
    private readonly int _rollsMax = 1;
    private readonly float _bonusRolls;
    private readonly IReadOnlyList<LootEntry> _entries = [];

    public int RollsMin
    {
        get => _rollsMin;
        init => _rollsMin = Math.Clamp(value, 0, MaxRolls);
    }

    public int RollsMax
    {
        get => Math.Max(_rollsMax, _rollsMin);
        init => _rollsMax = Math.Clamp(value, 0, MaxRolls);
    }

    /// <summary>Rolls added per level of luck of the player (fishing, chests).</summary>
    public float BonusRolls
    {
        get => _bonusRolls;
        init => _bonusRolls = float.IsFinite(value) ? Math.Clamp(value, 0f, MaxRolls) : 0f;
    }

    public IReadOnlyList<LootEntry> Entries
    {
        get => _entries;
        init => _entries = [.. value ?? []];
    }

    public bool Equals(LootPool? other) =>
        other is not null && RollsMin == other.RollsMin && RollsMax == other.RollsMax && BonusRolls == other.BonusRolls && Entries.SequenceEqual(other.Entries);

    public override int GetHashCode() => HashCode.Combine(RollsMin, RollsMax, BonusRolls, Entries.Count);
}

/// <summary>
/// An entry of a pool: an item (or an item of a tag) or nothing, its weight among the entries, its count,
/// and what the game checks or does: chance, killed by a player, silk touch, survives an explosion,
/// more with looting, smelted when the mob burns, enchanted at random.
/// </summary>
public sealed record LootEntry
{
    public const int MaxCount = 64;
    public const int MaxWeight = 1000;

    private readonly int _weight = 1;
    private readonly int _countMin = 1;
    private readonly int _countMax = 1;
    private readonly int _chance = 100;
    private readonly int _lootingBonus;

    public LootEntryKind Kind { get; init; }

    /// <summary>The item, or a tag (one of its items at random); null for an empty entry.</summary>
    public ContentReference? Item { get; init; }

    public int Weight
    {
        get => _weight;
        init => _weight = Math.Clamp(value, 1, MaxWeight);
    }

    public int CountMin
    {
        get => _countMin;
        init => _countMin = Math.Clamp(value, 0, MaxCount);
    }

    public int CountMax
    {
        get => Math.Max(_countMax, _countMin);
        init => _countMax = Math.Clamp(value, 0, MaxCount);
    }

    /// <summary>Chance, in percent, that the entry gives something when drawn (100: always).</summary>
    public int Chance
    {
        get => _chance;
        init => _chance = Math.Clamp(value, 0, 100);
    }

    public bool KilledByPlayer { get; init; }

    public LootSilkTouch SilkTouch { get; init; }

    public bool SurvivesExplosion { get; init; }

    /// <summary>Up to this many more per level of Looting (0: none).</summary>
    public int LootingBonus
    {
        get => _lootingBonus;
        init => _lootingBonus = Math.Clamp(value, 0, MaxCount);
    }

    public bool SmeltIfOnFire { get; init; }

    public bool EnchantRandomly { get; init; }
}
