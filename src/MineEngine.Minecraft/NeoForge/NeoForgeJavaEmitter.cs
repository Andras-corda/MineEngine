using MineEngine.Assets.Graphs;
using MineEngine.Minecraft.Java;

namespace MineEngine.Minecraft.NeoForge;

/// <summary>Code Java pour NeoForge : registres spécialisés DeferredRegister.Blocks et .Items.</summary>
public sealed class NeoForgeJavaEmitter : ModJavaEmitter
{
    public override string Name => "Code Java NeoForge";

    protected override IReadOnlyList<string> MainClassImports { get; } =
    [
        "net.neoforged.bus.api.IEventBus",
        "net.neoforged.fml.common.Mod",
    ];

    protected override string MainConstructorParameters => "IEventBus modEventBus";

    protected override IReadOnlyList<string> MainConstructorPrologue { get; } = [];

    protected override IReadOnlyList<string> BlocksRegistryImports { get; } =
    [
        "net.neoforged.neoforge.registries.DeferredBlock",
        "net.neoforged.neoforge.registries.DeferredRegister",
    ];

    protected override IReadOnlyList<string> ItemsRegistryImports { get; } =
    [
        "net.neoforged.neoforge.registries.DeferredItem",
        "net.neoforged.neoforge.registries.DeferredRegister",
    ];

    protected override IReadOnlyList<string> CreativeTabsRegistryImports { get; } =
    [
        "net.neoforged.neoforge.registries.DeferredHolder",
        "net.neoforged.neoforge.registries.DeferredRegister",
    ];

    protected override string BlockHolderType => "DeferredBlock<Block>";

    protected override string ItemHolderType => "DeferredItem<Item>";

    protected override string CreativeTabHolderType => "DeferredHolder<CreativeModeTab, CreativeModeTab>";

    protected override string BuildCreativeTabEventType => "net.neoforged.neoforge.event.BuildCreativeModeTabContentsEvent";

    protected override IReadOnlyList<string> EntitiesRegistryImports { get; } =
    [
        "net.minecraft.core.registries.Registries",
        "net.neoforged.neoforge.registries.DeferredHolder",
        "net.neoforged.neoforge.registries.DeferredRegister",
    ];

    protected override IReadOnlyList<string> SoundsRegistryImports { get; } =
    [
        "net.minecraft.core.registries.Registries",
        "net.neoforged.neoforge.registries.DeferredHolder",
        "net.neoforged.neoforge.registries.DeferredRegister",
    ];

    protected override string SoundHolderType => "DeferredHolder<SoundEvent, SoundEvent>";

    protected override string SpawnEggItemType => "net.neoforged.neoforge.common.DeferredSpawnEggItem";

    protected override string EntityAttributeEventType => "net.neoforged.neoforge.event.entity.EntityAttributeCreationEvent";

    protected override string SpawnPlacementEventType => "net.neoforged.neoforge.event.entity.RegisterSpawnPlacementsEvent";

    protected override string BlockEventType => "net.neoforged.neoforge.event.level.BlockEvent";

    protected override string EntityRenderersEventType => "net.neoforged.neoforge.client.event.EntityRenderersEvent";

    protected override IReadOnlyList<string> ClientEnvironmentImports { get; } =
    [
        "net.neoforged.api.distmarker.Dist",
        "net.neoforged.fml.loading.FMLEnvironment",
    ];

    protected override (string Expression, string Import) GameEventBus => ("NeoForge.EVENT_BUS", "net.neoforged.neoforge.common.NeoForge");

    protected override string GlobalPlayerEvent => "net.neoforged.neoforge.event.entity.player.PlayerEvent";

    protected override string ServerStartingEvent => "net.neoforged.neoforge.event.server.ServerStartingEvent";

    protected override string ServerStoppingEvent => "net.neoforged.neoforge.event.server.ServerStoppingEvent";

    protected override string ServerAboutToStartEvent => "net.neoforged.neoforge.event.server.ServerAboutToStartEvent";

