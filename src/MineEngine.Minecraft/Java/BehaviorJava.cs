using System.Globalization;
using MineEngine.Assets.Graphs;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Traduction Java des nœuds d'un graphe : expressions, instructions et petites méthodes
/// utilitaires (donner un item, faire apparaître un item...) ajoutées à la classe générée.
/// Le code est le même pour Forge et NeoForge ; seuls les événements et le bus diffèrent
/// (voir <see cref="ModJavaEmitter"/>).
/// </summary>
public sealed class BehaviorJava
{
    private readonly JavaGameApi _api;
    private readonly SortedSet<string> _imports = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _helpers = new(StringComparer.Ordinal);

    public BehaviorJava(JavaGameApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    /// <summary>Imports nécessaires au code traduit jusqu'ici.</summary>
    public IReadOnlyCollection<string> Imports => _imports;

    /// <summary>Lignes d'en-tête d'un gestionnaire : filtrage client/serveur, puis variables player, level et position.</summary>
    public IReadOnlyList<string> Prologue(GraphNodeKind eventKind)
    {
        Use("net.minecraft.core.BlockPos", "net.minecraft.world.entity.player.Player", "net.minecraft.world.level.Level");
        switch (eventKind)
        {
            case GraphNodeKind.OnRightClickBlock or GraphNodeKind.OnUseItem:
                Use("net.minecraft.world.InteractionHand");
                return
                [
                    // L'événement est appelé pour chaque main, et aussi côté client : on ne garde que le serveur et la main principale.
                    "if (event.getLevel().isClientSide() || event.getHand() != InteractionHand.MAIN_HAND) {",
                    "    return;",
                    "}",
                    "Player player = event.getEntity();",
                    "Level level = event.getLevel();",
                    eventKind == GraphNodeKind.OnRightClickBlock ? "BlockPos position = event.getPos();" : "BlockPos position = player.blockPosition();",
                ];
            case GraphNodeKind.OnPlayerJoin or GraphNodeKind.OnBlockBroken:
                return
                [
                    eventKind == GraphNodeKind.OnPlayerJoin ? "Player player = event.getEntity();" : "Player player = event.getPlayer();",
                    "Level level = player.level();",
                    "if (level.isClientSide()) {",
                    "    return;",
                    "}",
                    eventKind == GraphNodeKind.OnPlayerJoin ? "BlockPos position = player.blockPosition();" : "BlockPos position = event.getPos();",
                ];
            default:
                throw new NotSupportedException($"Événement {eventKind} non traduit en Java.");
        }
    }

    /// <summary>Instruction Java d'une action (terminée par ';').</summary>
    public string Action(IRActionStatement action)
    {
        string[] args = [.. action.Arguments.Select(Expression)];
        return action.Action switch
        {
            GraphNodeKind.SpawnItem => Helper("spawnItem", $"spawnItem(level, {args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.GiveItem => Helper("giveItem", $"giveItem({args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.RemoveItem => Helper("removeItem", $"removeItem({args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.SendMessage => Helper("sendMessage", $"sendMessage({args[0]}, {args[1]});"),
            GraphNodeKind.PlaySound => Helper("playSound", $"playSound(level, {args[0]}, {args[1]});"),
            GraphNodeKind.SetBlock => Helper("setBlock", $"setBlock(level, {args[0]}, {args[1]});"),
            GraphNodeKind.Explosion => Helper("explode", $"explode(level, {args[0]}, {args[1]});"),
            GraphNodeKind.CancelEvent => "event.setCanceled(true);",
            _ => throw new NotSupportedException($"Action {action.Action} non traduite en Java."),
        };
    }

    public string Expression(IRExpression expression) => expression switch
    {
        IRNumberLiteral number => number.Value < 0
            ? string.Create(CultureInfo.InvariantCulture, $"({number.Value})")
            : number.Value.ToString(CultureInfo.InvariantCulture),
        IRTextLiteral text => JavaNames.StringLiteral(text.Value),
        IRBooleanLiteral flag => flag.Value ? "true" : "false",
        IRContentLiteral content => Content(content),
        IREventValue value => value.Name,
        IRToText toText => $"String.valueOf({Expression(toText.Value)})",
        IRCallExpression call => Call(call),
        _ => throw new NotSupportedException($"Expression {expression.GetType().Name} non traduite en Java."),
    };

    /// <summary>Méthodes utilitaires utilisées par le code traduit, prêtes à écrire dans la classe.</summary>
    public IEnumerable<IReadOnlyList<string>> HelperMethods() => _helpers.Select(HelperMethod);

    private string Call(IRCallExpression call)
    {
        string[] a = [.. call.Arguments.Select(Expression)];
        return call.Kind switch
        {
            GraphNodeKind.HasItem => $"{a[0]}.getInventory().countItem({a[1]}) >= {a[2]}",
            GraphNodeKind.HoldsItem => $"{a[0]}.getMainHandItem().is({a[1]})",
            GraphNodeKind.IsSneaking => $"{a[0]}.isShiftKeyDown()",
            GraphNodeKind.Chance => $"level.random.nextInt(100) < {a[0]}",
            GraphNodeKind.PlayerPosition => $"{a[0]}.blockPosition()",
            GraphNodeKind.PlayerName => $"{a[0]}.getName().getString()",
            GraphNodeKind.OffsetPosition => $"{a[0]}.offset({a[1]}, {a[2]}, {a[3]})",
            GraphNodeKind.RandomNumber => Helper("randomBetween", $"randomBetween(level, {a[0]}, {a[1]})"),
            GraphNodeKind.JoinText => $"({a[0]} + {a[1]})",
            GraphNodeKind.GreaterThan => $"({a[0]} > {a[1]})",
            GraphNodeKind.NumbersEqual => $"({a[0]} == {a[1]})",
            GraphNodeKind.Not => $"!({a[0]})",
            _ => throw new NotSupportedException($"Nœud {call.Kind} non traduit en Java."),
        };
    }

    /// <summary>Items, blocs et sons sont retrouvés par identifiant : même code pour le jeu et pour le mod.</summary>
    private string Content(IRContentLiteral content)
    {
        string id = JavaNames.StringLiteral(content.Id.ToString());
        return content.ContentType switch
        {
            PinType.Item => Helper("item", $"item({id})"),
            PinType.Block => Helper("block", $"block({id})"),
            PinType.Sound => Helper("sound", $"sound({id})"),
            _ => throw new NotSupportedException($"Contenu {content.ContentType} non traduit en Java."),
        };
    }

    private string Helper(string name, string code)
    {
        if (_helpers.Add(name))
        {
            Use(HelperImports(name));
        }

        return code;
    }

    private void Use(params string[] imports) => _imports.UnionWith(imports);

    private static string[] HelperImports(string name) => name switch
    {
        "item" => ["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.world.item.Item"],
        "block" => ["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.world.level.block.Block"],
        "sound" => ["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.sounds.SoundEvent"],
        "spawnItem" => ["net.minecraft.world.entity.item.ItemEntity", "net.minecraft.world.item.Item", "net.minecraft.world.item.ItemStack"],
        "giveItem" => ["net.minecraft.world.item.Item", "net.minecraft.world.item.ItemStack"],
        "removeItem" => ["net.minecraft.world.item.Item"],
        "sendMessage" => ["net.minecraft.network.chat.Component"],
        "playSound" => ["net.minecraft.sounds.SoundEvent", "net.minecraft.sounds.SoundSource"],
        "setBlock" => ["net.minecraft.world.level.block.Block"],
        _ => [],
    };

    private IReadOnlyList<string> HelperMethod(string name) => name switch
    {
        "item" =>
        [
            "private static Item item(String id) {",
            $"    return BuiltInRegistries.ITEM.get({_api.ParseResourceLocation("id")});",
            "}",
        ],
        "block" =>
        [
            "private static Block block(String id) {",
            $"    return BuiltInRegistries.BLOCK.get({_api.ParseResourceLocation("id")});",
            "}",
        ],
        "sound" =>
        [
            "private static SoundEvent sound(String id) {",
            $"    return BuiltInRegistries.SOUND_EVENT.get({_api.ParseResourceLocation("id")});",
            "}",
        ],
        "spawnItem" =>
        [
            "private static void spawnItem(Level level, BlockPos position, Item item, int count) {",
            "    if (count > 0) {",
            "        level.addFreshEntity(new ItemEntity(level, position.getX() + 0.5, position.getY() + 1.0, position.getZ() + 0.5, new ItemStack(item, count)));",
            "    }",
            "}",
        ],
        "giveItem" =>
        [
            "private static void giveItem(Player player, Item item, int count) {",
            "    if (count <= 0) {",
            "        return;",
            "    }",
            "    ItemStack stack = new ItemStack(item, count);",
            "    if (!player.getInventory().add(stack)) {",
            "        player.drop(stack, false);",
            "    }",
            "}",
        ],
        "removeItem" =>
        [
            "private static void removeItem(Player player, Item item, int count) {",
            "    if (count > 0) {",
            "        player.getInventory().clearOrCountMatchingItems(stack -> stack.is(item), count, player.inventoryMenu.getCraftSlots());",
            "    }",
            "}",
        ],
        "sendMessage" =>
        [
            "private static void sendMessage(Player player, String message) {",
            "    player.sendSystemMessage(Component.literal(message));",
            "}",
        ],
        "playSound" =>
        [
            "private static void playSound(Level level, BlockPos position, SoundEvent sound) {",
            "    if (sound != null) {",
            "        level.playSound(null, position, sound, SoundSource.BLOCKS, 1.0f, 1.0f);",
            "    }",
            "}",
        ],
        "setBlock" =>
        [
            "private static void setBlock(Level level, BlockPos position, Block block) {",
            "    level.setBlockAndUpdate(position, block.defaultBlockState());",
            "}",
        ],
        "explode" =>
        [
            "private static void explode(Level level, BlockPos position, int power) {",
            "    if (power > 0) {",
            "        level.explode(null, position.getX() + 0.5, position.getY() + 0.5, position.getZ() + 0.5, power, Level.ExplosionInteraction.TNT);",
            "    }",
            "}",
        ],
        "randomBetween" =>
        [
            "private static int randomBetween(Level level, int first, int second) {",
            "    int low = Math.min(first, second);",
            "    int high = Math.max(first, second);",
            "    return low + level.random.nextInt(high - low + 1);",
            "}",
        ],
        _ => throw new NotSupportedException($"Méthode utilitaire {name} inconnue."),
    };
}
