using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Sections of the mob added in V0.7.9 (Entity.md): visual (scale, transparency, collision box),
/// variations and layers, attributes (type, taming, riding, water, immunities and weaknesses),
/// spawning (dimensions, structures, groups, despawn, egg) and special properties (boss, golem).
/// Every change goes through the history of the project.
/// </summary>
public sealed partial class MobAssetViewModel
{
    private bool _showVariations;
    private bool _showLayers;

    private void InitializeEntitySections()
    {
        GolemActivator = new ReferencePickerViewModel(
            Context, ReferenceDomain.Block, () => _mob.Golem.Activator,
            v => ChangeIfDifferent(Tr.T("Change the golem block"), nameof(MobAsset.Golem), () => _mob.Golem, g => _mob.Golem = g,
                _mob.Golem with { Activator = v ?? MobGolem.DefaultActivator }));
        WaterFluid = new ReferencePickerViewModel(
            Context, ReferenceDomain.Item, () => _mob.Water.Fluid,
            v => ChangeIfDifferent(Tr.T("Change the fluid"), nameof(MobAsset.Water), () => _mob.Water, w => _mob.Water = w, _mob.Water with { Fluid = v }));
        EggTexture = new TextureSlotViewModel(
            _mob, Context, nameof(MobAsset.EggTextureId), Tr.T("Egg texture"), Tr.T("None (the colored egg of the game)"),
            () => _mob.EggTextureId, v => _mob.EggTextureId = v);
        VehicleTexture = new TextureSlotViewModel(
            _mob, Context, nameof(MobAsset.Vehicle), Tr.T("Item texture"), Tr.T("None (the item of the game)"),
            () => _mob.Vehicle.ItemTextureId, v => _mob.Vehicle = _mob.Vehicle with { ItemTextureId = v });

        ImmunityOptions = [.. GameDataChoices.MobImmunities.Select(c => new ToggleOptionViewModel(c.Label, () => _mob.Immunities.Contains(c.Value), on => Toggle(c.Value, on)))];
        VulnerabilityOptions = [.. GameDataChoices.MobVulnerabilities.Select(c => new ToggleOptionViewModel(c.Label, () => _mob.Vulnerabilities.Contains(c.Value), on => Toggle(c.Value, on)))];
        DimensionOptions = [.. GameDataChoices.MobDimensions.Select(c => new ToggleOptionViewModel(c.Label, () => _mob.Dimensions.Contains(c.Value), on => Toggle(c.Value, on)))];
        StructureOptions = [.. GameDataChoices.Structures.Select(c => new ToggleOptionViewModel(c.Label, () => _mob.Structures.Contains(c.Value), on => Toggle(c.Value, on)))];

        AddVariationCommand = new RelayCommand(() => ChangeIfDifferent(Tr.T("Add a variation"), nameof(MobAsset.Variations), () => _mob.Variations, v => _mob.Variations = v,
            (IReadOnlyList<Guid>)[.. _mob.Variations, _mob.TextureId ?? Guid.Empty]));
        AddLayerCommand = new RelayCommand(() => ChangeIfDifferent(Tr.T("Add a layer"), nameof(MobAsset.Layers), () => _mob.Layers, v => _mob.Layers = v,
            (IReadOnlyList<MobLayer>)[.. _mob.Layers, new MobLayer(_mob.TextureId ?? Guid.Empty)]));
        AddTamingItemCommand = new RelayCommand(() => ChangeTaming(Tr.T("Add a taming item"),
            t => t with { Items = t.Items.Add(new MobTamingItem(ContentReference.ToId(Core.Identifiers.NamespacedId.TryParse("minecraft:bone", out var bone) ? bone! : throw new InvalidOperationException()), MobTamingItem.DefaultChance)) }));
        RebuildEntityLists();
    }

    // ----- Choices -----

    public IReadOnlyList<Choice<MobValueMode>> ValueModeChoices => GameDataChoices.MobValueModes;

    public IReadOnlyList<Choice<MobCreatureType>> CreatureTypeChoices => GameDataChoices.MobCreatureTypes;

    public IReadOnlyList<Choice<MobGroupMode>> GroupModeChoices => GameDataChoices.MobGroupModes;

    public IReadOnlyList<Choice<MobBossColor>> BossColorChoices => GameDataChoices.MobBossColors;

    public IReadOnlyList<Choice<MobBossOverlay>> BossOverlayChoices => GameDataChoices.MobBossOverlays;

