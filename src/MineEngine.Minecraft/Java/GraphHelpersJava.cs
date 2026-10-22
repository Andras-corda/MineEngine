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
        "raycastPosition" => ["raycastBlock"],
        "lookedAtCreature" or "entityLookedAt" => ["raycastEntity"],
        "stateProperty" or "stateWithProperty" => ["propertyValue"],
        "makeEffect" => ["effect"],
        "playMusic" => ["stopSounds"],
        "setCreatureAppearance" or "creatureAppearance" => ["appearanceHolders"],
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
            "divideDecimal" or "entityLookedAt" => [],
            "toLong" or "toDecimal" or "charAt" => [],
            "uuid" => ["java.util.UUID"],
            "entityUuid" => ["java.util.UUID", "net.minecraft.world.entity.Entity"],
            "entityByUuid" => ["java.util.UUID", "net.minecraft.world.entity.Entity", "net.minecraft.server.level.ServerLevel"],
            "styledText" => ["net.minecraft.network.chat.Component", "net.minecraft.network.chat.MutableComponent", "net.minecraft.ChatFormatting", "java.util.Locale"],
            "identifier" => ["net.minecraft.resources.ResourceLocation"],
            "toLiving" => ["net.minecraft.world.entity.Entity", "net.minecraft.world.entity.LivingEntity"],
            "toPlayer" => ["net.minecraft.world.entity.Entity"],
            "equipmentItem" => ["net.minecraft.world.entity.LivingEntity", "net.minecraft.world.entity.EquipmentSlot", "net.minecraft.world.item.ItemStack"],
            "entityBox" => ["net.minecraft.world.entity.Entity", "net.minecraft.world.phys.AABB"],
            "makeBox" => ["net.minecraft.world.phys.AABB"],
            "entitiesInBox" => ["net.minecraft.world.entity.Entity", "net.minecraft.world.phys.AABB", "java.util.List", "java.util.ArrayList"],
            "genericDamage" => ["net.minecraft.world.entity.Entity", "net.minecraft.world.damagesource.DamageSource"],
            "damageWithSource" => ["net.minecraft.world.entity.Entity", "net.minecraft.world.damagesource.DamageSource"],
            "makeEffect" => modern
                ? ["net.minecraft.core.Holder", "net.minecraft.world.effect.MobEffect", "net.minecraft.world.effect.MobEffectInstance"]
                : ["net.minecraft.world.effect.MobEffect", "net.minecraft.world.effect.MobEffectInstance"],
            "applyEffect" => ["net.minecraft.world.entity.LivingEntity", "net.minecraft.world.effect.MobEffectInstance"],
            "worldOf" => ["net.minecraft.world.entity.Entity"],
            "setEntityGroup" or "entityGroup" => ["net.minecraft.world.entity.Entity"],
            "listGet" or "listSet" or "listRemoveAt" or "arrayResize" => ["java.util.List"],
            "stateProperty" or "stateWithProperty" => [BlockState, Property],
            "entityPosition" or "isEntityAlive" or "removeEntity" or "teleportEntity" or "damageEntity" => ["net.minecraft.world.entity.Entity"],
            "entityTypeId" => ["net.minecraft.world.entity.Entity", "net.minecraft.world.entity.EntityType"],
            "entityHealth" => ["net.minecraft.world.entity.Entity", "net.minecraft.world.entity.LivingEntity"],
            "nearestEntity" =>
            [
                "net.minecraft.world.entity.Entity", "net.minecraft.world.entity.EntityType", "net.minecraft.world.phys.AABB", "net.minecraft.world.phys.Vec3",
            ],
            "flipFlop" => ["java.util.Set", "java.util.concurrent.ConcurrentHashMap"],
            "runCommandAsPlayer" or "setVelocity" or "addVelocity" or "launchForward" => [],
            "setItemAppearance" or "itemAppearance" => modern
                ? [Stack, "net.minecraft.core.component.DataComponents", "net.minecraft.world.item.component.CustomModelData"]
                : [Stack],
            "setBlockAppearance" or "blockAppearance" =>
            [
                "net.minecraft.world.level.block.state.BlockState", "net.minecraft.world.level.block.state.properties.IntegerProperty",
            ],
            "appearanceHolders" =>
            [
                "java.util.List", "net.minecraft.world.entity.Entity", "net.minecraft.world.entity.EntityType", "net.minecraft.resources.ResourceLocation",
                "net.minecraft.world.phys.AABB",
            ],
            "setCreatureAppearance" or "creatureAppearance" => ["java.util.List", "java.util.Comparator", "net.minecraft.world.entity.Entity"],
            "runCommandAsServer" => ["net.minecraft.server.level.ServerLevel", "net.minecraft.world.phys.Vec3"],
            "raycastBlock" => ["net.minecraft.world.phys.HitResult"],
            "raycastPosition" => ["net.minecraft.world.phys.HitResult", "net.minecraft.world.phys.BlockHitResult"],
            "raycastEntity" =>
            [
                "net.minecraft.world.entity.Entity", "net.minecraft.world.entity.projectile.ProjectileUtil", "net.minecraft.world.phys.AABB",
                "net.minecraft.world.phys.EntityHitResult", "net.minecraft.world.phys.HitResult", "net.minecraft.world.phys.Vec3",
            ],
            "lookedAtCreature" => ["net.minecraft.world.entity.Entity", "net.minecraft.world.entity.EntityType"],
            "playSoundAt" or "playSoundTo" => ["net.minecraft.sounds.SoundEvent", "net.minecraft.sounds.SoundSource"],
            "playMusic" or "stopSounds" =>
            [
                "net.minecraft.sounds.SoundEvent", "net.minecraft.sounds.SoundSource", "net.minecraft.server.level.ServerPlayer",
                "net.minecraft.network.protocol.game.ClientboundStopSoundPacket",
            ],


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
            "setItemAppearance" => modern
                ?
                [
                    "// The appearance of an item is its custom model data: the model of the item lists one override per appearance.",
                    "private static void setItemAppearance(ItemStack stack, int appearance) {",
                    "    if (appearance <= 0) {",
                    "        stack.remove(DataComponents.CUSTOM_MODEL_DATA);",
                    "    } else {",
                    "        stack.set(DataComponents.CUSTOM_MODEL_DATA, new CustomModelData(appearance));",
                    "    }",
                    "}",
                ]
                :
                [
                    "// The appearance of an item is its custom model data: the model of the item lists one override per appearance.",
                    "private static void setItemAppearance(ItemStack stack, int appearance) {",
                    "    if (appearance <= 0) {",
                    "        if (stack.getTag() != null) {",
                    "            stack.getTag().remove(\"CustomModelData\");",
                    "        }",
                    "    } else {",
                    "        stack.getOrCreateTag().putInt(\"CustomModelData\", appearance);",
                    "    }",
                    "}",
                ],
            "itemAppearance" => modern
                ?
                [
                    "private static int itemAppearance(ItemStack stack) {",
                    "    CustomModelData data = stack.get(DataComponents.CUSTOM_MODEL_DATA);",
                    "    return data == null ? 0 : data.value();",
                    "}",
                ]
                :
                [
                    "private static int itemAppearance(ItemStack stack) {",
                    "    return stack.getTag() == null ? 0 : stack.getTag().getInt(\"CustomModelData\");",
                    "}",
                ],
            "setBlockAppearance" =>
            [
                "// A block with appearances has the property \"appearance\" (0: normal look).",
                "private static void setBlockAppearance(Level level, BlockPos position, int appearance) {",
                "    BlockState state = level.getBlockState(position);",
                "    if (state.getBlock().getStateDefinition().getProperty(\"appearance\") instanceof IntegerProperty property",
                "            && property.getPossibleValues().contains(appearance)) {",
                "        level.setBlockAndUpdate(position, state.setValue(property, appearance));",
                "    }",
                "}",
            ],
            "blockAppearance" =>
            [
                "private static int blockAppearance(Level level, BlockPos position) {",
                "    BlockState state = level.getBlockState(position);",
                "    return state.getBlock().getStateDefinition().getProperty(\"appearance\") instanceof IntegerProperty property ? state.getValue(property) : 0;",
                "}",
            ],
            "appearanceHolders" =>
            [
                "/** Mobs of the mod with appearances around the position; creature: \"goblin\", \"mymod:goblin\" or empty for all. */",
                "private static List<Entity> appearanceHolders(Level level, BlockPos position, int radius, String creature) {",
                "    int r = Math.max(0, radius);",
                "    AABB area = new AABB(position.getX() - r, position.getY() - r, position.getZ() - r, position.getX() + r + 1, position.getY() + r + 1, position.getZ() + r + 1);",
                "    String wanted = creature.strip();",
                "    return level.getEntities((Entity) null, area, entity -> {",
                "        if (!(entity instanceof MineEngineAppearance)) {",
                "            return false;",
                "        }",
                "        ResourceLocation key = EntityType.getKey(entity.getType());",
                "        return wanted.isEmpty() || key.toString().equals(wanted) || key.getPath().equals(wanted);",
                "    });",
                "}",
            ],
            "setCreatureAppearance" =>
            [
                "private static void setCreatureAppearance(Level level, BlockPos position, int radius, String creature, int appearance) {",
                "    if (!level.isClientSide()) {",
                "        for (Entity entity : appearanceHolders(level, position, radius, creature)) {",
                "            ((MineEngineAppearance) entity).setAppearance(appearance);",
                "        }",
                "    }",
                "}",
            ],
            "creatureAppearance" =>
            [
                "private static int creatureAppearance(Level level, BlockPos position, int radius, String creature) {",
                "    List<Entity> found = appearanceHolders(level, position, radius, creature);",
                "    return found.stream()",
                "        .min(Comparator.comparingDouble(entity -> entity.distanceToSqr(position.getX() + 0.5, position.getY() + 0.5, position.getZ() + 0.5)))",
                "        .map(entity -> ((MineEngineAppearance) entity).getAppearance())",
                "        .orElse(0);",
                "}",
            ],
            "runCommandAsPlayer" =>
            [
                "private static void runCommandAsPlayer(Player player, String command) {",
                "    if (player.getServer() != null && !command.isBlank()) {",
                "        player.getServer().getCommands().performPrefixedCommand(player.createCommandSourceStack(), command.strip());",
                "    }",
                "}",
            ],
            "runCommandAsServer" =>
            [
                "private static void runCommandAsServer(Level level, BlockPos position, String command) {",
                "    if (level instanceof ServerLevel server && !command.isBlank()) {",
                "        server.getServer().getCommands().performPrefixedCommand(",
                "            server.getServer().createCommandSourceStack().withLevel(server).withPosition(Vec3.atCenterOf(position)).withSuppressedOutput(),",
                "            command.strip());",
                "    }",
                "}",
            ],
            "setVelocity" =>
            [
                "// Speeds are given in blocks per second; Minecraft counts in blocks per tick (20 ticks per second).",
                "private static void setVelocity(Player player, int x, int y, int z) {",
                "    player.setDeltaMovement(x / 20.0, y / 20.0, z / 20.0);",
                "    player.hurtMarked = true;",
                "}",
            ],
            "addVelocity" =>
            [
                "private static void addVelocity(Player player, int x, int y, int z) {",
                "    player.push(x / 20.0, y / 20.0, z / 20.0);",
                "    player.hurtMarked = true;",
                "}",
            ],
            "launchForward" =>
            [
                "private static void launchForward(Player player, int speed) {",
                "    player.setDeltaMovement(player.getLookAngle().scale(speed / 20.0));",
                "    player.hurtMarked = true;",
                "}",
            ],
            "raycastBlock" =>
            [
                "private static HitResult raycastBlock(Player player, int distance, boolean fluids) {",
                "    return player.pick(Math.max(1, distance), 0.0f, fluids);",
                "}",
            ],
            "raycastPosition" =>
            [
                "private static BlockPos raycastPosition(Player player, int distance, boolean fluids) {",
                "    HitResult hit = raycastBlock(player, distance, fluids);",
                "    return hit instanceof BlockHitResult block && hit.getType() == HitResult.Type.BLOCK ? block.getBlockPos() : BlockPos.containing(hit.getLocation());",
                "}",
            ],
            "raycastEntity" =>
            [
                "private static Entity raycastEntity(Player player, int distance) {",
                "    double reach = Math.max(1, distance);",
                "    Vec3 eye = player.getEyePosition();",
                "    Vec3 look = player.getViewVector(1.0f).scale(reach);",
                "    AABB area = player.getBoundingBox().expandTowards(look).inflate(1.0);",
                "    EntityHitResult hit = ProjectileUtil.getEntityHitResult(player, eye, eye.add(look), area, entity -> !entity.isSpectator() && entity.isPickable(), reach * reach);",
                "    if (hit == null) {",
                "        return null;",
                "    }",
                "    // A block between the player and the creature hides it.",
                "    HitResult block = player.pick(reach, 0.0f, false);",
                "    return block.getType() != HitResult.Type.MISS && block.getLocation().distanceToSqr(eye) < hit.getLocation().distanceToSqr(eye) ? null : hit.getEntity();",
                "}",
            ],
            "lookedAtCreature" =>
            [
                "private static String lookedAtCreature(Player player, int distance) {",
                "    Entity entity = raycastEntity(player, distance);",
                "    return entity == null ? \"\" : EntityType.getKey(entity.getType()).toString();",
                "}",
            ],
            "playSoundAt" =>
            [
                "private static void playSoundAt(Level level, BlockPos position, SoundEvent sound, int volume, int pitch) {",
                "    if (sound != null) {",
                "        level.playSound(null, position, sound, SoundSource.BLOCKS, Math.max(0, volume) / 100.0f, Math.max(1, pitch) / 100.0f);",
                "    }",
                "}",
            ],
            "playSoundTo" =>
            [
                "private static void playSoundTo(Player player, SoundEvent sound, int volume, int pitch) {",
                "    if (sound != null) {",
                "        player.playNotifySound(sound, SoundSource.PLAYERS, Math.max(0, volume) / 100.0f, Math.max(1, pitch) / 100.0f);",
                "    }",
                "}",
            ],
            "stopSounds" =>
            [
                "private static void stopSounds(Player player, boolean musicOnly) {",
                "    if (player instanceof ServerPlayer server) {",
                "        server.connection.send(new ClientboundStopSoundPacket(null, musicOnly ? SoundSource.MUSIC : null));",
                "    }",
                "}",
            ],
            "playMusic" =>
            [
                "private static void playMusic(Player player, SoundEvent music) {",
                "    if (music != null && player instanceof ServerPlayer server) {",
                "        stopSounds(player, true);",
                "        server.playNotifySound(music, SoundSource.MUSIC, 1.0f, 1.0f);",
                "    }",
                "}",
            ],
            "flipFlop" =>
            [
                "// Flip Flop nodes whose next run is B.",
                "private static final Set<String> FLIP_FLOPS = ConcurrentHashMap.newKeySet();",
                "",
                "private static boolean flipFlop(String node) {",
                "    // A the first time, then B, then A...",
                "    return FLIP_FLOPS.add(node) || !FLIP_FLOPS.remove(node);",
                "}",
            ],
            "toLong" =>
            [
                "private static long toLong(String text) {",
                "    try {",
                "        return Long.parseLong(text.trim());",
                "    } catch (NumberFormatException exception) {",
                "        return 0L;",
                "    }",
                "}",
            ],
            "toDecimal" =>
            [
                "private static double toDecimal(String text) {",
                "    try {",
                "        return Double.parseDouble(text.trim().replace(',', '.'));",
                "    } catch (NumberFormatException exception) {",
                "        return 0.0;",
                "    }",
                "}",
            ],
            "charAt" =>
            [
                "private static char charAt(String text, int index) {",
                "    return text != null && index >= 0 && index < text.length() ? text.charAt(index) : ' ';",
                "}",
            ],
            "uuid" =>
            [
                "private static UUID uuid(String text) {",
                "    try {",
                "        return UUID.fromString(text.trim());",
                "    } catch (IllegalArgumentException exception) {",
                "        return new UUID(0L, 0L);",
                "    }",
                "}",
            ],
            "entityUuid" =>
            [
                "private static UUID entityUuid(Entity entity) {",
                "    return entity == null ? new UUID(0L, 0L) : entity.getUUID();",
                "}",
            ],
            "entityByUuid" =>
            [
                "private static Entity entityByUuid(Level level, UUID uuid) {",
                "    return level instanceof ServerLevel server && uuid != null ? server.getEntity(uuid) : null;",
                "}",
            ],
            "styledText" =>
            [
                "private static Component styledText(String text, String color, boolean bold, boolean italic) {",
                "    MutableComponent component = Component.literal(text);",
                "    ChatFormatting formatting = ChatFormatting.getByName(color.trim().toLowerCase(Locale.ROOT));",
                "    if (formatting != null && formatting.isColor()) {",
                "        component = component.withStyle(formatting);",
                "    }",
                "    if (bold) {",
                "        component = component.withStyle(ChatFormatting.BOLD);",
                "    }",
                "    if (italic) {",
                "        component = component.withStyle(ChatFormatting.ITALIC);",
                "    }",
                "    return component;",
                "}",
            ],
            "identifier" =>
            [
                "private static ResourceLocation identifier(String text) {",
                "    ResourceLocation id = ResourceLocation.tryParse(text.trim());",
                $"    return id != null ? id : {api.ParseResourceLocation("\"minecraft:air\"")};",
                "}",
            ],
            "toLiving" =>
            [
                "private static LivingEntity toLiving(Entity entity) {",
                "    return entity instanceof LivingEntity living ? living : null;",
                "}",
            ],
            "toPlayer" =>
            [
                "private static Player toPlayer(Entity entity) {",
                "    return entity instanceof Player player ? player : null;",
                "}",
            ],
            "equipmentItem" =>
            [
                "private static ItemStack equipmentItem(LivingEntity entity, EquipmentSlot slot) {",
                "    return entity == null ? ItemStack.EMPTY : entity.getItemBySlot(slot);",
                "}",
            ],
            "entityBox" =>
            [
                "private static AABB entityBox(Entity entity) {",
                "    return entity == null ? new AABB(0, 0, 0, 0, 0, 0) : entity.getBoundingBox();",
                "}",
            ],
            "makeBox" =>
            [
                "private static AABB makeBox(BlockPos from, BlockPos to) {",
                "    return new AABB(Math.min(from.getX(), to.getX()), Math.min(from.getY(), to.getY()), Math.min(from.getZ(), to.getZ()),",
                "        Math.max(from.getX(), to.getX()) + 1, Math.max(from.getY(), to.getY()) + 1, Math.max(from.getZ(), to.getZ()) + 1);",
                "}",
            ],
            "entitiesInBox" =>
            [
                "private static List<Entity> entitiesInBox(Level level, AABB box) {",
                "    return new ArrayList<>(level.getEntities((Entity) null, box, entity -> true));",
                "}",
            ],
            "genericDamage" =>
            [
                "private static DamageSource genericDamage(Level level, Entity entity) {",
                "    return entity == null ? level.damageSources().generic() : entity.damageSources().generic();",
                "}",
            ],
            "damageWithSource" =>
            [
                "private static void damageWithSource(Entity entity, DamageSource source, int amount) {",
                "    if (entity != null && source != null && amount > 0) {",
                "        entity.hurt(source, amount);",
                "    }",
                "}",
            ],
            "makeEffect" =>
            [
                "private static MobEffectInstance makeEffect(String id, int seconds, int level) {",
                $"    {EffectType(modern)} effect = effect(id);",
                "    return effect == null ? null : new MobEffectInstance(effect, Math.max(1, seconds) * 20, Math.max(0, level - 1));",
                "}",
            ],
            "applyEffect" =>
            [
                "private static void applyEffect(LivingEntity entity, MobEffectInstance effect) {",
                "    if (entity != null && effect != null) {",
                "        entity.addEffect(new MobEffectInstance(effect));",
                "    }",
                "}",
            ],
            "setEntityGroup" =>
            [
                "private static void setEntityGroup(Entity entity, String group) {",
                "    if (entity != null) {",
                "        entity.getPersistentData().putString(\"mineengine_group\", group);",
                "    }",
                "}",
            ],
            "entityGroup" =>
            [
                "private static String entityGroup(Entity entity) {",
                "    return entity == null ? \"\" : entity.getPersistentData().getString(\"mineengine_group\");",
                "}",
            ],
            "worldOf" =>
            [
                "private static Level worldOf(Level level, Entity entity) {",
                "    return entity == null ? level : entity.level();",
                "}",
            ],
            "divideDecimal" =>
            [
                "private static double divideDecimal(double a, double b) {",
                "    return b == 0.0 ? 0.0 : a / b;",
                "}",
            ],
            "listGet" =>
            [
                "private static <T> T listGet(List<T> list, int index, T fallback) {",
                "    return index >= 0 && index < list.size() ? list.get(index) : fallback;",
                "}",
            ],
            "listSet" =>
            [
                "private static <T> void listSet(List<T> list, int index, T value) {",
                "    if (index >= 0 && index < list.size()) {",
                "        list.set(index, value);",
                "    }",
                "}",
            ],
            "listRemoveAt" =>
            [
                "private static void listRemoveAt(List<?> list, int index) {",
                "    if (index >= 0 && index < list.size()) {",
                "        list.remove(index);",
                "    }",
                "}",
            ],
            "arrayResize" =>
            [
                "private static <T> void arrayResize(List<T> list, int size, T fallback) {",
                "    int target = Math.max(0, Math.min(size, 100000));",
                "    while (list.size() > target) {",
                "        list.remove(list.size() - 1);",
                "    }",
                "    while (list.size() < target) {",
                "        list.add(fallback);",
                "    }",
                "}",
            ],
            "stateProperty" =>
            [
                "private static String stateProperty(BlockState state, String name) {",
                "    Property<?> property = state.getBlock().getStateDefinition().getProperty(name);",
                "    return property == null ? \"\" : propertyText(state, property);",
                "}",
            ],
            "stateWithProperty" =>
            [
                "private static BlockState stateWithProperty(BlockState state, String name, String value) {",
                "    Property<?> property = state.getBlock().getStateDefinition().getProperty(name);",
                "    return property == null ? state : withProperty(state, property, value);",
                "}",
            ],
            "entityPosition" =>
            [
                "private static BlockPos entityPosition(Entity entity) {",
                "    return entity == null ? BlockPos.ZERO : entity.blockPosition();",
                "}",
            ],
            "entityTypeId" =>
            [
                "private static String entityTypeId(Entity entity) {",
                "    return entity == null ? \"\" : EntityType.getKey(entity.getType()).toString();",
                "}",
            ],
            "isEntityAlive" =>
            [
                "private static boolean isEntityAlive(Entity entity) {",
                "    return entity != null && entity.isAlive();",
                "}",
            ],
            "removeEntity" =>
            [
                "private static void removeEntity(Entity entity) {",
                "    if (entity != null && !(entity instanceof Player)) {",
                "        entity.discard();",
                "    }",
                "}",
            ],
            "nearestEntity" =>
            [
                "private static Entity nearestEntity(Level level, BlockPos position, int radius, String id) {",
                "    int r = Math.max(1, Math.min(radius, 128));",
                "    AABB area = new AABB(position.getX() - r, position.getY() - r, position.getZ() - r, position.getX() + r + 1, position.getY() + r + 1, position.getZ() + r + 1);",
                "    Vec3 center = Vec3.atCenterOf(position);",
                "    Entity nearest = null;",
                "    double best = Double.MAX_VALUE;",
                "    for (Entity entity : level.getEntities((Entity) null, area, candidate -> id.isEmpty() ? !(candidate instanceof Player) : EntityType.getKey(candidate.getType()).toString().equals(id))) {",
                "        double distance = entity.distanceToSqr(center);",
                "        if (distance < best) {",
                "            best = distance;",
                "            nearest = entity;",
                "        }",
                "    }",
                "    return nearest;",
                "}",
            ],
            "entityLookedAt" =>
            [
                "private static Entity entityLookedAt(Player player, int distance) {",
                "    return raycastEntity(player, distance);",
                "}",
            ],
            "teleportEntity" =>
            [
                "private static void teleportEntity(Entity entity, BlockPos position) {",
                "    if (entity != null) {",
                "        entity.teleportTo(position.getX() + 0.5, position.getY(), position.getZ() + 0.5);",
                "    }",
                "}",
            ],
            "damageEntity" =>
            [
                "private static void damageEntity(Entity entity, int amount) {",
                "    if (entity != null && amount > 0) {",
                "        entity.hurt(entity.damageSources().generic(), amount);",
                "    }",
                "}",
            ],
            "entityHealth" =>
            [
                "private static int entityHealth(Entity entity) {",
                "    return entity instanceof LivingEntity living ? Math.round(living.getHealth()) : 0;",
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
