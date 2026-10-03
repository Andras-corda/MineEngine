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