    protected override string FmlPaths => "net.neoforged.fml.loading.FMLPaths";

    protected override (string Import, string TypeName) GameEventType(GraphNodeKind eventKind) => eventKind switch
    {
        GraphNodeKind.OnRightClickBlock => ("net.neoforged.neoforge.event.entity.player.PlayerInteractEvent", "PlayerInteractEvent.RightClickBlock"),
        GraphNodeKind.OnUseItem => ("net.neoforged.neoforge.event.entity.player.PlayerInteractEvent", "PlayerInteractEvent.RightClickItem"),
        GraphNodeKind.OnPlayerJoin => ("net.neoforged.neoforge.event.entity.player.PlayerEvent", "PlayerEvent.PlayerLoggedInEvent"),
        GraphNodeKind.OnBlockBroken => ("net.neoforged.neoforge.event.level.BlockEvent", "BlockEvent.BreakEvent"),
        GraphNodeKind.OnPlayerLeave => ("net.neoforged.neoforge.event.entity.player.PlayerEvent", "PlayerEvent.PlayerLoggedOutEvent"),
        GraphNodeKind.OnPlayerTick => ("net.neoforged.neoforge.event.tick.PlayerTickEvent", "PlayerTickEvent.Post"),
        GraphNodeKind.OnPlayerHurt => ("net.neoforged.neoforge.event.entity.living.LivingIncomingDamageEvent", "LivingIncomingDamageEvent"),
        GraphNodeKind.OnEntitySpawn => ("net.neoforged.neoforge.event.entity.EntityJoinLevelEvent", "EntityJoinLevelEvent"),
        GraphNodeKind.OnEntityDeath => ("net.neoforged.neoforge.event.entity.living.LivingDeathEvent", "LivingDeathEvent"),
        _ => throw new NotSupportedException($"{eventKind} n'est pas un événement."),
    };

    // ----- V0.6.2 / V0.6.3 -----

    protected override IReadOnlyList<string> RegistryImports { get; } =
    [
        "net.neoforged.neoforge.registries.DeferredHolder",
        "net.neoforged.neoforge.registries.DeferredRegister",
    ];

    protected override string RegistryHolderType(string registryType, string valueType) => $"DeferredHolder<{registryType}, {valueType}>";

    // A DeferredHolder is a Holder.
    protected override string HolderOf(string field) => field;

    protected override string RegisterCommandsEventType => "net.neoforged.neoforge.event.RegisterCommandsEvent";

    protected override string RegisterKeyMappingsEventType => "net.neoforged.neoforge.client.event.RegisterKeyMappingsEvent";

    protected override string InputEventType => "net.neoforged.neoforge.client.event.InputEvent";

    protected override string ClientSetupEventType => "net.neoforged.fml.event.lifecycle.FMLClientSetupEvent";

    protected override string CommonSetupEventType => "net.neoforged.fml.event.lifecycle.FMLCommonSetupEvent";

    protected override string FurnaceFuelEventType => "net.neoforged.neoforge.event.furnace.FurnaceFuelBurnTimeEvent";

    protected override string PlayerInteractEventType => "net.neoforged.neoforge.event.entity.player.PlayerInteractEvent";

    protected override string ServerLifecycleHooksType => "net.neoforged.neoforge.server.ServerLifecycleHooks";

    protected override string PlayerDestroyItemEventType => "net.neoforged.neoforge.event.entity.player.PlayerDestroyItemEvent";

    protected override (string Type, string Import, string Player, string Stack, string ItemEntity) ItemPickupEvent =>
        ("ItemEntityPickupEvent.Post", "net.neoforged.neoforge.event.entity.player.ItemEntityPickupEvent", "event.getPlayer()", "event.getOriginalStack()", "event.getItemEntity()");

    /// <summary>The compostables are a data map (see FuelDataMapEmitter).</summary>
    protected override bool CompostablesAreData => true;