    /// <summary>Functions of the project a dynamic setting can use, after "None".</summary>
    public IReadOnlyList<Choice<Guid?>> FunctionChoices =>
    [
        new(null, Tr.T("None")),
        .. Context.Project.Assets.OfType<GraphAsset>().Where(g => g.Kind == GraphKind.Function && g.ElementId is null)
            .OrderBy(g => g.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new Choice<Guid?>(g.Id, DisplayLabel(g))),
    ];

    /// <summary>Creative tabs of the project for the egg, after the default (tab of the mod and Spawn Eggs).</summary>
    public IReadOnlyList<Choice<Guid?>> CreativeTabChoices =>
    [
        new(null, Tr.T("Tab of the mod and Spawn Eggs")),
        .. Context.Project.Assets.OfType<CreativeTabAsset>().Select(t => new Choice<Guid?>(t.Id, DisplayLabel(t))),
    ];

    /// <summary>Layers that can show the saddle, after "None".</summary>
    public IReadOnlyList<Choice<int>> SaddleLayerChoices =>
    [
        new(-1, Tr.T("None")),
        .. _mob.Layers.Select((_, i) => new Choice<int>(i, Tr.F("Layer {0}", i + 1))),
    ];

    public ReferencePickerViewModel GolemActivator { get; private set; } = null!;

    public ReferencePickerViewModel WaterFluid { get; private set; } = null!;

    public TextureSlotViewModel EggTexture { get; private set; } = null!;

    /// <summary>Texture of the item that places the boat or the minecart.</summary>
    public TextureSlotViewModel VehicleTexture { get; private set; } = null!;

    public IReadOnlyList<ToggleOptionViewModel> ImmunityOptions { get; private set; } = [];

    public IReadOnlyList<ToggleOptionViewModel> VulnerabilityOptions { get; private set; } = [];

    public IReadOnlyList<ToggleOptionViewModel> DimensionOptions { get; private set; } = [];

    public IReadOnlyList<ToggleOptionViewModel> StructureOptions { get; private set; } = [];

    public ObservableCollection<MobVariationViewModel> VariationRows { get; } = [];

    public ObservableCollection<MobLayerViewModel> LayerRows { get; } = [];

    public ObservableCollection<MobTamingItemViewModel> TamingRows { get; } = [];

    public ICommand AddVariationCommand { get; private set; } = null!;

    public ICommand AddLayerCommand { get; private set; } = null!;

    public ICommand AddTamingItemCommand { get; private set; } = null!;

    // ----- Visual -----

    public MobValueMode VisualScaleMode
    {
        get => _mob.VisualScale.Mode;
        set => ChangeVisualScale(Tr.T("Change the visual scale"), s => s with { Mode = value });
    }

    public bool IsVisualScaleDynamic => _mob.VisualScale.Mode == MobValueMode.Dynamic;

    public double VisualScaleValue
    {
        get => _mob.VisualScale.Value;
        set
        {
            if (!double.IsNaN(value))
            {
                ChangeVisualScale(Tr.T("Change the visual scale"), s => s with { Value = Math.Clamp(value, MobScale.Min, MobScale.Max) });
            }
        }
    }

    public Guid? VisualScaleFunction
    {
        get => _mob.VisualScale.Function;
        set => ChangeVisualScale(Tr.T("Change the visual scale"), s => s with { Function = value });
    }

    public bool TextureTransparent
    {
        get => _mob.TextureTransparent;
        set => ChangeIfDifferent(Tr.T("Change the transparency"), nameof(MobAsset.TextureTransparent), () => _mob.TextureTransparent, v => _mob.TextureTransparent = v, value);
    }

    public double ShadowRadius
    {
        get => _mob.ShadowRadius;
        set => ChangeNumber(Tr.T("Change the shadow"), nameof(MobAsset.ShadowRadius), value, () => _mob.ShadowRadius, v => _mob.ShadowRadius = v,
            v => Math.Clamp(v, 0, MobAsset.MaxShadowRadius));
    }

    public double PassengerOffset
    {
        get => _mob.PassengerOffset;
        set => ChangeNumber(Tr.T("Change the seat of the rider"), nameof(MobAsset.PassengerOffset), value, () => _mob.PassengerOffset, v => _mob.PassengerOffset = v,
            v => Math.Clamp(v, 0, MobAsset.MaxPassengerOffset));
    }

    public bool NoCollision
    {
        get => _mob.NoCollision;
        set => ChangeIfDifferent(Tr.T("Change the collisions"), nameof(MobAsset.NoCollision), () => _mob.NoCollision, v => _mob.NoCollision = v, value);
    }

    public MobValueMode HitboxScaleMode
    {
        get => _mob.HitboxScale.Mode;
        set => ChangeHitboxScale(Tr.T("Change the collision box scale"), s => s with { Mode = value });
    }

