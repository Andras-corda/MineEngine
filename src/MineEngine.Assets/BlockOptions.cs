using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MineEngine.Assets;

/// <summary>How a block turns when it is placed.</summary>
public enum BlockRotation
{
    /// <summary>Always the same orientation.</summary>
    None,

    /// <summary>Faces the player, horizontally (north, south, east, west), like a furnace.</summary>
    Horizontal,

    /// <summary>Faces the player in the six directions, like a dispenser.</summary>
    AllDirections,
}

/// <summary>Random shift of the model, like the grass and the flowers.</summary>
public enum BlockOffset
{
    None,
    XZ,
    XYZ,
}

/// <summary>What a piston does to the block.</summary>
public enum BlockPushReaction
{
    Normal,
    Destroy,
    Block,
    PushOnly,
    Ignore,
}

/// <summary>Kind of a property of the block state added by the user.</summary>
public enum BlockPropertyKind
{
    /// <summary>true or false ("lit").</summary>
    Boolean,

    /// <summary>A whole number between a minimum and a maximum ("age", 0 to 7).</summary>
    Integer,

    /// <summary>One value among names ("color": red, green, blue).</summary>
    Choice,
}

/// <summary>Shape of the heights of an ore: the same chance everywhere, or more often in the middle.</summary>
public enum OreShape
{
    Uniform,
    Triangle,
}

/// <summary>A box of the shape of the block, in pixels (0 to 16); a subtracted box makes a hole in the others.</summary>
public sealed record BlockBox(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ, bool Subtract = false)
{
    public static BlockBox Full { get; } = new(0, 0, 0, 16, 16, 16);
}

/// <summary>A property of the block state added by the user, readable and changeable by the graphs (State property nodes).</summary>
public sealed record BlockCustomProperty(string Name, BlockPropertyKind Kind, int Min = 0, int Max = 1)
{
    /// <summary>Values of a choice property ("red", "green").</summary>
    public IReadOnlyList<string> Values { get; init; } = [];

    /// <summary>Number of values the property can take (for the count of combinations).</summary>
    public int Count => Kind switch
    {
        BlockPropertyKind.Boolean => 2,
        BlockPropertyKind.Integer => Math.Max(1, Max - Min + 1),
        _ => Math.Max(1, Values.Count),
    };
}

/// <summary>Generation of the block as an ore in the world (like the ore features of MCreator).</summary>
public sealed record BlockOreGeneration
{
    public bool Enabled { get; init; }

    /// <summary>Blocks or tags the ore replaces ("#minecraft:stone_ore_replaceables", "minecraft:deepslate").</summary>
    public IReadOnlyList<string> Replace { get; init; } = ["#minecraft:stone_ore_replaceables"];

    /// <summary>Biomes or tags where the ore is generated; empty: every biome of the overworld.</summary>
    public IReadOnlyList<string> Biomes { get; init; } = [];

    public OreShape Shape { get; init; } = OreShape.Uniform;

    /// <summary>Groups of ores per chunk (more than 32 may make the generation crash).</summary>
    public int GroupsPerChunk { get; init; } = 10;

    /// <summary>Ores per group.</summary>
    public int GroupSize { get; init; } = 16;

    public int MinHeight { get; init; }

    public int MaxHeight { get; init; } = 64;
}

/// <summary>
/// Advanced settings of a block, like the block editor of MCreator: rotation, water, gravity,
/// rendering, shape, redstone, fire, beacon, ladder, plants, item and particle textures, properties
/// of the block state and ore generation. Immutable: a change is one step of the history.
/// </summary>
public sealed record BlockOptions
{
    public const int MaxRedstonePower = 15;

    public static BlockOptions Default { get; } = new();

    public BlockRotation Rotation { get; init; } = BlockRotation.None;

    /// <summary>A horizontal block can also be placed on the floor, the walls and the ceiling (like a button).</summary>
    public bool HasPitchRotation { get; init; }

    /// <summary>The block can hold water, like a slab or a fence.</summary>
    public bool Waterloggable { get; init; }

    /// <summary>The block falls like sand.</summary>
    public bool HasGravity { get; init; }

    /// <summary>The block is drawn bright even in the dark (glow).</summary>
    public bool Emissive { get; init; }

    /// <summary>Another block can be placed in its place, like the tall grass.</summary>
    public bool Replaceable { get; init; }

    /// <summary>The entities go through the block (no collision).</summary>
    public bool WalkThrough { get; init; }

    public bool CustomLightOpacity { get; init; }

    /// <summary>Light stopped by the block (0: lets every light through, 15: opaque).</summary>
    public int LightOpacity { get; init; } = 15;

    /// <summary>The faces between two blocks of this type are hidden (like glass).</summary>
    public bool ConnectedSides { get; init; }

    public BlockOffset RandomOffset { get; init; } = BlockOffset.None;

    /// <summary>Color of the block on the maps ("COLOR_RED", "STONE"...); empty: the default one.</summary>
    public string MapColor { get; init; } = string.Empty;

    /// <summary>Instrument of a note block placed on it ("HARP", "BASS"...); empty: the default one.</summary>
    public string Instrument { get; init; } = string.Empty;

    public BlockPushReaction PushReaction { get; init; } = BlockPushReaction.Normal;

