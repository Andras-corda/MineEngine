using MineEngine.Assets.Graphs;
using MineEngine.Minecraft.Java;

namespace MineEngine.Minecraft.Forge;

/// <summary>Code Java pour Forge (1.20.1 et 1.21.1) : DeferredRegister + RegistryObject.</summary>
public sealed class ForgeJavaEmitter : ModJavaEmitter
{
    public override string Name => "Code Java Forge";

    protected override IReadOnlyList<string> MainClassImports { get; } =
    [
        "net.minecraftforge.eventbus.api.IEventBus",
        "net.minecraftforge.fml.common.Mod",
        "net.minecraftforge.fml.javafmlmod.FMLJavaModLoadingContext",
    ];

    protected override string MainConstructorParameters => "FMLJavaModLoadingContext context";

    protected override IReadOnlyList<string> MainConstructorPrologue { get; } =
    [
        "IEventBus modEventBus = context.getModEventBus();",
    ];

    protected override IReadOnlyList<string> BlocksRegistryImports { get; } =
    [
        "net.minecraftforge.registries.DeferredRegister",
        "net.minecraftforge.registries.ForgeRegistries",
        "net.minecraftforge.registries.RegistryObject",
    ];

    protected override IReadOnlyList<string> ItemsRegistryImports { get; } =
    [
        "net.minecraftforge.registries.DeferredRegister",
        "net.minecraftforge.registries.ForgeRegistries",
        "net.minecraftforge.registries.RegistryObject",
    ];

    protected override IReadOnlyList<string> CreativeTabsRegistryImports { get; } =
    [
        "net.minecraftforge.registries.DeferredRegister",
        "net.minecraftforge.registries.RegistryObject",
    ];

    protected override string BlockHolderType => "RegistryObject<Block>";

    protected override string ItemHolderType => "RegistryObject<Item>";

    protected override string CreativeTabHolderType => "RegistryObject<CreativeModeTab>";

    protected override string BuildCreativeTabEventType => "net.minecraftforge.event.BuildCreativeModeTabContentsEvent";

    protected override IReadOnlyList<string> EntitiesRegistryImports { get; } =
    [
        "net.minecraftforge.registries.DeferredRegister",
        "net.minecraftforge.registries.ForgeRegistries",
        "net.minecraftforge.registries.RegistryObject",
    ];

    protected override IReadOnlyList<string> SoundsRegistryImports { get; } =
    [
        "net.minecraftforge.registries.DeferredRegister",
        "net.minecraftforge.registries.ForgeRegistries",
        "net.minecraftforge.registries.RegistryObject",
    ];

    protected override string SoundHolderType => "RegistryObject<SoundEvent>";

    protected override string SpawnEggItemType => "net.minecraftforge.common.ForgeSpawnEggItem";

    protected override string EntityAttributeEventType => "net.minecraftforge.event.entity.EntityAttributeCreationEvent";

    protected override string SpawnPlacementEventType => "net.minecraftforge.event.entity.SpawnPlacementRegisterEvent";

    protected override string EntityRenderersEventType => "net.minecraftforge.client.event.EntityRenderersEvent";

    protected override IReadOnlyList<string> ClientEnvironmentImports { get; } =
    [
        "net.minecraftforge.api.distmarker.Dist",
        "net.minecraftforge.fml.loading.FMLEnvironment",
    ];

    protected override (string Expression, string Import) GameEventBus => ("MinecraftForge.EVENT_BUS", "net.minecraftforge.common.MinecraftForge");

