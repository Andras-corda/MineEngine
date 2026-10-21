using System.Globalization;
using MineEngine.Assets.Graphs;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Java code of V0.6.2 and V0.6.3: commands (Brigadier), keybinds (client), enchantments, fluids,
/// game rules and status effects with their potions. Creative tabs are in the class of the tabs.
/// </summary>
public abstract partial class ModJavaEmitter
{
    // ----- Loader -----

    /// <summary>Imports of a DeferredRegister created with a vanilla registry key and of its holders.</summary>
    protected abstract IReadOnlyList<string> RegistryImports { get; }

    /// <summary>Type of a registered value ("RegistryObject&lt;MobEffect&gt;", "DeferredHolder&lt;MobEffect, MobEffect&gt;").</summary>
    protected abstract string RegistryHolderType(string registryType, string valueType);

    /// <summary>Java expression of the Holder of a registered value.</summary>
    protected abstract string HolderOf(string field);

    protected abstract string RegisterCommandsEventType { get; }

    protected abstract string RegisterKeyMappingsEventType { get; }

    /// <summary>Class of the input events (its nested class Key is used).</summary>
    protected abstract string InputEventType { get; }

    protected abstract string ClientSetupEventType { get; }

    protected abstract (string Expression, string Import) FluidTypesRegistry { get; }

    protected abstract string FluidTypeClass { get; }

    /// <summary>ForgeFlowingFluid (Forge) or BaseFlowingFluid (NeoForge): its Source, Flowing and Properties.</summary>
    protected abstract string FlowingFluidClass { get; }

    protected abstract string ClientFluidExtensionsClass { get; }

    /// <summary>Event that receives the client extensions of fluids (NeoForge), or null when the fluid type gives them (Forge).</summary>
    protected abstract string? RegisterClientExtensionsEventType { get; }

    // ----- Names -----

    private const string CommandsClass = "ModCommands";
    private const string KeybindsClass = "ModKeybinds";
    private const string EnchantmentsClass = "ModEnchantments";
    private const string FluidsClass = "ModFluids";
    private const string FluidsClientClass = "ModFluidsClient";
    private const string GameRulesClass = "ModGameRules";
    private const string EffectsClass = "ModEffects";

    /// <summary>Hidden command through which a keybind (client) runs its events on the server.</summary>
    private static string KeyCommand(GenerationContext context) => context.Mod.ModId.Value + "_key";

    private static string ConstantOf(ResourceId id) => JavaNames.ToConstantName(id);

    private static string ConstantOf(string id) => id.ToUpperInvariant();

    // ----- Entry point -----

    private void WriteGameplay(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        IRContent content = context.Ir.Content;
        if (content.Commands.Count > 0 || content.Keybinds.Count > 0)
        {
            Write(context, naming, naming.RegistryPackage, CommandsClass, WriteCommandsClass(context, naming));
        }

        if (content.Keybinds.Count > 0)
        {
            Write(context, naming, naming.ClientPackage, KeybindsClass, WriteKeybindsClass(context, naming));
        }

        if (content.Enchantments.Count > 0 && !api.EnchantmentsAreData)
        {
            Write(context, naming, naming.RegistryPackage, EnchantmentsClass, WriteEnchantmentsClass(context, naming));
            foreach (IREnchantment enchantment in content.Enchantments)
            {
                WriteEnchantmentClass(context, naming, enchantment);
            }
        }

        if (content.Fluids.Count > 0)
        {
            Write(context, naming, naming.RegistryPackage, FluidsClass, WriteFluidsClass(context, naming, api));
            Write(context, naming, naming.ClientPackage, FluidsClientClass, WriteFluidsClientClass(context, naming));
        }

        if (content.GameRules.Count > 0)
        {
            Write(context, naming, naming.RegistryPackage, GameRulesClass, WriteGameRulesClass(context, naming));
        }

        if (content.Effects.Count > 0)
        {
            Write(context, naming, naming.RegistryPackage, EffectsClass, WriteEffectsClass(context, naming, api));
            foreach (IREffect effect in content.Effects)
            {
                WriteEffectClass(context, naming, api, effect);
            }
        }
    }

    /// <summary>Imports of the main class for the registrations below.</summary>
    private IEnumerable<string> GameplayMainImports(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        IRContent content = context.Ir.Content;
        if (content.Commands.Count > 0 || content.Keybinds.Count > 0)
        {
            yield return $"{naming.RegistryPackage}.{CommandsClass}";
            yield return GameEventBus.Import;
        }

        if (content.Keybinds.Count > 0)
        {
            yield return $"{naming.ClientPackage}.{KeybindsClass}";
        }

        if (content.Enchantments.Count > 0 && !api.EnchantmentsAreData)
        {
            yield return $"{naming.RegistryPackage}.{EnchantmentsClass}";
        }

        if (content.Fluids.Count > 0)
        {
            yield return $"{naming.RegistryPackage}.{FluidsClass}";
            yield return $"{naming.ClientPackage}.{FluidsClientClass}";
        }

        if (content.GameRules.Count > 0)
        {
            yield return $"{naming.RegistryPackage}.{GameRulesClass}";
        }

        if (content.Effects.Count > 0)
        {
            yield return $"{naming.RegistryPackage}.{EffectsClass}";
        }

        if (content.Keybinds.Count > 0 || content.Fluids.Count > 0)
        {
            foreach (string import in ClientEnvironmentImports)
            {
                yield return import;
            }
        }
    }

