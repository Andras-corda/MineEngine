namespace MineEngine.Minecraft.Java;

/// <summary>
/// Helper methods of the block and item nodes (V0.6.2), added to the generated class when a
/// graph uses them. Most of them are the same for every version; enchantments, custom names,
/// food, smelting, block data and the use of a block changed in 1.20.5 (data components,
/// enchantment registry), so those have two versions.
/// </summary>
public static class GraphHelpersJava
{
    /// <summary>Other helpers a helper calls (they are added with it).</summary>
    public static IReadOnlyList<string> Dependencies(string name) => name switch
    {
        "replaceBlock" => ["blockData"],
        "setBlockProperty" or "blockProperty" => ["propertyValue"],
        "addEnchantment" or "removeEnchantment" or "enchantmentLevel" => ["enchantment"],
        "setBlockNbtNumber" or "setBlockNbtText" or "setBlockNbtBoolean" or "blockNbtNumber" or "blockNbtText" or "blockNbtBoolean" => ["customData"],
        "gameRuleBoolean" or "gameRuleNumber" or "setGameRuleBoolean" or "setGameRuleNumber" => ["gameRule"],
        "giveEffect" or "removeEffect" or "hasEffect" => ["effect"],
        _ => [],
    };

    /// <summary>Imports of a helper, or null if the name is not one of these helpers.</summary>
    public static IReadOnlyList<string>? Imports(string name, JavaGameApi api)
    {
        bool modern = api.UsesDataComponents;
        const string BlockState = "net.minecraft.world.level.block.state.BlockState";
        const string Property = "net.minecraft.world.level.block.state.properties.Property";
        const string BlockEntity = "net.minecraft.world.level.block.entity.BlockEntity";
        const string Stack = "net.minecraft.world.item.ItemStack";
        return name switch
        {
            "setTime" or "setWeather" => ["net.minecraft.server.level.ServerLevel"],
            "spawnEntity" => ["net.minecraft.server.level.ServerLevel", "net.minecraft.world.entity.EntityType", "net.minecraft.world.entity.MobSpawnType"],
            "spawnParticles" =>
            [
                "net.minecraft.server.level.ServerLevel", "net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation",
                "net.minecraft.core.particles.ParticleType", "net.minecraft.core.particles.SimpleParticleType",
            ],
            "armorItem" => ["net.minecraft.world.entity.EquipmentSlot", Stack, "java.util.Locale"],
            "lookedAtBlock" => ["net.minecraft.world.phys.BlockHitResult", "net.minecraft.world.phys.HitResult"],
            "slotItem" or "clearSlot" => [Stack],
            "setItemNbtNumber" or "setItemNbtText" or "itemNbtNumber" or "itemNbtText" => modern
                ? [Stack, "net.minecraft.core.component.DataComponents", "net.minecraft.world.item.component.CustomData", "net.minecraft.nbt.CompoundTag"]
                : [Stack, "net.minecraft.nbt.CompoundTag"],
            "offsetInDirection" => ["net.minecraft.core.Direction", "java.util.Locale"],
            "divide" or "modulo" or "toNumber" or "teleport" or "isInArea" => [],

            "replaceBlock" => [BlockState, Property, "net.minecraft.world.level.block.Block"],
            "blockData" => [BlockEntity, "net.minecraft.nbt.CompoundTag"],
            "updateBlock" => [BlockState],
            "setBlockProperty" or "blockProperty" => [BlockState, Property],
            "propertyValue" => [BlockState, Property],
            "blockDirection" => [BlockState, "net.minecraft.world.level.block.state.properties.BlockStateProperties"],
            "setBlockDirection" =>
            [
                BlockState, "net.minecraft.world.level.block.state.properties.BlockStateProperties", "net.minecraft.core.Direction", "java.util.Locale",
            ],
            "setBlockNbtNumber" or "setBlockNbtText" or "setBlockNbtBoolean" or "blockNbtNumber" or "blockNbtText" or "blockNbtBoolean" => [BlockEntity, "net.minecraft.nbt.CompoundTag"],
            "customData" => modern
                ? [BlockEntity, "net.minecraft.nbt.CompoundTag", "net.minecraft.core.component.DataComponentMap", "net.minecraft.core.component.DataComponents",
                    "net.minecraft.world.item.component.CustomData"]
                : [BlockEntity, "net.minecraft.nbt.CompoundTag"],
            "useBlock" =>
            [
                "net.minecraft.world.phys.BlockHitResult", "net.minecraft.world.phys.Vec3", "net.minecraft.core.Direction",
                .. modern ? Array.Empty<string>() : ["net.minecraft.world.InteractionHand"],
            ],
            "enchantment" => modern
                ? ["net.minecraft.core.Holder", "net.minecraft.core.registries.Registries", "net.minecraft.resources.ResourceKey",
                    "net.minecraft.resources.ResourceLocation", "net.minecraft.world.item.enchantment.Enchantment"]
                : ["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.world.item.enchantment.Enchantment"],
            "addEnchantment" => modern ? [Stack, "net.minecraft.core.Holder", "net.minecraft.world.item.enchantment.Enchantment"] : [Stack, "net.minecraft.world.item.enchantment.Enchantment"],
            "removeEnchantment" => modern
                ? [Stack, "net.minecraft.core.Holder", "net.minecraft.world.item.enchantment.Enchantment", "net.minecraft.world.item.enchantment.EnchantmentHelper"]
                : [Stack, "java.util.Map", "net.minecraft.world.item.enchantment.Enchantment", "net.minecraft.world.item.enchantment.EnchantmentHelper"],
            "removeAllEnchantments" => modern
                ? [Stack, "net.minecraft.world.item.enchantment.EnchantmentHelper", "net.minecraft.world.item.enchantment.ItemEnchantments"]
                : [Stack, "java.util.HashMap", "net.minecraft.world.item.enchantment.EnchantmentHelper"],
            "enchantmentLevel" => modern
                ? [Stack, "net.minecraft.core.Holder", "net.minecraft.world.item.enchantment.Enchantment", "net.minecraft.world.item.enchantment.EnchantmentHelper"]
                : [Stack, "net.minecraft.world.item.enchantment.Enchantment", "net.minecraft.world.item.enchantment.EnchantmentHelper"],
            "setItemName" => modern
                ? [Stack, "net.minecraft.core.component.DataComponents", "net.minecraft.network.chat.Component"]
                : [Stack, "net.minecraft.network.chat.Component"],
            "foodValue" or "saturationValue" => modern
                ? [Stack, "net.minecraft.core.component.DataComponents", "net.minecraft.world.food.FoodProperties"]
                : [Stack, "net.minecraft.world.food.FoodProperties"],
            "isFood" => modern ? [Stack, "net.minecraft.core.component.DataComponents"] : [Stack],
            "gameRule" or "gameRuleBoolean" or "gameRuleNumber" or "setGameRuleBoolean" or "setGameRuleNumber" => ["net.minecraft.world.level.GameRules"],
            "effect" => modern
                ? ["net.minecraft.core.Holder", "net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.world.effect.MobEffect"]
                : ["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.world.effect.MobEffect"],
            "giveEffect" => modern
                ? ["net.minecraft.core.Holder", "net.minecraft.world.effect.MobEffect", "net.minecraft.world.effect.MobEffectInstance"]
                : ["net.minecraft.world.effect.MobEffect", "net.minecraft.world.effect.MobEffectInstance"],
            "removeEffect" or "hasEffect" => modern ? ["net.minecraft.core.Holder", "net.minecraft.world.effect.MobEffect"] : ["net.minecraft.world.effect.MobEffect"],
            "canBeSmelted" => modern
                ? [Stack, "net.minecraft.world.item.crafting.RecipeType", "net.minecraft.world.item.crafting.SingleRecipeInput"]
                : [Stack, "net.minecraft.world.item.crafting.RecipeType", "net.minecraft.world.SimpleContainer"],
            _ => null,
        };
    }