    public bool IsHitboxScaleDynamic => _mob.HitboxScale.Mode == MobValueMode.Dynamic;

    public double HitboxScaleValue
    {
        get => _mob.HitboxScale.Value;
        set
        {
            if (!double.IsNaN(value))
            {
                ChangeHitboxScale(Tr.T("Change the collision box scale"), s => s with { Value = Math.Clamp(value, MobScale.Min, MobScale.Max) });
            }
        }
    }

    public Guid? HitboxScaleFunction
    {
        get => _mob.HitboxScale.Function;
        set => ChangeHitboxScale(Tr.T("Change the collision box scale"), s => s with { Function = value });
    }

    // ----- Variations and layers -----

    /// <summary>Unchecking removes the variations (undoable); checking shows the list.</summary>
    public bool HasVariations
    {
        get => _showVariations || _mob.Variations.Count > 0;
        set
        {
            _showVariations = value;
            if (!value && _mob.Variations.Count > 0)
            {
                ChangeIfDifferent(Tr.T("Remove the variations"), nameof(MobAsset.Variations), () => _mob.Variations, v => _mob.Variations = v, (IReadOnlyList<Guid>)[]);
            }

            OnPropertyChanged();
        }
    }

    public bool HasLayers
    {
        get => _showLayers || _mob.Layers.Count > 0;
        set
        {
            _showLayers = value;
            if (!value && _mob.Layers.Count > 0)
            {
                ChangeIfDifferent(Tr.T("Remove the layers"), nameof(MobAsset.Layers), () => _mob.Layers, v => _mob.Layers = v, (IReadOnlyList<MobLayer>)[]);
            }

            OnPropertyChanged();
        }
    }

    internal Guid GetVariation(int index) => _mob.Variations[index];

    internal void SetVariation(int index, Guid? texture)
    {
        List<Guid> variations = [.. _mob.Variations];
        if (texture is { } id && index < variations.Count && variations[index] != id)
        {
            variations[index] = id;
            ChangeIfDifferent(Tr.T("Change a variation"), nameof(MobAsset.Variations), () => _mob.Variations, v => _mob.Variations = v, (IReadOnlyList<Guid>)variations);
        }
    }

    internal void RemoveVariation(int index) =>
        ChangeIfDifferent(Tr.T("Remove a variation"), nameof(MobAsset.Variations), () => _mob.Variations, v => _mob.Variations = v,
            (IReadOnlyList<Guid>)[.. _mob.Variations.Where((_, i) => i != index)]);

    internal MobLayer GetLayer(int index) => _mob.Layers[index];

    internal void ChangeLayer(int index, Func<MobLayer, MobLayer> change, string description) =>
        ChangeIfDifferent(description, nameof(MobAsset.Layers), () => _mob.Layers, v => _mob.Layers = v,
            (IReadOnlyList<MobLayer>)[.. _mob.Layers.Select((l, i) => i == index ? change(l) : l)]);

    internal void RemoveLayer(int index) =>
        ChangeIfDifferent(Tr.T("Remove a layer"), nameof(MobAsset.Layers), () => _mob.Layers, v => _mob.Layers = v,
            (IReadOnlyList<MobLayer>)[.. _mob.Layers.Where((_, i) => i != index)]);

    // ----- Attributes -----

    public MobCreatureType CreatureType
    {
        get => _mob.CreatureType;
        set => ChangeIfDifferent(Tr.T("Change the type of creature"), nameof(MobAsset.CreatureType), () => _mob.CreatureType, v => _mob.CreatureType = v, value);
    }

    public double StepHeight
    {
        get => _mob.StepHeight;
        set => ChangeNumber(Tr.T("Change the step height"), nameof(MobAsset.StepHeight), value, () => _mob.StepHeight, v => _mob.StepHeight = v,
            v => Math.Clamp(v, 0, MobAsset.MaxStepHeight));
    }

    public double TrackingRange
    {
        get => _mob.TrackingRange;
        set => ChangeNumber(Tr.T("Change the tracking range"), nameof(MobAsset.TrackingRange), value, () => _mob.TrackingRange, v => _mob.TrackingRange = v,
            v => Math.Clamp((int)Math.Round(v), 1, MobAsset.MaxTrackingRange));
    }

    public double Knockback
    {
        get => _mob.Knockback;
        set => ChangeNumber(Tr.T("Change the knockback"), nameof(MobAsset.Knockback), value, () => _mob.Knockback, v => _mob.Knockback = v,
            v => Math.Clamp(v, 0, MobAsset.MaxKnockback));
    }

    public double MaxStepHeight => MobAsset.MaxStepHeight;