    /// <summary>Registrations of the main class: registries on the mod bus, commands on the game bus, client listeners.</summary>
    private void WriteGameplayRegistrations(JavaSourceWriter w, GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        IRContent content = context.Ir.Content;
        string gameBus = GameEventBus.Expression;
        if (content.GameRules.Count > 0)
        {
            w.Line($"{GameRulesClass}.init();");
        }

        if (content.Enchantments.Count > 0 && !api.EnchantmentsAreData)
        {
            w.Line($"{EnchantmentsClass}.ENCHANTMENTS.register(modEventBus);");
        }

        if (content.Fluids.Count > 0)
        {
            w.Line($"{FluidsClass}.FLUID_TYPES.register(modEventBus);")
                .Line($"{FluidsClass}.FLUIDS.register(modEventBus);")
                .Line($"{FluidsClass}.BLOCKS.register(modEventBus);")
                .Line($"{FluidsClass}.ITEMS.register(modEventBus);");
        }

        if (content.Effects.Count > 0)
        {
            w.Line($"{EffectsClass}.MOB_EFFECTS.register(modEventBus);");
            if (content.Effects.Any(e => e.PotionTicks > 0))
            {
                w.Line($"{EffectsClass}.POTIONS.register(modEventBus);");
            }
        }

        if (content.Commands.Count > 0 || content.Keybinds.Count > 0)
        {
            w.Line($"{gameBus}.addListener({CommandsClass}::register);");
        }

        if (content.Keybinds.Count > 0 || content.Fluids.Count > 0)
        {
            w.OpenBlock($"if ({ClientEnvironmentCheck})");
            if (content.Keybinds.Count > 0)
            {
                w.Line($"modEventBus.addListener({KeybindsClass}::registerKeyMappings);")
                    .Line($"{gameBus}.addListener({KeybindsClass}::onKeyInput);");
            }

            if (content.Fluids.Count > 0)
            {
                w.Line($"modEventBus.addListener({FluidsClientClass}::clientSetup);");
                if (RegisterClientExtensionsEventType is not null)
                {
                    w.Line($"modEventBus.addListener({FluidsClientClass}::registerClientExtensions);");
                }
            }

            w.CloseBlock();
        }
    }

    // ----- Commands -----

