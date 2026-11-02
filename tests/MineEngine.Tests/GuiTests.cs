using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>GUIs of V0.8.0: the element, its components, its checks and the grid of the editor.</summary>
public sealed class GuiTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static ModProject CreateProject(TemporaryDirectory temp, ModLoaderKind loader = ModLoaderKind.NeoForge, string version = "1.21.1")
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod")
        {
            LoaderId = loader.ToId(), MinecraftVersion = version,
        });
        SampleContent.AddTo(project);
        return project;
    }

    [Fact]
    public void A_gui_survives_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp);
        GuiAsset gui = project.Assets.OfType<GuiAsset>().Single();
        gui.Width = 200;
        gui.PauseGame = true;
        gui.GridX = 6;
        gui.InventoryOffsetY = -4;

        var reloaded = (GuiAsset)Serializer.Deserialize(Serializer.Serialize(gui), "rune_table");

        Assert.Equal(200, reloaded.Width);
        Assert.True(reloaded.HasInventory);
        Assert.True(reloaded.PauseGame);
        Assert.Equal(6, reloaded.GridX);
        Assert.Equal(-4, reloaded.InventoryOffsetY);
        Assert.Equal(gui.Components, reloaded.Components);
        Assert.Equal(Enum.GetValues<GuiComponentKind>().Length, reloaded.Components.Select(c => c.Kind).Distinct().Count());
        Assert.Equal(GuiSlotMode.Output, reloaded.Components.Single(c => c.Name == "output").SlotMode);
        Assert.Equal("The rune", reloaded.Components.Single(c => c.Name == "rune").Tooltip);
        Assert.Equal(GuiHoverEffect.Blur, reloaded.Components.Single(c => c.Name == "rune").Style.HoverEffect);
        Assert.Equal(GuiAlign.Center, reloaded.Components.Single(c => c.Name == "carve").Style.Horizontal);
        Assert.Equal(gui.Components.Single(c => c.Name == "carve").TextureId, reloaded.Components.Single(c => c.Name == "carve").TextureId);
    }

    [Fact]
    public void The_image_of_a_gui_uses_its_texture()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp);
        GuiAsset gui = project.Assets.OfType<GuiAsset>().Single();
        Guid texture = gui.Components.Single(c => c.Kind == GuiComponentKind.Image).TextureId!.Value;

        Assert.Contains(project.Assets.FindReferencesTo(texture), r => r.Source == gui);
        Assert.Contains(ResourceGroup.Other, ResourceGroups.Of(project.Assets.Find(texture)!, project.Assets));
    }

    [Fact]
    public void A_gui_has_its_3_events_and_one_per_component_that_acts()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp);
        GuiAsset gui = project.Assets.OfType<GuiAsset>().Single();

        IReadOnlyList<MethodSlot> slots = GuiMethodSlots.For(gui);

        // Opened, tick, closed, then the 3 slots, the 2 buttons, the 2 checkboxes and the text input (not the text nor the image).
        Assert.Equal(11, slots.Count);
        Assert.Equal([GuiMethodSlots.Opened, GuiMethodSlots.Tick, GuiMethodSlots.Closed], slots.Take(3).Select(s => s.Key));
        MethodSlot word = GuiMethodSlots.Find(gui, GuiMethodSlots.ComponentKey(gui.Components.Single(c => c.Name == "word")))!;
        Assert.Equal("public static void onWordChanged(Player player, Level level, String text)", word.JavaSignature);
        Assert.Equal(GraphKind.Procedure, word.GraphKind);
        Assert.Equal(["player", "text"], word.Parameters.Select(p => p.Name));

        // The key follows the component, not its name: renaming it keeps its event.
        gui.Components = [.. gui.Components.Select(c => c.Name == "word" ? c with { Name = "rune_word" } : c)];
        Assert.Equal("onRune_wordChanged", GuiMethodSlots.Find(gui, word.Key)!.JavaName);

        // The event of a component that no longer exists is left out, without error.
        // (the graph of the GUI listens to it by name, so it goes too: it would be an error there).
        gui.Components = [.. gui.Components.Where(c => c.Name != "rune_word")];
        project.Assets.Remove(project.Assets.OfType<GraphAsset>().Single(g => g.ResourceId.Value == "rune_table_nodes"));
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        Assert.DoesNotContain(ir.Content.Guis.Single().Methods, m => m.Slot == word.Key);
        Assert.Equal(6, ir.Content.Guis.Single().Methods.Count);
    }

    [Fact]
    public void A_gui_event_must_name_a_component_of_its_gui()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp);
        GraphAsset nodes = project.Assets.OfType<GraphAsset>().Single(g => g.ResourceId.Value == "rune_table_nodes");
        GraphNode pressed = nodes.Graph.Nodes.First(n => n.Kind == GraphNodeKind.OnGuiPressed);
        nodes.Graph = nodes.Graph.ReplaceNode(pressed with { Values = pressed.Values.SetItem("component", "nothing") });
        var diagnostics = new DiagnosticBag();

        ModIRBuilder.CreateDefault().Build(project, diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("\"nothing\" is not a component", StringComparison.Ordinal));
    }

    [Fact]
    public void Wrong_components_are_reported()
    {
        var gui = new GuiAsset(Guid.NewGuid(), ResourceId.Parse("bad"), "Bad")
        {
            Width = 100,
            Height = 60,
            HasInventory = true,
            Components =
            [
                GuiComponent.Create(GuiComponentKind.Button, "go", 80, 10),
                GuiComponent.Create(GuiComponentKind.Text, "Go", 4, 4),
                GuiComponent.Create(GuiComponentKind.Image, "2pic", 4, 20),
            ],
        };
        var diagnostics = new DiagnosticBag();

        gui.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("\"go\"", StringComparison.OrdinalIgnoreCase) && d.Message.Contains("Several", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("2pic", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("goes out", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("no texture", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("too small", StringComparison.Ordinal));
    }

    [Fact]
    public void The_editor_snaps_names_and_places_the_inventory()
    {
        var gui = new GuiAsset(Guid.NewGuid(), ResourceId.Parse("grid"), "Grid") { GridX = 18, GridY = 18, GridOffsetX = 7, GridOffsetY = 3 };

        Assert.Equal((25, 21), gui.Snap(30, 26));
        gui.SnapToGrid = false;
        Assert.Equal((30, 26), gui.Snap(30.2, 25.6));

        gui.Components = [GuiComponent.Create(GuiComponentKind.Button, "button1", 0, 0)];
        Assert.Equal("button2", gui.NewComponentName(GuiComponentKind.Button));
        Assert.Equal("input1", gui.NewComponentName(GuiComponentKind.TextInput));

        // A slot keeps the size of the game; the inventory sits like in a chest of the game.
        gui.Components = [GuiComponent.Create(GuiComponentKind.Slot, "s", 0, 0) with { Width = 40 }];
        Assert.Equal(GuiComponent.SlotSize, gui.Components[0].Width);
        Assert.Equal((7, 83), gui.InventoryOrigin);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task A_gui_becomes_a_menu_and_a_screen(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, loader, version);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        bool modern = version == "1.21.1";
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
        string resources = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources");

        // The menus, opened by identifier; the main class registers them and, on the client, the screens.
        string guis = File.ReadAllText(Path.Combine(java, "gui/ModGuis.java"));
        Assert.Contains("DeferredRegister.create(Registries.MENU", guis);
        Assert.Contains("new MenuType<>(RuneTableMenu::new, FeatureFlags.DEFAULT_FLAGS)", guis);
        Assert.Contains("case \"rune_table\" -> server.openMenu(", guis);
        Assert.Contains("Component.translatable(\"gui.testmod.rune_table\")", guis);
        string main = File.ReadAllText(Directory.GetFiles(java, "*.java").Single(f => File.ReadAllText(f).Contains("@Mod(", StringComparison.Ordinal)));
        Assert.Contains("ModGuis.MENUS.register(modEventBus);", main);
        Assert.Contains("ModGuisClient::registerScreens", main);
        Assert.Contains("ModGuisNetwork.register(modEventBus);", main);
        string client = File.ReadAllText(Path.Combine(java, "client/ModGuisClient.java"));
        Assert.Contains(loader == ModLoaderKind.NeoForge ? "RegisterMenuScreensEvent event" : "MenuScreens.register(ModGuis.RUNE_TABLE.get(), RuneTableScreen::new)", client);
        Assert.Contains("screen.apply(action, component, value);", client);

        // The network: to the server and back, as the loader wants it.
        string network = File.ReadAllText(Path.Combine(java, "gui/ModGuisNetwork.java"));
        Assert.Contains((loader, modern) switch
        {
            (ModLoaderKind.NeoForge, _) => "PacketDistributor.sendToServer(new ToServer(",
            (_, true) => "PacketDistributor.SERVER.noArg()",
            _ => "CHANNEL.sendToServer(new ToServer(",
        }, network);
        Assert.Contains("ModGuis.receive(", network);

        // The menu: its two slots (the output one refuses items), the player's inventory, its events.
        string menu = File.ReadAllText(Path.Combine(java, "gui/RuneTableMenu.java"));
        Assert.Contains("private static final int[] SLOTS = {2, 3, 7};", menu);
        Assert.Contains("public boolean mayPickup(Player player)", menu);
        Assert.Contains("new Slot(this.container, 0, 45, 35)", menu);
        Assert.Contains("public boolean mayPlace(ItemStack stack)", menu);
        Assert.Contains("this.addSlot(new Slot(inventory, column, 8 + column * 18, 142));", menu);
        Assert.Contains("this.initTicked(5, true);", menu);
        Assert.Contains("onOpened(this.player, this.player.level());", menu);
        Assert.Contains("onGlowingTicked(this.player, this.player.level(), \"1\".equals(value));", menu);
        Assert.Contains("onWordChanged(this.player, this.player.level(), value);", menu);
        Assert.Contains("onInputChanged(this.player, this.player.level(), this.container.getItem(0));", menu);
        Assert.Contains("public static void onWordChanged(Player player, Level level, String text) {", menu);
        Assert.Contains("    ModGuis.setTicked(player, \"glowing\", text.length() > 3);", menu);
        Assert.Contains("public static void onEngraveClicked(Player player, Level level) {", menu);
        Assert.Contains("(level, player);", menu);
        string menuBase = File.ReadAllText(Path.Combine(java, "gui/ModGuiMenu.java"));
        Assert.Contains("this.clearContainer(player, this.container);", menuBase);
        Assert.Contains("event(this.slotComponents[s], SLOT, \"\");", menuBase);

        // The screen: the widgets of the version, the texts, the image, the tooltips, the messages to the server.
        string screen = File.ReadAllText(Path.Combine(java, "gui/RuneTableScreen.java"));
        Assert.Contains(modern ? "Checkbox.builder(" : "new Checkbox(", screen);
        Assert.Contains(modern ? ".onValueChange((box, ticked) -> this.send(ModGuiMenu.CHECK, 5," : "RuneTableScreen.this.send(ModGuiMenu.CHECK, 5,", screen);
        Assert.Contains("this.word.setMaxLength(12);", screen);
        Assert.Contains("this.text(8, \"\")", screen);
        Assert.Contains("this.word.setResponder(text -> this.send(ModGuiMenu.TEXT, 6, text));", screen);
        Assert.Contains("button -> this.send(ModGuiMenu.CLICK, 4, \"\")", screen);
        // The look (V0.8.1): the text centered, bigger, wrapped, with its text and color under the mouse.
        Assert.Contains("this.drawText(graphics, this.over(mouseX, mouseY, 8, 6, 60, 10) ? Component.translatable(\"gui.testmod.rune_table.title.hover\") : this.text(0, \"gui.testmod.rune_table.title\"), 8, 6, 60, 10, this.over(mouseX, mouseY, 8, 6, 60, 10) ? 0xFFAA00 : 0x3F3F3F, 1.5f, true, 1, 0, 1, 0, 0.9f, 0f);", screen);
        // The image fitted, tinted, framed, highlighted, turned, blurred under the mouse.
        Assert.Contains("this.drawImage(graphics, IMAGE_RUNE, x + 8, y + 20, 16, 16, 16, 16, 1, 0, 0, false, 0xFF8080, 1f, 1, 0x202020, (this.over(mouseX, mouseY, 8, 20, 16, 16) || false), true, 45f);", screen);
        // The button that is an image: tiled, tinted under the mouse, its text over it.
        Assert.Contains("this.carve = new Button(x + 8, y + 56, 40, 20,", screen);
        Assert.Contains("RuneTableScreen.this.drawImage(graphics, IMAGE_CARVE, this.getX(), this.getY(), 40, 20, 16, 16, 0, 1, 1, true, hovered ? 0x80FF80 : 0xFFFFFF,", screen);
        Assert.Contains("int alonePosition".Replace("alonePosition", "aloneX = place(x + 152, 20, 0, 0, aloneWidth);"), screen);
        string screenBase = File.ReadAllText(Path.Combine(java, "gui/ModGuiScreen.java"));
        Assert.Contains("protected void drawText(GuiGraphics graphics, Component text,", screenBase);
        Assert.Contains("graphics.pose().mulPose(Axis.ZP.rotationDegrees(rotation));", screenBase);
        Assert.Contains("RenderSystem.setShaderColor(red, green, blue, opacity);", screenBase);
        Assert.Contains("\"gui.testmod.rune_table.rune.tooltip\"", screen);
        Assert.Contains("case 4 -> this.engrave.setMessage(Component.literal(value));", screen);
        Assert.Contains(modern ? "ResourceLocation.parse(\"testmod:textures/gui/rune_table/rune.png\")" : "new ResourceLocation(\"testmod:textures/gui/rune_table/rune.png\")", screen);

        // The graphs: the one that opens and closes it, the one of the button.
        string behavior = File.ReadAllText(Path.Combine(java, "behavior/RuneTableEventsBehavior.java"));
        Assert.Contains("ModGuis.close(", behavior);
        Assert.Contains("ModGuis.open(", behavior);
        string engrave = string.Join('\n', Directory.GetFiles(Path.Combine(java, "code"), "*.java").Select(File.ReadAllText).Where(t => t.Contains("ModGuis.setText(", StringComparison.Ordinal)));
        Assert.Contains("ModGuis.text(", engrave);
        Assert.Contains("ModGuis.setSlotItem(", engrave);
        Assert.Contains("ModGuis.isOpen(", engrave);

        // The graph of the GUI (V0.8.1): its event nodes are methods called by the menu, with the action of the screen.
        string nodes = File.ReadAllText(Path.Combine(java, "behavior/RuneTableNodesBehavior.java"));
        Assert.Contains("public static void onGuiPressed1(Player player, Level level, String text, boolean ticked, ItemStack stack)", nodes);
        Assert.Contains("ModGuis.setVisible(player, \"rune\", false);", nodes);
        Assert.Contains("ModGuis.setText(player, \"title\", text);".Replace("text);", "\"typed\");"), nodes);
        Assert.Contains("import com.mineengine.mods.testmod.behavior.RuneTableNodesBehavior;", menu);
        Assert.Contains("if (action == CLICK) {", menu);
        Assert.Contains("RuneTableNodesBehavior.onGuiPressed1(this.player, this.player.level(), value, \"1\".equals(value), ItemStack.EMPTY);", menu);
        Assert.Contains("if (action == RELEASE) {", menu);
        Assert.Contains("RuneTableNodesBehavior.onGuiSlotChanged1(this.player, this.player.level(), value, \"1\".equals(value), this.container.getItem(0));", menu);
        Assert.Contains("RuneTableNodesBehavior.onGuiOpened1(this.player, this.player.level(), \"\", false, ItemStack.EMPTY);", menu);
        // The screen tells the server what the graphs listen to: the release of a button, the mouse over the image button, a click on the image.
        Assert.Contains("RuneTableScreen.this.send(ModGuiMenu.RELEASE, 4, \"\");", screen);
        Assert.Contains("this.track(9, this.over(mouseX, mouseY, 8, 56, 40, 20));", screen);
        Assert.Contains("this.send(ModGuiMenu.CLICK, 1, \"\");", screen);
        Assert.Contains("case 4 -> this.engrave.visible = value;", screen);
        Assert.Contains("if (this.visible[1]) {", screen);

        // The texts in the language file, the image in the textures of the GUI.
        string lang = File.ReadAllText(Path.Combine(resources, "assets/testmod/lang/en_us.json"));
        Assert.Contains("\"gui.testmod.rune_table\": \"Rune table\"", lang);
        Assert.Contains("\"gui.testmod.rune_table.engrave\": \"Engrave\"", lang);
        Assert.Contains("\"gui.testmod.rune_table.title.hover\": \"Engrave a rune\"", lang);
        Assert.True(File.Exists(Path.Combine(resources, "assets/testmod/textures/gui/rune_table/carve.png")));
        Assert.True(File.Exists(Path.Combine(resources, "assets/testmod/textures/gui/rune_table/rune.png")));
        Assert.Equal(AssetIdScope.Gui, AssetType.Gui.IdScope());
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task A_block_an_item_and_a_mob_open_their_gui(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, loader, version);
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        Assert.Equal("rune_table", ir.Content.Blocks.Single(b => b.Id.Value == "rune_table_block").Gui?.Value);
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        bool modern = version == "1.21.1";
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
        string Find(string text) => File.ReadAllText(Directory.GetFiles(java, "*.java", SearchOption.AllDirectories)
            .Single(f => File.ReadAllText(f).Contains(text, StringComparison.Ordinal)));

        // The block keeps the items of the slots in its block entity, saved with the world and dropped when it is broken.
        string entity = File.ReadAllText(Path.Combine(java, "gui/ModGuiBlockEntity.java"));
        Assert.Contains("DeferredRegister.create(Registries.BLOCK_ENTITY_TYPE", entity);
        Assert.Contains("BlockEntityType.Builder.of(ModGuiBlockEntity::new, ModBlocks.RUNE_TABLE_BLOCK.get()).build(null)", entity);
        Assert.Contains("public static final int SIZE = 3;", entity);
        Assert.Contains("return Container.stillValidBlockEntity(ModGuiBlockEntity.this, player);", entity);
        Assert.Contains(modern ? "protected void loadAdditional(CompoundTag tag, HolderLookup.Provider registries)" : "public void load(CompoundTag tag)", entity);
        Assert.Contains(modern ? "ContainerHelper.saveAllItems(tag, list, registries);" : "ContainerHelper.saveAllItems(tag, list);", entity);
        Assert.Contains("ModGuiBlockEntity.BLOCK_ENTITIES.register(modEventBus);", Find("@Mod("));
        string block = Find("class RuneTableBlockBlock");
        Assert.Contains("implements EntityBlock", block);
        Assert.Contains("return new ModGuiBlockEntity(position, state);", block);
        Assert.Contains("ModGuiBindings.blockOpensRuneTable(level, position, player, player, 0.0);", block);
        Assert.Contains("Containers.dropContents(level, position, kept.items());", block);
        Assert.Contains(modern ? "public InteractionResult useWithoutItem(" : "public InteractionResult use(", block);

        // The methods called by the right clicks; the menu works on the items of the block, and leaves them there.
        string bindings = File.ReadAllText(Path.Combine(java, "gui/ModGuiBindings.java"));
        Assert.Contains("ModGuis.open(player, \"rune_table\", block.items());", bindings);
        Assert.Contains("public static void itemOpensRuneTable(Player player, Level level, BlockPos position, Entity other, double amount)", bindings);
        Assert.Contains("public static void mobOpensRuneTable(Entity mob, Level level, BlockPos position, Player player, Entity other, double amount)", bindings);
        Assert.Contains("(containerId, inventory, owner) -> new RuneTableMenu(containerId, inventory, storage)", File.ReadAllText(Path.Combine(java, "gui/ModGuis.java")));
        Assert.Contains("super(ModGuis.RUNE_TABLE.get(), containerId, inventory, NAMES, SLOTS, storage);", File.ReadAllText(Path.Combine(java, "gui/RuneTableMenu.java")));
        string menuBase = File.ReadAllText(Path.Combine(java, "gui/ModGuiMenu.java"));
        Assert.Contains("this.container = storage != null ? storage : new SimpleContainer(slotComponents.length);", menuBase);
        Assert.Contains("if (!this.stored) {", menuBase);
        Assert.Contains("ModGuiBindings.itemOpensRuneTable(player, level, player.blockPosition(), null, 0.0);", Find("itemOpensRuneTable(player"));
        Assert.Contains("ModGuiBindings.mobOpensRuneTable(this, this.level(), this.blockPosition(), player, player, 0.0);", Find("mobOpensRuneTable(this"));
    }

    [Fact]
    public void The_gui_of_a_block_survives_a_save_and_a_deleted_gui_is_an_error()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp);
        GuiAsset gui = project.Assets.OfType<GuiAsset>().Single();
        Assert.Equal(gui.Id, project.Assets.OfType<BlockAsset>().Single(b => b.ResourceId.Value == "rune_table_block").Gui);
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        repository.Save(project);
        ModProject reloaded = repository.Open(project.Layout.RootDirectory);
        Assert.Equal(gui.Id, reloaded.Assets.OfType<ItemAsset>().Single(i => i.ResourceId.Value == "rune_book").Gui);
        Assert.Equal(gui.Id, reloaded.Assets.OfType<MobAsset>().Single(m => m.ResourceId.Value == "rune_keeper").Gui);
        Assert.Contains(project.Assets.FindReferencesTo(gui.Id), r => r.Source.ResourceId.Value == "rune_table_block" && r.Reference.Kind == Core.Assets.AssetReferenceKind.Gui);

        MobAsset keeper = project.Assets.OfType<MobAsset>().Single(m => m.ResourceId.Value == "rune_keeper");
        keeper.Gui = Guid.NewGuid();
        var diagnostics = new DiagnosticBag();
        ModIRBuilder.CreateDefault().Build(project, diagnostics);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Source == keeper.ToString());
    }
}