    public int MaxTrackingRange => MobAsset.MaxTrackingRange;

    public double MaxKnockback => MobAsset.MaxKnockback;

    public double MaxShadowRadius => MobAsset.MaxShadowRadius;

    public double MaxPassengerOffset => MobAsset.MaxPassengerOffset;

    public double MinScale => MobScale.Min;

    public double MaxScale => MobScale.Max;

    public bool Tamable
    {
        get => _mob.Taming.Enabled;
        set => ChangeTaming(value ? Tr.T("Make it tamable") : Tr.T("Make it untamable"), t => t with { Enabled = value });
    }

    internal MobTamingItem GetTamingItem(int index) => _mob.Taming.Items[index];

    internal void ChangeTamingItem(int index, Func<MobTamingItem, MobTamingItem> change) =>
        ChangeTaming(Tr.T("Change a taming item"), t => t with { Items = t.Items.SetItem(index, change(t.Items[index])) });

    internal void RemoveTamingItem(int index) => ChangeTaming(Tr.T("Remove a taming item"), t => t with { Items = t.Items.RemoveAt(index) });

    public bool Ridable
    {
        get => _mob.Riding.Enabled;
        set => ChangeRiding(value ? Tr.T("Make it ridable") : Tr.T("Make it not ridable"), r => r with { Enabled = value });
    }

    public bool RidingForward
    {
        get => _mob.Riding.Forward;
        set => ChangeRiding(Tr.T("Change the riding"), r => r with { Forward = value });
    }

    public bool RidingStrafe
    {
        get => _mob.Riding.Strafe;
        set => ChangeRiding(Tr.T("Change the riding"), r => r with { Strafe = value });
    }

    public bool RidingNeedsSaddle
    {
        get => _mob.Riding.NeedsSaddle;
        set => ChangeRiding(Tr.T("Change the riding"), r => r with { NeedsSaddle = value });
    }

    public int RidingSaddleLayer
    {
        get => _mob.Riding.SaddleLayer;
        set => ChangeRiding(Tr.T("Change the layer of the saddle"), r => r with { SaddleLayer = value });
    }

    public bool IsWaterEntity
    {
        get => _mob.Water.Enabled;
        set => ChangeWater(value ? Tr.T("Make it a water entity") : Tr.T("Make it a land entity"), w => w with { Enabled = value });
    }

    public bool WaterBreathes
    {
        get => _mob.Water.Breathes;
        set => ChangeWater(Tr.T("Change the breathing in fluids"), w => w with { Breathes = value });
    }

    public Guid? WaterBreathesFunction
    {
        get => _mob.Water.BreathesFunction;
        set => ChangeWater(Tr.T("Change the breathing in fluids"), w => w with { BreathesFunction = value });
    }

    public bool WaterPushed
    {
        get => _mob.Water.PushedByFluid;
        set => ChangeWater(Tr.T("Change the push of the fluids"), w => w with { PushedByFluid = value });
    }

    public Guid? WaterPushedFunction
    {
        get => _mob.Water.PushedFunction;
        set => ChangeWater(Tr.T("Change the push of the fluids"), w => w with { PushedFunction = value });
    }

    // ----- Spawning -----

    /// <summary>The opposite of "never despawns".</summary>
    public bool Despawns
    {
        get => !_mob.Persistent;
        set => ChangeIfDifferent(Tr.T("Change the despawning"), nameof(MobAsset.Persistent), () => _mob.Persistent, v => _mob.Persistent = v, !value);
    }

    public Guid? DespawnFunction
    {
        get => _mob.DespawnFunction;
        set => ChangeIfDifferent(Tr.T("Change the despawn condition"), nameof(MobAsset.DespawnFunction), () => _mob.DespawnFunction, v => _mob.DespawnFunction = v, value);
    }

    public MobGroupMode GroupMode
    {
        get => _mob.GroupMode;
        set => ChangeIfDifferent(Tr.T("Change the group size"), nameof(MobAsset.GroupMode), () => _mob.GroupMode, v => _mob.GroupMode = v, value);
    }

    public bool IsGroupFixed => _mob.GroupMode == MobGroupMode.Fixed;

    public bool IsGroupRange => _mob.GroupMode == MobGroupMode.Range;

    public bool IsGroupDynamic => _mob.GroupMode == MobGroupMode.Dynamic;

    public Guid? GroupFunction
    {
        get => _mob.GroupFunction;
        set => ChangeIfDifferent(Tr.T("Change the group size"), nameof(MobAsset.GroupFunction), () => _mob.GroupFunction, v => _mob.GroupFunction = v, value);
    }