    /// <summary>Source lines of a helper, or null if the name is not one of these helpers.</summary>
    public static IReadOnlyList<string>? Method(string name, JavaGameApi api)
    {
        bool modern = api.UsesDataComponents;
        return name switch
        {
            "replaceBlock" =>
            [
                "private static void replaceBlock(Level level, BlockPos position, Block block, boolean keepState, boolean keepData) {",
                "    BlockState old = level.getBlockState(position);",
                "    BlockState state = block.defaultBlockState();",
                "    if (keepState) {",
                "        for (Property<?> property : old.getProperties()) {",
                "            if (state.hasProperty(property)) {",
                "                state = copyProperty(old, state, property);",
                "            }",
                "        }",
                "    }",
                "    CompoundTag data = keepData ? saveBlockData(level, position) : null;",
                "    level.setBlockAndUpdate(position, state);",
                "    if (data != null) {",
                "        loadBlockData(level, position, data);",
                "    }",
                "}",
                string.Empty,
                "private static <T extends Comparable<T>> BlockState copyProperty(BlockState from, BlockState to, Property<T> property) {",
                "    return to.setValue(property, from.getValue(property));",
                "}",
            ],
            "blockData" => modern
                ?
                [
                    "private static CompoundTag saveBlockData(Level level, BlockPos position) {",
                    "    BlockEntity entity = level.getBlockEntity(position);",
                    "    return entity == null ? null : entity.saveWithoutMetadata(level.registryAccess());",
                    "}",
                    string.Empty,
                    "private static void loadBlockData(Level level, BlockPos position, CompoundTag data) {",
                    "    BlockEntity entity = level.getBlockEntity(position);",
                    "    if (entity != null) {",
                    "        entity.loadWithComponents(data, level.registryAccess());",
                    "        entity.setChanged();",
                    "    }",
                    "}",
                ]
                :
                [
                    "private static CompoundTag saveBlockData(Level level, BlockPos position) {",
                    "    BlockEntity entity = level.getBlockEntity(position);",
                    "    return entity == null ? null : entity.saveWithoutMetadata();",
                    "}",
                    string.Empty,
                    "private static void loadBlockData(Level level, BlockPos position, CompoundTag data) {",
                    "    BlockEntity entity = level.getBlockEntity(position);",
                    "    if (entity != null) {",
                    "        entity.load(data);",
                    "        entity.setChanged();",
                    "    }",
                    "}",
                ],
            "updateBlock" =>
            [
                "private static void updateBlock(Level level, BlockPos position) {",
                "    BlockState state = level.getBlockState(position);",
                "    level.sendBlockUpdated(position, state, state, 3);",
                "    level.updateNeighborsAt(position, state.getBlock());",
                "}",
            ],
            "propertyValue" =>
            [
                "private static <T extends Comparable<T>> BlockState withProperty(BlockState state, Property<T> property, String value) {",
                "    return property.getValue(value).map(parsed -> state.setValue(property, parsed)).orElse(state);",
                "}",
                string.Empty,
                "private static <T extends Comparable<T>> String propertyText(BlockState state, Property<T> property) {",
                "    return property.getName(state.getValue(property));",
                "}",
            ],
            "setBlockProperty" =>
            [
                "private static void setBlockProperty(Level level, BlockPos position, String name, String value) {",
                "    BlockState state = level.getBlockState(position);",
                "    Property<?> property = state.getBlock().getStateDefinition().getProperty(name);",
                "    if (property != null) {",
                "        BlockState changed = withProperty(state, property, value);",
                "        if (changed != state) {",
                "            level.setBlockAndUpdate(position, changed);",
                "        }",
                "    }",
                "}",
            ],
            "blockProperty" =>
            [
                "private static String blockProperty(Level level, BlockPos position, String name) {",
                "    BlockState state = level.getBlockState(position);",
                "    Property<?> property = state.getBlock().getStateDefinition().getProperty(name);",
                "    return property == null ? \"\" : propertyText(state, property);",
                "}",
            ],
            "blockDirection" =>
            [
                "private static String blockDirection(Level level, BlockPos position) {",
                "    BlockState state = level.getBlockState(position);",
                "    if (state.hasProperty(BlockStateProperties.FACING)) {",
                "        return state.getValue(BlockStateProperties.FACING).getName();",
                "    }",
                "    if (state.hasProperty(BlockStateProperties.HORIZONTAL_FACING)) {",
                "        return state.getValue(BlockStateProperties.HORIZONTAL_FACING).getName();",
                "    }",
                "    return \"\";",
                "}",
            ],
            "setBlockDirection" =>
            [
                "private static void setBlockDirection(Level level, BlockPos position, String name) {",
                "    Direction direction = Direction.byName(name.toLowerCase(Locale.ROOT));",
                "    if (direction == null) {",
                "        return;",
                "    }",
                "    BlockState state = level.getBlockState(position);",
                "    if (state.hasProperty(BlockStateProperties.FACING)) {",
                "        level.setBlockAndUpdate(position, state.setValue(BlockStateProperties.FACING, direction));",
                "    } else if (state.hasProperty(BlockStateProperties.HORIZONTAL_FACING) && direction.getAxis().isHorizontal()) {",
                "        level.setBlockAndUpdate(position, state.setValue(BlockStateProperties.HORIZONTAL_FACING, direction));",
                "    }",
                "}",
            ],
            "setBlockNbtNumber" => SetNbt("setBlockNbtNumber", "int", "putInt"),
            "setBlockNbtText" => SetNbt("setBlockNbtText", "String", "putString"),
            "setBlockNbtBoolean" => SetNbt("setBlockNbtBoolean", "boolean", "putBoolean"),
            "blockNbtNumber" => GetNbt("blockNbtNumber", "int", "getInt", "0"),
            "blockNbtText" => GetNbt("blockNbtText", "String", "getString", "\"\""),
            "blockNbtBoolean" => GetNbt("blockNbtBoolean", "boolean", "getBoolean", "false"),
            "customData" => modern
                ?
                [
                    // Forge 1.21 no longer has the persistent data of block entities: the values go in the vanilla custom data component, saved with the block.
                    "private static CompoundTag customData(BlockEntity entity) {",
                    "    return entity.components().getOrDefault(DataComponents.CUSTOM_DATA, CustomData.EMPTY).copyTag();",
                    "}",
                    string.Empty,
                    "private static void setCustomData(BlockEntity entity, CompoundTag data) {",
                    "    entity.setComponents(DataComponentMap.builder().addAll(entity.components()).set(DataComponents.CUSTOM_DATA, CustomData.of(data)).build());",
                    "    entity.setChanged();",
                    "}",
                ]
                :
                [
                    "private static CompoundTag customData(BlockEntity entity) {",
                    "    return entity.getPersistentData();",
                    "}",
                    string.Empty,
                    "private static void setCustomData(BlockEntity entity, CompoundTag data) {",
                    "    entity.setChanged();",
                    "}",
                ],
            "useBlock" =>
            [
                "private static void useBlock(Level level, Player player, BlockPos position) {",
                "    BlockHitResult hit = new BlockHitResult(Vec3.atCenterOf(position), Direction.UP, position, false);",
                modern
                    ? "    level.getBlockState(position).useWithoutItem(level, player, hit);"
                    : "    level.getBlockState(position).use(level, player, InteractionHand.MAIN_HAND, hit);",
                "}",
            ],
            "enchantment" => modern
                ?
                [
                    "private static Holder<Enchantment> enchantment(Level level, String id) {",
                    $"    ResourceKey<Enchantment> key = ResourceKey.create(Registries.ENCHANTMENT, {api.ParseResourceLocation("id")});",
                    "    return level.registryAccess().registryOrThrow(Registries.ENCHANTMENT).getHolder(key).map(holder -> (Holder<Enchantment>) holder).orElse(null);",
                    "}",
                ]
                :
                [
                    "private static Enchantment enchantment(Level level, String id) {",
                    $"    return BuiltInRegistries.ENCHANTMENT.get({api.ParseResourceLocation("id")});",
                    "}",
                ],
            "addEnchantment" =>
            [
                "private static void addEnchantment(Level level, ItemStack stack, String id, int value) {",
                $"    {EnchantmentType(modern)} enchantment = enchantment(level, id);",
                "    if (enchantment != null && value > 0) {",
                "        stack.enchant(enchantment, value);",
                "    }",
                "}",
            ],
            "removeEnchantment" => modern
                ?
                [
                    "private static void removeEnchantment(Level level, ItemStack stack, String id) {",
                    "    Holder<Enchantment> enchantment = enchantment(level, id);",
                    "    if (enchantment != null) {",
                    "        EnchantmentHelper.updateEnchantments(stack, all -> all.removeIf(holder -> holder.equals(enchantment)));",
                    "    }",
                    "}",
                ]
                :
                [
                    "private static void removeEnchantment(Level level, ItemStack stack, String id) {",
                    "    Enchantment enchantment = enchantment(level, id);",
                    "    if (enchantment != null) {",
                    "        Map<Enchantment, Integer> all = EnchantmentHelper.getEnchantments(stack);",
                    "        all.remove(enchantment);",
                    "        EnchantmentHelper.setEnchantments(all, stack);",
                    "    }",
                    "}",
                ],
            "removeAllEnchantments" =>
            [
                "private static void removeAllEnchantments(ItemStack stack) {",
                modern ? "    EnchantmentHelper.setEnchantments(stack, ItemEnchantments.EMPTY);" : "    EnchantmentHelper.setEnchantments(new HashMap<>(), stack);",
                "}",
            ],
            "enchantmentLevel" =>
            [
                "private static int enchantmentLevel(Level level, ItemStack stack, String id) {",
                $"    {EnchantmentType(modern)} enchantment = enchantment(level, id);",
                "    return enchantment == null ? 0 : EnchantmentHelper.getItemEnchantmentLevel(enchantment, stack);",
                "}",
            ],
            "setItemName" =>
            [
                "private static void setItemName(ItemStack stack, String name) {",
                modern ? "    stack.set(DataComponents.CUSTOM_NAME, Component.literal(name));" : "    stack.setHoverName(Component.literal(name));",
                "}",
            ],
            "foodValue" =>
            [
                "private static int foodValue(ItemStack stack) {",
                modern ? "    FoodProperties food = stack.get(DataComponents.FOOD);" : "    FoodProperties food = stack.getItem().getFoodProperties();",
                modern ? "    return food == null ? 0 : food.nutrition();" : "    return food == null ? 0 : food.getNutrition();",
                "}",
            ],
            "saturationValue" =>
            [
                "private static int saturationValue(ItemStack stack) {",
                modern ? "    FoodProperties food = stack.get(DataComponents.FOOD);" : "    FoodProperties food = stack.getItem().getFoodProperties();",
                // Since 1.20.5, the food holds the saturation points (2 x nutrition x modifier): the modifier is computed back.
                modern
                    ? "    return food == null || food.nutrition() == 0 ? 0 : Math.round(food.saturation() / (2.0f * food.nutrition()) * 10);"
                    : "    return food == null ? 0 : Math.round(food.getSaturationModifier() * 10);",
                "}",
            ],
            "isFood" =>
            [
                "private static boolean isFood(ItemStack stack) {",
                modern ? "    return stack.has(DataComponents.FOOD);" : "    return stack.isEdible();",
                "}",
            ],
            "divide" =>
            [
                "private static int divide(int a, int b) {",
                "    return b == 0 ? 0 : a / b;",
                "}",
            ],
            "modulo" =>
            [
                "private static int modulo(int a, int b) {",
                "    return b == 0 ? 0 : Math.floorMod(a, b);",
                "}",
            ],
            "toNumber" =>
            [
                "private static int toNumber(String text) {",
                "    try {",
                "        return Integer.parseInt(text.trim());",
                "    } catch (NumberFormatException exception) {",
                "        return 0;",
                "    }",
                "}",
            ],
            "setTime" =>
            [
                "private static void setTime(Level level, int time) {",
                "    if (level instanceof ServerLevel server) {",
                "        server.setDayTime(time);",
                "    }",
                "}",
            ],
            "setWeather" =>
            [
                "private static void setWeather(Level level, boolean rain, boolean thunder, int seconds) {",
                "    if (level instanceof ServerLevel server) {",
                "        int ticks = Math.max(1, seconds) * 20;",
                "        server.setWeatherParameters(rain || thunder ? 0 : ticks, rain || thunder ? ticks : 0, rain || thunder, thunder);",
                "    }",
                "}",
            ],
            "spawnEntity" =>
            [
                "private static void spawnEntity(Level level, String id, BlockPos position) {",
                "    if (level instanceof ServerLevel server) {",
                "        EntityType.byString(id).ifPresent(type -> type.spawn(server, position, MobSpawnType.COMMAND));",
                "    }",
                "}",
            ],
            "spawnParticles" =>
            [
                "private static void spawnParticles(Level level, String id, BlockPos position, int count) {",
                $"    ParticleType<?> type = BuiltInRegistries.PARTICLE_TYPE.get({api.ParseResourceLocation("id")});",
                "    if (type instanceof SimpleParticleType particle && level instanceof ServerLevel server) {",
                "        server.sendParticles(particle, position.getX() + 0.5, position.getY() + 0.5, position.getZ() + 0.5, count, 0.3, 0.3, 0.3, 0.02);",
                "    }",
                "}",
            ],
            "teleport" =>
            [
                "private static void teleport(Player player, BlockPos position) {",
                "    player.teleportTo(position.getX() + 0.5, position.getY(), position.getZ() + 0.5);",
                "}",
            ],
            "armorItem" =>
            [
                "private static ItemStack armorItem(Player player, String slot) {",
                "    try {",
                "        return player.getItemBySlot(EquipmentSlot.byName(slot.toLowerCase(Locale.ROOT)));",
                "    } catch (IllegalArgumentException exception) {",
                "        return ItemStack.EMPTY;",
                "    }",
                "}",
            ],
            "lookedAtBlock" =>
            [
                "private static BlockPos lookedAtBlock(Player player) {",
                "    HitResult hit = player.pick(5.0, 0.0f, false);",
                "    return hit.getType() == HitResult.Type.BLOCK && hit instanceof BlockHitResult block ? block.getBlockPos() : player.blockPosition();",
                "}",
            ],
            "slotItem" =>
            [
                "private static ItemStack slotItem(Player player, int slot) {",
                "    return slot >= 0 && slot < player.getInventory().getContainerSize() ? player.getInventory().getItem(slot) : ItemStack.EMPTY;",
                "}",
            ],
            "clearSlot" =>
            [
                "private static void clearSlot(Player player, int slot) {",
                "    if (slot >= 0 && slot < player.getInventory().getContainerSize()) {",
                "        player.getInventory().setItem(slot, ItemStack.EMPTY);",
                "    }",
                "}",
            ],
            "setItemNbtNumber" => SetItemNbt("setItemNbtNumber", "int", "putInt", modern),
            "setItemNbtText" => SetItemNbt("setItemNbtText", "String", "putString", modern),
            "itemNbtNumber" => GetItemNbt("itemNbtNumber", "int", "getInt", "0", modern),
            "itemNbtText" => GetItemNbt("itemNbtText", "String", "getString", "\"\"", modern),
            "isInArea" =>
            [
                "private static boolean isInArea(BlockPos position, BlockPos from, BlockPos to) {",
                "    return position.getX() >= Math.min(from.getX(), to.getX()) && position.getX() <= Math.max(from.getX(), to.getX())",
                "        && position.getY() >= Math.min(from.getY(), to.getY()) && position.getY() <= Math.max(from.getY(), to.getY())",
                "        && position.getZ() >= Math.min(from.getZ(), to.getZ()) && position.getZ() <= Math.max(from.getZ(), to.getZ());",
                "}",
            ],
            "offsetInDirection" =>
            [
                "private static BlockPos offsetInDirection(BlockPos position, String name, int distance) {",
                "    Direction direction = Direction.byName(name.toLowerCase(Locale.ROOT));",
                "    return direction == null ? position : position.relative(direction, distance);",
                "}",
            ],
            // A rule is found by name among all the rules (of Minecraft and of the mods), whatever its type.
            "gameRule" =>
            [
                "private static <T extends GameRules.Value<T>> T gameRule(Level level, String name, Class<T> type) {",
                "    GameRules rules = level.getGameRules();",
                "    Object[] found = { null };",
                "    GameRules.visitGameRuleTypes(new GameRules.GameRuleTypeVisitor() {",
                "        @Override",
                "        public <V extends GameRules.Value<V>> void visit(GameRules.Key<V> key, GameRules.Type<V> ruleType) {",
                "            if (key.getId().equals(name)) {",
                "                found[0] = rules.getRule(key);",
                "            }",
                "        }",
                "    });",
                "    return type.isInstance(found[0]) ? type.cast(found[0]) : null;",
                "}",
            ],
            "gameRuleBoolean" =>
            [
                "private static boolean gameRuleBoolean(Level level, String name) {",
                "    GameRules.BooleanValue rule = gameRule(level, name, GameRules.BooleanValue.class);",
                "    return rule != null && rule.get();",
                "}",
            ],
            "gameRuleNumber" =>
            [
                "private static int gameRuleNumber(Level level, String name) {",
                "    GameRules.IntegerValue rule = gameRule(level, name, GameRules.IntegerValue.class);",
                "    return rule == null ? 0 : rule.get();",
                "}",
            ],
            "setGameRuleBoolean" =>
            [
                "private static void setGameRuleBoolean(Level level, String name, boolean value) {",
                "    GameRules.BooleanValue rule = gameRule(level, name, GameRules.BooleanValue.class);",
                "    if (rule != null) {",
                "        rule.set(value, level.getServer());",
                "    }",
                "}",
            ],
            "setGameRuleNumber" =>
            [
                "private static void setGameRuleNumber(Level level, String name, int value) {",
                "    GameRules.IntegerValue rule = gameRule(level, name, GameRules.IntegerValue.class);",
                "    if (rule != null) {",
                "        rule.set(value, level.getServer());",
                "    }",
                "}",
            ],
            "effect" => modern
                ?
                [
                    "private static Holder<MobEffect> effect(String id) {",
                    $"    return BuiltInRegistries.MOB_EFFECT.getHolder({api.ParseResourceLocation("id")}).map(holder -> (Holder<MobEffect>) holder).orElse(null);",
                    "}",
                ]
                :
                [
                    "private static MobEffect effect(String id) {",
                    $"    return BuiltInRegistries.MOB_EFFECT.get({api.ParseResourceLocation("id")});",
                    "}",
                ],
            "giveEffect" =>
            [
                "private static void giveEffect(Player player, String id, int seconds, int level) {",
                $"    {EffectType(modern)} effect = effect(id);",
                "    if (effect != null && seconds > 0) {",
                "        player.addEffect(new MobEffectInstance(effect, seconds * 20, Math.max(0, level - 1)));",
                "    }",
                "}",
            ],
            "removeEffect" =>
            [
                "private static void removeEffect(Player player, String id) {",
                $"    {EffectType(modern)} effect = effect(id);",
                "    if (effect != null) {",
                "        player.removeEffect(effect);",
                "    }",
                "}",
            ],
            "hasEffect" =>
            [
                "private static boolean hasEffect(Player player, String id) {",
                $"    {EffectType(modern)} effect = effect(id);",
                "    return effect != null && player.hasEffect(effect);",
                "}",
            ],
            "canBeSmelted" =>
            [
                "private static boolean canBeSmelted(Level level, ItemStack stack) {",
                modern
                    ? "    return level.getRecipeManager().getRecipeFor(RecipeType.SMELTING, new SingleRecipeInput(stack), level).isPresent();"
                    : "    return level.getRecipeManager().getRecipeFor(RecipeType.SMELTING, new SimpleContainer(stack), level).isPresent();",
                "}",
            ],
            _ => null,
        };
    }

