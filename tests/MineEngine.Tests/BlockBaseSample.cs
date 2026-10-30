using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// The block bases of V0.7.11: one block of each base. The wooden ones (stairs that burn, so with a
/// class of their own, door, trapdoor, button) open by hand; the metal pressure plate and the stone
/// ones follow their sounds. Added with the samples, so that the end-to-end builds compile them.
/// </summary>
internal static class BlockBaseSample
{
    public static IReadOnlyList<BlockAsset> AddTo(ModProject project)
    {
        Guid texture = project.Assets.OfType<TextureAsset>().First().Id;
        BlockAsset Base(string id, BlockModelKind model, BlockSoundType sound) =>
            new(Guid.NewGuid(), ResourceId.Parse(id), id) { Model = model, Sound = sound, TextureId = texture };

        BlockAsset[] blocks =
        [
            Base("oak_steps", BlockModelKind.Stairs, BlockSoundType.Wood),
            Base("rune_slab", BlockModelKind.Slab, BlockSoundType.Stone),
            Base("rune_fence", BlockModelKind.Fence, BlockSoundType.Wood),
            Base("rune_gate", BlockModelKind.FenceGate, BlockSoundType.Wood),
            Base("rune_wall", BlockModelKind.Wall, BlockSoundType.Stone),
            Base("rune_door", BlockModelKind.Door, BlockSoundType.Wood),
            Base("rune_hatch", BlockModelKind.Trapdoor, BlockSoundType.Wood),
            Base("rune_pane", BlockModelKind.Pane, BlockSoundType.Glass),
            Base("rune_button", BlockModelKind.Button, BlockSoundType.Wood),
            Base("rune_plate", BlockModelKind.PressurePlate, BlockSoundType.Metal),
        ];

        // Burning stairs need a class of their own, on top of the stairs of the game; the rotation is ignored.
        blocks[0].Options = new BlockOptions { Flammability = 20, FireSpreadSpeed = 5, Rotation = BlockRotation.Horizontal };

        // V0.7.11 step 3: the slab reacts to every event of a block and ticks every second.
        blocks[1].Options = new BlockOptions { TickEnabled = true, TickRate = 20 };
        foreach (BlockAsset block in blocks)
        {
            project.Assets.Add(block);
        }

        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("rune_events"), "Rune events") { Graph = EventsGraph() });
        return blocks;
    }

    /// <summary>
    /// Each event of the slab sends a message to its player, or makes an explosion at the block when it
    /// gives no player; the wall (named with the namespace of the mod) sends a message when placed.
    /// </summary>
    private static NodeGraph EventsGraph()
    {
        var builder = new GraphBuilder();
        int row = 0;
        foreach (BlockEventKind kind in Enum.GetValues<BlockEventKind>())
        {
            NodeInfo info = NodeCatalog.ForBlockEvent(kind);
            GraphNode node = builder.Add(info.Kind, 0, row++ * 200, ("block", "rune_slab"));
            if (info.Outputs.Any(o => o.Name == "player"))
            {
                GraphNode message = builder.Add(GraphNodeKind.SendMessage, 300, row * 200);
                builder.Exec(node, message).Data(node, "player", message, "player");
            }
            else
            {
                GraphNode boom = builder.Add(GraphNodeKind.Explosion, 300, row * 200, ("power", "1"));
                builder.Exec(node, boom).Data(node, "position", boom, "position");
            }
        }

        GraphNode placed = builder.Add(GraphNodeKind.OnBlockPlaced, 0, row * 200, ("block", "testmod:rune_wall"));
        GraphNode hello = builder.Add(GraphNodeKind.SendMessage, 300, row * 200);
        builder.Exec(placed, hello).Data(placed, "player", hello, "player");
        return builder.Build();
    }
}