    public bool InStructures
    {
        get => _mob.InStructures;
        set => ChangeIfDifferent(Tr.T("Change the structures"), nameof(MobAsset.InStructures), () => _mob.InStructures, v => _mob.InStructures = v, value);
    }

    public Guid? EggCreativeTab
    {
        get => _mob.EggCreativeTab;
        set => ChangeIfDifferent(Tr.T("Change the tab of the egg"), nameof(MobAsset.EggCreativeTab), () => _mob.EggCreativeTab, v => _mob.EggCreativeTab = v, value);
    }

    // ----- Special properties -----

    public bool IsBoss
    {
        get => _mob.Boss.Enabled;
        set => ChangeBoss(value ? Tr.T("Make it a boss") : Tr.T("Make it a normal mob"), b => b with { Enabled = value });
    }

    public bool BossShowBar
    {
        get => _mob.Boss.ShowBar;
        set => ChangeBoss(Tr.T("Change the boss bar"), b => b with { ShowBar = value });
    }

    public MobBossColor BossColor
    {
        get => _mob.Boss.Color;
        set => ChangeBoss(Tr.T("Change the boss bar"), b => b with { Color = value });
    }

    public MobBossOverlay BossOverlay
    {
        get => _mob.Boss.Overlay;
        set => ChangeBoss(Tr.T("Change the boss bar"), b => b with { Overlay = value });
    }

    public bool IsGolem
    {
        get => _mob.Golem.Enabled;
        set => ChangeIfDifferent(value ? Tr.T("Make it a golem") : Tr.T("Make it a normal mob"), nameof(MobAsset.Golem), () => _mob.Golem, g => _mob.Golem = g,
            _mob.Golem with { Enabled = value });
    }

    public Guid? GolemFunction
    {
        get => _mob.Golem.Function;
        set => ChangeIfDifferent(Tr.T("Change the golem detection"), nameof(MobAsset.Golem), () => _mob.Golem, g => _mob.Golem = g, _mob.Golem with { Function = value });
    }

    // ----- Vehicle (step 3) -----

    public static IReadOnlyList<Choice<ItemRarity>> RarityChoices => GameDataChoices.Rarities;

    /// <summary>A boat: it cannot be a minecart at the same time (the box of the other is greyed out).</summary>
    public bool IsBoat
    {
        get => _mob.Vehicle.Kind == MobVehicleKind.Boat;
        set => SetVehicle(value ? MobVehicleKind.Boat : MobVehicleKind.None);
    }

    public bool IsMinecart
    {
        get => _mob.Vehicle.Kind == MobVehicleKind.Minecart;
        set => SetVehicle(value ? MobVehicleKind.Minecart : MobVehicleKind.None);
    }

    public bool IsVehicle => _mob.Vehicle.Enabled;

    /// <summary>Spawning, egg and AI only apply to a mob that is not a vehicle.</summary>
    public bool IsNotVehicle => !_mob.Vehicle.Enabled;

    public bool CanBeBoat => _mob.Vehicle.Kind != MobVehicleKind.Minecart;

    public bool CanBeMinecart => _mob.Vehicle.Kind != MobVehicleKind.Boat;

    public ItemRarity VehicleRarity
    {
        get => _mob.Vehicle.Rarity;
        set => ChangeIfDifferent(Tr.T("Change the rarity of the vehicle"), nameof(MobAsset.Vehicle), () => _mob.Vehicle, v => _mob.Vehicle = v, _mob.Vehicle with { Rarity = value });
    }

    /// <summary>"Item placing it: storm_wolf_boat".</summary>
    public string VehicleItemHint => _mob.VehicleItemId is { } id ? Tr.F("Item that places it: {0}", id) : string.Empty;

    private void SetVehicle(MobVehicleKind kind) => ChangeIfDifferent(
        kind switch
        {
            MobVehicleKind.Boat => Tr.T("Make it a boat"),
            MobVehicleKind.Minecart => Tr.T("Make it a minecart"),
            _ => Tr.T("Make it a normal mob"),
        },
        nameof(MobAsset.Vehicle), () => _mob.Vehicle, v => _mob.Vehicle = v, _mob.Vehicle with { Kind = kind });

    // ----- Changes -----

    private void ChangeVisualScale(string description, Func<MobScale, MobScale> change) =>
        ChangeIfDifferent(description, nameof(MobAsset.VisualScale), () => _mob.VisualScale, v => _mob.VisualScale = v, change(_mob.VisualScale));

    private void ChangeHitboxScale(string description, Func<MobScale, MobScale> change) =>
        ChangeIfDifferent(description, nameof(MobAsset.HitboxScale), () => _mob.HitboxScale, v => _mob.HitboxScale = v, change(_mob.HitboxScale));

