using MineEngine.Assets;
using MineEngine.Core.GameData;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Events of the game for the advanced properties of the items (V0.7.12): the burn time given by
/// functions of the project, the compostables of the composter, the condition to compost, and the
/// item events only the loader sees (an item that breaks, an item picked up).
/// </summary>
public abstract partial class ModJavaEmitter
{
    private const string ItemHooksClass = "ModItemHooks";

    /// <summary>Full name of the common setup event (mod bus).</summary>
    protected abstract string CommonSetupEventType { get; }

    /// <summary>Full name of the event that asks the burn time of a fuel (game bus).</summary>
    protected abstract string FurnaceFuelEventType { get; }

    /// <summary>Full name of the class of the events of a player who clicks (game bus).</summary>
    protected abstract string PlayerInteractEventType { get; }

    /// <summary>Full name of the class that gives the running server.</summary>
    protected abstract string ServerLifecycleHooksType { get; }

    /// <summary>The compostables are a file of data (NeoForge 1.21) rather than a map filled by the code.</summary>
    protected virtual bool CompostablesAreData => false;

    /// <summary>Full name of the event of an item that breaks in the hands of a player (game bus).</summary>
    protected abstract string PlayerDestroyItemEventType { get; }

    /// <summary>Event of an item picked up from the ground: its type, its import and how to read the player, the stack and the item entity.</summary>
    protected abstract (string Type, string Import, string Player, string Stack, string ItemEntity) ItemPickupEvent { get; }

    /// <summary>Items with graphs on an event of the loader, with the calls of these graphs.</summary>
    private List<(IRItem Item, List<string> Calls)> LoaderEventItems(GenerationContext context, JavaModNaming naming, ItemEventKind kind) =>
        [.. context.Ir.Content.Items
            .Select(i => (Item: i, Calls: ItemEventCalls(i, context, naming).Where(c => c.Event == kind).Select(c => c.Call).ToList()))
            .Where(i => i.Calls.Count > 0)];

    private static IEnumerable<IRItem> FuelHookItems(IRContent content) => content.Items.Where(i => i.HasFuelFunctions);

    private IEnumerable<IRItem> CompostSetupItems(IRContent content) =>
        CompostablesAreData ? [] : content.Items.Where(i => i.CompostChance > 0);

    private static IEnumerable<IRItem> CompostConditionItems(IRContent content) => content.Items.Where(i => i.HasCompostCondition);

    /// <summary>Call of a method of the class of an item, with its import, or null when the item has no such method.</summary>
    private static string? ItemMethodCall(IRItem item, string slot, string arguments, JavaModNaming naming, ISet<string> imports)
    {
        if (item.Method(slot) is not { } method)
        {
            return null;
        }

        imports.Add($"{ItemPackage(naming)}.{ItemClassName(item)}");
        return $"{ItemClassName(item)}.{method.JavaName}({arguments})";
    }

    private bool NeedsItemHooks(GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        return FuelHookItems(content).Any() || CompostSetupItems(content).Any() || CompostConditionItems(content).Any()
            || LoaderEventItems(context, naming, ItemEventKind.Broken).Count > 0 || LoaderEventItems(context, naming, ItemEventKind.PickedUp).Count > 0;
    }

    private IEnumerable<string> ItemHooksMainImports(GenerationContext context, JavaModNaming naming)
    {
        if (!NeedsItemHooks(context, naming))
        {
            yield break;
        }

        yield return $"{naming.RegistryPackage}.{ItemHooksClass}";
        yield return GameEventBus.Import;
    }

    private void WriteItemHooksRegistrations(JavaSourceWriter w, GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        string gameBus = GameEventBus.Expression;
        if (FuelHookItems(content).Any())
        {
            w.Line($"{gameBus}.addListener({ItemHooksClass}::onFuelBurnTime);");
        }

        if (CompostSetupItems(content).Any())
        {
            w.Line($"modEventBus.addListener({ItemHooksClass}::commonSetup);");
        }

        if (CompostConditionItems(content).Any())
        {
            w.Line($"{gameBus}.addListener({ItemHooksClass}::onRightClickBlock);");
        }

        if (LoaderEventItems(context, naming, ItemEventKind.Broken).Count > 0)
        {
            w.Line($"{gameBus}.addListener({ItemHooksClass}::onItemBroken);");
        }

        if (LoaderEventItems(context, naming, ItemEventKind.PickedUp).Count > 0)
        {
            w.Line($"{gameBus}.addListener({ItemHooksClass}::onItemPickedUp);");
        }
    }

