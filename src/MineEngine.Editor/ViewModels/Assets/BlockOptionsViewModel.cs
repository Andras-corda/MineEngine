using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>A box of the shape of a block, edited in the Shape tab.</summary>
public sealed class BlockBoxViewModel : ObservableObject
{
    private readonly BlockOptionsViewModel _owner;

    public BlockBoxViewModel(BlockOptionsViewModel owner, int index, BlockBox box)
    {
        _owner = owner;
        Index = index;
        Box = box;
        RemoveCommand = new RelayCommand(() => _owner.RemoveBox(Index));
        UpCommand = new RelayCommand(() => _owner.MoveBox(Index, -1), () => Index > 0);
        DownCommand = new RelayCommand(() => _owner.MoveBox(Index, 1), () => Index < _owner.Boxes.Count - 1);
    }

    public int Index { get; }

    public BlockBox Box { get; }

    public ICommand RemoveCommand { get; }

    public ICommand UpCommand { get; }

    public ICommand DownCommand { get; }

    public double MinX { get => Box.MinX; set => Set(Box with { MinX = Pixel(value) }); }

    public double MinY { get => Box.MinY; set => Set(Box with { MinY = Pixel(value) }); }

    public double MinZ { get => Box.MinZ; set => Set(Box with { MinZ = Pixel(value) }); }

    public double MaxX { get => Box.MaxX; set => Set(Box with { MaxX = Pixel(value) }); }

    public double MaxY { get => Box.MaxY; set => Set(Box with { MaxY = Pixel(value) }); }

    public double MaxZ { get => Box.MaxZ; set => Set(Box with { MaxZ = Pixel(value) }); }

    public bool Subtract { get => Box.Subtract; set => Set(Box with { Subtract = value }); }

    private static double Pixel(double value) => double.IsFinite(value) ? Math.Clamp(Math.Round(value, 3), -16, 32) : 0;

    private void Set(BlockBox box)
    {
        if (box != Box)
        {
            _owner.ReplaceBox(Index, box);
        }
    }
}

/// <summary>A property of the block state added by the user, edited in the States tab.</summary>
public sealed class BlockPropertyViewModel : ObservableObject
{
    private readonly BlockOptionsViewModel _owner;

    public BlockPropertyViewModel(BlockOptionsViewModel owner, int index, BlockCustomProperty property)
    {
        _owner = owner;
        Index = index;
        Property = property;
        RemoveCommand = new RelayCommand(() => _owner.RemoveProperty(Index));
    }

    public int Index { get; }

    public BlockCustomProperty Property { get; }

    public ICommand RemoveCommand { get; }

    public IReadOnlyList<Choice<BlockPropertyKind>> KindChoices => BlockOptionsViewModel.PropertyKinds;

    public string Name
    {
        get => Property.Name;
        set => Set(Property with { Name = (value ?? string.Empty).Trim().ToLowerInvariant() });
    }

    public BlockPropertyKind Kind
    {
        get => Property.Kind;
        set => Set(Property with { Kind = value });
    }

    public bool IsInteger => Property.Kind == BlockPropertyKind.Integer;

    public bool IsChoice => Property.Kind == BlockPropertyKind.Choice;

    public double Min
    {
        get => Property.Min;
        set => Set(Property with { Min = double.IsFinite(value) ? Math.Clamp((int)Math.Round(value), 0, 255) : 0 });
    }

    public double Max
    {
        get => Property.Max;
        set => Set(Property with { Max = double.IsFinite(value) ? Math.Clamp((int)Math.Round(value), 1, 256) : 1 });
    }

    /// <summary>Values of a choice, separated by commas ("red, green, blue").</summary>
    public string Values
    {
        get => string.Join(", ", Property.Values);
        set => Set(Property with { Values = BlockOptionsViewModel.SplitList(value).Select(v => v.ToLowerInvariant()).ToList() });
    }

