using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// GUIs (V0.8.0): every GUI is a menu (the server side: its slots, the player's inventory, the state of
/// its components and its events) and a screen (the client side: the window drawn like the menus of the
/// game, its texts, images, buttons, checkboxes and text inputs, with their tooltips). ModGuiMenu and
/// ModGuiScreen hold what all the GUIs share; ModGuisNetwork carries the clicks, ticks and texts to the
/// server and the changes made by the graphs back to the screen. ModGuis registers the menus, opens a GUI
/// by its identifier and gives the nodes the components of the GUI open; ModGuisClient ties each screen to
/// its menu and applies the changes sent by the server.
/// </summary>
public abstract partial class ModJavaEmitter
{
    public const string GuiPackage = "gui";

    public const string GuisClass = "ModGuis";

    private const string GuisClientClass = "ModGuisClient";

    private const string GuiMenuBaseClass = "ModGuiMenu";

    private const string GuiScreenBaseClass = "ModGuiScreen";

    private const string GuisNetworkClass = "ModGuisNetwork";

    /// <summary>The block entity of the blocks that open a GUI (V0.8.1): it keeps the items of its slots.</summary>
    public const string GuiBlockEntityClass = "ModGuiBlockEntity";

    /// <summary>The methods called by the right click of a block, an item or a mob that opens a GUI (V0.8.1).</summary>
    public const string GuiBindingsClass = "ModGuiBindings";

    /// <summary>
    /// Event that ties the screens to the menus (NeoForge); null when it is done in the client setup
    /// with MenuScreens.register (Forge).
    /// </summary>
    protected virtual string? MenuScreensEventType => null;

    /// <summary>
    /// The network of the GUIs for the loader and the version: two messages (container, action,
    /// component, value), ToServer handled by ModGuis.receive and ToClient by ModGuisClient.apply; the
    /// members register(IEventBus), toServer(...) and toClient(ServerPlayer, ...).
    /// </summary>
    protected abstract (IReadOnlyList<string> Imports, IReadOnlyList<string> Members) GuiNetwork(JavaModNaming naming, JavaGameApi api);

    public static string GuiMenuClass(IRGui gui) => JavaNames.ToTypeName(gui.Id.Value, "Gui") + "Menu";

    public static string GuiScreenClass(IRGui gui) => JavaNames.ToTypeName(gui.Id.Value, "Gui") + "Screen";

    private static string GuiPackageOf(JavaModNaming naming) => $"{naming.RootPackage}.{GuiPackage}";

    private static readonly GraphNodeKind[] GuiNodes =
    [
        GraphNodeKind.OpenGui, GraphNodeKind.CloseGui, GraphNodeKind.GuiText, GraphNodeKind.GuiTicked, GraphNodeKind.GuiSlotItem,
        GraphNodeKind.IsGuiOpen, GraphNodeKind.SetGuiText, GraphNodeKind.SetGuiTicked, GraphNodeKind.SetGuiSlotItem, GraphNodeKind.SetGuiVisible,
    ];

    /// <summary>True if the mod has a GUI or if a graph uses a node of the GUIs (the records name their nodes).</summary>
    private static bool UsesGuis(IRContent content)
    {
        if (content.Guis.Count > 0)
        {
            return true;
        }

        IEnumerable<IRStatement> statements = content.Behaviors.SelectMany(b => b.Handlers).SelectMany(h => h.Body)
            .Concat(content.Definitions.OfType<IRFunction>().SelectMany(f => f.Body.Statements))
            .SelectMany(AllStatements);
        return statements.Any(s => s is IRActionStatement a && GuiNodes.Contains(a.Action)
            || GuiNodes.Any(kind => s.ToString().Contains($"Kind = {kind},", StringComparison.Ordinal)));
    }

    private void WriteGuis(GenerationContext context, JavaModNaming naming, JavaGameApi api, JavaProgramNames names)
    {
        IReadOnlyList<IRGui> guis = context.Ir.Content.Guis;
        string package = GuiPackageOf(naming);
        Write(context, naming, package, GuisClass, WriteGuisClass(context, naming, guis));
        Write(context, naming, package, GuiMenuBaseClass, WriteGuiMenuBase(naming));
        Write(context, naming, package, GuiScreenBaseClass, WriteGuiScreenBase(naming, api));
        Write(context, naming, package, GuisNetworkClass, WriteGuisNetwork(naming, api));
        Write(context, naming, naming.ClientPackage, GuisClientClass, WriteGuisClientClass(naming, guis));
        foreach (IRGui gui in guis)
        {
            Write(context, naming, package, GuiMenuClass(gui), WriteGuiMenu(context, naming, names, gui));
            Write(context, naming, package, GuiScreenClass(gui), WriteGuiScreen(context, naming, api, gui));
        }

        // The blocks, items and mobs that open a GUI by a right click (V0.8.1).
        IReadOnlyList<IRBlock> blocks = GuiBlocks(context.Ir.Content);
        if (blocks.Count > 0)
        {
            Write(context, naming, package, GuiBlockEntityClass, WriteGuiBlockEntity(naming, api, blocks, context.Ir.Content));
        }

        IReadOnlyList<IRGui> bound = BoundGuis(context.Ir.Content);
        if (bound.Count > 0)
        {
            Write(context, naming, package, GuiBindingsClass, WriteGuiBindings(naming, bound, blocks.Count > 0));
        }
    }

    /// <summary>The GUI of the mod with this identifier, or null.</summary>
    private static IRGui? FindGui(IRContent content, ResourceId? id) =>
        id is null ? null : content.Guis.FirstOrDefault(g => g.Id == id);

    /// <summary>The blocks that open a GUI of the mod by a right click: they keep the items of its slots.</summary>
    private static IReadOnlyList<IRBlock> GuiBlocks(IRContent content) =>
        [.. content.Blocks.Where(b => b.Plant is null && FindGui(content, b.Gui) is not null)];

    /// <summary>The GUIs opened by a right click on a block, an item or a mob.</summary>
    private static IReadOnlyList<IRGui> BoundGuis(IRContent content) =>
        [.. content.Guis.Where(g => GuiBlocks(content).Any(b => b.Gui == g.Id) || content.Items.Any(i => i.Gui == g.Id) || content.Mobs.Any(m => m.Gui == g.Id))];

    /// <summary>"blockOpensRuneTable": the method of <see cref="GuiBindingsClass"/> called by the right click of an element.</summary>
    private static string GuiBindingMethod(string element, IRGui gui) => element + "Opens" + JavaNames.ToTypeName(gui.Id.Value, "Gui");

    /// <summary>The call of the right click of a block, an item or a mob that opens its GUI ("ModGuiBindings.itemOpensRuneTable"), or null.</summary>
    public static string? GuiBindingCall(IRContent content, string element, ResourceId? gui) =>
        FindGui(content, gui) is { } found && (element != "block" || content.Blocks.Any(b => b.Plant is null && b.Gui == gui))
            ? $"{GuiBindingsClass}.{GuiBindingMethod(element, found)}"
            : null;

    public static string GuiBindingImport(JavaModNaming naming) => $"{GuiPackageOf(naming)}.{GuiBindingsClass}";

    public static string GuiBlockEntityImport(JavaModNaming naming) => $"{GuiPackageOf(naming)}.{GuiBlockEntityClass}";

    /// <summary>
    /// One method per GUI and per kind of element, with the arguments of the events of that element: a
    /// block opens the GUI on the items it keeps, an item and a mob open it on new slots.
    /// </summary>
    private static JavaSourceWriter WriteGuiBindings(JavaModNaming naming, IReadOnlyList<IRGui> guis, bool blocks)
    {
        JavaSourceWriter w = WriteHeader(GuiPackageOf(naming),
                ["net.minecraft.core.BlockPos", "net.minecraft.world.entity.Entity", "net.minecraft.world.entity.player.Player", "net.minecraft.world.level.Level"],
                "Les GUI ouverts par un clic droit sur un bloc (avec les items qu'il garde), un item tenu ou un mob.")
            .OpenBlock($"public final class {GuiBindingsClass}");
        foreach (IRGui gui in guis)
        {
            string id = JavaNames.StringLiteral(gui.Id.Value);
            if (blocks)
            {
                w.OpenBlock($"public static void {GuiBindingMethod("block", gui)}(Level level, BlockPos position, Player player, Entity other, double amount)")
                    .OpenBlock($"if (player != null && level.getBlockEntity(position) instanceof {GuiBlockEntityClass} block)")
                    .Line($"{GuisClass}.open(player, {id}, block.items());")
                    .CloseBlock()
                    .CloseBlock()
                    .Line();
            }

            w.OpenBlock($"public static void {GuiBindingMethod("item", gui)}(Player player, Level level, BlockPos position, Entity other, double amount)")
                .Line($"{GuisClass}.open(player, {id});")
                .CloseBlock()
                .Line()
                .OpenBlock($"public static void {GuiBindingMethod("mob", gui)}(Entity mob, Level level, BlockPos position, Player player, Entity other, double amount)")
                .Line($"{GuisClass}.open(player, {id});")
                .CloseBlock()
                .Line();
        }

        return WritePrivateConstructor(w, GuiBindingsClass);
    }