    /// <summary>The entities can climb the block, like a ladder.</summary>
    public bool Ladder { get; init; }

    /// <summary>Bonus given to a nearby enchanting table (a bookshelf gives 1).</summary>
    public float EnchantPowerBonus { get; init; }

    public bool IgnitedByLava { get; init; }

    /// <summary>Chance to burn (0: never; wool: 60).</summary>
    public int Flammability { get; init; }

    /// <summary>How fast the fire spreads from it (0: default; wool: 30).</summary>
    public int FireSpreadSpeed { get; init; }

    /// <summary>The redstone dust connects to the block.</summary>
    public bool RedstoneConnects { get; init; }

    /// <summary>The block powers the redstone around it.</summary>
    public bool EmitsRedstone { get; init; }

    public int RedstonePower { get; init; } = MaxRedstonePower;

    /// <summary>Saplings and flowers can be planted on it (it is in the tag minecraft:dirt).</summary>
    public bool PlantsCanSustain { get; init; }

    /// <summary>Color given to the beam of a beacon that goes through it ("RED"...); empty: none.</summary>
    public string BeaconColor { get; init; } = string.Empty;

    /// <summary>Texture of the block in the inventory and in hand; null: the block itself.</summary>
    public Guid? ItemTextureId { get; init; }

    /// <summary>Texture of the particles when the block breaks; null: its main texture.</summary>
    public Guid? ParticleTextureId { get; init; }

    /// <summary>Shape of the block (collisions and outline); empty: the full block.</summary>
    public IReadOnlyList<BlockBox> Boxes { get; init; } = [];

    /// <summary>Properties of the block state added by the user.</summary>
    public IReadOnlyList<BlockCustomProperty> Properties { get; init; } = [];

    public BlockOreGeneration Ore { get; init; } = new();

    /// <summary>The block ticks (V0.7.11): the "tick" event of its graphs runs.</summary>
    public bool TickEnabled { get; init; }

    /// <summary>It ticks at random, like crops, instead of every <see cref="TickRate"/> ticks.</summary>
    public bool TickRandomly { get; init; }

    /// <summary>Ticks between two ticks of the block (20: one second).</summary>
    public int TickRate { get; init; } = 20;

    /// <summary>Number of combinations of the custom properties (keep it small: every one is a state of the block).</summary>
    public long Combinations => Properties.Aggregate(1L, (total, p) => total * p.Count);

    /// <summary>True when the block needs a Java class of its own (rotation, water, redstone...).</summary>
    public bool NeedsOwnClass => Rotation != BlockRotation.None || Waterloggable || HasGravity || CustomLightOpacity || ConnectedSides
                                 || EnchantPowerBonus > 0 || Flammability > 0 || FireSpreadSpeed > 0 || RedstoneConnects || EmitsRedstone
                                 || BeaconColor.Length > 0 || Boxes.Count > 0 || Properties.Count > 0;

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public JsonNode? ToJson() => JsonSerializer.SerializeToNode(this, Json);

    /// <summary>Options read from a project file; the default ones when the data is missing or unreadable.</summary>
    public static BlockOptions FromJson(JsonNode? node)
    {
        if (node is null)
        {
            return Default;
        }

        try
        {
            return node.Deserialize<BlockOptions>(Json) ?? Default;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            return Default;
        }
    }

    /// <summary>Names of the map colors of Minecraft.</summary>
    public static IReadOnlyList<string> MapColors { get; } =
    [
        "NONE", "GRASS", "SAND", "WOOL", "FIRE", "ICE", "METAL", "PLANT", "SNOW", "CLAY", "DIRT", "STONE", "WATER", "WOOD", "QUARTZ",
        "COLOR_ORANGE", "COLOR_MAGENTA", "COLOR_LIGHT_BLUE", "COLOR_YELLOW", "COLOR_LIGHT_GREEN", "COLOR_PINK", "COLOR_GRAY", "COLOR_LIGHT_GRAY",
        "COLOR_CYAN", "COLOR_PURPLE", "COLOR_BLUE", "COLOR_BROWN", "COLOR_GREEN", "COLOR_RED", "COLOR_BLACK", "GOLD", "DIAMOND", "LAPIS",
        "EMERALD", "PODZOL", "NETHER", "TERRACOTTA_WHITE", "CRIMSON_NYLIUM", "WARPED_NYLIUM", "DEEPSLATE", "RAW_IRON", "GLOW_LICHEN",
    ];

    /// <summary>Names of the instruments of the note blocks.</summary>
    public static IReadOnlyList<string> Instruments { get; } =
    [
        "HARP", "BASEDRUM", "SNARE", "HAT", "BASS", "FLUTE", "BELL", "GUITAR", "CHIME", "XYLOPHONE", "IRON_XYLOPHONE", "COW_BELL",
        "DIDGERIDOO", "BIT", "BANJO", "PLING",
    ];

    /// <summary>Names of the dye colors (beacon beam).</summary>
    public static IReadOnlyList<string> DyeColors { get; } =
    [
        "WHITE", "ORANGE", "MAGENTA", "LIGHT_BLUE", "YELLOW", "LIME", "PINK", "GRAY", "LIGHT_GRAY", "CYAN", "PURPLE", "BLUE", "BROWN",
        "GREEN", "RED", "BLACK",
    ];
}