    private void Set(BlockCustomProperty property)
    {
        if (property != Property && !(property.Kind == Property.Kind && property.Name == Property.Name && property.Min == Property.Min
                                       && property.Max == Property.Max && property.Values.SequenceEqual(Property.Values)))
        {
            _owner.ReplaceProperty(Index, property);
        }
    }
}

/// <summary>
/// Advanced settings of a block (tabs Advanced, Redstone and fire, Shape, States, Ore): each change
/// replaces the <see cref="BlockOptions"/> of the block in one step of the history.
/// </summary>
public sealed class BlockOptionsViewModel : ObservableObject
{
    private readonly BlockAsset _block;
    private readonly Action<string, BlockOptions> _change;

    public BlockOptionsViewModel(BlockAsset block, Action<string, BlockOptions> change)
    {
        _block = block ?? throw new ArgumentNullException(nameof(block));
        _change = change ?? throw new ArgumentNullException(nameof(change));
        AddBoxCommand = new RelayCommand(() => Set(Tr.T("Add a box to the shape"), o => o with { Boxes = [.. o.Boxes, BlockBox.Full] }));
        AddPropertyCommand = new RelayCommand(AddProperty);
        Refresh();
    }

    private BlockOptions Options => _block.Options;

    public static IReadOnlyList<Choice<BlockRotation>> Rotations { get; } =
    [
        new(BlockRotation.None, Tr.T("No rotation (fixed orientation)")),
        new(BlockRotation.Horizontal, Tr.T("Faces the player (north, south, east, west)")),
        new(BlockRotation.AllDirections, Tr.T("Faces the player (six directions)")),
    ];

    public static IReadOnlyList<Choice<BlockOffset>> Offsets { get; } =
    [
        new(BlockOffset.None, Tr.T("None")),
        new(BlockOffset.XZ, Tr.T("Horizontal (like flowers)")),
        new(BlockOffset.XYZ, Tr.T("Horizontal and vertical (like tall grass)")),
    ];

    public static IReadOnlyList<Choice<BlockPushReaction>> PushReactions { get; } =
    [
        new(BlockPushReaction.Normal, Tr.T("Normal")),
        new(BlockPushReaction.Destroy, Tr.T("Destroyed (like a torch)")),
        new(BlockPushReaction.Block, Tr.T("Blocks the piston (like obsidian)")),
        new(BlockPushReaction.PushOnly, Tr.T("Can only be pushed")),
        new(BlockPushReaction.Ignore, Tr.T("Ignored")),
    ];

    public static IReadOnlyList<Choice<BlockPropertyKind>> PropertyKinds { get; } =
    [
        new(BlockPropertyKind.Boolean, Tr.T("True / false")),
        new(BlockPropertyKind.Integer, Tr.T("Whole number")),
        new(BlockPropertyKind.Choice, Tr.T("Choice of values")),
    ];

    public static IReadOnlyList<Choice<OreShape>> OreShapes { get; } =
    [
        new(OreShape.Uniform, Tr.T("Uniform (same chance at every height)")),
        new(OreShape.Triangle, Tr.T("Triangle (more often in the middle)")),
    ];

    public IReadOnlyList<Choice<string>> MapColors { get; } = Named(BlockOptions.MapColors);

    public IReadOnlyList<Choice<string>> Instruments { get; } = Named(BlockOptions.Instruments);

    public IReadOnlyList<Choice<string>> BeaconColors { get; } = Named(BlockOptions.DyeColors);

    private static IReadOnlyList<Choice<string>> Named(IReadOnlyList<string> names) =>
        [new Choice<string>(string.Empty, Tr.T("Default")), .. names.Select(n => new Choice<string>(n, n))];

    // ----- Rotation and water -----

    public BlockRotation Rotation { get => Options.Rotation; set => Set(Tr.T("Change the rotation of the block"), o => o with { Rotation = value }); }

