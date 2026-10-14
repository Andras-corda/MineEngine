using MineEngine.Core.GameData;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Partie "items avancés" du code Java (V0.4.1) : construction des outils, armes et armures,
/// méthodes redéfinies dans la classe de l'item (item rendu après fabrication, combustible)
/// et appels aux graphes reliés aux événements de l'item (clic droit, frappe, fabrication...).
/// </summary>
public abstract partial class ModJavaEmitter
{
    /// <summary>
    /// Méthode qui fait de l'item un combustible, ou null si le loader déclare les combustibles
    /// autrement (NeoForge 1.21 : data map furnace_fuels, écrite par un émetteur de ressources).
    /// </summary>
    protected virtual IReadOnlyList<string>? BurnTimeOverride(int ticks) => null;

    /// <summary>Imports de <see cref="BurnTimeOverride"/>.</summary>
    protected virtual IReadOnlyList<string> BurnTimeImports => [];

    /// <summary>Début, appels de propriétés supplémentaires et fin de la construction d'un item.</summary>
    private static (string Head, IReadOnlyList<string> PropertyCalls, string Tail) ItemConstruction(IRItem item, JavaGameApi api) =>
        item.Kind.IsEquipment() ? api.EquipmentConstruction(item) : ("new Item(", [], ")");

    private static IEnumerable<string> ItemConstructionImports(IRItem item) => item.Kind switch
    {
        ItemKind.Simple => [],
        _ when item.Kind.IsArmor() => ["net.minecraft.world.item.ArmorItem", "net.minecraft.world.item.ArmorMaterials"],
        _ => [$"net.minecraft.world.item.{item.Kind}Item", "net.minecraft.world.item.Tiers"],
    };

    /// <summary>Méthodes ajoutées à la classe de l'item pour ses propriétés avancées et ses événements.</summary>
    private List<string> ItemBehaviourOverrides(IRItem item, GenerationContext context, JavaModNaming naming, JavaGameApi api, ISet<string> imports)
    {
        var lines = new List<string>();
        void Add(params string[] block)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(block);
        }

        if (item.CraftRemainder is { } remainder)
        {
            imports.UnionWith(["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.world.item.ItemStack"]);
            Add(
                "@Override",
                "public boolean hasCraftingRemainingItem(ItemStack stack) {",
                "    return true;",
                "}",
                string.Empty,
                "@Override",
                "public ItemStack getCraftingRemainingItem(ItemStack stack) {",
                $"    return new ItemStack(BuiltInRegistries.ITEM.get({api.ParseResourceLocation(JavaNames.StringLiteral(remainder.ToString()))}));",
                "}");
        }

        // Ni Properties.setNoRepair() ni isRepairable n'existent dans Forge 1.21.1 : on refuse le
        // matériau de réparation à l'enclume (méthode du jeu de base, présente partout).
        if (item.NoRepair)
        {
            imports.Add("net.minecraft.world.item.ItemStack");
            Add("@Override", "public boolean isValidRepairItem(ItemStack stack, ItemStack repairCandidate) {", "    return false;", "}");
        }

        if (item.BurnTime > 0 && BurnTimeOverride(item.BurnTime) is { } burn)
        {
            imports.UnionWith(BurnTimeImports);
            Add([.. burn]);
        }

        string itemId = context.Paths.Reference(item.Id.Value);
        List<(ItemEventKind Event, string Call)> calls = [.. context.Ir.Content.Behaviors
            .SelectMany(b => HandlerMethods(b)
                .Where(h => h.Handler.Item?.ToString() == itemId)
                .Select(h => (Event: NodeEvent(h.Handler), Call: $"{naming.BehaviorClassFor(b.Id)}.{h.Method}")))
            .Where(c => c.Event is not null)
            .Select(c => (c.Event!.Value, c.Call))];
        foreach (IGrouping<ItemEventKind, string> group in calls.GroupBy(c => c.Event, c => c.Call).OrderBy(g => g.Key))
        {
            // Chaque appel est "Classe.méthode" : la classe du graphe est importée.
            imports.UnionWith(group.Select(call => $"{naming.BehaviorPackage}.{call[..call.IndexOf('.')]}"));
            imports.UnionWith(ItemEventImports(group.Key));
            Add([.. ItemEventOverride(group.Key, group.ToList())]);
        }

