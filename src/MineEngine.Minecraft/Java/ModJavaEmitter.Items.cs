using MineEngine.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
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

    /// <summary>Simple names of the item classes of the game: a class of the mod never takes one of them.</summary>
    private static readonly HashSet<string> GameItemClasses =
    [
        "Item", "SwordItem", "PickaxeItem", "AxeItem", "ShovelItem", "HoeItem", "DiggerItem", "TieredItem", "ArmorItem",
        "BowItem", "CrossbowItem", "FishingRodItem", "BlockItem", "DoubleHighBlockItem", "SpawnEggItem", "ProjectileWeaponItem",
    ];

    /// <summary>Package of the classes of the items (V0.7.13), next to the one of the blocks.</summary>
    private static string ItemPackage(JavaModNaming naming) => naming.RootPackage + ".item";

    /// <summary>Class of an item of the project ("RubyChiselItem"); "Mod" in front when the name is one of the game.</summary>
    private static string ItemClassName(IRItem item)
    {
        string name = JavaNames.ToTypeName(item.Id.Value, "Custom") + "Item";
        return GameItemClasses.Contains(name) ? "Mod" + name : name;
    }

    /// <summary>Début, appels de propriétés supplémentaires et fin de la construction d'un item.</summary>
    private static (string Head, IReadOnlyList<string> PropertyCalls, string Tail) ItemConstruction(IRItem item, JavaGameApi api) =>
        item.Kind.IsEquipment()
            ? api.EquipmentConstruction(item, item.CustomTier is null ? null : $"{ItemClassName(item)}.TIER")
            : item.Ranged?.Kind == RangedItemKind.FishingRod ? ("new FishingRodItem(", [], ")") : ("new Item(", [], ")");

    /// <summary>Arguments a method of an element passes to its graph, in the order of the parameters the graph is created with.</summary>
    private static IEnumerable<string> MethodArguments(MethodReturn kind) =>
        kind == MethodReturn.Void ? ["player", "position", "other", "amount"] : ["entity"];

    /// <summary>
    /// A method of the class of an element (V0.7.13): its Java code, or the call of its graph (the world
    /// first, then the parameters the graph has). Without a body, it gives the value of the game.
    /// </summary>
    private static IEnumerable<string> ElementMethodLines(IRElementMethod method, GenerationContext context, JavaProgramNames names, ISet<string> imports)
    {
        imports.UnionWith(["net.minecraft.world.level.Level", "net.minecraft.world.entity.Entity"]);
        string signature = method.Return switch
        {
            MethodReturn.Boolean => $"public static boolean {method.JavaName}(Level level, Entity entity)",
            MethodReturn.Decimal => $"public static double {method.JavaName}(Level level, Entity entity)",
            _ => $"public static void {method.JavaName}(Player player, Level level, BlockPos position, Entity other, double amount)",
        };
        if (method.Return == MethodReturn.Void)
        {
            imports.UnionWith(["net.minecraft.world.entity.player.Player", "net.minecraft.core.BlockPos"]);
        }

        string? fallback = method.Return switch { MethodReturn.Boolean => "return false;", MethodReturn.Decimal => "return 0.0;", _ => null };
        var body = new List<string>();
        if (method.JavaBody is { } java)
        {
            body.AddRange(java.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).SkipWhile(l => l.Length == 0).Reverse().SkipWhile(l => l.Length == 0).Reverse());
        }
        else if (context.Ir.Content.Definitions.OfType<IRFunction>().FirstOrDefault(f => f.Id == method.FunctionId) is { } function)
        {
            string arguments = string.Join(", ", ["level", .. MethodArguments(method.Return).Take(function.Parameters.Count)]);
            string call = $"{names.Package}.{names.CallPrefix(function, null)}({arguments})";
            body.Add(method.Return == MethodReturn.Void ? call + ";" : $"return {call};");
        }

        if (body.Count == 0 && fallback is not null)
        {
            body.Add(fallback);
        }

        return [$"{signature} {{", .. body.Select(l => l.Length == 0 ? l : "    " + l), "}"];
    }

    /// <summary>Import of the class of a call ("ItemEventsBehavior.onX" from the graphs, "RubyChiselItem.onX" from an item).</summary>
    private static string CallImport(string call, JavaModNaming naming)
    {
        string type = call[..call.IndexOf('.')];
        return type.EndsWith("Behavior", StringComparison.Ordinal) ? $"{naming.BehaviorPackage}.{type}" : $"{ItemPackage(naming)}.{type}";
    }

    /// <summary>
    /// The class of an item of the project (V0.7.13, "an item is a class"): it extends the class of the
    /// game its construction names, passes it the same arguments, and holds the methods of the item.
    /// </summary>
    private void WriteItemClass(GenerationContext context, JavaModNaming naming, JavaProgramNames names, JavaGameApi api, IRItem item)
    {
        string className = ItemClassName(item);
        var imports = new SortedSet<string>(StringComparer.Ordinal) { "net.minecraft.world.item.Item" };
        imports.UnionWith(ItemConstructionImports(item, api));

        // "new SwordItem(Tiers.DIAMOND, 3, -2.4f, " → the class of the game and the arguments before the properties.
        (string head, _, _) = ItemConstruction(item, api);
        string construction = head["new ".Length..];
        string baseClass = construction[..construction.IndexOf('(')];
        string superArguments = construction[(construction.IndexOf('(') + 1)..];

        bool glintAlways = item.HasGlint && item.GlintCondition is null && item.Method(ItemMethodSlots.GlintCondition) is null;
        List<string> methods = ItemOverrides(item, item.Form, glintAlways, context, api);
        if (methods.Count > 0)
        {
            imports.Add("net.minecraft.world.item.ItemStack");
            if (item.Form.TooltipLines.Count > 0)
            {
                imports.UnionWith(api.TooltipImports);
            }
        }

        List<string> behaviours = ItemBehaviourOverrides(item, context, naming, names, api, imports);
        if (methods.Count > 0 && behaviours.Count > 0)
        {
            methods.Add(string.Empty);
        }

        methods.AddRange(behaviours);

        // The methods of the class (V0.7.13) come first, the methods of the game after them.
        List<string> own = [.. item.Methods.SelectMany((m, i) => (i > 0 ? [string.Empty] : Array.Empty<string>()).Concat(ElementMethodLines(m, context, names, imports)))];
        if (own.Count > 0 && methods.Count > 0)
        {
            own.Add(string.Empty);
        }

        methods.InsertRange(0, own);
        imports.Remove($"{ItemPackage(naming)}.{className}");

        string file = naming.SourcePath(ItemPackage(naming), className);
        JavaSourceWriter w = WriteHeader(ItemPackage(naming), imports, $"Item « {item.DisplayName} » du mod ({item.Id}).");
        int first = w.NextLineNumber;
        w.OpenBlock($"public class {className} extends {baseClass}");
        if (item.CustomTier is not null)
        {
            foreach (string line in CustomTierField(item, api))
            {
                w.Line(line);
            }

            w.Line();
        }

        w.OpenBlock($"public {className}(Item.Properties properties)")
            .Line($"super({superArguments}properties);")
            .CloseBlock();
        if (methods.Count > 0)
        {
            w.Line();
        }

        foreach (string line in methods)
        {
            if (line.Length == 0)
            {
                w.Line();
            }
            else
            {
                w.Line(line);
            }
        }

        w.CloseBlock();
        context.SourceMap.Add(file, first, w.NextLineNumber - 1, item.SourceAssetId, $"Item '{item.Id}'");
        Write(context, naming, ItemPackage(naming), className, w);
    }

    private static IEnumerable<string> ItemConstructionImports(IRItem item, JavaGameApi api) => item.Kind switch
    {
        ItemKind.Simple when item.Ranged?.Kind == RangedItemKind.FishingRod => ["net.minecraft.world.item.FishingRodItem"],
        ItemKind.Simple => [],
        _ when item.Kind.IsArmor() => ["net.minecraft.world.item.ArmorItem", "net.minecraft.world.item.ArmorMaterials"],
        _ when item.CustomTier is { } tier =>
        [
            $"net.minecraft.world.item.{item.Kind}Item", "net.minecraft.world.item.Tier", "net.minecraft.world.item.crafting.Ingredient",
            .. api.TierMiningLevel(tier.MiningLevel).Imports,
        ],
        _ => [$"net.minecraft.world.item.{item.Kind}Item", "net.minecraft.world.item.Tiers"],
    };


    /// <summary>
    /// Material of a tool chosen in the editor (V0.7.12). Its repair item is the one of the item
    /// (isValidRepairItem); the damage of the tool is added by its own constructor.
    /// </summary>
    private static IEnumerable<string> CustomTierField(IRItem item, JavaGameApi api)
    {
        IRToolTier tier = item.CustomTier!;
        return
        [
            "public static final Tier TIER = new Tier() {",
            $"    @Override public int getUses() {{ return {tier.Uses}; }}",
            $"    @Override public float getSpeed() {{ return {JavaNames.FloatLiteral(tier.Speed)}; }}",
            "    @Override public float getAttackDamageBonus() { return 0.0F; }",
            $"    @Override {api.TierMiningLevel(tier.MiningLevel).Method}",
            $"    @Override public int getEnchantmentValue() {{ return {tier.Enchantability}; }}",
            "    @Override public Ingredient getRepairIngredient() { return Ingredient.EMPTY; }",
            "};",
        ];
    }

    /// <summary>Méthodes ajoutées à la classe de l'item pour ses propriétés avancées et ses événements.</summary>
    private List<string> ItemBehaviourOverrides(IRItem item, GenerationContext context, JavaModNaming naming, JavaProgramNames names, JavaGameApi api, ISet<string> imports)
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

        if (item.Remainder is ItemRemainderKind.Itself or ItemRemainderKind.ItselfDamaged)
        {
            imports.Add("net.minecraft.world.item.ItemStack");
            string[] body = item.Remainder == ItemRemainderKind.Itself
                ? ["    return stack.copyWithCount(1);"]
                :
                [
                    "    ItemStack remainder = stack.copyWithCount(1);",
                    "    remainder.setDamageValue(remainder.getDamageValue() + 1);",
                    "    return remainder.getDamageValue() >= remainder.getMaxDamage() ? ItemStack.EMPTY : remainder;",
                ];
            Add(
            [
                "@Override",
                "public boolean hasCraftingRemainingItem(ItemStack stack) {",
                "    return true;",
                "}",
                string.Empty,
                "@Override",
                "public ItemStack getCraftingRemainingItem(ItemStack stack) {",
                .. body,
                "}",
            ]);
        }
        else if (item.CraftRemainder is { } remainder)
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
        else if (item.RepairItem is { } repair)
        {
            imports.UnionWith(["net.minecraft.world.item.ItemStack", "net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation"]);
            string location = api.ParseResourceLocation(JavaNames.StringLiteral(repair.Id.ToString()));
            if (repair.IsTag)
            {
                imports.UnionWith(["net.minecraft.tags.TagKey", "net.minecraft.core.registries.Registries"]);
            }

            Add(
                "@Override",
                "public boolean isValidRepairItem(ItemStack stack, ItemStack repairCandidate) {",
                repair.IsTag
                    ? $"    return repairCandidate.is(TagKey.create(Registries.ITEM, {location}));"
                    : $"    return repairCandidate.is(BuiltInRegistries.ITEM.get({location}));",
                "}");
        }

        if (item.PiglinCurrency)
        {
            imports.Add("net.minecraft.world.item.ItemStack");
            Add("@Override", "public boolean isPiglinCurrency(ItemStack stack) {", "    return true;", "}");
        }

        if (item.HarvestsAnyBlock)
        {
            imports.UnionWith(["net.minecraft.world.item.ItemStack", "net.minecraft.world.level.block.state.BlockState"]);
            Add(
                "@Override",
                api.UsesDataComponents
                    ? "public boolean isCorrectToolForDrops(ItemStack stack, BlockState state) {"
                    : "public boolean isCorrectToolForDrops(BlockState state) {",
                "    return true;",
                "}");
        }

        if (item.Food is { } food)
        {
            if (food.EatTicks != 32)
            {
                imports.UnionWith(["net.minecraft.world.item.ItemStack", "net.minecraft.world.entity.LivingEntity"]);
                Add(
                    "@Override",
                    api.UsesDataComponents ? "public int getUseDuration(ItemStack stack, LivingEntity entity) {" : "public int getUseDuration(ItemStack stack) {",
                    $"    return {food.EatTicks};",
                    "}");
            }

            if (food.Animation != ItemUseAnimation.Eat)
            {
                imports.UnionWith(["net.minecraft.world.item.ItemStack", "net.minecraft.world.item.UseAnim"]);
                string animation = food.Animation == ItemUseAnimation.TootHorn ? "TOOT_HORN" : food.Animation.ToString().ToUpperInvariant();
                Add("@Override", "public UseAnim getUseAnimation(ItemStack stack) {", $"    return UseAnim.{animation};", "}");
            }
        }

        if (item.BurnTime > 0 && BurnTimeOverride(item.BurnTime) is { } burn)
        {
            imports.UnionWith(BurnTimeImports);
            Add([.. burn]);
        }

        List<(ItemEventKind Event, string Call)> calls = ItemEventCalls(item, context, naming);

        // A ranged item writes its own "use", which also runs the graphs of the right click.
        bool shoots = item.Ranged is { Shoots: true };
        foreach (IGrouping<ItemEventKind, string> group in calls.GroupBy(c => c.Event, c => c.Call)
            .Where(g => OwnMethodEvents.Contains(g.Key) && !(shoots && g.Key == ItemEventKind.RightClick)).OrderBy(g => g.Key))
        {
            // Chaque appel est "Classe.méthode" : la classe du graphe est importée.
            imports.UnionWith(group.Select(call => CallImport(call, naming)));
            imports.UnionWith(ItemEventImports(group.Key));
            Add([.. ItemEventOverride(group.Key, group.ToList())]);
        }

        // finishUsingItem and inventoryTick also hold the item left once eaten and the glint condition (V0.7.12).
        List<string> Calls(ItemEventKind kind)
        {
            List<string> found = [.. calls.Where(c => c.Event == kind).Select(c => c.Call)];
            imports.UnionWith(found.Select(call => CallImport(call, naming)));
            if (found.Count > 0)
            {
                imports.UnionWith(ItemEventImports(kind));
            }

            return found;
        }

        if (shoots)
        {
            Add([.. RangedMethods(item, Calls, api, imports)]);
        }
        else
        {
            List<string> started = Calls(ItemEventKind.EatingStarted);
            List<string> eating = Calls(ItemEventKind.EatingTick);
            List<string> stopped = Calls(ItemEventKind.EatingStopped);
            if (started.Count > 0 || eating.Count > 0)
            {
                Add([.. EatingTick(started, eating, api, imports)]);
            }

            if (stopped.Count > 0)
            {
                imports.UnionWith(["net.minecraft.world.entity.LivingEntity", "net.minecraft.world.entity.player.Player"]);
                Add(
                [
                    "@Override",
                    "public void releaseUsing(ItemStack stack, Level level, LivingEntity entity, int timeLeft) {",
                    "    super.releaseUsing(stack, level, entity, timeLeft);",
                    "    if (entity instanceof Player player && !level.isClientSide()) {",
                    .. stopped.Select(c => $"        {EventCall(c, "player", "level", "player.blockPosition()", "null", "timeLeft")}"),
                    "    }",
                    "}",
                ]);
            }
        }

        List<string> consumed = Calls(ItemEventKind.Consumed);
        if (consumed.Count > 0 || item.Food?.Result is not null)
        {
            Add([.. FinishUsingItem(item.Food?.Result, consumed, api, imports)]);
        }

        List<string> held = Calls(ItemEventKind.HeldTick);
        List<string> swinging = Calls(ItemEventKind.ToolSwingTick);
        List<string> swung = Calls(ItemEventKind.ToolSwung);
        string? glint = item.Method(ItemMethodSlots.GlintCondition) is { } glintMethod
            ? $"{glintMethod.JavaName}(level, entity)"
            : item.GlintCondition is null
                ? null
                : FunctionCall(context, names, item.GlintCondition, "level", "(entity instanceof LivingEntity living ? living : null)");
        if (item.GlintCondition is not null && glint is null)
        {
            // The function is not in the mod (reported by the lowering): the glint stays on.
            imports.Add("net.minecraft.world.item.ItemStack");
            Add("@Override", "public boolean isFoil(ItemStack stack) {", "    return true;", "}");
        }

        if (held.Count > 0 || swinging.Count > 0 || swung.Count > 0 || glint is not null)
        {
            Add([.. InventoryTick(glint, held, swinging, swung, api, imports)]);
        }

        return lines;
    }

    /// <summary>Events written in a method of their own (the others share a method, or are events of the loader).</summary>
    private static readonly HashSet<ItemEventKind> OwnMethodEvents =
    [
        ItemEventKind.RightClick, ItemEventKind.UseOnBlock, ItemEventKind.HitEntity, ItemEventKind.BlockMined,
        ItemEventKind.Crafted, ItemEventKind.UseOnEntity, ItemEventKind.Dropped,
    ];

    /// <summary>The graphs connected to the events of an item: "Class.method" for each event.</summary>
    private List<(ItemEventKind Event, string Call)> ItemEventCalls(IRItem item, GenerationContext context, JavaModNaming naming)
    {
        string itemId = context.Paths.Reference(item.Id.Value);
        List<(ItemEventKind Event, string Call)> calls = [.. context.Ir.Content.Behaviors
            .SelectMany(b => HandlerMethods(b)
                .Where(h => h.Handler.Item?.ToString() == itemId)
                .Select(h => (Event: NodeEvent(h.Handler), Call: $"{naming.BehaviorClassFor(b.Id)}.{h.Method}")))
            .Where(c => c.Event is not null)
            .Select(c => (c.Event!.Value, c.Call))];

        // The event methods of the class of the item (V0.7.13) run with the graphs of the event.
        calls.AddRange(Enum.GetValues<ItemEventKind>()
            .Where(k => item.Method(ItemMethodSlots.EventKey(k)) is not null)
            .Select(k => (k, $"{ItemClassName(item)}.{item.Method(ItemMethodSlots.EventKey(k))!.JavaName}")));
        return calls;
    }

    /// <summary>A call of a graph of an item event: the player, the world, a position, another entity and an amount.</summary>
    private static string EventCall(string call, string player, string level, string position, string other = "null", string amount = "0.0") =>
        $"{call}({player}, {level}, {position}, {other}, {amount});";

    /// <summary>Each tick of the use of a food: the first one starts the eating.</summary>
    private static IEnumerable<string> EatingTick(IReadOnlyList<string> started, IReadOnlyList<string> eating, JavaGameApi api, ISet<string> imports)
    {
        imports.UnionWith(["net.minecraft.world.entity.LivingEntity", "net.minecraft.world.entity.player.Player"]);
        yield return "@Override";
        yield return "public void onUseTick(Level level, LivingEntity entity, ItemStack stack, int remaining) {";
        yield return "    super.onUseTick(level, entity, stack, remaining);";
        yield return "    if (entity instanceof Player player && !level.isClientSide()) {";
        if (started.Count > 0)
        {
            yield return $"        if (remaining == this.getUseDuration({(api.UsesDataComponents ? "stack, entity" : "stack")})) {{";
            foreach (string call in started)
            {
                yield return $"            {EventCall(call, "player", "level", "player.blockPosition()")}";
            }

            yield return "        }";
        }

        foreach (string call in eating)
        {
            yield return $"        {EventCall(call, "player", "level", "player.blockPosition()", "null", "remaining")}";
        }

        yield return "    }";
        yield return "}";
    }

    private const string GlintTag = "mineengine_glint";

    /// <summary>
    /// A ranged item of V0.7.12: it finds its ammunition in the inventory, then shoots on release (the
    /// power grows like a bow), at once (then waits) or while held (one shot every few ticks).
    /// </summary>
    private static IEnumerable<string> RangedMethods(IRItem item, Func<ItemEventKind, List<string>> events, JavaGameApi api, ISet<string> imports)
    {
        IRRanged ranged = item.Ranged!;
        List<string> clickCalls = events(ItemEventKind.RightClick);
        List<string> chargeStarted = events(ItemEventKind.ChargeStarted);
        List<string> charged = ranged.Mode == RangedFireMode.Charge ? events(ItemEventKind.Charged) : [];
        List<string> charging = events(ItemEventKind.ChargingTick);
        List<string> shot = events(ItemEventKind.Shot);
        List<string> broken = item.Durability > 0 ? events(ItemEventKind.Broken) : [];
        IEnumerable<string> Server(IReadOnlyList<string> calls, string indent, string other = "null", string amount = "0.0") => calls.Count == 0
            ? []
            : [$"{indent}if (!level.isClientSide()) {{", .. calls.Select(c => $"{indent}    {EventCall(c, "player", "level", "player.blockPosition()", other, amount)}"), $"{indent}}}"];
        bool modern = api.UsesDataComponents;
        imports.UnionWith(
        [
            "net.minecraft.world.InteractionHand", "net.minecraft.world.InteractionResultHolder", "net.minecraft.world.entity.player.Player",
            "net.minecraft.world.entity.LivingEntity", "net.minecraft.world.level.Level", "net.minecraft.world.item.ItemStack",
            "net.minecraft.world.item.BowItem", "net.minecraft.world.item.UseAnim", "net.minecraft.world.item.Items",
            "net.minecraft.sounds.SoundEvents", "net.minecraft.sounds.SoundSource",
            "net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation",
        ]);
        string free = ranged.NeedsAmmo ? "player.getAbilities().instabuild" : "true";
        string ammo = $"BuiltInRegistries.ITEM.get({api.ParseResourceLocation(JavaNames.StringLiteral(ranged.Ammo.ToString()))})";
        string speed = JavaNames.FloatLiteral(ranged.Speed);

        var lines = new List<string>
        {
            "@Override",
            "public InteractionResultHolder<ItemStack> use(Level level, Player player, InteractionHand hand) {",
            "    ItemStack stack = player.getItemInHand(hand);",
            $"    if (!({free}) && mineengineAmmo(player).isEmpty()) {{",
            "        return InteractionResultHolder.fail(stack);",
            "    }",
        };
        lines.AddRange(Server(clickCalls, "    "));

        if (ranged.Mode == RangedFireMode.Instant)
        {
            lines.AddRange(
            [
                "    mineengineShoot(level, player, stack, hand, 1.0F);",
                $"    player.getCooldowns().addCooldown(this, {ranged.RateTicks});",
                "    return InteractionResultHolder.sidedSuccess(stack, level.isClientSide());",
                "}",
            ]);
        }
        else
        {
            string animation = ranged.Kind switch { RangedItemKind.Bow => "BOW", RangedItemKind.Crossbow => "CROSSBOW", _ => "NONE" };
            lines.Add("    player.startUsingItem(hand);");
            lines.AddRange(Server(chargeStarted, "    "));
            lines.AddRange(
            [
                "    return InteractionResultHolder.consume(stack);",
                "}",
                string.Empty,
                "@Override",
                modern ? "public int getUseDuration(ItemStack stack, LivingEntity entity) {" : "public int getUseDuration(ItemStack stack) {",
                "    return 72000;",
                "}",
                string.Empty,
                "@Override",
                "public UseAnim getUseAnimation(ItemStack stack) {",
                $"    return UseAnim.{animation};",
                "}",
                string.Empty,
            ]);
            if (ranged.Mode == RangedFireMode.Charge)
            {
                lines.AddRange(
                [
                    "@Override",
                    "public void releaseUsing(ItemStack stack, Level level, LivingEntity entity, int timeLeft) {",
                    "    if (entity instanceof Player player) {",
                    "        float power = BowItem.getPowerForTime(72000 - timeLeft);",
                    "        if (power >= 0.1F) {",
                    "            mineengineShoot(level, player, stack, player.getUsedItemHand(), power);",
                    "        }",
                    "    }",
                    "}",
                ]);
                if (charging.Count > 0 || charged.Count > 0)
                {
                    lines.AddRange(
                    [
                        string.Empty,
                        "@Override",
                        "public void onUseTick(Level level, LivingEntity entity, ItemStack stack, int remaining) {",
                        "    if (entity instanceof Player player) {",
                        "        float power = BowItem.getPowerForTime(72000 - remaining);",
                        .. Server(charging, "        ", "null", "power"),
                        .. charged.Count == 0 ? [] : new[] { "        if (72000 - remaining == 20) {" }.Concat(Server(charged, "            ", "null", "1.0")).Append("        }"),
                        "    }",
                        "}",
                    ]);
                }
            }
            else
            {
                lines.AddRange(
                [
                    "@Override",
                    "public void onUseTick(Level level, LivingEntity entity, ItemStack stack, int remaining) {",
                    "    if (entity instanceof Player player) {",
                    .. Server(charging, "        ", "null", "1.0"),
                    $"        if ((72000 - remaining) % {ranged.RateTicks} == 0) {{",
                    "            mineengineShoot(level, player, stack, player.getUsedItemHand(), 1.0F);",
                    "        }",
                    "    }",
                    "}",
                ]);
            }
        }

        lines.AddRange(
        [
            string.Empty,
            "/** The first stack of ammunition of the inventory, or an empty stack. */",
            "private ItemStack mineengineAmmo(Player player) {",
            "    for (int slot = 0; slot < player.getInventory().getContainerSize(); slot++) {",
            "        ItemStack candidate = player.getInventory().getItem(slot);",
            $"        if (candidate.is({ammo})) {{",
            "            return candidate;",
            "        }",
            "    }",
            "    return ItemStack.EMPTY;",
            "}",
            string.Empty,
            "/** Shoots one projectile; the ammunition is used up unless the player is in creative mode. */",
            "private void mineengineShoot(Level level, Player player, ItemStack weapon, InteractionHand hand, float power) {",
            "    ItemStack ammo = mineengineAmmo(player);",
            $"    boolean free = {free};",
            "    if (!free && ammo.isEmpty()) {",
            "        return;",
            "    }",
            "    if (!level.isClientSide()) {",
            .. RangedProjectileLines(ranged, modern, imports),
            $"        projectile.shootFromRotation(player, player.getXRot(), player.getYRot(), 0.0F, power * {speed}, 1.0F);",
            "        level.addFreshEntity(projectile);",
            .. shot.Select(c => $"        {EventCall(c, "player", "level", "player.blockPosition()", "projectile", "power")}"),
        ]);
        if (item.Durability > 0)
        {
            lines.Add(modern
                ? "        weapon.hurtAndBreak(1, player, LivingEntity.getSlotForHand(hand));"
                : "        weapon.hurtAndBreak(1, player, p -> p.broadcastBreakEvent(hand));");
            if (broken.Count > 0)
            {
                // The game tells the loader only for the tools it damages itself: the shot says it here.
                lines.Add("        if (weapon.isEmpty()) {");
                lines.AddRange(broken.Select(c => $"            {EventCall(c, "player", "level", "player.blockPosition()")}"));
                lines.Add("        }");
            }
        }

        string sound = ranged.Kind switch { RangedItemKind.Bow => "ARROW_SHOOT", RangedItemKind.Crossbow => "CROSSBOW_SHOOT", _ => "SNOWBALL_THROW" };
        lines.AddRange(
        [
            "    }",
            $"    level.playSound(null, player.getX(), player.getY(), player.getZ(), SoundEvents.{sound}, SoundSource.PLAYERS, 1.0F, 1.0F / (level.getRandom().nextFloat() * 0.4F + 1.2F) + power * 0.5F);",
            "    if (!free) {",
            "        ammo.shrink(1);",
            "    }",
            "}",
        ]);
        return lines;
    }

    /// <summary>Creation of the projectile: an arrow keeps its ammunition (picked up again), its damage and its critical hit.</summary>
    private static IEnumerable<string> RangedProjectileLines(IRRanged ranged, bool modern, ISet<string> imports)
    {
        if (ranged.Projectile is RangedProjectile.Snowball or RangedProjectile.Egg)
        {
            string type = ranged.Projectile == RangedProjectile.Snowball ? "Snowball" : "ThrownEgg";
            imports.Add($"net.minecraft.world.entity.projectile.{type}");
            return [$"        {type} projectile = new {type}(level, player);"];
        }

        string arrow = ranged.Projectile == RangedProjectile.SpectralArrow ? "SpectralArrow" : "Arrow";
        string vanilla = ranged.Projectile == RangedProjectile.SpectralArrow ? "SPECTRAL_ARROW" : "ARROW";
        imports.UnionWith([$"net.minecraft.world.entity.projectile.{arrow}", "net.minecraft.world.entity.projectile.AbstractArrow"]);
        return
        [
            modern
                ? $"        {arrow} projectile = new {arrow}(level, player, ammo.isEmpty() ? new ItemStack(Items.{vanilla}) : ammo.copyWithCount(1), weapon);"
                : $"        {arrow} projectile = new {arrow}(level, player);",
            $"        projectile.setBaseDamage({JavaNames.DoubleLiteral(ranged.Damage)});",
            "        if (power >= 1.0F) {",
            "            projectile.setCritArrow(true);",
            "        }",
            "        if (free) {",
            "            projectile.pickup = AbstractArrow.Pickup.CREATIVE_ONLY;",
            "        }",
        ];
    }

    /// <summary>
    /// End of eating: the graphs of the "eaten" event run, then the item left once eaten is given
    /// back like the bowl of a stew (in the hand when the stack is used up, otherwise in the inventory).
    /// </summary>
    private static IEnumerable<string> FinishUsingItem(NamespacedId? result, IReadOnlyList<string> calls, JavaGameApi api, ISet<string> imports)
    {
        imports.UnionWith(["net.minecraft.world.item.ItemStack", "net.minecraft.world.level.Level", "net.minecraft.world.entity.LivingEntity", "net.minecraft.world.entity.player.Player"]);
        yield return "@Override";
        yield return "public ItemStack finishUsingItem(ItemStack stack, Level level, LivingEntity entity) {";
        yield return "    ItemStack result = super.finishUsingItem(stack, level, entity);";
        if (calls.Count > 0)
        {
            yield return "    if (entity instanceof Player player && !level.isClientSide()) {";
            foreach (string call in calls)
            {
                yield return $"        {EventCall(call, "player", "level", "player.blockPosition()")}";
            }

            yield return "    }";
        }

        if (result is not null)
        {
            imports.UnionWith(["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation"]);
            yield return $"    ItemStack leftover = new ItemStack(BuiltInRegistries.ITEM.get({api.ParseResourceLocation(JavaNames.StringLiteral(result.ToString()))}));";
            yield return "    if (result.isEmpty()) {";
            yield return "        return leftover;";
            yield return "    }";
            yield return "    if (entity instanceof Player eater && !eater.getAbilities().instabuild && !eater.getInventory().add(leftover)) {";
            yield return "        eater.drop(leftover, false);";
            yield return "    }";
        }

        yield return "    return result;";
        yield return "}";
    }

    /// <summary>
    /// Each tick in an inventory: the glint condition is checked on the server and kept on the stack
    /// (a component of the game since 1.20.5, a tag before), then the graphs of "held" run.
    /// </summary>
    private static IEnumerable<string> InventoryTick(
        string? glint, IReadOnlyList<string> calls, IReadOnlyList<string> swinging, IReadOnlyList<string> swung, JavaGameApi api, ISet<string> imports)
    {
        imports.UnionWith(["net.minecraft.world.item.ItemStack", "net.minecraft.world.level.Level", "net.minecraft.world.entity.Entity"]);
        var lines = new List<string>
        {
            "@Override",
            "public void inventoryTick(ItemStack stack, Level level, Entity entity, int slot, boolean selected) {",
            "    super.inventoryTick(stack, level, entity, slot, selected);",
        };
        if (glint is not null)
        {
            imports.Add("net.minecraft.world.entity.LivingEntity");
            lines.Add("    if (!level.isClientSide()) {");
            lines.Add($"        boolean glint = {glint};");
            if (api.UsesDataComponents)
            {
                imports.Add("net.minecraft.core.component.DataComponents");
                lines.AddRange(
                [
                    "        if (glint != stack.has(DataComponents.ENCHANTMENT_GLINT_OVERRIDE)) {",
                    "            if (glint) {",
                    "                stack.set(DataComponents.ENCHANTMENT_GLINT_OVERRIDE, true);",
                    "            } else {",
                    "                stack.remove(DataComponents.ENCHANTMENT_GLINT_OVERRIDE);",
                    "            }",
                    "        }",
                ]);
            }
            else
            {
                lines.AddRange(
                [
                    $"        if (glint != (stack.hasTag() && stack.getTag().getBoolean(\"{GlintTag}\"))) {{",
                    "            if (glint) {",
                    $"                stack.getOrCreateTag().putBoolean(\"{GlintTag}\", true);",
                    "            } else {",
                    $"                stack.removeTagKey(\"{GlintTag}\");",
                    "            }",
                    "        }",
                ]);
            }

            lines.Add("    }");
        }

        if (calls.Count > 0 || swinging.Count > 0 || swung.Count > 0)
        {
            imports.Add("net.minecraft.world.entity.player.Player");
            lines.Add("    if (selected && entity instanceof Player player && !level.isClientSide()) {");
            lines.AddRange(calls.Select(c => $"        {EventCall(c, "player", "level", "player.blockPosition()")}"));
            if (swinging.Count > 0)
            {
                lines.Add("        if (player.swinging) {");
                lines.AddRange(swinging.Select(c => $"            {EventCall(c, "player", "level", "player.blockPosition()")}"));
                lines.Add("        }");
            }

            // swingTime goes through 0 once per swing.
            if (swung.Count > 0)
            {
                lines.Add("        if (player.swinging && player.swingTime == 0) {");
                lines.AddRange(swung.Select(c => $"            {EventCall(c, "player", "level", "player.blockPosition()")}"));
                lines.Add("        }");
            }

            lines.Add("    }");
        }

        lines.Add("}");
        if (glint is not null && !api.UsesDataComponents)
        {
            lines.AddRange(
            [
                string.Empty,
                "@Override",
                "public boolean isFoil(ItemStack stack) {",
                $"    return stack.hasTag() && stack.getTag().getBoolean(\"{GlintTag}\");",
                "}",
            ]);
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
                ItemEventKind.UseOnEntity => ["net.minecraft.world.entity.LivingEntity", "net.minecraft.world.InteractionHand", "net.minecraft.world.InteractionResult"],
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
        IEnumerable<string> Call(string player, string level, string position, string other = "null") =>
            calls.Select(c => $"        {EventCall(c, player, level, position, other)}");

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
                .. Call("player", "attacker.level()", "target.blockPosition()", "target"),
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
            ItemEventKind.UseOnEntity =>
            [
                "@Override",
                "public InteractionResult interactLivingEntity(ItemStack stack, Player player, LivingEntity target, InteractionHand hand) {",
                "    if (!player.level().isClientSide()) {",
                .. Call("player", "player.level()", "target.blockPosition()", "target"),
                "    }",
                "    return super.interactLivingEntity(stack, player, target, hand);",
                "}",
            ],
            ItemEventKind.Dropped =>
            [
                "@Override",
                "public boolean onDroppedByPlayer(ItemStack stack, Player player) {",
                "    if (!player.level().isClientSide()) {",
                .. Call("player", "player.level()", "player.blockPosition()"),
                "    }",
                "    return super.onDroppedByPlayer(stack, player);",
                "}",
            ],
            _ =>
            [
                "@Override",
                "public void onCraftedBy(ItemStack stack, Level level, Player player) {",
                "    super.onCraftedBy(stack, level, player);",
                "    if (!level.isClientSide()) {",
                .. Call("player", "level", "player.blockPosition()"),
                "    }",
                "}",
            ],
        };
    }
}
