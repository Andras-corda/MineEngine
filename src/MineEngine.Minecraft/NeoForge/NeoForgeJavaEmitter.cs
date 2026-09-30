using MineEngine.IR.Model;
using MineEngine.Minecraft.Java;

namespace MineEngine.Minecraft.NeoForge;

/// <summary>Code Java pour NeoForge 1.21 : registres spécialisés DeferredRegister.Blocks et .Items.</summary>
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

    protected override IReadOnlyList<string> BlocksImports { get; } =
    [
        "net.minecraft.world.level.block.Block",
        "net.minecraft.world.level.block.state.BlockBehaviour",
        "net.neoforged.neoforge.registries.DeferredBlock",
        "net.neoforged.neoforge.registries.DeferredRegister",
    ];

    protected override IReadOnlyList<string> ItemsImports { get; } =
    [
        "net.minecraft.world.item.BlockItem",
        "net.minecraft.world.item.Item",
        "net.neoforged.neoforge.registries.DeferredItem",
        "net.neoforged.neoforge.registries.DeferredRegister",
    ];

    protected override IReadOnlyList<string> CreativeTabsImports { get; } =
    [
        "net.neoforged.neoforge.registries.DeferredHolder",
        "net.neoforged.neoforge.registries.DeferredRegister",
    ];

    protected override string CreativeTabHolderType => "DeferredHolder<CreativeModeTab, CreativeModeTab>";

    protected override string BlocksRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister.Blocks BLOCKS = DeferredRegister.createBlocks({naming.MainClass}.MOD_ID);";

    protected override string ItemsRegisterDeclaration(JavaModNaming naming) =>
        $"public static final DeferredRegister.Items ITEMS = DeferredRegister.createItems({naming.MainClass}.MOD_ID);";

    protected override void WriteBlockField(JavaSourceWriter writer, IRBlock block, JavaModNaming naming) =>
        writer.Line($"public static final DeferredBlock<Block> {naming.ConstantFor(block.Id)} = BLOCKS.registerSimpleBlock(")
            .Indent().Indent()
            .Line($"{JavaNames.StringLiteral(block.Id.Value)}, {BlockProperties(block)});")
            .Unindent().Unindent();

    protected override void WriteItemField(JavaSourceWriter writer, IRItem item, JavaModNaming naming) =>
        writer.Line($"public static final DeferredItem<Item> {naming.ConstantFor(item.Id)} = ITEMS.registerSimpleItem(")
            .Indent().Indent()
            .Line($"{JavaNames.StringLiteral(item.Id.Value)}, new Item.Properties().stacksTo({item.MaxStackSize}));")
            .Unindent().Unindent();

    protected override void WriteBlockItemField(JavaSourceWriter writer, IRBlock block, JavaModNaming naming)
    {
        string constant = naming.ConstantFor(block.Id);
        writer.Line($"public static final DeferredItem<BlockItem> {constant} = ITEMS.registerSimpleBlockItem(")
            .Indent().Indent()
            .Line($"{JavaNames.StringLiteral(block.Id.Value)}, {naming.BlocksClass}.{constant});")
            .Unindent().Unindent();
    }
}
