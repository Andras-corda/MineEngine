using MineEngine.Core.GameData;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// The classes of the game behind the block bases (V0.7.11): stairs, slab, fence, fence gate, wall,
/// door, trapdoor, pane, button and pressure plate. Their constructors changed in 1.20.5 (the type of
/// the block set comes first); the set follows the sounds: wood (oak), metal (iron: a door opened by
/// redstone only) or stone.
/// </summary>
internal static class BlockBaseJava
{
    public static string ClassName(BlockModelKind model) => model switch
    {
        BlockModelKind.Stairs => "StairBlock",
        BlockModelKind.Slab => "SlabBlock",
        BlockModelKind.Fence => "FenceBlock",
        BlockModelKind.FenceGate => "FenceGateBlock",
        BlockModelKind.Wall => "WallBlock",
        BlockModelKind.Door => "DoorBlock",
        BlockModelKind.Trapdoor => "TrapDoorBlock",
        BlockModelKind.Pane => "IronBarsBlock",
        BlockModelKind.Button => "ButtonBlock",
        BlockModelKind.PressurePlate => "PressurePlateBlock",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, null),
    };

    public static IEnumerable<string> Imports(BlockModelKind model)
    {
        yield return "net.minecraft.world.level.block." + ClassName(model);
        switch (model)
        {
            case BlockModelKind.Stairs:
                yield return "net.minecraft.world.level.block.Blocks";
                break;
            case BlockModelKind.FenceGate:
                yield return "net.minecraft.world.level.block.state.properties.WoodType";
                break;
            case BlockModelKind.Door or BlockModelKind.Trapdoor or BlockModelKind.Button or BlockModelKind.PressurePlate:
                yield return "net.minecraft.world.level.block.state.properties.BlockSetType";
                break;
        }
    }

    /// <summary>The arguments before and after the properties in the constructor of the class.</summary>
    public static (string Before, string After) Arguments(BlockModelKind model, BlockSoundType sound, bool modern)
    {
        bool wood = sound == BlockSoundType.Wood;
        string set = "BlockSetType." + (wood ? "OAK" : sound == BlockSoundType.Metal ? "IRON" : "STONE");
        int ticks = wood ? 30 : 20;
        return model switch
        {
            BlockModelKind.Stairs => ("Blocks.STONE.defaultBlockState(), ", string.Empty),
            BlockModelKind.FenceGate => modern ? ("WoodType.OAK, ", string.Empty) : (string.Empty, ", WoodType.OAK"),
            BlockModelKind.Door or BlockModelKind.Trapdoor => modern ? (set + ", ", string.Empty) : (string.Empty, ", " + set),
            BlockModelKind.Button => modern ? ($"{set}, {ticks}, ", string.Empty) : (string.Empty, $", {set}, {ticks}, {(wood ? "true" : "false")}"),
            BlockModelKind.PressurePlate => modern
                ? (set + ", ", string.Empty)
                : ($"PressurePlateBlock.Sensitivity.{(wood ? "EVERYTHING" : "MOBS")}, ", ", " + set),
            _ => (string.Empty, string.Empty),
        };
    }

    /// <summary>Calls of the properties the game gives to the base: no occlusion (holes), no collision (flat).</summary>
    public static IEnumerable<string> PropertyCalls(BlockModelKind model) => model switch
    {
        BlockModelKind.Door or BlockModelKind.Trapdoor or BlockModelKind.Pane => [".noOcclusion()"],
        BlockModelKind.Button or BlockModelKind.PressurePlate => [".noCollission()"],
        _ => [],
    };
}
