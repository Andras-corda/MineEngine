using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
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

/// <summary>Item tab of V0.7.12 (item.md): visual, item, food and tool properties.</summary>
public sealed class ItemTabTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static ModProject CreateProject(TemporaryDirectory temp, ModLoaderKind loader, string version)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        SampleContent.AddTo(project);
        return project;
    }

    private static ItemAsset Item(ModProject project, string id) => project.Assets.OfType<ItemAsset>().Single(i => i.ResourceId.Value == id);

    [Fact]
    public void The_new_item_properties_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        ItemAsset Reload(string id) => (ItemAsset)Serializer.Deserialize(Serializer.Serialize(Item(project, id)), id);

        ItemAsset chisel = Reload("ruby_chisel");
        ItemAsset drill = Reload("ruby_drill");
        ItemAsset juice = Reload("berry_juice");

        Assert.Equal(ItemRemainderKind.ItselfDamaged, chisel.Remainder);
        Assert.True(chisel.HarvestsAnyBlock);
        Assert.Equal("#c:gems/ruby", chisel.RepairItem!.ToStorageText());
        Assert.Equal(Item(project, "ruby_chisel").GlintCondition, chisel.GlintCondition);
        Assert.Equal(new ItemCustomTier { Uses = 900, Speed = 7.5f, Enchantability = 18, MiningLevel = ItemTier.Diamond }, drill.CustomTier);
        Assert.Equal(ItemTier.Custom, drill.Tier);
        Assert.Equal(2.4f, juice.EatSeconds);
        Assert.Equal(ItemUseAnimation.Drink, juice.UseAnimation);
        Assert.Equal("minecraft:glass_bottle", juice.EatResult!.ToStorageText());
        Assert.Equal(Item(project, "cat_figurine").GuiTextureId, Reload("cat_figurine").GuiTextureId);
    }

    [Fact]
    public void A_remainder_that_cannot_wear_out_stays_as_it_is()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        ItemAsset juice = Item(project, "berry_juice");
        juice.Remainder = ItemRemainderKind.ItselfDamaged;
        var diagnostics = new DiagnosticBag();

        juice.Validate(diagnostics);
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, new DiagnosticBag())!;

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("no durability", StringComparison.Ordinal));
        Assert.Equal(ItemRemainderKind.Itself, ir.Content.Items.Single(i => i.Id.Value == "berry_juice").Remainder);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_item_properties_become_methods_of_the_item(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, loader, version);
        bool modern = version != "1.20.1";
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string items = GeneratedItems.Read(Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod"));
        string location(string id) => modern ? $"ResourceLocation.parse(\"{id}\")" : $"new ResourceLocation(\"{id}\")";

        // Tool of a custom material.
        Assert.Contains("public static final Tier TIER = new Tier() {", items);
        Assert.Contains("@Override public int getUses() { return 900; }", items);
        Assert.Contains(modern
            ? "@Override public TagKey<Block> getIncorrectBlocksForDrops() { return BlockTags.INCORRECT_FOR_DIAMOND_TOOL; }"
            : "@Override public int getLevel() { return 3; }", items);
        Assert.Contains(modern ? ".attributes(PickaxeItem.createAttributes(RubyDrillItem.TIER, 1.0f, -2.8f))" : "super(RubyDrillItem.TIER, 1, -2.8f, properties);", items);
        Assert.Contains($"return repairCandidate.is(BuiltInRegistries.ITEM.get({location("testmod:ruby")}));", items);

        // Chisel: stays in the grid and wears out, harvests any block, glows when its function says so.
        Assert.Contains("return remainder.getDamageValue() >= remainder.getMaxDamage() ? ItemStack.EMPTY : remainder;", items);
        Assert.Contains(modern ? "public boolean isCorrectToolForDrops(ItemStack stack, BlockState state) {" : "public boolean isCorrectToolForDrops(BlockState state) {", items);
        Assert.Contains($"return repairCandidate.is(TagKey.create(Registries.ITEM, {location("c:gems/ruby")}));", items);
        Assert.Contains("boolean glint = getGlintCondition(level, entity);", items); // The method of the class wins over the function (V0.7.13).
        Assert.Contains(modern ? "stack.set(DataComponents.ENCHANTMENT_GLINT_OVERRIDE, true);" : "stack.getOrCreateTag().putBoolean(\"mineengine_glint\", true);", items);

        // Juice: drunk in 2.4 s, gives its bottle back, stays in the grid.
        Assert.Contains(modern ? "public int getUseDuration(ItemStack stack, LivingEntity entity) {" : "public int getUseDuration(ItemStack stack) {", items);
        Assert.Contains("return 48;", items);
        Assert.Contains("return UseAnim.DRINK;", items);
        Assert.Contains($"ItemStack leftover = new ItemStack(BuiltInRegistries.ITEM.get({location("minecraft:glass_bottle")}));", items);
        Assert.Contains("return stack.copyWithCount(1);", items);

        // Figurine: flat in the inventories.
        string assets = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources/assets/testmod");
        string figurine = File.ReadAllText(Path.Combine(assets, "models/item/cat_figurine.json"));
        Assert.Contains(loader == ModLoaderKind.NeoForge ? "\"neoforge:separate_transforms\"" : "\"forge:separate_transforms\"", figurine);
        Assert.Contains("\"testmod:item/cat_figurine_gui\"", figurine);
        Assert.True(File.Exists(Path.Combine(assets, "textures/item/cat_figurine_gui.png")));
    }

    [Fact]
    public void The_advanced_and_ranged_properties_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        ItemAsset Reload(string id) => (ItemAsset)Serializer.Deserialize(Serializer.Serialize(Item(project, id)), id);

        ItemAsset ember = Reload("ember_chip");
        Assert.Equal(Item(project, "ember_chip").BurnTimeFunction, ember.BurnTimeFunction);
        Assert.Equal(Item(project, "ember_chip").BurnCondition, ember.BurnCondition);
        Assert.Equal(40, ember.CompostChance);
        Assert.Equal(Item(project, "ember_chip").CompostCondition, ember.CompostCondition);
        Assert.True(ember.PiglinCurrency);
        Assert.Equal(Item(project, "rune_repeater").Ranged, Reload("rune_repeater").Ranged);
        Assert.Equal(Item(project, "frost_wand").Ranged, Reload("frost_wand").Ranged);
    }

    [Fact]
    public void A_food_cannot_shoot()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        ItemAsset juice = Item(project, "berry_juice");
        juice.Ranged = new ItemRanged { Kind = RangedItemKind.Bow };
        var diagnostics = new DiagnosticBag();

        juice.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("ranged item", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_advanced_and_ranged_properties_are_generated(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, loader, version);
        bool modern = version != "1.20.1";
        bool neo = loader == ModLoaderKind.NeoForge;
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter[] emitters = neo ? [new NeoForgeJavaEmitter(), new FuelDataMapEmitter()] : [new ForgeJavaEmitter()];
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitters))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
        string items = GeneratedItems.Read(java);
        string hooks = File.ReadAllText(Path.Combine(java, "registry/ModItemHooks.java"));
        string main = File.ReadAllText(Directory.GetFiles(java, "*.java").Single(f => File.ReadAllText(f).Contains("@Mod(", StringComparison.Ordinal)));

        // Fuel and compost decided by functions, piglins.
        Assert.Contains("public static void onFuelBurnTime(FurnaceFuelBurnTimeEvent event) {", hooks);
        Assert.Contains("if (stack.is(ModItems.EMBER_CHIP.get())) {", hooks);
        Assert.Contains("* 20) : 0);", hooks);
        Assert.Contains("public static void onRightClickBlock(PlayerInteractEvent.RightClickBlock event) {", hooks);
        Assert.Contains("ModItemHooks::onFuelBurnTime", main);
        Assert.Contains("ModItemHooks::onRightClickBlock", main);
        if (neo)
        {
            Assert.DoesNotContain("COMPOSTABLES", hooks);
            string compost = File.ReadAllText(Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources/data/neoforge/data_maps/item/compostables.json"));
            Assert.Contains("\"testmod:ember_chip\"", compost);
            Assert.Contains("0.4", compost);
        }
        else
        {
            Assert.Contains("ComposterBlock.COMPOSTABLES.put(ModItems.EMBER_CHIP.get(), 0.4f);", hooks);
            Assert.Contains("modEventBus.addListener(ModItemHooks::commonSetup);", main);
        }

        Assert.Contains("public boolean isPiglinCurrency(ItemStack stack) {", items);

        // Ranged items.
        Assert.Contains("public class RuneRodItem extends FishingRodItem {", items);
        Assert.Contains("float power = BowItem.getPowerForTime(72000 - timeLeft);", items);
        Assert.Contains(modern
            ? "Arrow projectile = new Arrow(level, player, ammo.isEmpty() ? new ItemStack(Items.ARROW) : ammo.copyWithCount(1), weapon);"
            : "Arrow projectile = new Arrow(level, player);", items);
        Assert.Contains("projectile.setBaseDamage(3.0);", items);
        Assert.Contains(modern ? "weapon.hurtAndBreak(1, player, LivingEntity.getSlotForHand(hand));" : "weapon.hurtAndBreak(1, player, p -> p.broadcastBreakEvent(hand));", items);
        Assert.Contains("Snowball projectile = new Snowball(level, player);", items);
        Assert.Contains("player.getCooldowns().addCooldown(this, 10);", items);
        Assert.Contains("boolean free = true;", items);
        Assert.Contains("if ((72000 - remaining) % 5 == 0) {", items);
        Assert.Contains("return UseAnim.CROSSBOW;", items);
        Assert.Contains("SoundEvents.CROSSBOW_SHOOT", items);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_new_item_events_call_their_graphs(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, loader, version);
        bool modern = version != "1.20.1";
        bool neo = loader == ModLoaderKind.NeoForge;
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = neo ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
        string items = GeneratedItems.Read(java);
        string hooks = File.ReadAllText(Path.Combine(java, "registry/ModItemHooks.java"));
        string behavior = File.ReadAllText(Path.Combine(java, "behavior/ItemEventsV12Behavior.java"));
        string Call(string method, string args) => $"ItemEventsV12Behavior.{method}({args});";

        Assert.Contains("public static void onItemShot1(Player player, Level level, BlockPos position, Entity other, double amount) {", behavior);

        // Bow: the right click, the charge and the shot are in its own methods.
        Assert.Contains(Call("onItemRightClick1", "player, level, player.blockPosition(), null, 0.0"), items);
        Assert.Contains(Call("onItemChargeStarted1", "player, level, player.blockPosition(), null, 0.0"), items);
        Assert.Contains(Call("onItemChargingTick1", "player, level, player.blockPosition(), null, power"), items);
        Assert.Contains("if (72000 - remaining == 20) {", items);
        Assert.Contains(Call("onItemShot1", "player, level, player.blockPosition(), projectile, power"), items);
        Assert.Contains("if (weapon.isEmpty()) {", items);

        // Juice: start, tick and stop of the eating, then the end.
        Assert.Contains(modern ? "if (remaining == this.getUseDuration(stack, entity)) {" : "if (remaining == this.getUseDuration(stack)) {", items);
        Assert.Contains(Call("onItemEatingTick1", "player, level, player.blockPosition(), null, remaining"), items);
        Assert.Contains(Call("onItemEatingStopped1", "player, level, player.blockPosition(), null, timeLeft"), items);
        Assert.Contains(Call("onItemConsumed1", "player, level, player.blockPosition(), null, 0.0"), items);

        // Chisel: a creature clicked, dropped, swung, held; broken and picked up through the loader.
        Assert.Contains("public InteractionResult interactLivingEntity(ItemStack stack, Player player, LivingEntity target, InteractionHand hand) {", items);
        Assert.Contains(Call("onItemUseOnEntity1", "player, player.level(), target.blockPosition(), target, 0.0"), items);
        Assert.Contains("public boolean onDroppedByPlayer(ItemStack stack, Player player) {", items);
        Assert.Contains("if (player.swinging && player.swingTime == 0) {", items);
        Assert.Contains("public static void onItemBroken(PlayerDestroyItemEvent event) {", hooks);
        Assert.Contains(neo ? "public static void onItemPickedUp(ItemEntityPickupEvent.Post event) {" : "public static void onItemPickedUp(PlayerEvent.ItemPickupEvent event) {", hooks);
        Assert.Contains(Call("onItemPickedUp1", neo
            ? "player, level, player.blockPosition(), event.getItemEntity(), stack.getCount()"
            : "player, level, player.blockPosition(), event.getOriginalEntity(), stack.getCount()"), hooks);
    }

    [Fact]
    public void The_methods_of_an_item_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        ItemAsset chisel = Item(project, "ruby_chisel");
        GraphAsset procedure = project.Assets.OfType<GraphAsset>().Single(g => g.ResourceId.Value == "ruby_chisel_on_dropped");

        var reloaded = (ItemAsset)Serializer.Deserialize(Serializer.Serialize(chisel), "ruby_chisel");
        var graph = (GraphAsset)Serializer.Deserialize(Serializer.Serialize(procedure), "ruby_chisel_on_dropped");

        Assert.Equal(chisel.Methods, reloaded.Methods);
        Assert.Equal(chisel.Id, graph.ElementId);
    }

    [Fact]
    public void A_nodal_method_without_its_graph_is_reported()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        ItemAsset chisel = Item(project, "ruby_chisel");
        chisel.Methods = [.. chisel.Methods, ElementMethod.Nodal(ItemMethodSlots.EventKey(ItemEventKind.Shot), Guid.NewGuid())];
        var diagnostics = new DiagnosticBag();

        ModIRBuilder.CreateDefault().Build(project, diagnostics);

        // The reference to the graph is broken: the check of the project reports it before the lowering.
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Source.Contains("ruby_chisel", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_methods_of_an_item_are_written_in_its_class(ModLoaderKind loader, string version)
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
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");
        string chisel = File.ReadAllText(Path.Combine(java, "item/RubyChiselItem.java"));
        string ember = File.ReadAllText(Path.Combine(java, "item/EmberChipItem.java"));
        string hooks = File.ReadAllText(Path.Combine(java, "registry/ModItemHooks.java"));

        // Java methods keep their code; the glint condition is asked each tick.
        Assert.Contains("public static boolean getGlintCondition(Level level, Entity entity) {", chisel);
        Assert.Contains("        return entity != null && entity.isShiftKeyDown();", chisel);
        Assert.Contains("boolean glint = getGlintCondition(level, entity);", chisel);
        Assert.DoesNotContain("import com.mineengine.mods.testmod.item.RubyChiselItem;", chisel);

        // A nodal method calls the graph of the item, with the world first.
        Assert.Contains("public static void onDropped(Player player, Level level, BlockPos position, Entity other, double amount) {", chisel);
        Assert.Contains("(level, player, position, other, amount);", chisel);
        Assert.Contains("RubyChiselItem.onDropped(player, player.level(), player.blockPosition(), null, 0.0);", chisel);

        // The events of the loader and the fuel call the methods of the classes.
        Assert.Contains("RubyChiselItem.onPickedUp(player, level, player.blockPosition(), ", hooks);
        Assert.Contains("import com.mineengine.mods.testmod.item.RubyChiselItem;", hooks);
        Assert.Contains("public static double getBurnTime(Level level, Entity entity) {", ember);
        Assert.Contains("(int) Math.round((double) (EmberChipItem.getBurnTime(level, null)) * 20)", hooks);
        Assert.Contains("!(EmberChipItem.getCompostCondition(level, player))", hooks);
        Assert.Contains("public static boolean getCompostCondition(Level level, Entity entity) {", ember);
    }

    [Fact]
    public void The_graph_of_a_new_method_takes_the_parameters_and_the_result_of_its_place()
    {
        MethodSlot glint = ItemMethodSlots.Find(ItemMethodSlots.GlintCondition)!;
        MethodSlot shot = ItemMethodSlots.Find(ItemMethodSlots.EventKey(ItemEventKind.Shot))!;
        var element = Guid.NewGuid();

        GraphAsset function = glint.CreateGraph(ResourceId.Parse("thing_getglintcondition"), element);
        GraphAsset procedure = shot.CreateGraph(ResourceId.Parse("thing_onshot"), element);
        var diagnostics = new DiagnosticBag();
        function.Validate(diagnostics);
        procedure.Validate(diagnostics);

        Assert.Equal(GraphKind.Function, function.Kind);
        Assert.Equal(element, function.ElementId);
        Assert.Equal(["entity"], function.Parameters.Select(p => p.Name));
        Assert.Equal(PinType.Boolean, function.Returns.Single().Type.Kind);
        Assert.Equal(2, function.Graph.Nodes.Count());
        Assert.Equal(GraphKind.Procedure, procedure.Kind);
        Assert.Equal(["player", "position", "other", "amount"], procedure.Parameters.Select(p => p.Name));
        Assert.Empty(procedure.Returns);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("public static void onShot(Player player, Level level, BlockPos position, Entity other, double amount)", shot.JavaSignature);
        Assert.Equal("return false;", glint.DefaultJavaBody);
    }
}
