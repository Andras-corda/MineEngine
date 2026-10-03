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

    protected override string EntityRenderersEventType => "net.neoforged.neoforge.client.event.EntityRenderersEvent";

    protected override IReadOnlyList<string> ClientEnvironmentImports { get; } =
    [
        "net.neoforged.api.distmarker.Dist",
        "net.neoforged.fml.loading.FMLEnvironment",
    ];

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
