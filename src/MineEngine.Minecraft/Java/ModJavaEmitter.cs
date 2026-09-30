using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Génère le code Java d'enregistrement d'un mod : classe principale, blocs,
/// items et onglet créatif. La structure est commune à tous les loaders ; les
/// sous-classes ne fournissent que ce qui diffère (imports, API d'enregistrement).
/// </summary>
public abstract class ModJavaEmitter : IFileEmitter
{
    protected const string GeneratedNotice = " * Généré par Mine Engine. Ne pas modifier : ce fichier est réécrit à chaque build.";

    public abstract string Name { get; }

    /// <summary>Imports de la classe principale.</summary>
    protected abstract IReadOnlyList<string> MainClassImports { get; }

    /// <summary>Paramètres du constructeur de la classe principale.</summary>
    protected abstract string MainConstructorParameters { get; }

    /// <summary>Instructions placées en tête du constructeur ; elles doivent définir la variable "modEventBus".</summary>
    protected abstract IReadOnlyList<string> MainConstructorPrologue { get; }

    protected abstract IReadOnlyList<string> BlocksImports { get; }

    protected abstract IReadOnlyList<string> ItemsImports { get; }

    protected abstract IReadOnlyList<string> CreativeTabsImports { get; }

    /// <summary>Type Java du champ qui porte l'onglet créatif.</summary>
    protected abstract string CreativeTabHolderType { get; }

    public void Emit(GenerationContext context)
    {
        var naming = new JavaModNaming(context.Mod);
        IRContent content = context.Ir.Content;

        Write(context, naming, naming.RootPackage, naming.MainClass, WriteMainClass(context.Mod, naming));
        Write(context, naming, naming.RegistryPackage, naming.BlocksClass, WriteBlocksClass(content, naming));
        Write(context, naming, naming.RegistryPackage, naming.ItemsClass, WriteItemsClass(content, naming));
        Write(context, naming, naming.RegistryPackage, naming.CreativeTabsClass, WriteCreativeTabsClass(context, naming));
    }

    /// <summary>Déclaration du registre des blocs (champ BLOCKS).</summary>
    protected abstract string BlocksRegisterDeclaration(JavaModNaming naming);

    /// <summary>Déclaration du registre des items (champ ITEMS).</summary>
    protected abstract string ItemsRegisterDeclaration(JavaModNaming naming);

    protected abstract void WriteBlockField(JavaSourceWriter writer, IRBlock block, JavaModNaming naming);

    protected abstract void WriteItemField(JavaSourceWriter writer, IRItem item, JavaModNaming naming);

    protected abstract void WriteBlockItemField(JavaSourceWriter writer, IRBlock block, JavaModNaming naming);

    protected static string BlockProperties(IRBlock block) =>
        $"BlockBehaviour.Properties.of().strength({JavaNames.FloatLiteral(block.Hardness)}, {JavaNames.FloatLiteral(block.Resistance)})";

    private static void Write(GenerationContext context, JavaModNaming naming, string package, string className, JavaSourceWriter source) =>
        context.Files.WriteText(naming.SourcePath(package, className), source.ToString());

    private static JavaSourceWriter WriteHeader(string package, IEnumerable<string> imports, string description)
    {
        var w = new JavaSourceWriter();
        w.Line($"package {package};").Line();
        foreach (string import in imports.Distinct().Order(StringComparer.Ordinal))
        {
            w.Line($"import {import};");
        }

        return w.Line()
            .Line("/**")
            .Line($" * {description}")
            .Line(GeneratedNotice)
            .Line(" */");
    }

