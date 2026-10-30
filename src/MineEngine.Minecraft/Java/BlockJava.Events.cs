using MineEngine.Core.GameData;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Calls of the graphs that react to the events of a block (V0.7.11), by event: "RuneEventsBehavior.onBlockPlaced1".
/// Every method takes the level, the position of the block, the player, the other entity and an amount.
/// </summary>
public sealed class BlockEventCalls(IReadOnlyDictionary<BlockEventKind, IReadOnlyList<string>> calls, IReadOnlyList<string> imports)
{
    public static BlockEventCalls None { get; } = new(new Dictionary<BlockEventKind, IReadOnlyList<string>>(), []);

    public IReadOnlyList<string> Imports => imports;

    public bool Any => calls.Count > 0;

    public bool Has(BlockEventKind kind) => calls.ContainsKey(kind);

    /// <summary>One line per graph of the event; nothing when no graph reacts to it.</summary>
    public void Write(JavaSourceWriter w, BlockEventKind kind, string player, string other, string amount)
    {
        foreach (string call in calls.TryGetValue(kind, out IReadOnlyList<string>? list) ? list : [])
        {
            w.Line($"{call}(level, position, {player}, {other}, {amount});");
        }
    }
}

/// <summary>
/// Events and triggers of a block (V0.7.11, block.md): each event is a method of the game, written
/// public (1.20.1 and 1.21.1 accept it), that calls the graphs on the server only. The tick is
/// scheduled when the block is added, or random like the crops; the redstone is remembered in a
/// property of the block state so that on and off run once per change.
/// </summary>
public sealed partial class BlockJava
{
    private bool TracksRedstone => _events.Has(BlockEventKind.RedstoneOn) || _events.Has(BlockEventKind.RedstoneOff);

    private bool ScheduledTicks => _events.Has(BlockEventKind.Tick) && _options.TickEnabled && !_options.TickRandomly;

    private static bool HasEvents(IRBlock block, IRContent content) =>
        content.Behaviors.Any(b => b.Handlers.Any(h => h.Block == block.Id.Value));

    private static string PlayerOf(string entity) => $"({entity} instanceof Player ? (Player) {entity} : null)";

    private void WriteEvents(JavaSourceWriter w)
    {
        if (!_events.Any)
        {
            return;
        }

        Use([.. _events.Imports]);
        Use("net.minecraft.core.BlockPos", "net.minecraft.world.level.Level", "net.minecraft.world.entity.Entity", "net.minecraft.world.entity.player.Player");
        WriteRightClick(w);
        WritePlacementEvents(w);
        WriteTicks(w);
        WriteNeighbor(w);
        WriteDestruction(w);
        WriteContacts(w);
    }

    /// <summary>Opens an override that runs on the server: the line before (super call), then the guard.</summary>
    private void Open(JavaSourceWriter w, string signature, string before, string guard = "!level.isClientSide")
    {
        w.Line().Line("@Override").OpenBlock(signature);
        if (before.Length > 0)
        {
            w.Line(before);
        }

        w.OpenBlock($"if ({guard})");
    }

    private void WriteRightClick(JavaSourceWriter w)
    {
        if (!_events.Has(BlockEventKind.RightClicked))
        {
            return;
        }

        // A click the block does not use becomes a success, so that the hand swings and nothing else is used.
        Use("net.minecraft.world.InteractionResult", "net.minecraft.world.phys.BlockHitResult");
        if (Modern)
        {
            Open(w, "public InteractionResult useWithoutItem(BlockState state, Level level, BlockPos position, Player player, BlockHitResult hit)",
                "InteractionResult result = super.useWithoutItem(state, level, position, player, hit);");
        }
        else
        {
            Use("net.minecraft.world.InteractionHand");
            Open(w, "public InteractionResult use(BlockState state, Level level, BlockPos position, Player player, InteractionHand hand, BlockHitResult hit)",
                "InteractionResult result = super.use(state, level, position, player, hand, hit);", "!level.isClientSide && hand == InteractionHand.MAIN_HAND");
        }

        _events.Write(w, BlockEventKind.RightClicked, "player", "player", "0.0");
        w.CloseBlock()
            .Line()
            .Line("return result == InteractionResult.PASS ? InteractionResult.sidedSuccess(level.isClientSide) : result;")
            .CloseBlock();
    }