    public bool CanHavePitch => Options.Rotation == BlockRotation.Horizontal;

    public bool HasPitchRotation { get => Options.HasPitchRotation; set => Set(Tr.T("Change the pitch rotation"), o => o with { HasPitchRotation = value }); }

    public bool Waterloggable { get => Options.Waterloggable; set => Set(Tr.T("Change \"waterloggable\""), o => o with { Waterloggable = value }); }

    public bool HasGravity { get => Options.HasGravity; set => Set(Tr.T("Change the gravity of the block"), o => o with { HasGravity = value }); }

    // ----- Rendering and physics -----

    public bool Emissive { get => Options.Emissive; set => Set(Tr.T("Change the glow of the block"), o => o with { Emissive = value }); }

    public bool Replaceable { get => Options.Replaceable; set => Set(Tr.T("Change \"replaceable\""), o => o with { Replaceable = value }); }

    public bool WalkThrough { get => Options.WalkThrough; set => Set(Tr.T("Change \"can walk through\""), o => o with { WalkThrough = value }); }

    public bool CustomLightOpacity { get => Options.CustomLightOpacity; set => Set(Tr.T("Change the light opacity"), o => o with { CustomLightOpacity = value }); }

    public double LightOpacity
    {
        get => Options.LightOpacity;
        set => Set(Tr.T("Change the light opacity"), o => o with { LightOpacity = Whole(value, 0, 15) });
    }

    public bool ConnectedSides { get => Options.ConnectedSides; set => Set(Tr.T("Change \"connected sides\""), o => o with { ConnectedSides = value }); }

    public BlockOffset RandomOffset { get => Options.RandomOffset; set => Set(Tr.T("Change the random offset"), o => o with { RandomOffset = value }); }

    public string MapColor { get => Options.MapColor; set => Set(Tr.T("Change the color on the map"), o => o with { MapColor = value ?? string.Empty }); }

    public string Instrument { get => Options.Instrument; set => Set(Tr.T("Change the note block instrument"), o => o with { Instrument = value ?? string.Empty }); }

    public BlockPushReaction PushReaction { get => Options.PushReaction; set => Set(Tr.T("Change the reaction to pistons"), o => o with { PushReaction = value }); }

    public bool Ladder { get => Options.Ladder; set => Set(Tr.T("Change \"acts like a ladder\""), o => o with { Ladder = value }); }

    public bool PlantsCanSustain { get => Options.PlantsCanSustain; set => Set(Tr.T("Change \"plants can grow on it\""), o => o with { PlantsCanSustain = value }); }

    public double EnchantPowerBonus
    {
        get => Options.EnchantPowerBonus;
        set => Set(Tr.T("Change the enchanting power bonus"), o => o with { EnchantPowerBonus = (float)(double.IsFinite(value) ? Math.Clamp(value, 0, 15) : 0) });
    }

    public string BeaconColor { get => Options.BeaconColor; set => Set(Tr.T("Change the beacon color"), o => o with { BeaconColor = value ?? string.Empty }); }

    // ----- Redstone and fire -----

    public bool RedstoneConnects { get => Options.RedstoneConnects; set => Set(Tr.T("Change \"redstone connects\""), o => o with { RedstoneConnects = value }); }

    public bool EmitsRedstone { get => Options.EmitsRedstone; set => Set(Tr.T("Change \"emits redstone\""), o => o with { EmitsRedstone = value }); }

    public double RedstonePower
    {
        get => Options.RedstonePower;
        set => Set(Tr.T("Change the redstone power"), o => o with { RedstonePower = Whole(value, 0, BlockOptions.MaxRedstonePower) });
    }

    public bool IgnitedByLava { get => Options.IgnitedByLava; set => Set(Tr.T("Change \"ignited by lava\""), o => o with { IgnitedByLava = value }); }

    private bool _showFire;