    private void ChangeTaming(string description, Func<MobTaming, MobTaming> change) =>
        ChangeIfDifferent(description, nameof(MobAsset.Taming), () => _mob.Taming, v => _mob.Taming = v, change(_mob.Taming));

    private void ChangeRiding(string description, Func<MobRiding, MobRiding> change) =>
        ChangeIfDifferent(description, nameof(MobAsset.Riding), () => _mob.Riding, v => _mob.Riding = v, change(_mob.Riding));

    private void ChangeWater(string description, Func<MobWater, MobWater> change) =>
        ChangeIfDifferent(description, nameof(MobAsset.Water), () => _mob.Water, v => _mob.Water = v, change(_mob.Water));

    private void ChangeBoss(string description, Func<MobBoss, MobBoss> change) =>
        ChangeIfDifferent(description, nameof(MobAsset.Boss), () => _mob.Boss, v => _mob.Boss = v, change(_mob.Boss));

    /// <summary>The damage types of the project, each a box (V0.7.10): the mob ignores the checked ones.</summary>
    public IReadOnlyList<ToggleOptionViewModel> DamageTypeImmunityOptions =>
    [
        .. Context.Project.Assets.OfType<DamageTypeAsset>().OrderBy(d => d.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => new ToggleOptionViewModel(DisplayLabel(d), () => _mob.ImmuneDamageTypes.Contains(d.Id), on => ToggleDamageType(d.Id, on))),
    ];

    public bool HasDamageTypes => Context.Project.Assets.OfType<DamageTypeAsset>().Any();

    private void ToggleDamageType(Guid id, bool on) =>
        ChangeIfDifferent(Tr.T("Change the immunities"), nameof(MobAsset.ImmuneDamageTypes), () => _mob.ImmuneDamageTypes, v => _mob.ImmuneDamageTypes = v,
            on ? new HashSet<Guid>(_mob.ImmuneDamageTypes) { id } : new HashSet<Guid>(_mob.ImmuneDamageTypes.Where(t => t != id)));

    private void Toggle(MobImmunity value, bool on) =>
        ChangeIfDifferent(Tr.T("Change the immunities"), nameof(MobAsset.Immunities), () => _mob.Immunities, v => _mob.Immunities = v, Toggled(_mob.Immunities, value, on));

    private void Toggle(MobVulnerability value, bool on) =>
        ChangeIfDifferent(Tr.T("Change the weaknesses"), nameof(MobAsset.Vulnerabilities), () => _mob.Vulnerabilities, v => _mob.Vulnerabilities = v, Toggled(_mob.Vulnerabilities, value, on));

    private void Toggle(MobDimension value, bool on)
    {
        IReadOnlySet<MobDimension> dimensions = Toggled(_mob.Dimensions, value, on);
        if (dimensions.Count > 0)
        {
            ChangeIfDifferent(Tr.T("Change the dimensions"), nameof(MobAsset.Dimensions), () => _mob.Dimensions, v => _mob.Dimensions = v, dimensions);
        }
        else
        {
            // At least one dimension: the box stays checked.
            OnPropertyChanged(nameof(DimensionOptions));
            foreach (ToggleOptionViewModel option in DimensionOptions)
            {
                option.Refresh();
            }
        }
    }

    private void Toggle(string structure, bool on) =>
        ChangeIfDifferent(Tr.T("Change the structures"), nameof(MobAsset.Structures), () => _mob.Structures, v => _mob.Structures = v, Toggled(_mob.Structures, structure, on));

    /// <summary>The set with or without the value; the same set when nothing changes (no empty step in the history).</summary>
    private static IReadOnlySet<T> Toggled<T>(IReadOnlySet<T> set, T value, bool on)
    {
        if (set.Contains(value) == on)
        {
            return set;
        }

        var copy = new HashSet<T>(set);
        if (on)
        {
            copy.Add(value);
        }
        else
        {
            copy.Remove(value);
        }

        return copy;
    }