    /// <summary>Added (and the first scheduled tick), and placed by a player or a mob.</summary>
    private void WritePlacementEvents(JavaSourceWriter w)
    {
        if (_events.Has(BlockEventKind.Added) || ScheduledTicks)
        {
            Open(w, "public void onPlace(BlockState state, Level level, BlockPos position, BlockState oldState, boolean moved)",
                "super.onPlace(state, level, position, oldState, moved);", "!level.isClientSide && !oldState.is(state.getBlock())");
            if (ScheduledTicks)
            {
                w.Line($"level.scheduleTick(position, this, {Math.Max(1, _options.TickRate)});");
            }

            _events.Write(w, BlockEventKind.Added, "null", "null", "0.0");
            w.CloseBlock().CloseBlock();
        }

        if (_events.Has(BlockEventKind.Placed))
        {
            Use("net.minecraft.world.entity.LivingEntity", "net.minecraft.world.item.ItemStack");
            Open(w, "public void setPlacedBy(Level level, BlockPos position, BlockState state, LivingEntity placer, ItemStack stack)",
                "super.setPlacedBy(level, position, state, placer, stack);");
            _events.Write(w, BlockEventKind.Placed, PlayerOf("placer"), "placer", "0.0");
            w.CloseBlock().CloseBlock();
        }
    }

    private void WriteTicks(JavaSourceWriter w)
    {
        if (!_events.Has(BlockEventKind.Tick) || !_options.TickEnabled)
        {
            return;
        }

        Use("net.minecraft.server.level.ServerLevel", "net.minecraft.util.RandomSource");
        string method = _options.TickRandomly ? "randomTick" : "tick";
        w.Line()
            .Line("@Override")
            .OpenBlock($"public void {method}(BlockState state, ServerLevel level, BlockPos position, RandomSource random)")
            .Line($"super.{method}(state, level, position, random);");
        _events.Write(w, BlockEventKind.Tick, "null", "null", "0.0");
        if (!_options.TickRandomly)
        {
            w.Line($"level.scheduleTick(position, this, {Math.Max(1, _options.TickRate)});");
        }

        w.CloseBlock();
    }

    /// <summary>A neighbor changes; the signal it gives turns the redstone on or off.</summary>
    private void WriteNeighbor(JavaSourceWriter w)
    {
        if (!_events.Has(BlockEventKind.NeighborChanged) && !TracksRedstone)
        {
            return;
        }

        Use("net.minecraft.world.level.block.Block");
        Open(w, "public void neighborChanged(BlockState state, Level level, BlockPos position, Block neighbor, BlockPos neighborPosition, boolean moved)",
            "super.neighborChanged(state, level, position, neighbor, neighborPosition, moved);");
        _events.Write(w, BlockEventKind.NeighborChanged, "null", "null", "0.0");
        if (TracksRedstone)
        {
            w.Line("BlockState current = level.getBlockState(position);")
                .Line("boolean powered = level.hasNeighborSignal(position);")
                .OpenBlock("if (current.is(this) && powered != current.getValue(REDSTONE_POWERED))")
                .Line("level.setBlock(position, current.setValue(REDSTONE_POWERED, powered), Block.UPDATE_CLIENTS);")
                .OpenBlock("if (powered)");
            _events.Write(w, BlockEventKind.RedstoneOn, "null", "null", "level.getBestNeighborSignal(position)");
            w.CloseBlock().OpenBlock("else");
            _events.Write(w, BlockEventKind.RedstoneOff, "null", "null", "0.0");
            w.CloseBlock().CloseBlock();
        }

        w.CloseBlock().CloseBlock();
    }