    /// <summary>The rotation, the boxes and the states are those of the block base when there is one (V0.7.11).</summary>
    public bool IsNotBase => !_block.Model.IsBase();

    /// <summary>The block ticks (V0.7.11): the "tick" event of its graphs runs.</summary>
    public bool TickEnabled
    {
        get => Options.TickEnabled;
        set => Set(value ? Tr.T("Make the block tick") : Tr.T("Stop the ticks of the block"), o => o with { TickEnabled = value });
    }

    /// <summary>At random, like crops (the tick rate is then not used).</summary>
    public bool TickRandomly
    {
        get => Options.TickRandomly;
        set => Set(Tr.T("Change the ticks of the block"), o => o with { TickRandomly = value });
    }

    public bool UsesTickRate => Options.TickEnabled && !Options.TickRandomly;

    public double TickRate
    {
        get => Options.TickRate;
        set => Set(Tr.T("Change the tick rate"), o => o with { TickRate = Whole(value, 1, 72000) });
    }

    /// <summary>The block burns (block.md "can block be flammed"): its flammability and fire speed are then chosen.</summary>
    public bool IsFlammable
    {
        get => _showFire || Options.Flammability > 0 || Options.FireSpreadSpeed > 0;
        set
        {
            _showFire = value;
            if (!value)
            {
                Set(Tr.T("Make it not flammable"), o => o with { Flammability = 0, FireSpreadSpeed = 0 });
            }

            OnPropertyChanged(nameof(IsFlammable));
        }
    }

    /// <summary>The shape of the block is the full cube (no box drawn).</summary>
    public bool UsesDefaultShape
    {
        get => Options.Boxes.Count == 0;
        set => Set(value ? Tr.T("Use the default shape") : Tr.T("Draw the shape with boxes"), o => o with { Boxes = value ? [] : [BlockBox.Full] });
    }

    public double Flammability
    {
        get => Options.Flammability;
        set => Set(Tr.T("Change the flammability"), o => o with { Flammability = Whole(value, 0, 300) });
    }

    public double FireSpreadSpeed
    {
        get => Options.FireSpreadSpeed;
        set => Set(Tr.T("Change the fire spreading speed"), o => o with { FireSpreadSpeed = Whole(value, 0, 300) });
    }

    // ----- Shape -----

    public ObservableCollection<BlockBoxViewModel> Boxes { get; } = [];

    public ICommand AddBoxCommand { get; }

    public bool HasBoxes => Options.Boxes.Count > 0;

    internal void ReplaceBox(int index, BlockBox box) =>
        Set(Tr.T("Change a box of the shape"), o => o with { Boxes = [.. o.Boxes.Select((b, i) => i == index ? box : b)] });

    internal void RemoveBox(int index) =>
        Set(Tr.T("Remove a box of the shape"), o => o with { Boxes = [.. o.Boxes.Where((_, i) => i != index)] });

    internal void MoveBox(int index, int offset)
    {
        int target = index + offset;
        if (target < 0 || target >= Options.Boxes.Count)
        {
            return;
        }

        Set(Tr.T("Move a box of the shape"), o =>
        {
            var boxes = o.Boxes.ToList();
            (boxes[index], boxes[target]) = (boxes[target], boxes[index]);
            return o with { Boxes = boxes };
        });
    }

    // ----- Properties of the block state -----

    public ObservableCollection<BlockPropertyViewModel> Properties { get; } = [];

    public ICommand AddPropertyCommand { get; }

    /// <summary>"4 combinations": number of states of the block made by the custom properties.</summary>
    public string CombinationsLabel => Tr.F("Combinations of properties: {0}", Options.Combinations);

    public bool HasTooManyCombinations => Options.Combinations > BlockAsset.MaxPropertyCombinations;