    /// <summary>
    /// The block entity of the blocks that open a GUI: as many slots as the largest of their GUIs, saved
    /// with the block (the save differs between 1.20.1 and 1.21.1), and a player far away cannot use them.
    /// </summary>
    private JavaSourceWriter WriteGuiBlockEntity(JavaModNaming naming, JavaGameApi api, IReadOnlyList<IRBlock> blocks, IRContent content)
    {
        int size = Math.Max(1, blocks.Max(b => FindGui(content, b.Gui)!.Slots.Count()));
        bool modern = api.UsesDataComponents;
        var imports = new List<string>(RegistryImports)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            $"{naming.RegistryPackage}.{naming.BlocksClass}",
            "net.minecraft.core.BlockPos",
            "net.minecraft.core.NonNullList",
            "net.minecraft.core.registries.Registries",
            "net.minecraft.nbt.CompoundTag",
            "net.minecraft.world.Container",
            "net.minecraft.world.ContainerHelper",
            "net.minecraft.world.SimpleContainer",
            "net.minecraft.world.entity.player.Player",
            "net.minecraft.world.item.ItemStack",
            "net.minecraft.world.level.block.entity.BlockEntity",
            "net.minecraft.world.level.block.entity.BlockEntityType",
            "net.minecraft.world.level.block.state.BlockState",
        };
        if (modern)
        {
            imports.Add("net.minecraft.core.HolderLookup");
        }