    protected override (string Import, string TypeName) GameEventType(GraphNodeKind eventKind) => eventKind switch
    {
        GraphNodeKind.OnRightClickBlock => ("net.minecraftforge.event.entity.player.PlayerInteractEvent", "PlayerInteractEvent.RightClickBlock"),
        GraphNodeKind.OnUseItem => ("net.minecraftforge.event.entity.player.PlayerInteractEvent", "PlayerInteractEvent.RightClickItem"),
        GraphNodeKind.OnPlayerJoin => ("net.minecraftforge.event.entity.player.PlayerEvent", "PlayerEvent.PlayerLoggedInEvent"),
        GraphNodeKind.OnBlockBroken => ("net.minecraftforge.event.level.BlockEvent", "BlockEvent.BreakEvent"),
        GraphNodeKind.OnPlayerLeave => ("net.minecraftforge.event.entity.player.PlayerEvent", "PlayerEvent.PlayerLoggedOutEvent"),
        GraphNodeKind.OnPlayerTick => ("net.minecraftforge.event.TickEvent", "TickEvent.PlayerTickEvent"),
        GraphNodeKind.OnPlayerHurt => ("net.minecraftforge.event.entity.living.LivingHurtEvent", "LivingHurtEvent"),
        GraphNodeKind.OnEntitySpawn => ("net.minecraftforge.event.entity.EntityJoinLevelEvent", "EntityJoinLevelEvent"),
        GraphNodeKind.OnEntityDeath => ("net.minecraftforge.event.entity.living.LivingDeathEvent", "LivingDeathEvent"),
        _ => throw new NotSupportedException($"{eventKind} n'est pas un événement."),
    };

    /// <summary>The tick of a player is read in the field "player", once per tick (at its end).</summary>
    protected override IReadOnlyList<string>? EventPrologue(GraphNodeKind eventKind) => eventKind == GraphNodeKind.OnPlayerTick
        ?
        [
            "if (event.phase != TickEvent.Phase.END) {",
            "    return;",
            "}",
            "Player player = event.player;",
            "Level level = player.level();",
            "if (level.isClientSide()) {",
            "    return;",
            "}",
            "BlockPos position = player.blockPosition();",
        ]
        : null;

    /// <summary>Forge (1.20.1 et 1.21.1) : la durée de combustion est une méthode de l'item (IForgeItem).</summary>
    protected override IReadOnlyList<string>? BurnTimeOverride(int ticks) =>
    [
        "@Override",
        "public int getBurnTime(ItemStack stack, RecipeType<?> recipeType) {",
        $"    return {ticks};",
        "}",
    ];

    protected override IReadOnlyList<string> BurnTimeImports { get; } =
    [
        "net.minecraft.world.item.ItemStack",
        "net.minecraft.world.item.crafting.RecipeType",
    ];

    // ----- V0.6.2 / V0.6.3 -----

    protected override IReadOnlyList<string> RegistryImports { get; } =
    [
        "net.minecraftforge.registries.DeferredRegister",
        "net.minecraftforge.registries.RegistryObject",
    ];

    protected override string RegistryHolderType(string registryType, string valueType) => $"RegistryObject<{valueType}>";

    protected override string HolderOf(string field) => $"{field}.getHolder().get()";

    protected override string RegisterCommandsEventType => "net.minecraftforge.event.RegisterCommandsEvent";

    protected override string RegisterKeyMappingsEventType => "net.minecraftforge.client.event.RegisterKeyMappingsEvent";

    protected override string InputEventType => "net.minecraftforge.client.event.InputEvent";

    protected override string ClientSetupEventType => "net.minecraftforge.fml.event.lifecycle.FMLClientSetupEvent";

    protected override (string Expression, string Import) FluidTypesRegistry =>
        ("ForgeRegistries.Keys.FLUID_TYPES", "net.minecraftforge.registries.ForgeRegistries");

    protected override string FluidTypeClass => "net.minecraftforge.fluids.FluidType";

    protected override string FlowingFluidClass => "net.minecraftforge.fluids.ForgeFlowingFluid";

    protected override string ClientFluidExtensionsClass => "net.minecraftforge.client.extensions.common.IClientFluidTypeExtensions";

    protected override string? RegisterClientExtensionsEventType => null;

    protected override string EntityTypeHolderType(string entityClass) => $"RegistryObject<EntityType<{entityClass}>>";

    protected override string EntityTypesRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister<EntityType<?>> ENTITY_TYPES = DeferredRegister.create(ForgeRegistries.ENTITY_TYPES, {naming.MainClass}.MOD_ID);";

    protected override string SoundEventsRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister<SoundEvent> SOUND_EVENTS = DeferredRegister.create(ForgeRegistries.SOUND_EVENTS, {naming.MainClass}.MOD_ID);";

    protected override string BlocksRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister<Block> BLOCKS = DeferredRegister.create(ForgeRegistries.BLOCKS, {naming.MainClass}.MOD_ID);";

    protected override string ItemsRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister<Item> ITEMS = DeferredRegister.create(ForgeRegistries.ITEMS, {naming.MainClass}.MOD_ID);";
}