    private void AddProperty()
    {
        var used = Options.Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        string name = "property";
        for (int i = 2; used.Contains(name); i++)
        {
            name = "property" + i.ToString(CultureInfo.InvariantCulture);
        }

        Set(Tr.T("Add a block property"), o => o with { Properties = [.. o.Properties, new BlockCustomProperty(name, BlockPropertyKind.Boolean)] });
    }

    internal void ReplaceProperty(int index, BlockCustomProperty property) =>
        Set(Tr.T("Change a block property"), o => o with { Properties = [.. o.Properties.Select((p, i) => i == index ? property : p)] });

    internal void RemoveProperty(int index) =>
        Set(Tr.T("Remove a block property"), o => o with { Properties = [.. o.Properties.Where((_, i) => i != index)] });

    // ----- Ore generation -----

    public bool OreEnabled { get => Options.Ore.Enabled; set => SetOre(Tr.T("Change the ore generation"), r => r with { Enabled = value }); }

    /// <summary>Blocks or tags replaced, separated by commas.</summary>
    public string OreReplace
    {
        get => string.Join(", ", Options.Ore.Replace);
        set => SetOre(Tr.T("Change the blocks replaced by the ore"), r => r with { Replace = SplitList(value) });
    }

    /// <summary>Biomes or a tag, separated by commas; empty: the overworld.</summary>
    public string OreBiomes
    {
        get => string.Join(", ", Options.Ore.Biomes);
        set => SetOre(Tr.T("Change the biomes of the ore"), r => r with { Biomes = SplitList(value) });
    }

    public OreShape OreShape { get => Options.Ore.Shape; set => SetOre(Tr.T("Change the shape of the ore generation"), r => r with { Shape = value }); }

    public double OreGroups { get => Options.Ore.GroupsPerChunk; set => SetOre(Tr.T("Change the groups of ores per chunk"), r => r with { GroupsPerChunk = Whole(value, 1, 64) }); }

    public double OreSize { get => Options.Ore.GroupSize; set => SetOre(Tr.T("Change the size of the groups of ores"), r => r with { GroupSize = Whole(value, 1, 64) }); }

    public double OreMinHeight { get => Options.Ore.MinHeight; set => SetOre(Tr.T("Change the heights of the ore"), r => r with { MinHeight = Whole(value, -64, 320) }); }

    public double OreMaxHeight { get => Options.Ore.MaxHeight; set => SetOre(Tr.T("Change the heights of the ore"), r => r with { MaxHeight = Whole(value, -64, 320) }); }

    private void SetOre(string description, Func<BlockOreGeneration, BlockOreGeneration> change) =>
        Set(description, o => o with { Ore = change(o.Ore) });

    // ----- Changes -----

    /// <summary>"a, b ,c" gives ["a", "b", "c"].</summary>
    public static IReadOnlyList<string> SplitList(string? text) =>
        [.. (text ?? string.Empty).Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static int Whole(double value, int min, int max) => double.IsFinite(value) ? Math.Clamp((int)Math.Round(value), min, max) : min;

    private void Set(string description, Func<BlockOptions, BlockOptions> change)
    {
        BlockOptions value = change(Options);
        if (value != Options)
        {
            _change(description, value);
        }
    }

    /// <summary>Reads the options again (change, undo).</summary>
    public void Refresh()
    {
        if (Boxes.Count != Options.Boxes.Count || !Boxes.Select(b => b.Box).SequenceEqual(Options.Boxes))
        {
            Boxes.Clear();
            for (int i = 0; i < Options.Boxes.Count; i++)
            {
                Boxes.Add(new BlockBoxViewModel(this, i, Options.Boxes[i]));
            }
        }

        if (Properties.Count != Options.Properties.Count || !Properties.Select(p => p.Property).SequenceEqual(Options.Properties))
        {
            Properties.Clear();
            for (int i = 0; i < Options.Properties.Count; i++)
            {
                Properties.Add(new BlockPropertyViewModel(this, i, Options.Properties[i]));
            }
        }

        OnPropertyChanged(string.Empty);
    }
}
