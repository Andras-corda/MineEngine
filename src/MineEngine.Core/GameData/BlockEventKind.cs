namespace MineEngine.Core.GameData;

/// <summary>
/// Event of a block of the mod (V0.7.11, block.md): its graph runs on the server when it happens to
/// the block. The tick needs the ticks of the block (Block properties).
/// </summary>
public enum BlockEventKind
{
    RightClicked,
    Added,
    NeighborChanged,
    Tick,
    Destroyed,
    DestroyedByPlayer,
    DestroyedByExplosion,
    StartDestroy,
    EntityCollides,
    PlayerCollides,
    EntityWalksOn,
    PlayerWalksOn,
    EntityFallsOn,
    HitByProjectile,
    Placed,
    RedstoneOn,
    RedstoneOff,
}