    private static string EnchantmentType(bool modern) => modern ? "Holder<Enchantment>" : "Enchantment";

    // Data of an item stack: its NBT tag in 1.20.1, its custom data component from 1.20.5.
    private static IReadOnlyList<string> SetItemNbt(string method, string type, string put, bool modern) => modern
        ?
        [
            $"private static void {method}(ItemStack stack, String name, {type} value) {{",
            $"    CustomData.update(DataComponents.CUSTOM_DATA, stack, tag -> tag.{put}(name, value));",
            "}",
        ]
        :
        [
            $"private static void {method}(ItemStack stack, String name, {type} value) {{",
            $"    stack.getOrCreateTag().{put}(name, value);",
            "}",
        ];

    private static IReadOnlyList<string> GetItemNbt(string method, string type, string get, string fallback, bool modern) => modern
        ?
        [
            $"private static {type} {method}(ItemStack stack, String name) {{",
            "    CompoundTag tag = stack.getOrDefault(DataComponents.CUSTOM_DATA, CustomData.EMPTY).copyTag();",
            $"    return tag.contains(name) ? tag.{get}(name) : {fallback};",
            "}",
        ]
        :
        [
            $"private static {type} {method}(ItemStack stack, String name) {{",
            "    CompoundTag tag = stack.getTag();",
            $"    return tag != null && tag.contains(name) ? tag.{get}(name) : {fallback};",
            "}",
        ];

    private static string EffectType(bool modern) => modern ? "Holder<MobEffect>" : "MobEffect";

    private static IReadOnlyList<string> SetNbt(string method, string type, string put) =>
    [
        $"private static void {method}(Level level, BlockPos position, String name, {type} value) {{",
        "    BlockEntity entity = level.getBlockEntity(position);",
        "    if (entity != null) {",
        "        CompoundTag data = customData(entity);",
        $"        data.{put}(name, value);",
        "        setCustomData(entity, data);",
        "    }",
        "}",
    ];

    private static IReadOnlyList<string> GetNbt(string method, string type, string get, string fallback) =>
    [
        $"private static {type} {method}(Level level, BlockPos position, String name) {{",
        "    BlockEntity entity = level.getBlockEntity(position);",
        $"    return entity == null ? {fallback} : customData(entity).{get}(name);",
        "}",
    ];
}
