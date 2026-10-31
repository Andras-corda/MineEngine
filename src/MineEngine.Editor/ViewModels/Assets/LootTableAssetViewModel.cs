using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Drop table (V0.7.10): its kind, then a section per pool with its rolls and a card per entry. Every
/// change replaces the pools in one step that can be undone; the sections are rebuilt only when a pool
/// or an entry is added or removed, so that a number being typed keeps its box.
/// </summary>
public sealed class LootTableAssetViewModel : AssetViewModel
{
    private readonly LootTableAsset _table;

    public LootTableAssetViewModel(LootTableAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _table = model;
        AddPoolCommand = new RelayCommand(() =>
        {
            Change(Tr.T("Add a pool"), nameof(LootTableAsset.Pools), () => _table.Pools, v => _table.Pools = v, [.. _table.Pools, new LootPool()]);
            Context.History.Seal();
        });
        RebuildPools();
    }

    public IReadOnlyList<Choice<LootTableKind>> KindChoices => GameDataChoices.LootTableKinds;

    public LootTableKind Kind
    {
        get => _table.Kind;
        set => ChangeIfDifferent(Tr.T("Change the kind of drop table"), nameof(LootTableAsset.Kind), () => _table.Kind, v => _table.Kind = v, value);
    }

    /// <summary>Identifier other mods and commands use ("/loot spawn ~ ~ ~ loot testmod:gems").</summary>
    public string Usage => Tr.F("Used by blocks, mobs and graphs; in the game: /loot spawn ~ ~ ~ loot {0}", ModNamespace + ":" + _table.ResourceId.Value);

    public ObservableCollection<LootPoolViewModel> Pools { get; } = [];

    public ICommand AddPoolCommand { get; }

    internal LootPool GetPool(int pool) => _table.Pools[pool];

    internal void UpdatePool(int pool, string description, Func<LootPool, LootPool> update)
    {
        List<LootPool> pools = [.. _table.Pools];
        pools[pool] = update(pools[pool]);
        Change(description, nameof(LootTableAsset.Pools), () => _table.Pools, v => _table.Pools = v, pools);
    }

    internal void UpdateEntry(int pool, int entry, string description, Func<LootEntry, LootEntry> update) =>
        UpdatePool(pool, description, p =>
        {
            List<LootEntry> entries = [.. p.Entries];
            entries[entry] = update(entries[entry]);
            return p with { Entries = entries };
        });

    internal void AddEntry(int pool)
    {
        UpdatePool(pool, Tr.T("Add an entry"), p => p with { Entries = [.. p.Entries, new LootEntry()] });
        Context.History.Seal();
    }

    internal void RemoveEntry(int pool, int entry)
    {
        UpdatePool(pool, Tr.T("Remove an entry"), p => p with { Entries = [.. p.Entries.Where((_, i) => i != entry)] });
        Context.History.Seal();
    }

    internal void RemovePool(int pool)
    {
        Change(Tr.T("Remove a pool"), nameof(LootTableAsset.Pools), () => _table.Pools, v => _table.Pools = v, [.. _table.Pools.Where((_, i) => i != pool)]);
        Context.History.Seal();
    }

    internal AssetEditingContext EditingContext => Context;

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        foreach (LootEntryViewModel entry in Pools.SelectMany(p => p.Entries))
        {
            entry.ItemPicker.Refresh();
        }
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName == nameof(LootTableAsset.Pools))
        {
            bool same = Pools.Count == _table.Pools.Count && Pools.All(p => p.Entries.Count == _table.Pools[p.Index].Entries.Count);
            if (same)
            {
                foreach (LootPoolViewModel pool in Pools)
                {
                    pool.Refresh();
                }
            }
            else
            {
                RebuildPools();
            }
        }
        else if (propertyName == nameof(Asset.ResourceId))
        {
            OnPropertyChanged(nameof(Usage));
        }
    }

    private string ModNamespace => Context.Project.Settings.ModId.Value;

    private void RebuildPools()
    {
        Pools.Clear();
        for (int i = 0; i < _table.Pools.Count; i++)
        {
            Pools.Add(new LootPoolViewModel(this, i));
        }
    }
}

/// <summary>A pool of a drop table: its rolls and its entries.</summary>
public sealed class LootPoolViewModel : ObservableObject
{
    private readonly LootTableAssetViewModel _owner;

    public LootPoolViewModel(LootTableAssetViewModel owner, int index)
    {
        _owner = owner;
        Index = index;
        AddEntryCommand = new RelayCommand(() => _owner.AddEntry(Index));
        RemoveCommand = new RelayCommand(() => _owner.RemovePool(Index));
        Entries = [.. Enumerable.Range(0, Pool.Entries.Count).Select(i => new LootEntryViewModel(owner, Index, i))];
    }

    public int Index { get; }

    public string Title => Tr.F("Pool {0}", Index + 1);

    public IReadOnlyList<LootEntryViewModel> Entries { get; }

    public ICommand AddEntryCommand { get; }

    public ICommand RemoveCommand { get; }

    public int MaxRolls => LootPool.MaxRolls;

    public double RollsMin
    {
        get => Pool.RollsMin;
        set => Set(value, Pool.RollsMin, v => Pool with { RollsMin = v });
    }