    private void WriteItemHooksClass(GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        if (!NeedsItemHooks(context, naming))
        {
            return;
        }

        var names = new JavaProgramNames(naming, content.Definitions);
        string Item(IRItem item) => $"{naming.ItemsClass}.{naming.ConstantFor(item.Id)}.get()";
        var imports = new SortedSet<string>(StringComparer.Ordinal) { "net.minecraft.world.item.ItemStack", "net.minecraft.world.level.Level" };
        var body = new JavaSourceWriter();
        List<IRItem> fuels = [.. FuelHookItems(content)];
        if (fuels.Count > 0)
        {
            imports.UnionWith([FurnaceFuelEventType, ServerLifecycleHooksType, "net.minecraft.server.MinecraftServer"]);
            body.Line("/** Burn time of the fuels whose functions decide it; without a running server, the time entered stays. */")
                .OpenBlock($"public static void onFuelBurnTime({SimpleName(FurnaceFuelEventType)} event)")
                .Line($"MinecraftServer server = {SimpleName(ServerLifecycleHooksType)}.getCurrentServer();")
                .OpenBlock("if (server == null)").Line("return;").CloseBlock()
                .Line("Level level = server.overworld();")
                .Line("ItemStack stack = event.getItemStack();");
            foreach (IRItem item in fuels)
            {
                string condition = ItemMethodCall(item, ItemMethodSlots.BurnCondition, "level, null", naming, imports)
                    ?? FunctionCall(context, names, item.BurnCondition, "level", "null") ?? "true";
                string? time = ItemMethodCall(item, ItemMethodSlots.BurnTime, "level, null", naming, imports)
                    ?? FunctionCall(context, names, item.BurnTimeFunction, "level", "null");
                string ticks = time is null ? item.BurnTime.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"(int) Math.round((double) ({time}) * 20)";
                body.OpenBlock($"if (stack.is({Item(item)}))")
                    .Line($"event.setBurnTime({condition} ? {ticks} : 0);")
                    .CloseBlock();
            }

            body.CloseBlock();
        }

        List<IRItem> compostables = [.. CompostSetupItems(content)];
        if (compostables.Count > 0)
        {
            imports.UnionWith([CommonSetupEventType, "net.minecraft.world.level.block.ComposterBlock"]);
            body.Line().Line("/** The compostables of the mod, added to the map of the composter. */")
                .OpenBlock($"public static void commonSetup({SimpleName(CommonSetupEventType)} event)")
                .OpenBlock("event.enqueueWork(() ->");
            foreach (IRItem item in compostables)
            {
                body.Line($"ComposterBlock.COMPOSTABLES.put({Item(item)}, {JavaNames.FloatLiteral(item.CompostChance)});");
            }

            body.CloseBlock(");").CloseBlock();
        }

        List<IRItem> conditions = [.. CompostConditionItems(content)];
        if (conditions.Count > 0)
        {
            imports.UnionWith([PlayerInteractEventType, "net.minecraft.world.entity.player.Player", "net.minecraft.world.level.block.Blocks"]);
            body.Line().Line("/** A compostable goes into a composter only when its function says yes. */")
                .OpenBlock($"public static void onRightClickBlock({SimpleName(PlayerInteractEventType)}.RightClickBlock event)")
                .Line("Level level = event.getLevel();")
                .OpenBlock("if (level.isClientSide() || !level.getBlockState(event.getPos()).is(Blocks.COMPOSTER))").Line("return;").CloseBlock()
                .Line("Player player = event.getEntity();")
                .Line("ItemStack stack = event.getItemStack();");
            foreach (IRItem item in conditions)
            {
                string condition = ItemMethodCall(item, ItemMethodSlots.CompostCondition, "level, player", naming, imports)
                    ?? FunctionCall(context, names, item.CompostCondition, "level", "player") ?? "true";
                body.OpenBlock($"if (stack.is({Item(item)}) && !({condition}))")
                    .Line("event.setCanceled(true);")
                    .CloseBlock();
            }

            body.CloseBlock();
        }

        // Item events of V0.7.12 that only the loader sees: the graphs run on the server.
        void Behaviors(IEnumerable<string> calls) => imports.UnionWith(calls.Select(c => CallImport(c, naming)));
        List<(IRItem Item, List<string> Calls)> broken = LoaderEventItems(context, naming, ItemEventKind.Broken);
        if (broken.Count > 0)
        {
            imports.UnionWith([PlayerDestroyItemEventType, "net.minecraft.world.entity.player.Player"]);
            body.Line().Line("/** An item of the mod breaks in the hands of a player. */")
                .OpenBlock($"public static void onItemBroken({SimpleName(PlayerDestroyItemEventType)} event)")
                .Line("Player player = event.getEntity();")
                .Line("Level level = player.level();")
                .OpenBlock("if (level.isClientSide())").Line("return;").CloseBlock()
                .Line("ItemStack stack = event.getOriginal();");
            foreach ((IRItem item, List<string> calls) in broken)
            {
                Behaviors(calls);
                body.OpenBlock($"if (stack.is({Item(item)}))");
                foreach (string call in calls)
                {
                    body.Line(EventCall(call, "player", "level", "player.blockPosition()"));
                }

                body.CloseBlock();
            }

            body.CloseBlock();
        }

        List<(IRItem Item, List<string> Calls)> picked = LoaderEventItems(context, naming, ItemEventKind.PickedUp);
        if (picked.Count > 0)
        {
            (string type, string import, string player, string stack, string itemEntity) = ItemPickupEvent;
            imports.UnionWith([import, "net.minecraft.world.entity.player.Player"]);
            body.Line().Line("/** A player picks an item of the mod up from the ground. */")
                .OpenBlock($"public static void onItemPickedUp({type} event)")
                .Line($"Player player = {player};")
                .Line("Level level = player.level();")
                .OpenBlock("if (level.isClientSide())").Line("return;").CloseBlock()
                .Line($"ItemStack stack = {stack};");
            foreach ((IRItem item, List<string> calls) in picked)
            {
                Behaviors(calls);
                body.OpenBlock($"if (stack.is({Item(item)}))");
                foreach (string call in calls)
                {
                    body.Line(EventCall(call, "player", "level", "player.blockPosition()", itemEntity, "stack.getCount()"));
                }

                body.CloseBlock();
            }

            body.CloseBlock();
        }

        // The imports are known once the methods are written: the header comes after them.
        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Événements du jeu pour les propriétés avancées des items.")
            .OpenBlock($"public final class {ItemHooksClass}");
        foreach (string line in body.ToString().Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
        {
            w.Line(line);
        }

        Write(context, naming, naming.RegistryPackage, ItemHooksClass, WritePrivateConstructor(w, ItemHooksClass));
    }
}