    /// <summary>Properties to raise when a property of the asset changes (the sections are records).</summary>
    private void OnEntityPropertyChanged(string propertyName)
    {
        string[] derived = propertyName switch
        {
            nameof(MobAsset.VisualScale) => [nameof(VisualScaleMode), nameof(VisualScaleValue), nameof(VisualScaleFunction), nameof(IsVisualScaleDynamic)],
            nameof(MobAsset.HitboxScale) => [nameof(HitboxScaleMode), nameof(HitboxScaleValue), nameof(HitboxScaleFunction), nameof(IsHitboxScaleDynamic)],
            nameof(MobAsset.Taming) => [nameof(Tamable)],
            nameof(MobAsset.Riding) => [nameof(Ridable), nameof(RidingForward), nameof(RidingStrafe), nameof(RidingNeedsSaddle), nameof(RidingSaddleLayer)],
            nameof(MobAsset.Water) => [nameof(IsWaterEntity), nameof(WaterBreathes), nameof(WaterBreathesFunction), nameof(WaterPushed), nameof(WaterPushedFunction)],
            nameof(MobAsset.Boss) => [nameof(IsBoss), nameof(BossShowBar), nameof(BossColor), nameof(BossOverlay)],
            nameof(MobAsset.Golem) => [nameof(IsGolem), nameof(GolemFunction)],
            nameof(MobAsset.Vehicle) or nameof(MobAsset.ResourceId) =>
                [nameof(IsBoat), nameof(IsMinecart), nameof(IsVehicle), nameof(IsNotVehicle), nameof(CanBeBoat), nameof(CanBeMinecart), nameof(VehicleRarity), nameof(VehicleItemHint)],
            nameof(MobAsset.Persistent) => [nameof(Despawns)],
            nameof(MobAsset.GroupMode) => [nameof(IsGroupFixed), nameof(IsGroupRange), nameof(IsGroupDynamic)],
            nameof(MobAsset.Variations) => [nameof(HasVariations)],
            nameof(MobAsset.Layers) => [nameof(HasLayers), nameof(SaddleLayerChoices), nameof(RidingSaddleLayer)],
            _ => [],
        };
        foreach (string name in derived)
        {
            OnPropertyChanged(name);
        }

        switch (propertyName)
        {
            case nameof(MobAsset.Golem):
                GolemActivator.Refresh();
                break;
            case nameof(MobAsset.Water):
                WaterFluid.Refresh();
                break;
            case nameof(MobAsset.Immunities):
                RefreshAll(ImmunityOptions);
                break;
            case nameof(MobAsset.ImmuneDamageTypes):
                OnPropertyChanged(nameof(DamageTypeImmunityOptions));
                break;
            case nameof(MobAsset.Vulnerabilities):
                RefreshAll(VulnerabilityOptions);
                break;
            case nameof(MobAsset.Dimensions):
                RefreshAll(DimensionOptions);
                break;
            case nameof(MobAsset.Structures):
                RefreshAll(StructureOptions);
                break;
            case nameof(MobAsset.Variations) or nameof(MobAsset.Layers) or nameof(MobAsset.Taming):
                SyncEntityLists();
                break;
        }
    }

    /// <summary>The project changed (a function or a tab was added or renamed): the lists of choices follow.</summary>
    private void RefreshEntityChoices()
    {
        OnPropertyChanged(nameof(FunctionChoices));
        OnPropertyChanged(nameof(CreativeTabChoices));
        GolemActivator.Refresh();
        WaterFluid.Refresh();
        SyncEntityLists();
    }

    private static void RefreshAll(IEnumerable<ToggleOptionViewModel> options)
    {
        foreach (ToggleOptionViewModel option in options)
        {
            option.Refresh();
        }
    }

    private void SyncEntityLists()
    {
        if (VariationRows.Count != _mob.Variations.Count || LayerRows.Count != _mob.Layers.Count || TamingRows.Count != _mob.Taming.Items.Count)
        {
            RebuildEntityLists();
            return;
        }

        foreach (MobVariationViewModel row in VariationRows)
        {
            row.Refresh();
        }

        foreach (MobLayerViewModel row in LayerRows)
        {
            row.Refresh();
        }

        foreach (MobTamingItemViewModel row in TamingRows)
        {
            row.Refresh();
        }
    }

    private void RebuildEntityLists()
    {
        VariationRows.Clear();
        for (int i = 0; i < _mob.Variations.Count; i++)
        {
            VariationRows.Add(new MobVariationViewModel(this, _mob, Context, i));
        }

        LayerRows.Clear();
        for (int i = 0; i < _mob.Layers.Count; i++)
        {
            LayerRows.Add(new MobLayerViewModel(this, _mob, Context, i));
        }

        TamingRows.Clear();
        for (int i = 0; i < _mob.Taming.Items.Count; i++)
        {
            TamingRows.Add(new MobTamingItemViewModel(this, Context, i));
        }
    }
}

/// <summary>A variation of the mob: a texture one of which each new mob takes at random.</summary>
public sealed class MobVariationViewModel : ObservableObject
{
    private readonly MobAssetViewModel _owner;

