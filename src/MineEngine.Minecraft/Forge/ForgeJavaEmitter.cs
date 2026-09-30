using MineEngine.IR.Model;
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

    protected override IReadOnlyList<string> BlocksImports { get; } =
    [
        "net.minecraft.world.level.block.Block",
        "net.minecraft.world.level.block.state.BlockBehaviour",
        "net.minecraftforge.registries.DeferredRegister",
        "net.minecraftforge.registries.ForgeRegistries",
        "net.minecraftforge.registries.RegistryObject",
    ];

    protected override IReadOnlyList<string> ItemsImports { get; } =
    [
        "net.minecraft.world.item.BlockItem",
        "net.minecraft.world.item.Item",
        "net.minecraftforge.registries.DeferredRegister",
        "net.minecraftforge.registries.ForgeRegistries",
        "net.minecraftforge.registries.RegistryObject",
    ];

    protected override IReadOnlyList<string> CreativeTabsImports { get; } =
    [
        "net.minecraftforge.registries.DeferredRegister",
        "net.minecraftforge.registries.RegistryObject",
    ];

    protected override string CreativeTabHolderType => "RegistryObject<CreativeModeTab>";

    protected override string BlocksRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister<Block> BLOCKS = DeferredRegister.create(ForgeRegistries.BLOCKS, {naming.MainClass}.MOD_ID);";

    protected override string ItemsRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister<Item> ITEMS = DeferredRegister.create(ForgeRegistries.ITEMS, {naming.MainClass}.MOD_ID);";

    protected override void WriteBlockField(JavaSourceWriter writer, IRBlock block, JavaModNaming naming) =>
        writer.Line($"public static final RegistryObject<Block> {naming.ConstantFor(block.Id)} = BLOCKS.register(")
            .Indent().Indent()
            .Line($"{JavaNames.StringLiteral(block.Id.Value)}, () -> new Block({BlockProperties(block)}));")
            .Unindent().Unindent();

    protected override void WriteItemField(JavaSourceWriter writer, IRItem item, JavaModNaming naming) =>
        writer.Line($"public static final RegistryObject<Item> {naming.ConstantFor(item.Id)} = ITEMS.register(")
            .Indent().Indent()
            .Line($"{JavaNames.StringLiteral(item.Id.Value)}, () -> new Item(new Item.Properties().stacksTo({item.MaxStackSize})));")
            .Unindent().Unindent();

    protected override void WriteBlockItemField(JavaSourceWriter writer, IRBlock block, JavaModNaming naming)
    {
        string constant = naming.ConstantFor(block.Id);
        writer.Line($"public static final RegistryObject<Item> {constant} = ITEMS.register(")
            .Indent().Indent()
            .Line($"{JavaNames.StringLiteral(block.Id.Value)}, () -> new BlockItem({naming.BlocksClass}.{constant}.get(), new Item.Properties()));")
            .Unindent().Unindent();
    }
}