    private void WriteDestruction(JavaSourceWriter w)
    {
        if (_events.Has(BlockEventKind.Destroyed))
        {
            // Run before the block entity and the neighbors forget it.
            w.Line().Line("@Override")
                .OpenBlock("public void onRemove(BlockState state, Level level, BlockPos position, BlockState newState, boolean moved)")
                .OpenBlock("if (!level.isClientSide && !state.is(newState.getBlock()))");
            _events.Write(w, BlockEventKind.Destroyed, "null", "null", "0.0");
            w.CloseBlock()
                .Line()
                .Line("super.onRemove(state, level, position, newState, moved);")
                .CloseBlock();
        }

        if (_events.Has(BlockEventKind.DestroyedByPlayer))
        {
            Use("net.minecraft.world.item.ItemStack", "net.minecraft.world.level.block.entity.BlockEntity");
            Open(w, "public void playerDestroy(Level level, Player player, BlockPos position, BlockState state, BlockEntity blockEntity, ItemStack tool)",
                "super.playerDestroy(level, player, position, state, blockEntity, tool);");
            _events.Write(w, BlockEventKind.DestroyedByPlayer, "player", "player", "0.0");
            w.CloseBlock().CloseBlock();
        }

        if (_events.Has(BlockEventKind.DestroyedByExplosion))
        {
            Use("net.minecraft.world.level.Explosion");
            Open(w, "public void wasExploded(Level level, BlockPos position, Explosion explosion)", "super.wasExploded(level, position, explosion);");
            _events.Write(w, BlockEventKind.DestroyedByExplosion, "null", "null", "0.0");
            w.CloseBlock().CloseBlock();
        }

        if (_events.Has(BlockEventKind.StartDestroy))
        {
            Open(w, "public void attack(BlockState state, Level level, BlockPos position, Player player)", "super.attack(state, level, position, player);");
            _events.Write(w, BlockEventKind.StartDestroy, "player", "player", "0.0");
            w.CloseBlock().CloseBlock();
        }
    }

    /// <summary>Entities inside the block, walking or falling on it, and the projectiles that hit it.</summary>
    private void WriteContacts(JavaSourceWriter w)
    {
        void Pair(BlockEventKind entityEvent, BlockEventKind playerEvent, string signature, string before)
        {
            if (!_events.Has(entityEvent) && !_events.Has(playerEvent))
            {
                return;
            }

            Open(w, signature, before);
            _events.Write(w, entityEvent, PlayerOf("entity"), "entity", "0.0");
            if (_events.Has(playerEvent))
            {
                w.OpenBlock("if (entity instanceof Player)");
                _events.Write(w, playerEvent, "(Player) entity", "entity", "0.0");
                w.CloseBlock();
            }

            w.CloseBlock().CloseBlock();
        }

        Pair(BlockEventKind.EntityCollides, BlockEventKind.PlayerCollides, "public void entityInside(BlockState state, Level level, BlockPos position, Entity entity)",
            "super.entityInside(state, level, position, entity);");
        Pair(BlockEventKind.EntityWalksOn, BlockEventKind.PlayerWalksOn, "public void stepOn(Level level, BlockPos position, BlockState state, Entity entity)",
            "super.stepOn(level, position, state, entity);");

        if (_events.Has(BlockEventKind.EntityFallsOn))
        {
            Open(w, "public void fallOn(Level level, BlockState state, BlockPos position, Entity entity, float distance)",
                "super.fallOn(level, state, position, entity, distance);");
            _events.Write(w, BlockEventKind.EntityFallsOn, PlayerOf("entity"), "entity", "distance");
            w.CloseBlock().CloseBlock();
        }

        if (_events.Has(BlockEventKind.HitByProjectile))
        {
            Use("net.minecraft.world.entity.projectile.Projectile", "net.minecraft.world.phys.BlockHitResult");
            Open(w, "public void onProjectileHit(Level level, BlockState state, BlockHitResult hit, Projectile projectile)",
                "super.onProjectileHit(level, state, hit, projectile);");
            w.Line("BlockPos position = hit.getBlockPos();");
            _events.Write(w, BlockEventKind.HitByProjectile, PlayerOf("projectile.getOwner()"), "projectile", "0.0");
            w.CloseBlock().CloseBlock();
        }
    }
}