        return lines;
    }

    private static ItemEventKind? NodeEvent(IREventHandler handler) => Assets.Graphs.NodeCatalog.Get(handler.Event).ItemEvent;

    private static IEnumerable<string> ItemEventImports(ItemEventKind kind) =>
        new[] { "net.minecraft.world.entity.player.Player", "net.minecraft.world.level.Level", "net.minecraft.world.item.ItemStack" }
            .Concat(kind switch
            {
                ItemEventKind.RightClick => ["net.minecraft.world.InteractionHand", "net.minecraft.world.InteractionResultHolder"],
                ItemEventKind.UseOnBlock => ["net.minecraft.world.InteractionResult", "net.minecraft.world.item.context.UseOnContext"],
                ItemEventKind.HitEntity or ItemEventKind.Consumed => ["net.minecraft.world.entity.LivingEntity"],
                ItemEventKind.BlockMined => ["net.minecraft.world.entity.LivingEntity", "net.minecraft.core.BlockPos", "net.minecraft.world.level.block.state.BlockState"],
                ItemEventKind.HeldTick => ["net.minecraft.world.entity.Entity"],
                _ => [],
            });

    /// <summary>
    /// Méthode de l'item qui appelle les graphes reliés à un événement. Les graphes ne
    /// s'exécutent que côté serveur ; le comportement normal de l'item est conservé (super).
    /// </summary>
    private static IEnumerable<string> ItemEventOverride(ItemEventKind kind, IReadOnlyList<string> calls)
    {
        IEnumerable<string> Call(string player, string level, string position) =>
            calls.Select(c => $"        {c}({player}, {level}, {position});");

        return kind switch
        {
            ItemEventKind.RightClick =>
            [
                "@Override",
                "public InteractionResultHolder<ItemStack> use(Level level, Player player, InteractionHand hand) {",
                "    if (!level.isClientSide()) {",
                .. Call("player", "level", "player.blockPosition()"),
                "    }",
                "    return super.use(level, player, hand);",
                "}",
            ],
            ItemEventKind.UseOnBlock =>
            [
                "@Override",
                "public InteractionResult useOn(UseOnContext context) {",
                "    Player player = context.getPlayer();",
                "    if (player != null && !context.getLevel().isClientSide()) {",
                .. Call("player", "context.getLevel()", "context.getClickedPos()"),
                "    }",
                "    return InteractionResult.sidedSuccess(context.getLevel().isClientSide());",
                "}",
            ],
            ItemEventKind.HitEntity =>
            [
                "@Override",
                "public boolean hurtEnemy(ItemStack stack, LivingEntity target, LivingEntity attacker) {",
                "    if (attacker instanceof Player player && !attacker.level().isClientSide()) {",
                .. Call("player", "attacker.level()", "target.blockPosition()"),
                "    }",
                "    return super.hurtEnemy(stack, target, attacker);",
                "}",
            ],
            ItemEventKind.BlockMined =>
            [
                "@Override",
                "public boolean mineBlock(ItemStack stack, Level level, BlockState state, BlockPos pos, LivingEntity miner) {",
                "    if (miner instanceof Player player && !level.isClientSide()) {",
                .. Call("player", "level", "pos"),
                "    }",
                "    return super.mineBlock(stack, level, state, pos, miner);",
                "}",
            ],
            ItemEventKind.Consumed =>
            [
                "@Override",
                "public ItemStack finishUsingItem(ItemStack stack, Level level, LivingEntity entity) {",
                "    ItemStack result = super.finishUsingItem(stack, level, entity);",
                "    if (entity instanceof Player player && !level.isClientSide()) {",
                .. Call("player", "level", "player.blockPosition()"),
                "    }",
                "    return result;",
                "}",
            ],
            ItemEventKind.Crafted =>
            [
                "@Override",
                "public void onCraftedBy(ItemStack stack, Level level, Player player) {",
                "    super.onCraftedBy(stack, level, player);",
                "    if (!level.isClientSide()) {",
                .. Call("player", "level", "player.blockPosition()"),
                "    }",
                "}",
            ],
            _ =>
            [
                "@Override",
                "public void inventoryTick(ItemStack stack, Level level, Entity entity, int slot, boolean selected) {",
                "    super.inventoryTick(stack, level, entity, slot, selected);",
                "    if (selected && entity instanceof Player player && !level.isClientSide()) {",
                .. Call("player", "level", "player.blockPosition()"),
                "    }",
                "}",
            ],
        };
    }
}