    private JavaSourceWriter WriteMainClass(IRModInfo mod, JavaModNaming naming)
    {
        IEnumerable<string> imports = MainClassImports.Concat(
        [
            $"{naming.RegistryPackage}.{naming.BlocksClass}",
            $"{naming.RegistryPackage}.{naming.CreativeTabsClass}",
            $"{naming.RegistryPackage}.{naming.ItemsClass}",
        ]);

        JavaSourceWriter w = WriteHeader(naming.RootPackage, imports, $"Classe principale du mod {mod.Name}.")
            .Line($"@Mod({naming.MainClass}.MOD_ID)")
            .OpenBlock($"public final class {naming.MainClass}")
            .Line($"public static final String MOD_ID = {JavaNames.StringLiteral(mod.ModId.Value)};")
            .Line()
            .OpenBlock($"public {naming.MainClass}({MainConstructorParameters})");

        foreach (string line in MainConstructorPrologue)
        {
            w.Line(line);
        }

        return w.Line($"{naming.BlocksClass}.BLOCKS.register(modEventBus);")
            .Line($"{naming.ItemsClass}.ITEMS.register(modEventBus);")
            .Line($"{naming.CreativeTabsClass}.CREATIVE_MODE_TABS.register(modEventBus);")
            .CloseBlock()
            .CloseBlock();
    }

    private JavaSourceWriter WriteBlocksClass(IRContent content, JavaModNaming naming)
    {
        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, BlocksImports.Append($"{naming.RootPackage}.{naming.MainClass}"), "Blocs du mod.")
            .OpenBlock($"public final class {naming.BlocksClass}")
            .Line(BlocksRegisterDeclaration(naming));

        foreach (IRBlock block in content.Blocks)
        {
            w.Line();
            WriteBlockField(w, block, naming);
        }

        return WritePrivateConstructor(w, naming.BlocksClass);
    }

    private JavaSourceWriter WriteItemsClass(IRContent content, JavaModNaming naming)
    {
        JavaSourceWriter w = WriteHeader(
                naming.RegistryPackage,
                ItemsImports.Append($"{naming.RootPackage}.{naming.MainClass}"),
                "Items du mod, y compris les items qui représentent les blocs.")
            .OpenBlock($"public final class {naming.ItemsClass}")
            .Line(ItemsRegisterDeclaration(naming));

        foreach (IRItem item in content.Items)
        {
            w.Line();
            WriteItemField(w, item, naming);
        }

        foreach (IRBlock block in content.Blocks)
        {
            w.Line();
            WriteBlockItemField(w, block, naming);
        }

        return WritePrivateConstructor(w, naming.ItemsClass);
    }

    private JavaSourceWriter WriteCreativeTabsClass(GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        IEnumerable<string> imports = CreativeTabsImports.Concat(
        [
            $"{naming.RootPackage}.{naming.MainClass}",
            "net.minecraft.core.registries.Registries",
            "net.minecraft.network.chat.Component",
            "net.minecraft.world.item.CreativeModeTab",
        ]);

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Onglet de l'inventaire créatif qui regroupe tout le contenu du mod.")
            .OpenBlock($"public final class {naming.CreativeTabsClass}")
            .Line("public static final DeferredRegister<CreativeModeTab> CREATIVE_MODE_TABS =")
            .Indent().Indent()
            .Line($"DeferredRegister.create(Registries.CREATIVE_MODE_TAB, {naming.MainClass}.MOD_ID);")
            .Unindent().Unindent();

        List<string> entries =
        [
            .. content.Items.Select(i => $"{naming.ItemsClass}.{naming.ConstantFor(i.Id)}"),
            .. content.Blocks.Select(b => $"{naming.ItemsClass}.{naming.ConstantFor(b.Id)}"),
        ];

        if (entries.Count > 0)
        {
            w.Line()
                .Line($"public static final {CreativeTabHolderType} MAIN_TAB = CREATIVE_MODE_TABS.register(")
                .Indent().Indent()
                .Line("\"main\", () -> CreativeModeTab.builder()")
                .Indent().Indent()
                .Line($".title(Component.translatable({JavaNames.StringLiteral(context.Paths.CreativeTabTranslationKey)}))")
                .Line($".icon(() -> {entries[0]}.get().getDefaultInstance())")
                .OpenBlock(".displayItems((parameters, output) ->");

            foreach (string entry in entries)
            {
                w.Line($"output.accept({entry}.get());");
            }

            w.CloseBlock(")")
                .Line(".build());")
                .Unindent().Unindent().Unindent().Unindent();
        }

        return WritePrivateConstructor(w, naming.CreativeTabsClass);
    }

    private static JavaSourceWriter WritePrivateConstructor(JavaSourceWriter w, string className) =>
        w.Line()
            .OpenBlock($"private {className}()")
            .CloseBlock()
            .CloseBlock();
}