    protected override (string Expression, string Import) FluidTypesRegistry =>
        ("NeoForgeRegistries.Keys.FLUID_TYPES", "net.neoforged.neoforge.registries.NeoForgeRegistries");

    protected override string FluidTypeClass => "net.neoforged.neoforge.fluids.FluidType";

    protected override string FlowingFluidClass => "net.neoforged.neoforge.fluids.BaseFlowingFluid";

    protected override string ClientFluidExtensionsClass => "net.neoforged.neoforge.client.extensions.common.IClientFluidTypeExtensions";

    protected override string? RegisterClientExtensionsEventType => "net.neoforged.neoforge.client.extensions.common.RegisterClientExtensionsEvent";

    // ----- V0.7.4: camera -----

    protected override string ClientEventPackage => "net.neoforged.neoforge.client.event";

    /// <summary>A payload registered on the mod bus and sent with PacketDistributor; its handler runs on the main thread.</summary>
    protected override (IReadOnlyList<string> Imports, IReadOnlyList<string> Members) CameraNetwork(JavaModNaming naming, JavaGameApi api)
    {
        string location = api.ParseResourceLocation($"{naming.MainClass}.MOD_ID + \":camera\"");
        IReadOnlyList<string> imports =
        [
            $"{naming.RootPackage}.{naming.MainClass}",
            "io.netty.buffer.ByteBuf",
            "net.minecraft.network.codec.ByteBufCodecs",
            "net.minecraft.network.codec.StreamCodec",
            "net.minecraft.network.protocol.common.custom.CustomPacketPayload",
            "net.minecraft.resources.ResourceLocation",
            "net.neoforged.bus.api.IEventBus",
            "net.neoforged.neoforge.network.PacketDistributor",
            "net.neoforged.neoforge.network.event.RegisterPayloadHandlersEvent",
        ];
        IReadOnlyList<string> members =
        [
            "/** Effect sent to the client: an action and its two values. */",
            "public record Message(int action, float a, float b) implements CustomPacketPayload {",
            $"    public static final CustomPacketPayload.Type<Message> TYPE = new CustomPacketPayload.Type<>({location});",
            "    public static final StreamCodec<ByteBuf, Message> STREAM_CODEC = StreamCodec.composite(",
            "            ByteBufCodecs.VAR_INT, Message::action, ByteBufCodecs.FLOAT, Message::a, ByteBufCodecs.FLOAT, Message::b, Message::new);",
            "",
            "    @Override",
            "    public CustomPacketPayload.Type<Message> type() {",
            "        return TYPE;",
            "    }",
            "}",
            "",
            "public static void register(IEventBus modEventBus) {",
            "    modEventBus.addListener(RegisterPayloadHandlersEvent.class, event -> event.registrar(\"1\").playToClient(Message.TYPE, Message.STREAM_CODEC,",
            $"            (message, context) -> {CameraClientClass}.apply(message.action(), message.a(), message.b())));",
            "}",
            "",
            "private static void sendTo(ServerPlayer player, int action, float a, float b) {",
            "    PacketDistributor.sendToPlayer(player, new Message(action, a, b));",
            "}",
        ];
        return (imports, members);
    }

    protected override string EntityTypeHolderType(string entityClass) => $"DeferredHolder<EntityType<?>, EntityType<{entityClass}>>";

    protected override string EntityTypesRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister<EntityType<?>> ENTITY_TYPES = DeferredRegister.create(Registries.ENTITY_TYPE, {naming.MainClass}.MOD_ID);";

    protected override string SoundEventsRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister<SoundEvent> SOUND_EVENTS = DeferredRegister.create(Registries.SOUND_EVENT, {naming.MainClass}.MOD_ID);";

    protected override string BlocksRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister.Blocks BLOCKS = DeferredRegister.createBlocks({naming.MainClass}.MOD_ID);";

    protected override string ItemsRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister.Items ITEMS = DeferredRegister.createItems({naming.MainClass}.MOD_ID);";
}