    public MobVariationViewModel(MobAssetViewModel owner, MobAsset mob, AssetEditingContext context, int index)
    {
        _owner = owner;
        Index = index;
        Texture = new TextureSlotViewModel(mob, context, nameof(MobAsset.Variations), Tr.F("Variation {0}", index + 1), Tr.T("None"),
            () => index < mob.Variations.Count ? mob.Variations[index] : null, v => _owner.SetVariation(index, v));
        RemoveCommand = new RelayCommand(() => _owner.RemoveVariation(Index));
    }

    public int Index { get; }

    public TextureSlotViewModel Texture { get; }

    public ICommand RemoveCommand { get; }

    internal void Refresh() => Texture.Refresh();
}

/// <summary>A layer of the mob: its texture, its light, its hurt flash, and the functions that decide them.</summary>
public sealed class MobLayerViewModel : ObservableObject
{
    private readonly MobAssetViewModel _owner;

    public MobLayerViewModel(MobAssetViewModel owner, MobAsset mob, AssetEditingContext context, int index)
    {
        _owner = owner;
        Index = index;
        Texture = new TextureSlotViewModel(mob, context, nameof(MobAsset.Layers), Tr.F("Layer {0}", index + 1), Tr.T("None"),
            () => index < mob.Layers.Count ? mob.Layers[index].TextureId : null,
            v =>
            {
                if (v is { } id)
                {
                    _owner.ChangeLayer(index, l => l with { TextureId = id }, Tr.T("Change the texture of a layer"));
                }
            });
        RemoveCommand = new RelayCommand(() => _owner.RemoveLayer(Index));
    }

    public int Index { get; }

    public string Title => Tr.F("Layer {0}", Index + 1);

    public TextureSlotViewModel Texture { get; }

    private MobLayer Layer => _owner.GetLayer(Index);

    public IReadOnlyList<Choice<Guid?>> FunctionChoices => _owner.FunctionChoices;

    public bool Glow
    {
        get => Layer.Glow;
        set => _owner.ChangeLayer(Index, l => l with { Glow = value }, Tr.T("Change the glow of a layer"));
    }

    public bool NoHurtOverlay
    {
        get => Layer.NoHurtOverlay;
        set => _owner.ChangeLayer(Index, l => l with { NoHurtOverlay = value }, Tr.T("Change the hurt flash of a layer"));
    }

    public Guid? VisibleFunction
    {
        get => Layer.VisibleFunction;
        set => _owner.ChangeLayer(Index, l => l with { VisibleFunction = value }, Tr.T("Change the display of a layer"));
    }

    public Guid? GlowFunction
    {
        get => Layer.GlowFunction;
        set => _owner.ChangeLayer(Index, l => l with { GlowFunction = value }, Tr.T("Change the glow of a layer"));
    }

    public Guid? HurtFunction
    {
        get => Layer.HurtFunction;
        set => _owner.ChangeLayer(Index, l => l with { HurtFunction = value }, Tr.T("Change the hurt flash of a layer"));
    }

    public ICommand RemoveCommand { get; }

    internal void Refresh()
    {
        Texture.Refresh();
        OnPropertyChanged(nameof(Glow));
        OnPropertyChanged(nameof(NoHurtOverlay));
        OnPropertyChanged(nameof(VisibleFunction));
        OnPropertyChanged(nameof(GlowFunction));
        OnPropertyChanged(nameof(HurtFunction));
        OnPropertyChanged(nameof(FunctionChoices));
    }
}

/// <summary>An item that tames the mob, with its chance in percent (bone: 33 %).</summary>
public sealed class MobTamingItemViewModel : ObservableObject
{
    private readonly MobAssetViewModel _owner;

    public MobTamingItemViewModel(MobAssetViewModel owner, AssetEditingContext context, int index)
    {
        _owner = owner;
        Index = index;
        ItemPicker = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => _owner.GetTamingItem(Index).Item,
            v =>
            {
                if (v is not null)
                {
                    _owner.ChangeTamingItem(Index, t => t with { Item = v });
                }
            });
        RemoveCommand = new RelayCommand(() => _owner.RemoveTamingItem(Index));
    }

    public int Index { get; }

    public ReferencePickerViewModel ItemPicker { get; }

    public double Chance
    {
        get => _owner.GetTamingItem(Index).Chance;
        set
        {
            if (!double.IsNaN(value))
            {
                _owner.ChangeTamingItem(Index, t => t with { Chance = Math.Clamp(value, 0, 100) });
            }
        }
    }

    public ICommand RemoveCommand { get; }

    internal void Refresh()
    {
        ItemPicker.Refresh();
        OnPropertyChanged(nameof(Chance));
    }
}