        string type = $"BlockEntityType<{GuiBlockEntityClass}>";
        JavaSourceWriter w = WriteHeader(GuiPackageOf(naming), imports,
                "Bloc qui ouvre un GUI : il garde les items de ses emplacements, sauvegardés avec le monde.")
            .OpenBlock($"public class {GuiBlockEntityClass} extends BlockEntity")
            .Line($"public static final DeferredRegister<BlockEntityType<?>> BLOCK_ENTITIES = DeferredRegister.create(Registries.BLOCK_ENTITY_TYPE, {naming.MainClass}.MOD_ID);")
            .Line($"public static final {RegistryHolderType("BlockEntityType<?>", type)} TYPE = BLOCK_ENTITIES.register(\"gui_block\",")
            .Indent().Indent()
            .Line($"() -> BlockEntityType.Builder.of({GuiBlockEntityClass}::new, {string.Join(", ", blocks.Select(b => $"{naming.BlocksClass}.{naming.ConstantFor(b.Id)}.get()"))}).build(null));")
            .Unindent().Unindent()
            .Line()
            .Line("/** As many slots as the largest GUI of the blocks. */")
            .Line($"public static final int SIZE = {size};")
            .Line()
            .OpenBlock("private final SimpleContainer items = new SimpleContainer(SIZE)")
            .Line("@Override")
            .OpenBlock("public void setChanged()")
            .Line("super.setChanged();")
            .Line($"{GuiBlockEntityClass}.this.setChanged();")
            .CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public boolean stillValid(Player player)")
            .Line($"return Container.stillValidBlockEntity({GuiBlockEntityClass}.this, player);")
            .CloseBlock()
            .Unindent().Line("};")
            .Line()
            .OpenBlock($"public {GuiBlockEntityClass}(BlockPos position, BlockState state)")
            .Line("super(TYPE.get(), position, state);")
            .CloseBlock()
            .Line()
            .Line("/** The items of the slots of the GUI, in their order. */")
            .OpenBlock("public Container items()")
            .Line("return this.items;")
            .CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock(modern ? "protected void saveAdditional(CompoundTag tag, HolderLookup.Provider registries)" : "protected void saveAdditional(CompoundTag tag)")
            .Line(modern ? "super.saveAdditional(tag, registries);" : "super.saveAdditional(tag);")
            .Line("NonNullList<ItemStack> list = NonNullList.withSize(SIZE, ItemStack.EMPTY);")
            .OpenBlock("for (int i = 0; i < SIZE; i++)")
            .Line("list.set(i, this.items.getItem(i));")
            .CloseBlock()
            .Line(modern ? "ContainerHelper.saveAllItems(tag, list, registries);" : "ContainerHelper.saveAllItems(tag, list);")
            .CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock(modern ? "protected void loadAdditional(CompoundTag tag, HolderLookup.Provider registries)" : "public void load(CompoundTag tag)")
            .Line(modern ? "super.loadAdditional(tag, registries);" : "super.load(tag);")
            .Line("NonNullList<ItemStack> list = NonNullList.withSize(SIZE, ItemStack.EMPTY);")
            .Line(modern ? "ContainerHelper.loadAllItems(tag, list, registries);" : "ContainerHelper.loadAllItems(tag, list);")
            .OpenBlock("for (int i = 0; i < SIZE; i++)")
            .Line("this.items.setItem(i, list.get(i));")
            .CloseBlock()
            .CloseBlock();
        return w.CloseBlock();
    }

    private IEnumerable<string> GuiMainImports(JavaModNaming naming, IRContent content) =>
    [
        $"{GuiPackageOf(naming)}.{GuisClass}", $"{GuiPackageOf(naming)}.{GuisNetworkClass}", $"{naming.ClientPackage}.{GuisClientClass}", .. ClientEnvironmentImports,
        .. GuiBlocks(content).Count > 0 ? [GuiBlockEntityImport(naming)] : Array.Empty<string>(),
    ];

    /// <summary>The menus, the block entity of the blocks with a GUI and the network on the mod bus; the client ties the screens to the menus.</summary>
    private void WriteGuiRegistrations(JavaSourceWriter w, IRContent content)
    {
        w.Line($"{GuisClass}.MENUS.register(modEventBus);");
        if (GuiBlocks(content).Count > 0)
        {
            w.Line($"{GuiBlockEntityClass}.BLOCK_ENTITIES.register(modEventBus);");
        }

        w.Line($"{GuisNetworkClass}.register(modEventBus);")
            .OpenBlock($"if ({ClientEnvironmentCheck})")
            .Line($"modEventBus.addListener({GuisClientClass}::registerScreens);")
            .CloseBlock();
    }

    private JavaSourceWriter WriteGuisNetwork(JavaModNaming naming, JavaGameApi api)
    {
        (IReadOnlyList<string> imports, IReadOnlyList<string> members) = GuiNetwork(naming, api);
        JavaSourceWriter w = WriteHeader(GuiPackageOf(naming),
                [.. imports, $"{naming.ClientPackage}.{GuisClientClass}", "net.minecraft.server.level.ServerPlayer"],
                "Réseau des GUI : clics, cases et textes envoyés au serveur ; textes et cases changés par les graphes, renvoyés à l'écran.")
            .OpenBlock($"public final class {GuisNetworkClass}");
        foreach (string line in members)
        {
            w.Line(line);
        }

        return WritePrivateConstructor(w, GuisNetworkClass);
    }

    private JavaSourceWriter WriteGuisClass(GenerationContext context, JavaModNaming naming, IReadOnlyList<IRGui> guis)
    {
        var imports = new List<string>(RegistryImports)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            "net.minecraft.core.registries.Registries",
            "net.minecraft.network.chat.Component",
            "net.minecraft.server.level.ServerPlayer",
            "net.minecraft.world.Container",
            "net.minecraft.world.SimpleMenuProvider",
            "net.minecraft.world.entity.player.Player",
            "net.minecraft.world.flag.FeatureFlags",
            "net.minecraft.world.inventory.MenuType",
            "net.minecraft.world.item.ItemStack",
        };
        JavaSourceWriter w = WriteHeader(GuiPackageOf(naming), imports,
                "GUIs du mod : leurs menus, leur ouverture par identifiant, et les composants du GUI ouvert pour les nœuds.")
            .OpenBlock($"public final class {GuisClass}")
            .Line($"public static final DeferredRegister<MenuType<?>> MENUS = DeferredRegister.create(Registries.MENU, {naming.MainClass}.MOD_ID);");
        foreach (IRGui gui in guis)
        {
            string menu = GuiMenuClass(gui);
            w.Line($"public static final {RegistryHolderType("MenuType<?>", $"MenuType<{menu}>")} {naming.ConstantFor(gui.Id)} = MENUS.register({JavaNames.StringLiteral(gui.Id.Value)},")
                .Indent().Indent()
                .Line($"() -> new MenuType<>({menu}::new, FeatureFlags.DEFAULT_FLAGS));")
                .Unindent().Unindent();
        }

        w.Line()
            .Line("/** Opens a GUI of the mod for the player (on the server); nothing for an unknown identifier. */")
            .OpenBlock("public static void open(Player player, String id)")
            .Line("open(player, id, null);")
            .CloseBlock()
            .Line()
            .Line("/** Opens a GUI on the items kept by a block (V0.8.1); null: new slots, given back to the player at the end. */")
            .OpenBlock("public static void open(Player player, String id, Container storage)")
            .OpenBlock("if (!(player instanceof ServerPlayer server) || id == null)")
            .Line("return;")
            .CloseBlock()
            .OpenBlock("switch (id.substring(id.indexOf(':') + 1))");
        foreach (IRGui gui in guis)
        {
            w.Line($"case {JavaNames.StringLiteral(gui.Id.Value)} -> server.openMenu(new SimpleMenuProvider(")
                .Indent().Indent()
                .Line($"(containerId, inventory, owner) -> new {GuiMenuClass(gui)}(containerId, inventory, storage), Component.translatable({JavaNames.StringLiteral(context.Paths.GuiTranslationKey(gui.Id))})));")
                .Unindent().Unindent();
        }

        w.Line("default -> {")
            .Line("    return;")
            .Line("}")
            .CloseBlock()
            .OpenBlock($"if (server.containerMenu instanceof {GuiMenuBaseClass} menu)")
            .Line("menu.opened();")
            .CloseBlock()
            .CloseBlock()
            .Line()
            .Line("/** Closes the GUI (or the inventory) the player has open. */")
            .OpenBlock("public static void close(Player player)")
            .OpenBlock("if (player instanceof ServerPlayer server)")
            .Line("server.closeContainer();")
            .CloseBlock()
            .CloseBlock()
            .Line()
            .Line("/** True when the player has this GUI open. */")
            .OpenBlock("public static boolean isOpen(Player player, String id)")
            .Line($"return player != null && id != null && player.containerMenu instanceof {GuiMenuBaseClass} menu && menu.guiId().equals(id.substring(id.indexOf(':') + 1));")
            .CloseBlock()
            .Line()
            .Line("/** Text typed in a component of the GUI open (or given to it); empty without one. */")
            .OpenBlock("public static String text(Player player, String component)")
            .Line($"return player != null && player.containerMenu instanceof {GuiMenuBaseClass} menu ? menu.text(component) : \"\";")
            .CloseBlock()
            .Line()
            .OpenBlock("public static boolean ticked(Player player, String component)")
            .Line($"return player != null && player.containerMenu instanceof {GuiMenuBaseClass} menu && menu.ticked(component);")
            .CloseBlock()
            .Line()
            .OpenBlock("public static ItemStack slotItem(Player player, String component)")
            .Line($"return player != null && player.containerMenu instanceof {GuiMenuBaseClass} menu ? menu.slotItem(component) : ItemStack.EMPTY;")
            .CloseBlock()
            .Line()
            .OpenBlock("public static void setText(Player player, String component, String text)")
            .OpenBlock($"if (player != null && player.containerMenu instanceof {GuiMenuBaseClass} menu)")
            .Line("menu.setText(component, text);")
            .CloseBlock()
            .CloseBlock()
            .Line()
            .OpenBlock("public static void setTicked(Player player, String component, boolean ticked)")
            .OpenBlock($"if (player != null && player.containerMenu instanceof {GuiMenuBaseClass} menu)")
            .Line("menu.setTicked(component, ticked);")
            .CloseBlock()
            .CloseBlock()
            .Line()
            .OpenBlock("public static void setVisible(Player player, String component, boolean visible)")
            .OpenBlock($"if (player != null && player.containerMenu instanceof {GuiMenuBaseClass} menu)")
            .Line("menu.setVisible(component, visible);")
            .CloseBlock()
            .CloseBlock()
            .Line()
            .OpenBlock("public static void setSlotItem(Player player, String component, ItemStack stack)")
            .OpenBlock($"if (player != null && player.containerMenu instanceof {GuiMenuBaseClass} menu)")
            .Line("menu.setSlotItem(component, stack);")
            .CloseBlock()
            .CloseBlock()
            .Line()
            .Line("/** A message of a screen: it counts only for the GUI the player has open. */")
            .OpenBlock("public static void receive(ServerPlayer player, int containerId, int action, int component, String value)")
            .OpenBlock($"if (player != null && player.containerMenu instanceof {GuiMenuBaseClass} menu && menu.containerId == containerId)")
            .Line("menu.receive(action, component, value);")
            .CloseBlock()
            .CloseBlock();
        return WritePrivateConstructor(w, GuisClass);
    }

    private static JavaSourceWriter WriteGuiMenuBase(JavaModNaming naming)
    {
        string[] imports =
        [
            "net.minecraft.server.level.ServerPlayer",
            "net.minecraft.world.Container",
            "net.minecraft.world.SimpleContainer",
            "net.minecraft.world.entity.player.Inventory",
            "net.minecraft.world.entity.player.Player",
            "net.minecraft.world.inventory.AbstractContainerMenu",
            "net.minecraft.world.inventory.MenuType",
            "net.minecraft.world.inventory.Slot",
            "net.minecraft.world.item.ItemStack",
        ];
        JavaSourceWriter w = WriteHeader(GuiPackageOf(naming), imports,
            "Ce que les menus des GUI partagent : l'état des composants, les messages des écrans, les événements.");
        foreach (string line in $$"""
            public abstract class {{GuiMenuBaseClass}} extends AbstractContainerMenu {
                /** Messages of the screen: a button clicked, a checkbox ticked, a text typed. */
                public static final int CLICK = 0;
                public static final int CHECK = 1;
                public static final int TEXT = 2;
                /** Message of the menu itself: the items of a slot changed. */
                public static final int SLOT = 3;
                /** Messages to the screen: a text or a checkbox changed by the graphs. */
                public static final int SET_TEXT = 10;
                public static final int SET_CHECK = 11;
                /** Messages of the screen (V0.8.1): a button released, the mouse over a component, out of it. */
                public static final int RELEASE = 4;
                public static final int HOVER = 5;
                public static final int UNHOVER = 6;
                /** Message to the screen: a component shown or hidden by the graphs. */
                public static final int SET_VISIBLE = 12;

                protected final Player player;
                protected final Container container;
                /** The items are kept by a block (V0.8.1): they stay in it when the GUI closes. */
                private final boolean stored;
                private final String[] names;
                private final int[] slotComponents;
                private final String[] texts;
                private final boolean[] ticked;
                private final boolean[] visible;
                private final ItemStack[] lastItems;

                /**
                 * names: the components of the GUI in their order; slotComponents: for each slot of the GUI, the
                 * index of its component.
                 */
                protected {{GuiMenuBaseClass}}(MenuType<?> type, int containerId, Inventory inventory, String[] names, int[] slotComponents, Container storage) {
                    super(type, containerId);
                    this.player = inventory.player;
                    this.names = names;
                    this.slotComponents = slotComponents;
                    this.stored = storage != null;
                    this.container = storage != null ? storage : new SimpleContainer(slotComponents.length);
                    this.texts = new String[names.length];
                    this.ticked = new boolean[names.length];
                    this.visible = new boolean[names.length];
                    java.util.Arrays.fill(this.visible, true);
                    this.lastItems = new ItemStack[slotComponents.length];
                    java.util.Arrays.fill(this.texts, "");
                    java.util.Arrays.fill(this.lastItems, ItemStack.EMPTY);
                }

                /** Identifier of the GUI ("rune_table"). */
                public abstract String guiId();

                /** The GUI opened (on the server). */
                public void opened() {
                }

                /** Every tick while the GUI is open (on the server). */
                protected void tick() {
                }

                /** The GUI closed (on the server). */
                protected void closed() {
                }

                /** An event of a component: the action, and the value for a checkbox ("1" / "0") or a text. */
                protected void event(int component, int action, String value) {
                }

                public int indexOf(String name) {
                    for (int i = 0; i < this.names.length; i++) {
                        if (this.names[i].equalsIgnoreCase(name)) {
                            return i;
                        }
                    }
                    return -1;
                }

                public String text(String name) {
                    int i = indexOf(name);
                    return i < 0 ? "" : this.texts[i];
                }

                public boolean ticked(String name) {
                    int i = indexOf(name);
                    return i >= 0 && this.ticked[i];
                }

                public ItemStack slotItem(String name) {
                    int slot = slotOf(indexOf(name));
                    return slot < 0 ? ItemStack.EMPTY : this.container.getItem(slot);
                }

                public void setText(String name, String text) {
                    int i = indexOf(name);
                    if (i >= 0) {
                        this.texts[i] = text == null ? "" : text;
                        send(SET_TEXT, i, this.texts[i]);
                    }
                }

                public void setTicked(String name, boolean value) {
                    int i = indexOf(name);
                    if (i >= 0) {
                        this.ticked[i] = value;
                        send(SET_CHECK, i, value ? "1" : "0");
                    }
                }

                public boolean visible(String name) {
                    int i = indexOf(name);
                    return i >= 0 && this.visible[i];
                }

                public void setVisible(String name, boolean value) {
                    int i = indexOf(name);
                    if (i >= 0) {
                        this.visible[i] = value;
                        send(SET_VISIBLE, i, value ? "1" : "0");
                    }
                }

                public void setSlotItem(String name, ItemStack stack) {
                    int slot = slotOf(indexOf(name));
                    if (slot >= 0) {
                        this.container.setItem(slot, stack == null ? ItemStack.EMPTY : stack.copy());
                    }
                }

                /** The value of a checkbox when the GUI opens. */
                protected void initTicked(int component, boolean value) {
                    this.ticked[component] = value;
                }

                /** A message of the screen (on the server). */
                public void receive(int action, int component, String value) {
                    if (component < 0 || component >= this.names.length || value == null) {
                        return;
                    }
                    if (action == CHECK) {
                        this.ticked[component] = "1".equals(value);
                    } else if (action == TEXT) {
                        this.texts[component] = value;
                    } else if (action != CLICK && action != RELEASE && action != HOVER && action != UNHOVER) {
                        return;
                    }
                    if (!this.visible[component]) {
                        return;
                    }
                    event(component, action, value);
                }

                private int slotOf(int component) {
                    for (int s = 0; s < this.slotComponents.length; s++) {
                        if (this.slotComponents[s] == component) {
                            return s;
                        }
                    }
                    return -1;
                }

                private void send(int action, int component, String value) {
                    if (this.player instanceof ServerPlayer server) {
                        {{GuisNetworkClass}}.toClient(server, this.containerId, action, component, value);
                    }
                }

                /** Every tick on the server: the slots whose items changed send their event, then the tick. */
                @Override
                public void broadcastChanges() {
                    super.broadcastChanges();
                    if (this.player.level().isClientSide()) {
                        return;
                    }
                    for (int s = 0; s < this.lastItems.length; s++) {
                        ItemStack now = this.container.getItem(s);
                        if (!ItemStack.matches(now, this.lastItems[s])) {
                            this.lastItems[s] = now.copy();
                            event(this.slotComponents[s], SLOT, "");
                        }
                    }
                    tick();
                }

                @Override
                public boolean stillValid(Player player) {
                    return this.container.stillValid(player);
                }

                /** Shift-click: from the GUI to the inventory, or from the inventory to the slots of the GUI that accept the item. */
                @Override
                public ItemStack quickMoveStack(Player player, int index) {
                    int count = this.slotComponents.length;
                    Slot slot = this.slots.get(index);
                    if (!slot.hasItem()) {
                        return ItemStack.EMPTY;
                    }
                    ItemStack stack = slot.getItem();
                    ItemStack result = stack.copy();
                    boolean moved = index < count
                            ? this.moveItemStackTo(stack, count, this.slots.size(), true)
                            : this.moveItemStackTo(stack, 0, count, false);
                    if (!moved) {
                        return ItemStack.EMPTY;
                    }
                    if (stack.isEmpty()) {
                        slot.set(ItemStack.EMPTY);
                    } else {
                        slot.setChanged();
                    }
                    return result;
                }

                /** The GUI closes: its event, then the items left in its slots go back to the player (a block keeps them). */
                @Override
                public void removed(Player player) {
                    super.removed(player);
                    if (!player.level().isClientSide()) {
                        closed();
                        if (!this.stored) {
                            this.clearContainer(player, this.container);
                        }
                    }
                }
            }
            """.ReplaceLineEndings("\n").Split('\n'))
        {
            w.Line(line);
        }

        return w;
    }

    private static JavaSourceWriter WriteGuiScreenBase(JavaModNaming naming, JavaGameApi api)
    {
        bool modern = api.UsesDataComponents;
        string[] imports =
        [
            "com.mojang.blaze3d.systems.RenderSystem",
            "com.mojang.math.Axis",
            "java.util.List",
            "net.minecraft.client.gui.GuiGraphics",
            "net.minecraft.client.gui.screens.inventory.AbstractContainerScreen",
            "net.minecraft.network.chat.Component",
            "net.minecraft.resources.ResourceLocation",
            "net.minecraft.util.FormattedCharSequence",
            "net.minecraft.world.entity.player.Inventory",
        ];
        JavaSourceWriter w = WriteHeader(GuiPackageOf(naming), imports,
            "Ce que les écrans des GUI partagent : la fenêtre du jeu, les emplacements, les textes changés par le serveur, le dessin des textes et des images.");

        // From 1.20.2 the screen draws its background itself; before, render() asks for it.
        string background = modern
            ? """
                  @Override
                  public void renderBackground(GuiGraphics graphics, int mouseX, int mouseY, float partialTick) {
                      if (this.darken) {
                          super.renderBackground(graphics, mouseX, mouseY, partialTick);
                      } else {
                          this.renderBg(graphics, partialTick, mouseX, mouseY);
                      }
                  }
              """
            : """
                  @Override
                  public void render(GuiGraphics graphics, int mouseX, int mouseY, float partialTick) {
                      if (this.darken) {
                          this.renderBackground(graphics);
                      }
                      super.render(graphics, mouseX, mouseY, partialTick);
                      this.renderTooltip(graphics, mouseX, mouseY);
                      this.renderTooltips(graphics, mouseX, mouseY);
                      this.trackHovers(mouseX, mouseY);
                  }
              """;
        string render = modern
            ? """
                  @Override
                  public void render(GuiGraphics graphics, int mouseX, int mouseY, float partialTick) {
                      super.render(graphics, mouseX, mouseY, partialTick);
                      this.renderTooltip(graphics, mouseX, mouseY);
                      this.renderTooltips(graphics, mouseX, mouseY);
                      this.trackHovers(mouseX, mouseY);
                  }
              """
            : string.Empty;
        foreach (string line in $$"""
            public abstract class {{GuiScreenBaseClass}}<T extends {{GuiMenuBaseClass}}> extends AbstractContainerScreen<T> {
                private final boolean pause;
                private final boolean darken;
                /** Texts given by the server (null: the one of the language file). */
                protected final String[] overrides;
                /** Components shown (a graph can hide one). */
                protected final boolean[] visible;
                /** Components under the mouse at the last frame, for the events "mouse over" and "mouse out". */
                private final boolean[] hovered;
                /** True while a change of the server is applied: the widgets then do not send it back. */
                protected boolean applying;

                protected {{GuiScreenBaseClass}}(T menu, Inventory inventory, Component title, int width, int height, int components, boolean pause, boolean darken) {
                    super(menu, inventory, title);
                    this.imageWidth = width;
                    this.imageHeight = height;
                    this.overrides = new String[components];
                    this.visible = new boolean[components];
                    this.hovered = new boolean[components];
                    java.util.Arrays.fill(this.visible, true);
                    this.pause = pause;
                    this.darken = darken;
                }

                @Override
                public boolean isPauseScreen() {
                    return this.pause;
                }

                /** The text of a component: the one given by the server, else the one of the language file. */
                protected Component text(int component, String key) {
                    if (this.overrides[component] != null) {
                        return Component.literal(this.overrides[component]);
                    }
                    return key.isEmpty() ? Component.empty() : Component.translatable(key);
                }

                /** Sends a click, a tick or a text to the server, unless it comes from the server. */
                protected void send(int action, int component, String value) {
                    if (!this.applying) {
                        {{GuisNetworkClass}}.toServer(this.menu.containerId, action, component, value);
                    }
                }

                /** A change sent by the server. */
                public void apply(int action, int component, String value) {
                    if (component < 0 || component >= this.overrides.length) {
                        return;
                    }
                    this.applying = true;
                    try {
                        if (action == {{GuiMenuBaseClass}}.SET_TEXT) {
                            this.overrides[component] = value;
                            onText(component, value);
                        } else if (action == {{GuiMenuBaseClass}}.SET_CHECK) {
                            onTicked(component, "1".equals(value));
                        } else if (action == {{GuiMenuBaseClass}}.SET_VISIBLE) {
                            this.visible[component] = "1".equals(value);
                            onVisible(component, this.visible[component]);
                        }
                    } finally {
                        this.applying = false;
                    }
                }

                /** A text given by the server: each GUI updates its widget. */
                protected void onText(int component, String value) {
                }

                /** A checkbox ticked by the server: each GUI updates its widget. */
                protected void onTicked(int component, boolean value) {
                }

                /** A component shown or hidden by the server: each GUI shows or hides its widget. */
                protected void onVisible(int component, boolean value) {
                }

                /** Tells the server when the mouse comes over a component or leaves it (only the ones a graph listens to). */
                protected void track(int component, boolean over) {
                    over = over && this.visible[component];
                    if (over != this.hovered[component]) {
                        this.hovered[component] = over;
                        send(over ? {{GuiMenuBaseClass}}.HOVER : {{GuiMenuBaseClass}}.UNHOVER, component, "");
                    }
                }

                /** Each GUI tracks here the components whose "mouse over" or "mouse out" a graph listens to. */
                protected void trackHovers(int mouseX, int mouseY) {
                }

                /** Tooltips of the texts, images and slots (the widgets show theirs). */
                protected void renderTooltips(GuiGraphics graphics, int mouseX, int mouseY) {
                }

                /** The texts are drawn by each GUI, instead of the title and the name of the inventory. */
                @Override
                protected void renderLabels(GuiGraphics graphics, int mouseX, int mouseY) {
                }

            {{background}}
            {{render}}
                /** Space before a content: none (start), half (center) or all (end). */
                protected static float align(int mode, float space) {
                    return mode == 1 ? space / 2 : mode == 2 ? space : 0;
                }

                /** Start of a content of the given size in a box, with the padding and the alignment. */
                protected static int place(int start, int size, int padding, int mode, int content) {
                    return start + padding + Math.round(align(mode, size - 2 * padding - content));
                }

                /**
                 * A text in its box: its size (1: the font of the game), its lines (cut at the end of the box when wrap),
                 * placed by the alignment (0 start, 1 center, 2 end), the lines placed between them, its opacity and its rotation.
                 */
                protected void drawText(GuiGraphics graphics, Component text, int x, int y, int width, int height, int color, float scale,
                        boolean wrap, int horizontal, int vertical, int justify, int padding, float opacity, float rotation) {
                    if (opacity <= 0) {
                        return;
                    }
                    int inner = Math.max(1, Math.round((width - 2 * padding) / scale));
                    List<FormattedCharSequence> lines = wrap ? this.font.split(text, inner) : List.of(text.getVisualOrderText());
                    int textWidth = 0;
                    for (FormattedCharSequence line : lines) {
                        textWidth = Math.max(textWidth, this.font.width(line));
                    }
                    float left = x + padding + align(horizontal, width - 2 * padding - textWidth * scale);
                    float top = y + padding + align(vertical, height - 2 * padding - lines.size() * this.font.lineHeight * scale);
                    graphics.pose().pushPose();
                    graphics.pose().translate(x + width / 2f, y + height / 2f, 0);
                    graphics.pose().mulPose(Axis.ZP.rotationDegrees(rotation));
                    graphics.pose().translate(left - (x + width / 2f), top - (y + height / 2f), 0);
                    graphics.pose().scale(scale, scale, 1);
                    // The font draws nothing under an alpha of 4.
                    int alpha = Math.max(4, Math.round(opacity * 255)) << 24;
                    for (int i = 0; i < lines.size(); i++) {
                        int lineX = Math.round(align(justify, textWidth - this.font.width(lines.get(i))));
                        graphics.drawString(this.font, lines.get(i), lineX, i * this.font.lineHeight, (color & 0xFFFFFF) | alpha, false);
                    }
                    graphics.pose().popPose();
                }

                /**
                 * An image in its box: stretched (size 0), fitted (1) or at the size of its file (2) placed by the alignment,
                 * or repeated (tiling); multiplied by the tint, with its opacity, a border, a blur, a highlight and a rotation.
                 */
                protected void drawImage(GuiGraphics graphics, ResourceLocation image, int x, int y, int width, int height, int textureWidth,
                        int textureHeight, int size, int horizontal, int vertical, boolean tiling, int tint, float opacity, int border,
                        int borderColor, boolean blur, boolean highlight, float rotation) {
                    if (opacity <= 0) {
                        return;
                    }
                    graphics.pose().pushPose();
                    graphics.pose().translate(x + width / 2f, y + height / 2f, 0);
                    graphics.pose().mulPose(Axis.ZP.rotationDegrees(rotation));
                    graphics.pose().translate(-(x + width / 2f), -(y + height / 2f), 0);
                    if (border > 0) {
                        int color = 0xFF000000 | borderColor;
                        graphics.fill(x, y, x + width, y + border, color);
                        graphics.fill(x, y + height - border, x + width, y + height, color);
                        graphics.fill(x, y + border, x + border, y + height - border, color);
                        graphics.fill(x + width - border, y + border, x + width, y + height - border, color);
                    }
                    int left = x + border;
                    int top = y + border;
                    int innerWidth = Math.max(1, width - 2 * border);
                    int innerHeight = Math.max(1, height - 2 * border);
                    RenderSystem.enableBlend();
                    float red = (tint >> 16 & 255) / 255f;
                    float green = (tint >> 8 & 255) / 255f;
                    float blue = (tint & 255) / 255f;
                    if (blur) {
                        // A soft image: the same one five times, a little moved and see-through.
                        RenderSystem.setShaderColor(red, green, blue, opacity * 0.35f);
                        int[] dx = { 0, -1, 1, 0, 0 };
                        int[] dy = { 0, 0, 0, -1, 1 };
                        for (int i = 0; i < dx.length; i++) {
                            blitImage(graphics, image, left + dx[i], top + dy[i], innerWidth, innerHeight, textureWidth, textureHeight, size, horizontal, vertical, tiling);
                        }
                    } else {
                        RenderSystem.setShaderColor(red, green, blue, opacity);
                        blitImage(graphics, image, left, top, innerWidth, innerHeight, textureWidth, textureHeight, size, horizontal, vertical, tiling);
                    }
                    RenderSystem.setShaderColor(1f, 1f, 1f, 1f);
                    if (highlight) {
                        graphics.fill(left, top, left + innerWidth, top + innerHeight, 0x40FFFFFF);
                    }
                    graphics.pose().popPose();
                }

                private static void blitImage(GuiGraphics graphics, ResourceLocation image, int x, int y, int width, int height, int textureWidth,
                        int textureHeight, int size, int horizontal, int vertical, boolean tiling) {
                    if (tiling) {
                        for (int ty = 0; ty < height; ty += textureHeight) {
                            for (int tx = 0; tx < width; tx += textureWidth) {
                                int w = Math.min(textureWidth, width - tx);
                                int h = Math.min(textureHeight, height - ty);
                                graphics.blit(image, x + tx, y + ty, w, h, 0, 0, w, h, textureWidth, textureHeight);
                            }
                        }
                        return;
                    }
                    int w = width;
                    int h = height;
                    if (size == 1) {
                        float k = Math.min(width / (float) textureWidth, height / (float) textureHeight);
                        w = Math.round(textureWidth * k);
                        h = Math.round(textureHeight * k);
                    } else if (size == 2) {
                        w = textureWidth;
                        h = textureHeight;
                    }
                    int left = x + Math.round(align(horizontal, width - w));
                    int top = y + Math.round(align(vertical, height - h));
                    graphics.enableScissor(x, y, x + width, y + height);
                    graphics.blit(image, left, top, w, h, 0, 0, textureWidth, textureHeight, textureWidth, textureHeight);
                    graphics.disableScissor();
                }

                protected boolean over(int mouseX, int mouseY, int x, int y, int width, int height) {
                    int left = this.leftPos + x;
                    int top = this.topPos + y;
                    return mouseX >= left && mouseX < left + width && mouseY >= top && mouseY < top + height;
                }

                /** The window of the menus of the game: light grey, white bevel at the top left, dark at the bottom right. */
                protected static void panel(GuiGraphics graphics, int x, int y, int width, int height) {
                    graphics.fill(x, y, x + width, y + height, 0xFF000000);
                    graphics.fill(x + 1, y + 1, x + width - 1, y + height - 1, 0xFFC6C6C6);
                    graphics.fill(x + 1, y + 1, x + width - 3, y + 3, 0xFFFFFFFF);
                    graphics.fill(x + 1, y + 1, x + 3, y + height - 3, 0xFFFFFFFF);
                    graphics.fill(x + 3, y + height - 3, x + width - 1, y + height - 1, 0xFF555555);
                    graphics.fill(x + width - 3, y + 3, x + width - 1, y + height - 1, 0xFF555555);
                }

                /** A slot of the game (18 x 18): dark at the top left, white at the bottom right. */
                protected static void slot(GuiGraphics graphics, int x, int y) {
                    graphics.fill(x, y, x + 18, y + 18, 0xFF8B8B8B);
                    graphics.fill(x, y, x + 17, y + 1, 0xFF373737);
                    graphics.fill(x, y, x + 1, y + 17, 0xFF373737);
                    graphics.fill(x + 1, y + 17, x + 18, y + 18, 0xFFFFFFFF);
                    graphics.fill(x + 17, y + 1, x + 18, y + 18, 0xFFFFFFFF);
                }
            }
            """.ReplaceLineEndings("\n").Split('\n'))
        {
            w.Line(line);
        }

        return w;
    }

    private JavaSourceWriter WriteGuisClientClass(JavaModNaming naming, IReadOnlyList<IRGui> guis)
    {
        string package = GuiPackageOf(naming);
        var imports = new List<string> { $"{package}.{GuisClass}", $"{package}.{GuiScreenBaseClass}", "net.minecraft.client.Minecraft" };
        imports.AddRange(guis.Select(g => $"{package}.{GuiScreenClass(g)}"));
        string? screensEvent = MenuScreensEventType;
        if (screensEvent is null)
        {
            imports.Add("net.minecraft.client.gui.screens.MenuScreens");
            imports.Add(ClientSetupEventType);
        }
        else
        {
            imports.Add(screensEvent);
        }

        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports, "Côté client des GUI : l'écran de chaque menu, et les changements envoyés par le serveur.")
            .OpenBlock($"public final class {GuisClientClass}");
        if (screensEvent is null)
        {
            w.OpenBlock($"public static void registerScreens({SimpleName(ClientSetupEventType)} event)")
                .OpenBlock("event.enqueueWork(() ->");
            foreach (IRGui gui in guis)
            {
                w.Line($"MenuScreens.register({GuisClass}.{naming.ConstantFor(gui.Id)}.get(), {GuiScreenClass(gui)}::new);");
            }

            w.CloseBlock(");").CloseBlock();
        }
        else
        {
            w.OpenBlock($"public static void registerScreens({SimpleName(screensEvent)} event)");
            foreach (IRGui gui in guis)
            {
                w.Line($"event.register({GuisClass}.{naming.ConstantFor(gui.Id)}.get(), {GuiScreenClass(gui)}::new);");
            }

            w.CloseBlock();
        }

        w.Line()
            .Line("/** A change sent by the server, for the screen of the GUI it is about. */")
            .OpenBlock("public static void apply(int containerId, int action, int component, String value)")
            .OpenBlock($"if (Minecraft.getInstance().screen instanceof {GuiScreenBaseClass}<?> screen && screen.getMenu().containerId == containerId)")
            .Line("screen.apply(action, component, value);")
            .CloseBlock()
            .CloseBlock();
        return WritePrivateConstructor(w, GuisClientClass);
    }

    private static JavaSourceWriter WriteGuiMenu(GenerationContext context, JavaModNaming naming, JavaProgramNames names, IRGui gui)
    {
        string className = GuiMenuClass(gui);
        IRGuiComponent[] components = [.. gui.Components];
        IRGuiComponent[] slots = [.. gui.Slots];
        var imports = new SortedSet<string>(StringComparer.Ordinal)
        {
            $"{GuiPackageOf(naming)}.{GuisClass}",
            "net.minecraft.world.Container",
            "net.minecraft.world.entity.player.Inventory",
            "net.minecraft.world.entity.player.Player",
            "net.minecraft.world.inventory.Slot",
            "net.minecraft.world.item.ItemStack",
            "net.minecraft.world.level.Level",
        };

        // The graphs of the GUI (V0.8.1): their event nodes are methods of their class, called here.
        List<(GraphNodeKind Event, string? Component, string Call)> handlers = GuiHandlers(context, naming, gui);
        imports.UnionWith(handlers.Select(h => $"{naming.BehaviorPackage}.{h.Call[..h.Call.IndexOf('.')]}"));
        string NameList(IEnumerable<string> items) => string.Join(", ", items);
        JavaSourceWriter w = WriteHeader(GuiPackageOf(naming), imports,
                $"Menu du GUI « {gui.Title} » (côté serveur) : ses emplacements{(gui.HasInventory ? ", l'inventaire du joueur" : string.Empty)} et ses événements.")
            .OpenBlock($"public class {className} extends {GuiMenuBaseClass}")
            .Line($"public static final String ID = {JavaNames.StringLiteral(gui.Id.Value)};")
            .Line($"private static final String[] NAMES = {{{NameList(components.Select(c => JavaNames.StringLiteral(c.Name)))}}};")
            .Line($"private static final int[] SLOTS = {{{NameList(slots.Select(s => Array.IndexOf(components, s).ToString(System.Globalization.CultureInfo.InvariantCulture)))}}};")
            .Line()
            .OpenBlock($"public {className}(int containerId, Inventory inventory)")
            .Line("this(containerId, inventory, null);")
            .CloseBlock()
            .Line()
            .Line("/** storage: the items kept by a block (V0.8.1), or null. */")
            .OpenBlock($"public {className}(int containerId, Inventory inventory, Container storage)")
            .Line($"super({GuisClass}.{naming.ConstantFor(gui.Id)}.get(), containerId, inventory, NAMES, SLOTS, storage);");
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is { Kind: GuiComponentKind.Checkbox, Checked: true })
            {
                w.Line($"this.initTicked({i}, true);");
            }
        }

        for (int i = 0; i < slots.Length; i++)
        {
            IRGuiComponent slot = slots[i];
            w.Line($"// {slot.Name}");
            if (slot.SlotMode is GuiSlotMode.Output or GuiSlotMode.Display)
            {
                // An output takes no item; an empty slot (shown only) gives none either.
                w.OpenBlock($"this.addSlot(new Slot(this.container, {i}, {slot.X + 1}, {slot.Y + 1})")
                    .Line("@Override")
                    .OpenBlock("public boolean mayPlace(ItemStack stack)")
                    .Line("return false;")
                    .CloseBlock();
                if (slot.SlotMode == GuiSlotMode.Display)
                {
                    w.Line()
                        .Line("@Override")
                        .OpenBlock("public boolean mayPickup(Player player)")
                        .Line("return false;")
                        .CloseBlock();
                }

                w.CloseBlock(");");
            }
            else
            {
                w.Line($"this.addSlot(new Slot(this.container, {i}, {slot.X + 1}, {slot.Y + 1}));");
            }
        }

        if (gui.HasInventory)
        {
            w.Line("// The player's inventory, then its hotbar.")
                .OpenBlock("for (int row = 0; row < 3; row++)")
                .OpenBlock("for (int column = 0; column < 9; column++)")
                .Line($"this.addSlot(new Slot(inventory, column + row * 9 + 9, {gui.InventoryX + 1} + column * 18, {gui.InventoryY + 1} + row * 18));")
                .CloseBlock()
                .CloseBlock()
                .OpenBlock("for (int column = 0; column < 9; column++)")
                .Line($"this.addSlot(new Slot(inventory, column, {gui.InventoryX + 1} + column * 18, {gui.InventoryY + 59}));")
                .CloseBlock();
        }

        w.CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public String guiId()")
            .Line("return ID;")
            .CloseBlock();

        IEnumerable<string> Calls(GraphNodeKind kind, string? component, string text, string ticked, string stack) =>
            handlers.Where(h => h.Event == kind && (component is null || string.Equals(h.Component, component, StringComparison.OrdinalIgnoreCase)))
                .Select(h => $"{h.Call}(this.player, this.player.level(), {text}, {ticked}, {stack});");

        // The 3 events of the GUI call their method and their nodes.
        foreach ((string slot, GraphNodeKind node, string signature) in new[]
                 {
                     (GuiMethodSlots.Opened, GraphNodeKind.OnGuiOpened, "public void opened()"),
                     (GuiMethodSlots.Tick, GraphNodeKind.OnGuiTick, "protected void tick()"),
                     (GuiMethodSlots.Closed, GraphNodeKind.OnGuiClosed, "protected void closed()"),
                 })
        {
            List<string> lines = [.. gui.Method(slot) is { } m ? [$"{m.JavaName}(this.player, this.player.level());"] : Array.Empty<string>(),
                .. Calls(node, null, "\"\"", "false", "ItemStack.EMPTY")];
            if (lines.Count > 0)
            {
                w.Line().Line("@Override").OpenBlock(signature);
                foreach (string line in lines)
                {
                    w.Line(line);
                }

                w.CloseBlock();
            }
        }

        // The events of the components, by their index then by the action of the screen.
        var cases = new List<(int Index, List<(string Action, List<string> Lines)> Actions)>();
        for (int i = 0; i < components.Length; i++)
        {
            IRGuiComponent c = components[i];
            string? own = gui.Method(ComponentSlot(c)) is { } m
                ? $"{m.JavaName}(this.player, this.player.level(){c.Kind switch
                {
                    GuiComponentKind.Checkbox => ", \"1\".equals(value)",
                    GuiComponentKind.TextInput => ", value",
                    GuiComponentKind.Slot => $", this.container.getItem({Array.IndexOf(slots, c)})",
                    _ => string.Empty,
                }});"
                : null;
            string stack = c.Kind == GuiComponentKind.Slot ? $"this.container.getItem({Array.IndexOf(slots, c)})" : "ItemStack.EMPTY";
            List<string> Lines(string? method, params GraphNodeKind[] nodes) =>
                [.. method is null ? Array.Empty<string>() : [method], .. nodes.SelectMany(n => Calls(n, c.Name, "value", "\"1\".equals(value)", stack))];
            var actions = new List<(string, List<string>)>
            {
                ("CLICK", Lines(c.Kind == GuiComponentKind.Button ? own : null, GraphNodeKind.OnGuiPressed, GraphNodeKind.OnGuiImageClicked)),
                ("RELEASE", Lines(null, GraphNodeKind.OnGuiReleased)),
                ("HOVER", Lines(null, GraphNodeKind.OnGuiHovered)),
                ("UNHOVER", Lines(null, GraphNodeKind.OnGuiUnhovered)),
                ("CHECK", Lines(c.Kind == GuiComponentKind.Checkbox ? own : null, GraphNodeKind.OnGuiTicked)),
                ("TEXT", Lines(c.Kind == GuiComponentKind.TextInput ? own : null, GraphNodeKind.OnGuiTextChanged)),
                ("SLOT", Lines(c.Kind == GuiComponentKind.Slot ? own : null, GraphNodeKind.OnGuiSlotChanged)),
            };
            actions.RemoveAll(a => a.Item2.Count == 0);
            if (actions.Count > 0)
            {
                cases.Add((i, actions));
            }
        }

        if (cases.Count > 0)
        {
            w.Line()
                .Line("@Override")
                .OpenBlock("protected void event(int component, int action, String value)")
                .OpenBlock("switch (component)");
            foreach ((int index, List<(string Action, List<string> Lines)> actions) in cases)
            {
                w.OpenBlock($"case {index} ->");
                foreach ((string action, List<string> lines) in actions)
                {
                    w.OpenBlock($"if (action == {action})");
                    foreach (string line in lines)
                    {
                        w.Line(line);
                    }

                    w.CloseBlock();
                }

                w.CloseBlock();
            }

            w.Line("default -> {")
                .Line("}")
                .CloseBlock()
                .CloseBlock();
        }

        // The methods: the Java code of the user, or the call of the graph of the event.
        foreach (IRElementMethod method in gui.Methods)
        {
            w.Line();
            foreach (string line in GuiMethodLines(method, gui, context, names))
            {
                w.Line(line);
            }
        }

        w.CloseBlock();
        return w;
    }

    /// <summary>The event nodes of the graphs for a GUI (V0.8.1): their kind, their component and their call ("RuneTableEventsBehavior.onGuiPressed1").</summary>
    private static List<(GraphNodeKind Event, string? Component, string Call)> GuiHandlers(GenerationContext context, JavaModNaming naming, IRGui gui) =>
        [.. context.Ir.Content.Behaviors.SelectMany(b => HandlerMethods(b)
            .Where(h => h.Handler.Gui == gui.Id.Value)
            .Select(h => (h.Handler.Event, h.Handler.Component, $"{naming.BehaviorClassFor(b.Id)}.{h.Method}")))];

    /// <summary>The components whose graph event of this kind exists, for the screen (what it tells the server).</summary>
    private static HashSet<string> GuiListened(GenerationContext context, JavaModNaming naming, IRGui gui, params GraphNodeKind[] kinds) =>
        [.. GuiHandlers(context, naming, gui).Where(h => kinds.Contains(h.Event) && h.Component is not null).Select(h => h.Component!.ToLowerInvariant())];

    private static string ComponentSlot(IRGuiComponent component) => component.Kind switch
    {
        GuiComponentKind.Button => "click.",
        GuiComponentKind.Checkbox => "check.",
        GuiComponentKind.TextInput => "text.",
        GuiComponentKind.Slot => "slot.",
        _ => "none.",
    } + component.Id.ToString("N");

    /// <summary>A method of a GUI: its Java code, or the call of its graph (the world, the player, then the value).</summary>
    private static IEnumerable<string> GuiMethodLines(IRElementMethod method, IRGui gui, GenerationContext context, JavaProgramNames names)
    {
        IRGuiComponent? component = gui.Components.FirstOrDefault(c => ComponentSlot(c) == method.Slot);
        (string JavaType, string Name, MineEngine.Assets.Graphs.DataType Type)? value = component is null ? null : GuiMethodSlots.ValueOf(component.Kind);
        string signature = $"public static void {method.JavaName}(Player player, Level level{(value is { } v ? $", {v.JavaType} {v.Name}" : string.Empty)})";
        var body = new List<string>();
        if (method.JavaBody is { } java)
        {
            body.AddRange(java.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).SkipWhile(l => l.Length == 0).Reverse().SkipWhile(l => l.Length == 0).Reverse());
        }
        else if (context.Ir.Content.Definitions.OfType<IRFunction>().FirstOrDefault(f => f.Id == method.FunctionId) is { } function)
        {
            string[] all = ["player", .. value is { } w ? [w.Name] : Array.Empty<string>()];
            string arguments = string.Join(", ", ["level", .. all.Take(function.Parameters.Count)]);
            body.Add($"{names.Package}.{names.CallPrefix(function, null)}({arguments});");
        }

        return [$"{signature} {{", .. body.Select(l => l.Length == 0 ? l : "    " + l), "}"];
    }

    private static JavaSourceWriter WriteGuiScreen(GenerationContext context, JavaModNaming naming, JavaGameApi api, IRGui gui)
    {
        bool modern = api.UsesDataComponents;
        string className = GuiScreenClass(gui);
        string menu = GuiMenuClass(gui);
        ResourcePaths paths = context.Paths;
        IRGuiComponent[] components = [.. gui.Components];
        int IndexOf(IRGuiComponent c) => Array.IndexOf(components, c);
        string Key(IRGuiComponent c, bool tooltip = false) =>
            JavaNames.StringLiteral(paths.GuiTranslationKey(gui.Id, c.Name) + (tooltip ? ".tooltip" : string.Empty));
        // A component without text (a checkbox alone) shows nothing rather than its translation key.
        string Text(IRGuiComponent c) => $"this.text({IndexOf(c)}, {(c.Text.Length == 0 ? "\"\"" : Key(c))})";
        string Tooltip(IRGuiComponent c) => $"Tooltip.create(Component.translatable({Key(c, tooltip: true)}))";
        string Field(IRGuiComponent c) => JavaNames.IsKeyword(c.Name) ? c.Name + "_" : c.Name;
        string ImageConstant(IRGuiComponent c) => "IMAGE_" + c.Name.ToUpperInvariant();
        HashSet<string> released = GuiListened(context, naming, gui, GraphNodeKind.OnGuiReleased);
        HashSet<string> hovers = GuiListened(context, naming, gui, GraphNodeKind.OnGuiHovered, GraphNodeKind.OnGuiUnhovered);
        HashSet<string> clicks = GuiListened(context, naming, gui, GraphNodeKind.OnGuiImageClicked);
        bool Listens(HashSet<string> set, IRGuiComponent c) => set.Contains(c.Name.ToLowerInvariant());

        // A button that tells the server when it is released (V0.8.1).
        void Release(JavaSourceWriter writer, IRGuiComponent c, int index)
        {
            if (Listens(released, c))
            {
                writer.Line("@Override")
                    .OpenBlock("public void onRelease(double mouseX, double mouseY)")
                    .Line("super.onRelease(mouseX, mouseY);")
                    .Line($"{className}.this.send({GuiMenuBaseClass}.RELEASE, {index}, \"\");")
                    .CloseBlock();
            }
        }

        static int Mode(GuiAlign align) => (int)align;
        static string F(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "f";
        static int Rgb(string hex, int fallback) =>
            Core.Theming.RgbColor.TryParse(hex, out Core.Theming.RgbColor color) ? color.R << 16 | color.G << 8 | color.B : fallback;
        string Hex(int rgb) => $"0x{rgb:X6}";

        // The drawing of an image (a component, or a button that is one), with its effect under the mouse.
        string DrawImage(IRGuiComponent c, string x, string y, string hovered)
        {
            GuiStyle st = c.Style;
            string Effect(GuiHoverEffect effect, string normal) => st.HoverEffect == effect ? $"({hovered} || {normal})" : normal;
            string tint = st.HoverEffect == GuiHoverEffect.Tint
                ? $"{hovered} ? {Hex(Rgb(st.HoverTint, 0xFFFF80))} : {Hex(Rgb(st.Tint, 0xFFFFFF))}"
                : Hex(Rgb(st.Tint, 0xFFFFFF));
            return $"this.drawImage(graphics, {ImageConstant(c)}, {x}, {y}, {c.Width}, {c.Height}, {c.TextureWidth}, {c.TextureHeight}, " +
                   $"{(int)st.ImageSize}, {Mode(st.Horizontal)}, {Mode(st.Vertical)}, {(st.Tiling ? "true" : "false")}, {tint}, {F(st.Opacity / 100.0)}, " +
                   $"{st.Border}, {Hex(Rgb(st.BorderColor, 0))}, {Effect(GuiHoverEffect.Blur, st.Blur ? "true" : "false")}, " +
                   $"{Effect(GuiHoverEffect.Highlight, st.Highlight ? "true" : "false")}, {F(st.Rotation)});";
        }

        var imports = new SortedSet<string>(StringComparer.Ordinal)
        {
            "net.minecraft.client.gui.GuiGraphics",
            "net.minecraft.network.chat.Component",
            "net.minecraft.world.entity.player.Inventory",
        };
        if (components.Any(c => c.Kind is GuiComponentKind.Button))
        {
            imports.Add("net.minecraft.client.gui.components.Button");
        }

        if (components.Any(c => c.Kind is GuiComponentKind.Checkbox))
        {
            imports.Add("net.minecraft.client.gui.components.Checkbox");
        }

        if (components.Any(c => c.Kind is GuiComponentKind.TextInput))
        {
            imports.Add("net.minecraft.client.gui.components.EditBox");
            imports.Add("net.minecraft.ChatFormatting");
        }

        if (components.Any(c => c.Tooltip.Length > 0 && c.Kind is GuiComponentKind.Button or GuiComponentKind.Checkbox or GuiComponentKind.TextInput))
        {
            imports.Add("net.minecraft.client.gui.components.Tooltip");
        }

        if (gui.Images.Any())
        {
            imports.Add("net.minecraft.resources.ResourceLocation");
        }

        JavaSourceWriter w = WriteHeader(GuiPackageOf(naming), imports, $"Écran du GUI « {gui.Title} » (côté client).")
            .OpenBlock($"public class {className} extends {GuiScreenBaseClass}<{menu}>");
        foreach (IRGuiComponent image in gui.Images)
        {
            string location = $"{paths.Namespace}:{paths.GuiTextureLocation(gui.Id, image.Name)}";
            w.Line($"private static final ResourceLocation {ImageConstant(image)} = {api.ParseResourceLocation(JavaNames.StringLiteral(location))};");
        }

        foreach (IRGuiComponent c in components.Where(c => c.Kind is GuiComponentKind.Button or GuiComponentKind.Checkbox or GuiComponentKind.TextInput))
        {
            string type = c.Kind switch { GuiComponentKind.Button => "Button", GuiComponentKind.Checkbox => "Checkbox", _ => "EditBox" };
            w.Line($"/** The component \"{c.Name}\". */")
                .Line($"public {type} {Field(c)};");
        }

        w.Line()
            .OpenBlock($"public {className}({menu} menu, Inventory inventory, Component title)")
            .Line($"super(menu, inventory, title, {gui.Width}, {gui.Height}, {components.Length}, {(gui.PauseGame ? "true" : "false")}, {(gui.RenderBackground ? "true" : "false")});")
            .CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("protected void init()")
            .Line("super.init();")
            .Line("int x = this.leftPos;")
            .Line("int y = this.topPos;");
        foreach (IRGuiComponent c in components)
        {
            string at = $"x + {c.X}, y + {c.Y}";
            int i = IndexOf(c);
            switch (c.Kind)
            {
                case GuiComponentKind.Button when c.Texture is not null:
                    GuiStyle bs = c.Style;
                    w.OpenBlock($"this.{Field(c)} = new Button({at}, {c.Width}, {c.Height}, {Text(c)}, button -> this.send({GuiMenuBaseClass}.CLICK, {i}, \"\"), narration -> narration.get())")
                        .Line("@Override")
                        .OpenBlock("public void renderWidget(GuiGraphics graphics, int mouseX, int mouseY, float partialTick)")
                        .Line("boolean hovered = this.isHoveredOrFocused();")
                        .Line($"{className}.this.{DrawImage(c, "this.getX()", "this.getY()", "hovered")[5..]}")
                        .Line($"{className}.this.drawText(graphics, this.getMessage(), this.getX(), this.getY(), this.width, this.height, 0xFFFFFF, {F(bs.FontScale / 100.0)}, false, " +
                              $"{Mode(bs.Horizontal)}, {Mode(bs.Vertical)}, {Mode(bs.Justify)}, {bs.Padding}, {F(bs.Opacity / 100.0)}, 0f);")
                        .CloseBlock();
                    Release(w, c, i);
                    w.CloseBlock(";");
                    if (c.Tooltip.Length > 0)
                    {
                        w.Line($"this.{Field(c)}.setTooltip({Tooltip(c)});");
                    }

                    w.Line($"this.addRenderableWidget(this.{Field(c)});");
                    break;
                case GuiComponentKind.Button when Listens(released, c):
                    w.OpenBlock($"this.{Field(c)} = new Button({at}, {c.Width}, {c.Height}, {Text(c)}, button -> this.send({GuiMenuBaseClass}.CLICK, {i}, \"\"), narration -> narration.get())");
                    Release(w, c, i);
                    w.CloseBlock(";");
                    if (c.Tooltip.Length > 0)
                    {
                        w.Line($"this.{Field(c)}.setTooltip({Tooltip(c)});");
                    }

                    w.Line($"this.addRenderableWidget(this.{Field(c)});");
                    break;
                case GuiComponentKind.Button:
                    w.Line($"this.{Field(c)} = Button.builder({Text(c)}, button -> this.send({GuiMenuBaseClass}.CLICK, {i}, \"\"))")
                        .Indent().Indent()
                        .Line($".bounds({at}, {c.Width}, {c.Height}){(c.Tooltip.Length > 0 ? $".tooltip({Tooltip(c)})" : string.Empty)}.build();")
                        .Unindent().Unindent()
                        .Line($"this.addRenderableWidget(this.{Field(c)});");
                    break;
                case GuiComponentKind.Checkbox:
                    string selected = $"this.menu.ticked({JavaNames.StringLiteral(c.Name)})";
                    GuiStyle cs = c.Style;
                    w.Line($"int {Field(c)}Width = 24 + this.font.width({Text(c)});")
                        .Line($"int {Field(c)}X = place(x + {c.X}, {c.Width}, {cs.Padding}, {Mode(cs.Horizontal)}, {Field(c)}Width);")
                        .Line($"int {Field(c)}Y = place(y + {c.Y}, {c.Height}, {cs.Padding}, {Mode(cs.Vertical)}, 20);");
                    at = $"{Field(c)}X, {Field(c)}Y";
                    if (modern)
                    {
                        w.Line($"this.{Field(c)} = Checkbox.builder({Text(c)}, this.font).pos({at}).selected({selected})")
                            .Indent().Indent()
                            .Line($".onValueChange((box, ticked) -> this.send({GuiMenuBaseClass}.CHECK, {i}, ticked ? \"1\" : \"0\")).build();")
                            .Unindent().Unindent();
                    }
                    else
                    {
                        w.OpenBlock($"this.{Field(c)} = new Checkbox({at}, {Field(c)}Width, 20, {Text(c)}, {selected})")
                            .Line("@Override")
                            .OpenBlock("public void onPress()")
                            .Line("super.onPress();")
                            .Line($"{className}.this.send({GuiMenuBaseClass}.CHECK, {i}, this.selected() ? \"1\" : \"0\");")
                            .CloseBlock()
                            .CloseBlock(";");
                    }

                    if (c.Tooltip.Length > 0)
                    {
                        w.Line($"this.{Field(c)}.setTooltip({Tooltip(c)});");
                    }

                    w.Line($"this.addRenderableWidget(this.{Field(c)});");
                    break;
                case GuiComponentKind.TextInput:
                    w.Line($"this.{Field(c)} = new EditBox(this.font, {at}, {c.Width}, {c.Height}, {(c.Text.Length == 0 ? "Component.empty()" : $"Component.translatable({Key(c)})")});")
                        .Line($"this.{Field(c)}.setMaxLength({c.MaxLength});")
                        .Line($"this.{Field(c)}.setValue(this.menu.text({JavaNames.StringLiteral(c.Name)}));");
                    if (c.Text.Length > 0)
                    {
                        w.Line($"this.{Field(c)}.setHint(Component.translatable({Key(c)}).withStyle(ChatFormatting.DARK_GRAY));");
                    }

                    if (c.Tooltip.Length > 0)
                    {
                        w.Line($"this.{Field(c)}.setTooltip({Tooltip(c)});");
                    }

                    w.Line($"this.{Field(c)}.setResponder(text -> this.send({GuiMenuBaseClass}.TEXT, {i}, text));")
                        .Line($"this.addRenderableWidget(this.{Field(c)});");
                    break;
            }
        }

        w.CloseBlock()
            .Line()
            .Line("/** The window of the game, its slots and its images. */")
            .Line("@Override")
            .OpenBlock("protected void renderBg(GuiGraphics graphics, float partialTick, int mouseX, int mouseY)")
            .Line("int x = this.leftPos;")
            .Line("int y = this.topPos;")
            .Line("panel(graphics, x, y, this.imageWidth, this.imageHeight);");
        foreach (IRGuiComponent slot in gui.Slots)
        {
            w.Line($"slot(graphics, x + {slot.X}, y + {slot.Y});");
        }

        if (gui.HasInventory)
        {
            w.OpenBlock("for (int column = 0; column < 9; column++)")
                .OpenBlock("for (int row = 0; row < 3; row++)")
                .Line($"slot(graphics, x + {gui.InventoryX} + column * 18, y + {gui.InventoryY} + row * 18);")
                .CloseBlock()
                .Line($"slot(graphics, x + {gui.InventoryX} + column * 18, y + {gui.InventoryY + 58});")
                .CloseBlock();
        }

        foreach (IRGuiComponent image in gui.Images.Where(c => c.Kind == GuiComponentKind.Image))
        {
            string hovered = image.Style.HoverEffect == GuiHoverEffect.None
                ? "false"
                : $"this.over(mouseX, mouseY, {image.X}, {image.Y}, {image.Width}, {image.Height})";
            w.OpenBlock($"if (this.visible[{IndexOf(image)}])")
                .Line(DrawImage(image, $"x + {image.X}", $"y + {image.Y}", hovered))
                .CloseBlock();
        }

        w.CloseBlock()
            .Line()
            .Line("/** The texts, relative to the window. */")
            .Line("@Override")
            .OpenBlock("protected void renderLabels(GuiGraphics graphics, int mouseX, int mouseY)");
        foreach (IRGuiComponent text in components.Where(c => c.Kind == GuiComponentKind.Text))
        {
            GuiStyle ts = text.Style;
            string over = $"this.over(mouseX, mouseY, {text.X}, {text.Y}, {text.Width}, {text.Height})";
            string label = ts.HoverText.Length > 0
                ? $"{over} ? Component.translatable({JavaNames.StringLiteral(paths.GuiTranslationKey(gui.Id, text.Name) + ".hover")}) : {Text(text)}"
                : Text(text);
            string color = ts.HoverColor.Length > 0 ? $"{over} ? {Hex(Rgb(ts.HoverColor, text.Color))} : {Hex(text.Color)}" : Hex(text.Color);
            w.OpenBlock($"if (this.visible[{IndexOf(text)}])")
                .Line($"this.drawText(graphics, {label}, {text.X}, {text.Y}, {text.Width}, {text.Height}, {color}, {F(ts.FontScale / 100.0)}, " +
                      $"{(ts.Wrap ? "true" : "false")}, {Mode(ts.Horizontal)}, {Mode(ts.Vertical)}, {Mode(ts.Justify)}, {ts.Padding}, {F(ts.Opacity / 100.0)}, {F(ts.Rotation)});")
                .CloseBlock();
        }

        w.CloseBlock();

        IRGuiComponent[] tooltips = [.. components.Where(c => c.Tooltip.Length > 0 && c.Kind is GuiComponentKind.Text or GuiComponentKind.Image or GuiComponentKind.Slot)];
        if (tooltips.Length > 0)
        {
            w.Line()
                .Line("@Override")
                .OpenBlock("protected void renderTooltips(GuiGraphics graphics, int mouseX, int mouseY)");
            foreach (IRGuiComponent c in tooltips)
            {
                w.OpenBlock($"if (this.visible[{IndexOf(c)}] && this.over(mouseX, mouseY, {c.X}, {c.Y}, {c.Width}, {c.Height}))")
                    .Line($"graphics.renderTooltip(this.font, Component.translatable({Key(c, tooltip: true)}), mouseX, mouseY);")
                    .CloseBlock();
            }

            w.CloseBlock();
        }

        // Texts and checkboxes changed by the graphs.
        IRGuiComponent[] widgets = [.. components.Where(c => c.Kind is GuiComponentKind.Button or GuiComponentKind.Checkbox or GuiComponentKind.TextInput)];
        if (widgets.Length > 0)
        {
            w.Line()
                .Line("@Override")
                .OpenBlock("protected void onText(int component, String value)")
                .OpenBlock("switch (component)");
            foreach (IRGuiComponent c in widgets)
            {
                w.Line(c.Kind == GuiComponentKind.TextInput
                    ? $"case {IndexOf(c)} -> this.{Field(c)}.setValue(value);"
                    : $"case {IndexOf(c)} -> this.{Field(c)}.setMessage(Component.literal(value));");
            }

            w.Line("default -> {")
                .Line("}")
                .CloseBlock()
                .CloseBlock();
        }

        if (widgets.Length > 0)
        {
            w.Line()
                .Line("@Override")
                .OpenBlock("protected void onVisible(int component, boolean value)")
                .OpenBlock("switch (component)");
            foreach (IRGuiComponent c in widgets)
            {
                w.Line($"case {IndexOf(c)} -> this.{Field(c)}.visible = value;");
            }

            w.Line("default -> {")
                .Line("}")
                .CloseBlock()
                .CloseBlock();
        }

        IRGuiComponent[] tracked = [.. components.Where(c => Listens(hovers, c))];
        if (tracked.Length > 0)
        {
            w.Line()
                .Line("@Override")
                .OpenBlock("protected void trackHovers(int mouseX, int mouseY)");
            foreach (IRGuiComponent c in tracked)
            {
                w.Line($"this.track({IndexOf(c)}, this.over(mouseX, mouseY, {c.X}, {c.Y}, {c.Width}, {c.Height}));");
            }

            w.CloseBlock();
        }

        IRGuiComponent[] clicked = [.. components.Where(c => c.Kind == GuiComponentKind.Image && Listens(clicks, c))];
        if (clicked.Length > 0)
        {
            w.Line()
                .Line("/** A click on an image the graphs listen to (V0.8.1). */")
                .Line("@Override")
                .OpenBlock("public boolean mouseClicked(double mouseX, double mouseY, int button)");
            foreach (IRGuiComponent c in clicked)
            {
                w.OpenBlock($"if (button == 0 && this.visible[{IndexOf(c)}] && this.over((int) mouseX, (int) mouseY, {c.X}, {c.Y}, {c.Width}, {c.Height}))")
                    .Line($"this.send({GuiMenuBaseClass}.CLICK, {IndexOf(c)}, \"\");")
                    .Line("return true;")
                    .CloseBlock();
            }

            w.Line("return super.mouseClicked(mouseX, mouseY, button);")
                .CloseBlock();
        }

        IRGuiComponent[] boxes = [.. components.Where(c => c.Kind == GuiComponentKind.Checkbox)];
        if (boxes.Length > 0)
        {
            w.Line()
                .Line("@Override")
                .OpenBlock("protected void onTicked(int component, boolean value)")
                .OpenBlock("switch (component)");
            foreach (IRGuiComponent c in boxes)
            {
                w.OpenBlock($"case {IndexOf(c)} ->")
                    .OpenBlock($"if (this.{Field(c)}.selected() != value)")
                    .Line($"this.{Field(c)}.onPress();")
                    .CloseBlock()
                    .CloseBlock();
            }

            w.Line("default -> {")
                .Line("}")
                .CloseBlock()
                .CloseBlock();
        }

        // A text input keeps the keys of the game while it is typed in (E would close the GUI).
        if (components.Any(c => c.Kind == GuiComponentKind.TextInput))
        {
            w.Line()
                .Line("@Override")
                .OpenBlock("public boolean keyPressed(int keyCode, int scanCode, int modifiers)")
                .OpenBlock("if (keyCode != 256 && this.getFocused() instanceof EditBox box && box.isFocused())")
                .Line("box.keyPressed(keyCode, scanCode, modifiers);")
                .Line("return true;")
                .CloseBlock()
                .Line("return super.keyPressed(keyCode, scanCode, modifiers);")
                .CloseBlock();
        }

        w.CloseBlock();
        return w;
    }
}