    private JavaSourceWriter WriteCommandsClass(GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        string file = naming.SourcePath(naming.RegistryPackage, CommandsClass);
        var imports = new SortedSet<string>(StringComparer.Ordinal)
        {
            RegisterCommandsEventType,
            "com.mojang.brigadier.CommandDispatcher",
            "com.mojang.brigadier.arguments.StringArgumentType",
            "com.mojang.brigadier.context.CommandContext",
            "com.mojang.brigadier.exceptions.CommandSyntaxException",
            "net.minecraft.commands.CommandSourceStack",
            "net.minecraft.commands.Commands",
            "net.minecraft.core.BlockPos",
            "net.minecraft.network.chat.Component",
            "net.minecraft.server.level.ServerPlayer",
            "net.minecraft.world.entity.player.Player",
            "net.minecraft.world.level.Level",
        };
        if (content.Commands.Any(c => c.Argument == CommandArgumentKind.Number))
        {
            imports.Add("com.mojang.brigadier.arguments.IntegerArgumentType");
        }

        if (content.Commands.Any(c => c.Argument == CommandArgumentKind.Player))
        {
            imports.Add("net.minecraft.commands.arguments.EntityArgument");
        }

        // The handlers of each command and keybind: "On command" and "On key pressed" events of the graphs.
        List<(GraphNodeKind Event, string Trigger, string Call)> handlers = [.. content.Behaviors
            .SelectMany(b => HandlerMethods(b)
                .Where(h => h.Handler.Trigger is not null)
                .Select(h => (h.Handler.Event, h.Handler.Trigger!, $"{naming.BehaviorClassFor(b.Id)}.{h.Method}")))];
        imports.UnionWith(handlers.Select(h => $"{naming.BehaviorPackage}.{h.Call[..h.Call.IndexOf('.')]}"));

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Commandes du mod : chaque commande lance les événements « Sur commande » des graphes.")
            .OpenBlock($"public final class {CommandsClass}")
            .OpenBlock($"public static void register({SimpleName(RegisterCommandsEventType)} event)")
            .Line("CommandDispatcher<CommandSourceStack> dispatcher = event.getDispatcher();");

        foreach (IRCommand command in content.Commands)
        {
            int first = w.NextLineNumber;
            string method = "run" + JavaNames.ToTypeName(command.Id.Value, "Command");
            string name = JavaNames.StringLiteral(command.Id.Value);
            string argument = JavaNames.StringLiteral(command.ArgumentName);
            w.Line($"dispatcher.register(Commands.literal({name})")
                .Indent().Indent()
                .Line($".requires(source -> source.hasPermission({command.PermissionLevel}))");
            (string? type, string getter) = command.Argument switch
            {
                CommandArgumentKind.Number => ($"IntegerArgumentType.integer({command.Minimum}, {command.Maximum})",
                    $"{method}(context, IntegerArgumentType.getInteger(context, {argument}), \"\", null)"),
                CommandArgumentKind.Word => ("StringArgumentType.word()", $"{method}(context, 0, StringArgumentType.getString(context, {argument}), null)"),
                CommandArgumentKind.Message => ("StringArgumentType.greedyString()", $"{method}(context, 0, StringArgumentType.getString(context, {argument}), null)"),
                CommandArgumentKind.Player => ("EntityArgument.player()", $"{method}(context, 0, \"\", EntityArgument.getPlayer(context, {argument}))"),
                _ => (null, $"{method}(context, 0, \"\", null)"),
            };
            if (type is null)
            {
                w.Line($".executes(context -> {getter}));");
            }
            else
            {
                w.Line($".then(Commands.argument({argument}, {type})")
                    .Indent().Indent()
                    .Line($".executes(context -> {getter})));")
                    .Unindent().Unindent();
            }

            w.Unindent().Unindent();
            context.SourceMap.Add(file, first, w.NextLineNumber - 1, command.SourceAssetId, $"Commande '{command.Id}'");
        }

        if (content.Keybinds.Count > 0)
        {
            w.Line($"dispatcher.register(Commands.literal({JavaNames.StringLiteral(KeyCommand(context))})")
                .Indent().Indent()
                .Line(".then(Commands.argument(\"key\", StringArgumentType.word())")
                .Indent().Indent()
                .Line(".executes(context -> runKey(context, StringArgumentType.getString(context, \"key\")))));")
                .Unindent().Unindent().Unindent().Unindent();
        }

        w.CloseBlock();

        foreach (IRCommand command in content.Commands)
        {
            int first = w.NextLineNumber;
            string method = "run" + JavaNames.ToTypeName(command.Id.Value, "Command");
            w.Line()
                .OpenBlock($"private static int {method}(CommandContext<CommandSourceStack> context, int number, String text, Player target) throws CommandSyntaxException")
                .Line("ServerPlayer player = context.getSource().getPlayerOrException();")
                .Line("Level level = player.level();")
                .Line("BlockPos position = player.blockPosition();")
                .Line("Player chosen = target == null ? player : target;");
            foreach ((_, _, string call) in handlers.Where(h => h.Event == GraphNodeKind.OnCommand && h.Trigger == command.Id.Value))
            {
                w.Line($"{call}(player, level, position, number, text, chosen);");
            }

            if (command.Feedback.Length > 0)
            {
                w.Line($"context.getSource().sendSuccess(() -> Component.literal({JavaNames.StringLiteral(command.Feedback)}), false);");
            }

            w.Line("return 1;").CloseBlock();
            context.SourceMap.Add(file, first, w.NextLineNumber - 1, command.SourceAssetId, $"Commande '{command.Id}'");
        }

        if (content.Keybinds.Count > 0)
        {
            w.Line()
                .Line("/** A keybind of the mod was pressed on the client of the player. */")
                .OpenBlock("private static int runKey(CommandContext<CommandSourceStack> context, String key) throws CommandSyntaxException")
                .Line("ServerPlayer player = context.getSource().getPlayerOrException();")
                .Line("Level level = player.level();")
                .Line("BlockPos position = player.blockPosition();")
                .OpenBlock("switch (key)");
            foreach (IRKeybind keybind in content.Keybinds)
            {
                w.Line($"case {JavaNames.StringLiteral(keybind.Id.Value)} -> {{").Indent();
                foreach ((_, _, string call) in handlers.Where(h => h.Event == GraphNodeKind.OnKeyPressed && h.Trigger == keybind.Id.Value))
                {
                    w.Line($"{call}(player, level, position);");
                }

                w.Unindent().Line("}");
            }

            w.Line("default -> {").Line("}").CloseBlock().Line("return 1;").CloseBlock();
        }

        return WritePrivateConstructor(w, CommandsClass);
    }

    // ----- Keybinds (client) -----

    private JavaSourceWriter WriteKeybindsClass(GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        string category = context.Paths.KeyCategoryTranslationKey;
        var imports = new[]
        {
            RegisterKeyMappingsEventType,
            InputEventType,
            "com.mojang.blaze3d.platform.InputConstants",
            "net.minecraft.client.KeyMapping",
            "net.minecraft.client.Minecraft",
        };

        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports, "Touches du mod (client) : une touche appuyée lance ses événements sur le serveur.")
            .OpenBlock($"public final class {KeybindsClass}");
        foreach (IRKeybind keybind in content.Keybinds)
        {
            w.Line($"public static final KeyMapping {ConstantOf(keybind.Id)} = new KeyMapping({JavaNames.StringLiteral(context.Paths.KeyTranslationKey(keybind.Id))}, "
                   + $"InputConstants.Type.KEYSYM, {keybind.KeyCode}, {JavaNames.StringLiteral(category)});");
        }