    public double RollsMax
    {
        get => Pool.RollsMax;
        set => Set(value, Pool.RollsMax, v => Pool with { RollsMax = v });
    }

    public double BonusRolls
    {
        get => Math.Round(Pool.BonusRolls, 2);
        set
        {
            if (!double.IsNaN(value) && Math.Abs(value - Pool.BonusRolls) > 0.001)
            {
                _owner.UpdatePool(Index, Tr.T("Change the rolls"), p => p with { BonusRolls = (float)value });
            }
        }
    }

    private LootPool Pool => _owner.GetPool(Index);

    public void Refresh()
    {
        OnPropertyChanged(nameof(RollsMin));
        OnPropertyChanged(nameof(RollsMax));
        OnPropertyChanged(nameof(BonusRolls));
        foreach (LootEntryViewModel entry in Entries)
        {
            entry.Refresh();
        }
    }

    private void Set(double value, int current, Func<int, LootPool> update)
    {
        if (!double.IsNaN(value) && (int)Math.Round(value) != current)
        {
            _owner.UpdatePool(Index, Tr.T("Change the rolls"), _ => update((int)Math.Round(value)));
        }
    }
}

/// <summary>An entry of a pool: the item, its weight and count, its conditions and functions.</summary>
public sealed class LootEntryViewModel : ObservableObject
{
    private readonly LootTableAssetViewModel _owner;
    private readonly int _pool;

    public LootEntryViewModel(LootTableAssetViewModel owner, int pool, int index)
    {
        _owner = owner;
        _pool = pool;
        Index = index;
        RemoveCommand = new RelayCommand(() => _owner.RemoveEntry(_pool, Index));
        ItemPicker = new ReferencePickerViewModel(owner.EditingContext, ReferenceDomain.Item, () => Entry.Item,
            v => Update(Tr.T("Change the item of the entry"), e => e with { Item = v }));
    }

    public int Index { get; }

    public ICommand RemoveCommand { get; }

    /// <summary>The item, or a tag (one of its items at random).</summary>
    public ReferencePickerViewModel ItemPicker { get; }

    public IReadOnlyList<Choice<LootSilkTouch>> SilkTouchChoices => GameDataChoices.LootSilkTouches;

    public int MaxCount => LootEntry.MaxCount;

    public int MaxWeight => LootEntry.MaxWeight;

    /// <summary>Unchecked, the entry gives nothing (a weighted chance of nothing).</summary>
    public bool GivesItem
    {
        get => Entry.Kind == LootEntryKind.Item;
        set => Update(Tr.T("Change the kind of entry"), e => e with { Kind = value ? LootEntryKind.Item : LootEntryKind.Empty });
    }

    public double Weight
    {
        get => Entry.Weight;
        set => SetNumber(value, Entry.Weight, v => Entry with { Weight = v });
    }

    public double CountMin
    {
        get => Entry.CountMin;
        set => SetNumber(value, Entry.CountMin, v => Entry with { CountMin = v });
    }

    public double CountMax
    {
        get => Entry.CountMax;
        set => SetNumber(value, Entry.CountMax, v => Entry with { CountMax = v });
    }

    public double Chance
    {
        get => Entry.Chance;
        set => SetNumber(value, Entry.Chance, v => Entry with { Chance = v });
    }

    public double LootingBonus
    {
        get => Entry.LootingBonus;
        set => SetNumber(value, Entry.LootingBonus, v => Entry with { LootingBonus = v });
    }

    public bool KilledByPlayer
    {
        get => Entry.KilledByPlayer;
        set => Update(Tr.T("Change the conditions of the entry"), e => e with { KilledByPlayer = value });
    }

    public bool SurvivesExplosion
    {
        get => Entry.SurvivesExplosion;
        set => Update(Tr.T("Change the conditions of the entry"), e => e with { SurvivesExplosion = value });
    }

    public LootSilkTouch SilkTouch
    {
        get => Entry.SilkTouch;
        set => Update(Tr.T("Change the conditions of the entry"), e => e with { SilkTouch = value });
    }

    public bool SmeltIfOnFire
    {
        get => Entry.SmeltIfOnFire;
        set => Update(Tr.T("Change the functions of the entry"), e => e with { SmeltIfOnFire = value });
    }

    public bool EnchantRandomly
    {
        get => Entry.EnchantRandomly;
        set => Update(Tr.T("Change the functions of the entry"), e => e with { EnchantRandomly = value });
    }

    private LootEntry Entry => _owner.GetPool(_pool).Entries[Index];

    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
        ItemPicker.Refresh();
    }

    private void Update(string description, Func<LootEntry, LootEntry> update)
    {
        LootEntry changed = update(Entry);
        if (changed != Entry)
        {
            _owner.UpdateEntry(_pool, Index, description, _ => changed);
        }
    }

    private void SetNumber(double value, int current, Func<int, LootEntry> update)
    {
        if (!double.IsNaN(value) && (int)Math.Round(value) != current)
        {
            _owner.UpdateEntry(_pool, Index, Tr.T("Change the entry"), _ => update((int)Math.Round(value)));
        }
    }
}