        w.Line().OpenBlock($"public static void registerKeyMappings({SimpleName(RegisterKeyMappingsEventType)} event)");
        foreach (IRKeybind keybind in content.Keybinds)
        {
            w.Line($"event.register({ConstantOf(keybind.Id)});");
        }

        w.CloseBlock()
            .Line()
            .Line("/** Keys pressed in the game (not in a menu) are sent to the server, which runs their events. */")
            .OpenBlock($"public static void onKeyInput({SimpleName(InputEventType)}.Key event)")
            .Line("Minecraft minecraft = Minecraft.getInstance();")
            .OpenBlock("if (minecraft.player == null || minecraft.screen != null)")
            .Line("return;")
            .CloseBlock();
        foreach (IRKeybind keybind in content.Keybinds)
        {
            w.OpenBlock($"while ({ConstantOf(keybind.Id)}.consumeClick())")
                .Line($"minecraft.player.connection.sendCommand({JavaNames.StringLiteral($"{KeyCommand(context)} {keybind.Id}")});")
                .CloseBlock();
        }

        w.CloseBlock();
        return WritePrivateConstructor(w, KeybindsClass);
    }

    // ----- Enchantments (1.20.1: Java classes; 1.21: data, see GameplayResourcesEmitter) -----

    private JavaSourceWriter WriteEnchantmentsClass(GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        string file = naming.SourcePath(naming.RegistryPackage, EnchantmentsClass);
        var imports = new List<string>(RegistryImports)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            "net.minecraft.core.registries.Registries",
            "net.minecraft.world.item.enchantment.Enchantment",
        };
        imports.AddRange(content.Enchantments.Select(e => $"{naming.RootPackage}.enchantment.{EnchantmentClassFor(e.Id)}"));

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Enchantements du mod.")
            .OpenBlock($"public final class {EnchantmentsClass}")
            .Line($"public static final DeferredRegister<Enchantment> ENCHANTMENTS = DeferredRegister.create(Registries.ENCHANTMENT, {naming.MainClass}.MOD_ID);");
        foreach (IREnchantment enchantment in content.Enchantments)
        {
            int first = w.NextLineNumber;
            w.Line()
                .Line($"public static final {RegistryHolderType("Enchantment", "Enchantment")} {ConstantOf(enchantment.Id)} =")
                .Indent().Indent()
                .Line($"ENCHANTMENTS.register({JavaNames.StringLiteral(enchantment.Id.Value)}, {EnchantmentClassFor(enchantment.Id)}::new);")
                .Unindent().Unindent();
            context.SourceMap.Add(file, first, w.NextLineNumber - 1, enchantment.SourceAssetId, $"Enchantement '{enchantment.Id}'");
        }

        return WritePrivateConstructor(w, EnchantmentsClass);
    }

    private static string EnchantmentClassFor(ResourceId id) => JavaNames.ToTypeName(id.Value, "Mod") + "Enchantment";

    private static void WriteEnchantmentClass(GenerationContext context, JavaModNaming naming, IREnchantment enchantment)
    {
        string package = naming.RootPackage + ".enchantment";
        string className = EnchantmentClassFor(enchantment.Id);
        var imports = new SortedSet<string>(StringComparer.Ordinal)
        {
            "net.minecraft.world.entity.EquipmentSlot",
            "net.minecraft.world.item.enchantment.Enchantment",
            "net.minecraft.world.item.enchantment.EnchantmentCategory",
        };
        if (enchantment.DamageBonus > 0)
        {
            imports.Add("net.minecraft.world.entity.MobType");
        }

        string rarity = enchantment.Rarity switch
        {
            EnchantmentRarity.Common => "COMMON",
            EnchantmentRarity.Uncommon => "UNCOMMON",
            EnchantmentRarity.Rare => "RARE",
            _ => "VERY_RARE",
        };
        string slots = string.Join(", ", LegacySlots(enchantment.Target).Select(s => "EquipmentSlot." + s));

        JavaSourceWriter w = WriteHeader(package, imports, $"Enchantement « {enchantment.Name} ».")
            .OpenBlock($"public class {className} extends Enchantment")
            .OpenBlock($"public {className}()")
            .Line($"super(Enchantment.Rarity.{rarity}, EnchantmentCategory.{LegacyCategory(enchantment.Target)}, new EquipmentSlot[] {{ {slots} }});")
            .CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public int getMaxLevel()").Line($"return {enchantment.MaxLevel};").CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public int getMinCost(int level)").Line("return 1 + (level - 1) * 10;").CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public int getMaxCost(int level)").Line("return this.getMinCost(level) + 50;").CloseBlock();
        if (enchantment.Treasure)
        {
            w.Line().Line("@Override").OpenBlock("public boolean isTreasureOnly()").Line("return true;").CloseBlock();
        }

        if (enchantment.Curse)
        {
            w.Line().Line("@Override").OpenBlock("public boolean isCurse()").Line("return true;").CloseBlock();
        }

        if (enchantment.DamageBonus > 0)
        {
            w.Line().Line("@Override")
                .OpenBlock("public float getDamageBonus(int level, MobType type)")
                .Line($"return level * {JavaNames.FloatLiteral(enchantment.DamageBonus)};")
                .CloseBlock();
        }

        w.CloseBlock();
        string path = naming.SourcePath(package, className);
        context.Files.WriteText(path, w.ToString());
        context.SourceMap.Add(path, 1, w.NextLineNumber - 1, enchantment.SourceAssetId, $"Enchantement '{enchantment.Id}'");
    }

    private static string LegacyCategory(EnchantmentTarget target) => target switch
    {
        EnchantmentTarget.Armor => "ARMOR",
        EnchantmentTarget.Helmet => "ARMOR_HEAD",
        EnchantmentTarget.Chestplate => "ARMOR_CHEST",
        EnchantmentTarget.Leggings => "ARMOR_LEGS",
        EnchantmentTarget.Boots => "ARMOR_FEET",
        EnchantmentTarget.Tool => "DIGGER",
        EnchantmentTarget.Bow => "BOW",
        EnchantmentTarget.Crossbow => "CROSSBOW",
        EnchantmentTarget.Trident => "TRIDENT",
        EnchantmentTarget.FishingRod => "FISHING_ROD",
        EnchantmentTarget.Breakable => "BREAKABLE",
        _ => "WEAPON",
    };

    private static IReadOnlyList<string> LegacySlots(EnchantmentTarget target) => target switch
    {
        EnchantmentTarget.Armor => ["HEAD", "CHEST", "LEGS", "FEET"],
        EnchantmentTarget.Helmet => ["HEAD"],
        EnchantmentTarget.Chestplate => ["CHEST"],
        EnchantmentTarget.Leggings => ["LEGS"],
        EnchantmentTarget.Boots => ["FEET"],
        EnchantmentTarget.Breakable => ["MAINHAND", "OFFHAND", "HEAD", "CHEST", "LEGS", "FEET"],
        _ => ["MAINHAND"],
    };

    // ----- Fluids -----

    private JavaSourceWriter WriteFluidsClass(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        IRContent content = context.Ir.Content;
        string file = naming.SourcePath(naming.RegistryPackage, FluidsClass);
        string flowing = SimpleName(FlowingFluidClass);
        bool clientInType = RegisterClientExtensionsEventType is null;
        var imports = new SortedSet<string>(RegistryImports, StringComparer.Ordinal)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            FluidTypesRegistry.Import,
            FluidTypeClass,
            FlowingFluidClass,
            "net.minecraft.core.registries.Registries",
            "net.minecraft.world.item.BucketItem",
            "net.minecraft.world.item.Item",
            "net.minecraft.world.item.Items",
            "net.minecraft.world.level.block.Block",
            "net.minecraft.world.level.block.Blocks",
            "net.minecraft.world.level.block.LiquidBlock",
            "net.minecraft.world.level.block.state.BlockBehaviour",
            "net.minecraft.world.level.material.FlowingFluid",
            "net.minecraft.world.level.material.Fluid",
        };
        if (clientInType)
        {
            imports.Add(ClientFluidExtensionsClass);
            imports.Add("java.util.function.Consumer");
            imports.Add("net.minecraft.resources.ResourceLocation");
        }

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Fluides du mod : type, source, fluide qui coule, bloc et seau.")
            .OpenBlock($"public final class {FluidsClass}")
            .Line($"public static final DeferredRegister<FluidType> FLUID_TYPES = DeferredRegister.create({FluidTypesRegistry.Expression}, {naming.MainClass}.MOD_ID);")
            .Line($"public static final DeferredRegister<Fluid> FLUIDS = DeferredRegister.create(Registries.FLUID, {naming.MainClass}.MOD_ID);")
            .Line($"public static final DeferredRegister<Block> BLOCKS = DeferredRegister.create(Registries.BLOCK, {naming.MainClass}.MOD_ID);")
            .Line($"public static final DeferredRegister<Item> ITEMS = DeferredRegister.create(Registries.ITEM, {naming.MainClass}.MOD_ID);");

        foreach (IRFluid fluid in content.Fluids)
        {
            int first = w.NextLineNumber;
            string c = ConstantOf(fluid.Id);
            string id = fluid.Id.Value;
            string type = RegistryHolderType("FluidType", "FluidType");
            w.Line()
                .Line($"public static final {type} {c}_TYPE = FLUID_TYPES.register({JavaNames.StringLiteral(id)}, () -> new FluidType(FluidType.Properties.create()")
                .Indent().Indent()
                .Line($".lightLevel({fluid.LightLevel}).density({fluid.Density}).viscosity({fluid.Viscosity}).canConvertToSource({(fluid.Infinite ? "true" : "false")}))");
            if (clientInType)
            {
                // Forge: the fluid type gives its textures and its color to the client.
                w.Unindent().Unindent().Line("{").Indent()
                    .Line("@Override")
                    .OpenBlock("public void initializeClient(Consumer<IClientFluidTypeExtensions> consumer)")
                    .Line($"consumer.accept({naming.ClientPackage}.{FluidsClientClass}.extensions({FluidTextureArguments(context, api, fluid)}));")
                    .CloseBlock()
                    .Unindent().Line("});");
            }
            else
            {
                w.Line(");").Unindent().Unindent();
            }

            string fluidType = RegistryHolderType("Fluid", "FlowingFluid");
            w.Line($"public static final {fluidType} {c} = FLUIDS.register({JavaNames.StringLiteral(id)}, () -> new {flowing}.Source({FluidsClass}.{c}_PROPERTIES));")
                .Line($"public static final {fluidType} {c}_FLOWING = FLUIDS.register({JavaNames.StringLiteral(id + "_flowing")}, () -> new {flowing}.Flowing({FluidsClass}.{c}_PROPERTIES));")
                .Line($"public static final {RegistryHolderType("Block", "LiquidBlock")} {c}_BLOCK = BLOCKS.register({JavaNames.StringLiteral(id)},")
                .Indent().Indent()
                .Line($"() -> new LiquidBlock({api.FluidArgument(c)}, {api.CopyBlockProperties("Blocks.WATER")}.noLootTable().lightLevel(state -> {fluid.LightLevel})));")
                .Unindent().Unindent();
            if (fluid.BucketId is { } bucket)
            {
                w.Line($"public static final {RegistryHolderType("Item", "Item")} {ConstantOf(bucket)} = ITEMS.register({JavaNames.StringLiteral(bucket)},")
                    .Indent().Indent()
                    .Line($"() -> new BucketItem({api.FluidArgument(c)}, new Item.Properties().craftRemainder(Items.BUCKET).stacksTo(1)));")
                    .Unindent().Unindent();
            }

            w.Line($"private static final {flowing}.Properties {c}_PROPERTIES = new {flowing}.Properties({c}_TYPE, {c}, {c}_FLOWING)")
                .Indent().Indent()
                .Line($".slopeFindDistance({fluid.SpreadDistance}).levelDecreasePerBlock({fluid.LevelDecrease}).tickRate({fluid.FlowSpeed})")
                .Line(fluid.BucketId is { } bucketId ? $".block({c}_BLOCK).bucket({ConstantOf(bucketId)});" : $".block({c}_BLOCK);")
                .Unindent().Unindent();
            context.SourceMap.Add(file, first, w.NextLineNumber - 1, fluid.SourceAssetId, $"Fluide '{fluid.Id}'");
        }

        return WritePrivateConstructor(w, FluidsClass);
    }

    /// <summary>Still texture, flowing texture and tint of a fluid: its textures, or those of water tinted by its color.</summary>
    private string FluidTextureArguments(GenerationContext context, JavaGameApi api, IRFluid fluid)
    {
        string Location(string path) => api.ParseResourceLocation(JavaNames.StringLiteral(path));
        string still = fluid.StillTexture is null ? "minecraft:block/water_still" : context.Paths.Reference($"block/{fluid.Id}_still");
        string flow = fluid.FlowingTexture is null ? "minecraft:block/water_flow" : context.Paths.Reference($"block/{fluid.Id}_flow");
        int tint = fluid.StillTexture is null ? fluid.Color : 0xFFFFFF;
        return $"{Location(still)}, {Location(flow)}, 0xFF{tint.ToString("X6", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Client side of the fluids: their textures and color, and their translucent rendering.</summary>
    private JavaSourceWriter WriteFluidsClientClass(GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        JavaGameApi api = JavaGameApi.For(context.MinecraftVersion);
        var imports = new SortedSet<string>(StringComparer.Ordinal)
        {
            ClientSetupEventType,
            ClientFluidExtensionsClass,
            $"{naming.RegistryPackage}.{FluidsClass}",
            "net.minecraft.client.renderer.ItemBlockRenderTypes",
            "net.minecraft.client.renderer.RenderType",
            "net.minecraft.resources.ResourceLocation",
        };
        if (RegisterClientExtensionsEventType is { } eventType)
        {
            imports.Add(eventType);
        }

        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports, "Affichage des fluides du mod (client).")
            .OpenBlock($"public final class {FluidsClientClass}")
            .Line("/** Textures and color of a fluid. */")
            .OpenBlock("public static IClientFluidTypeExtensions extensions(ResourceLocation still, ResourceLocation flowing, int tint)")
            .OpenBlock("return new IClientFluidTypeExtensions()")
            .Line("@Override")
            .OpenBlock("public ResourceLocation getStillTexture()").Line("return still;").CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public ResourceLocation getFlowingTexture()").Line("return flowing;").CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public int getTintColor()").Line("return tint;").CloseBlock()
            .CloseBlock(";")
            .CloseBlock();

        if (RegisterClientExtensionsEventType is { } registerEvent)
        {
            w.Line().OpenBlock($"public static void registerClientExtensions({SimpleName(registerEvent)} event)");
            foreach (IRFluid fluid in content.Fluids)
            {
                w.Line($"event.registerFluidType(extensions({FluidTextureArguments(context, api, fluid)}), {FluidsClass}.{ConstantOf(fluid.Id)}_TYPE.get());");
            }

            w.CloseBlock();
        }

        // Like water, the fluids are seen through.
        w.Line().OpenBlock($"public static void clientSetup({SimpleName(ClientSetupEventType)} event)")
            .OpenBlock("event.enqueueWork(() ->");
        foreach (IRFluid fluid in content.Fluids)
        {
            string c = ConstantOf(fluid.Id);
            w.Line($"ItemBlockRenderTypes.setRenderLayer({FluidsClass}.{c}.get(), RenderType.translucent());")
                .Line($"ItemBlockRenderTypes.setRenderLayer({FluidsClass}.{c}_FLOWING.get(), RenderType.translucent());");
        }

        w.CloseBlock(");").CloseBlock();
        return WritePrivateConstructor(w, FluidsClientClass);
    }

    // ----- Game rules -----

    private static JavaSourceWriter WriteGameRulesClass(GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        string file = naming.SourcePath(naming.RegistryPackage, GameRulesClass);
        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, ["net.minecraft.world.level.GameRules"], "Règles du jeu du mod (/gamerule).")
            .OpenBlock($"public final class {GameRulesClass}");
        foreach (IRGameRule rule in content.GameRules)
        {
            int first = w.NextLineNumber;
            string category = "GameRules.Category." + rule.Category.ToString().ToUpperInvariant();
            (string valueType, string value) = rule.Kind == GameRuleKind.Boolean
                ? ("GameRules.BooleanValue", $"GameRules.BooleanValue.create({(rule.DefaultBoolean ? "true" : "false")})")
                : ("GameRules.IntegerValue", $"GameRules.IntegerValue.create({rule.DefaultNumber})");
            w.Line($"public static final GameRules.Key<{valueType}> {ConstantOf(rule.Id)} =")
                .Indent().Indent()
                .Line($"GameRules.register({JavaNames.StringLiteral(rule.Id.Value)}, {category}, {value});")
                .Unindent().Unindent();
            context.SourceMap.Add(file, first, w.NextLineNumber - 1, rule.SourceAssetId, $"Règle '{rule.Id}'");
        }

        w.Line()
            .Line("/** Called by the main class: the rules are registered when the class is loaded. */")
            .OpenBlock("public static void init()")
            .CloseBlock();
        return WritePrivateConstructor(w, GameRulesClass);
    }

    // ----- Status effects and potions -----

    private static string EffectClassFor(ResourceId id) => JavaNames.ToTypeName(id.Value, "Mod") + "Effect";

    private JavaSourceWriter WriteEffectsClass(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        IRContent content = context.Ir.Content;
        string file = naming.SourcePath(naming.RegistryPackage, EffectsClass);
        bool potions = content.Effects.Any(e => e.PotionTicks > 0);
        var imports = new List<string>(RegistryImports)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            "net.minecraft.core.registries.Registries",
            "net.minecraft.world.effect.MobEffect",
        };
        imports.AddRange(content.Effects.Select(e => $"{naming.RootPackage}.effect.{EffectClassFor(e.Id)}"));
        if (potions)
        {
            imports.Add("net.minecraft.world.effect.MobEffectInstance");
            imports.Add("net.minecraft.world.item.alchemy.Potion");
        }

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Effets du mod et leurs potions.")
            .OpenBlock($"public final class {EffectsClass}")
            .Line($"public static final DeferredRegister<MobEffect> MOB_EFFECTS = DeferredRegister.create(Registries.MOB_EFFECT, {naming.MainClass}.MOD_ID);");
        if (potions)
        {
            w.Line($"public static final DeferredRegister<Potion> POTIONS = DeferredRegister.create(Registries.POTION, {naming.MainClass}.MOD_ID);");
        }

        foreach (IREffect effect in content.Effects)
        {
            int first = w.NextLineNumber;
            string c = ConstantOf(effect.Id);
            w.Line()
                .Line($"public static final {RegistryHolderType("MobEffect", "MobEffect")} {c} = MOB_EFFECTS.register({JavaNames.StringLiteral(effect.Id.Value)}, {EffectClassFor(effect.Id)}::new);");
            if (effect.PotionTicks > 0)
            {
                string holder = api.UsesDataComponents ? HolderOf(c) : $"{c}.get()";
                w.Line($"public static final {RegistryHolderType("Potion", "Potion")} {c}_POTION = POTIONS.register({JavaNames.StringLiteral(effect.Id.Value)},")
                    .Indent().Indent()
                    .Line($"() -> new Potion(new MobEffectInstance({holder}, {effect.PotionTicks}, 0)));")
                    .Unindent().Unindent();
            }

            context.SourceMap.Add(file, first, w.NextLineNumber - 1, effect.SourceAssetId, $"Effet '{effect.Id}'");
        }

        return WritePrivateConstructor(w, EffectsClass);
    }

    private static void WriteEffectClass(GenerationContext context, JavaModNaming naming, JavaGameApi api, IREffect effect)
    {
        string package = naming.RootPackage + ".effect";
        string className = EffectClassFor(effect.Id);
        var imports = new SortedSet<string>(StringComparer.Ordinal)
        {
            "net.minecraft.world.effect.MobEffect",
            "net.minecraft.world.effect.MobEffectCategory",
        };
        var modifiers = new List<string>();
        void Modifier(string attribute, string key, double amount, bool multiply)
        {
            if (Math.Abs(amount) > 1e-6)
            {
                modifiers.Add(api.EffectAttributeModifier(attribute, context.Paths.Reference($"effect.{effect.Id}.{key}"), amount, multiply));
            }
        }

        Modifier("MOVEMENT_SPEED", "speed", effect.SpeedPercent / 100.0, multiply: true);
        Modifier("ATTACK_DAMAGE", "attack_damage", effect.AttackDamage, multiply: false);
        Modifier("ARMOR", "armor", effect.Armor, multiply: false);
        Modifier("MAX_HEALTH", "max_health", effect.MaxHealth, multiply: false);
        if (modifiers.Count > 0)
        {
            imports.Add("net.minecraft.world.entity.ai.attributes.AttributeModifier");
            imports.Add("net.minecraft.world.entity.ai.attributes.Attributes");
            if (api.UsesDataComponents)
            {
                imports.Add("net.minecraft.resources.ResourceLocation");
            }
        }

        if (effect.HasPeriodicAction)
        {
            imports.Add("net.minecraft.world.entity.LivingEntity");
        }

        JavaSourceWriter w = WriteHeader(package, imports, $"Effet « {effect.Name} ».")
            .OpenBlock($"public class {className} extends MobEffect")
            .OpenBlock($"public {className}()")
            .Line($"super(MobEffectCategory.{effect.Category.ToString().ToUpperInvariant()}, 0x{effect.Color.ToString("X6", CultureInfo.InvariantCulture)});");
        foreach (string modifier in modifiers)
        {
            w.Line(modifier);
        }

        w.CloseBlock();
        if (effect.HasPeriodicAction)
        {
            var actions = new List<string>();
            if (effect.HealAmount > 0)
            {
                actions.Add($"entity.heal({JavaNames.FloatLiteral(effect.HealAmount)} * (amplifier + 1));");
            }

            if (effect.DamageAmount > 0)
            {
                actions.Add($"entity.hurt(entity.damageSources().magic(), {JavaNames.FloatLiteral(effect.DamageAmount)} * (amplifier + 1));");
            }

            w.Line();
            foreach (string line in api.EffectTick(effect.Interval, actions))
            {
                w.Line(line);
            }
        }

        w.CloseBlock();
        string path = naming.SourcePath(package, className);
        context.Files.WriteText(path, w.ToString());
        context.SourceMap.Add(path, 1, w.NextLineNumber - 1, effect.SourceAssetId, $"Effet '{effect.Id}'");
    }

    // ----- Creative tabs of the project -----

    /// <summary>Tabs of the project, added after the tab of the mod in the class of the tabs.</summary>
    private static void WriteProjectCreativeTabs(JavaSourceWriter w, GenerationContext context, string holderType)
    {
        foreach (IRCreativeTab tab in context.Ir.Content.CreativeTabs)
        {
            NamespacedId? icon = tab.Icon;
            w.Line()
                .Line($"public static final {holderType} TAB_{ConstantOf(tab.Id)} = CREATIVE_MODE_TABS.register(")
                .Indent().Indent()
                .Line($"{JavaNames.StringLiteral(tab.Id.Value)}, () -> CreativeModeTab.builder()")
                .Indent().Indent()
                .Line($".title(Component.translatable({JavaNames.StringLiteral(context.Paths.CreativeTabTranslationKeyFor(tab.Id))}))")
                .Line(icon is null ? ".icon(() -> new ItemStack(Items.BOOK))" : $".icon(() -> new ItemStack(item({JavaNames.StringLiteral(icon.ToString())})))")
                .OpenBlock(".displayItems((parameters, output) ->");
            foreach (NamespacedId item in tab.Items)
            {
                w.Line($"output.accept(item({JavaNames.StringLiteral(item.ToString())}));");
            }

            w.CloseBlock(")")
                .Line(".build());")
                .Unindent().Unindent().Unindent().Unindent();
        }
    }

    /// <summary>Items of the mod's own tab that come from V0.6.3 (buckets of fluids).</summary>
    private static IEnumerable<string> GameplayTabEntries(GenerationContext context) =>
        context.Ir.Content.Fluids.Where(f => f.BucketId is not null).Select(f => $"{FluidsClass}.{ConstantOf(f.BucketId!)}");
}
