using MineEngine.Core.GameData;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Génère le code Java d'enregistrement d'un mod : classe principale, blocs, items,
/// onglets créatifs, sons et mobs (voir ModJavaEmitter.Mobs.cs). La structure est commune à tous les loaders ; les sous-classes
/// ne fournissent que ce qui diffère (types des registres, imports, événements). Les
/// différences entre versions de Minecraft sont déléguées à <see cref="JavaGameApi"/>.
/// </summary>
public abstract partial class ModJavaEmitter : IFileEmitter
{
    protected const string GeneratedNotice = " * Généré par Mine Engine. Ne pas modifier : ce fichier est réécrit à chaque build.";

    /// <summary>Au-delà de ce nombre d'appels, la chaîne de propriétés est écrite sur plusieurs lignes.</summary>
    private const int InlineChainLimit = 2;

    public abstract string Name { get; }

    /// <summary>Imports de la classe principale.</summary>
    protected abstract IReadOnlyList<string> MainClassImports { get; }

    /// <summary>Paramètres du constructeur de la classe principale.</summary>
    protected abstract string MainConstructorParameters { get; }

    /// <summary>Instructions placées en tête du constructeur ; elles doivent définir la variable "modEventBus".</summary>
    protected abstract IReadOnlyList<string> MainConstructorPrologue { get; }

    /// <summary>Imports du registre des blocs (DeferredRegister, type du champ...).</summary>
    protected abstract IReadOnlyList<string> BlocksRegistryImports { get; }

    /// <summary>Imports du registre des items.</summary>
    protected abstract IReadOnlyList<string> ItemsRegistryImports { get; }

    protected abstract IReadOnlyList<string> CreativeTabsRegistryImports { get; }

    /// <summary>Type Java des champs qui portent un bloc.</summary>
    protected abstract string BlockHolderType { get; }

    /// <summary>Type Java des champs qui portent un item.</summary>
    protected abstract string ItemHolderType { get; }

    /// <summary>Type Java du champ qui porte l'onglet créatif.</summary>
    protected abstract string CreativeTabHolderType { get; }

    /// <summary>Nom complet de l'événement de remplissage des onglets créatifs.</summary>
    protected abstract string BuildCreativeTabEventType { get; }

    public void Emit(GenerationContext context)
    {
        var naming = new JavaModNaming(context.Mod);
        JavaGameApi api = JavaGameApi.For(context.MinecraftVersion);

        Write(context, naming, naming.RootPackage, naming.MainClass, WriteMainClass(context, naming));
        Write(context, naming, naming.RegistryPackage, naming.BlocksClass, WriteBlocksClass(context, naming, api));
        Write(context, naming, naming.RegistryPackage, naming.ItemsClass, WriteItemsClass(context, naming, api));
        Write(context, naming, naming.RegistryPackage, naming.CreativeTabsClass, WriteCreativeTabsClass(context, naming));

        IRContent content = context.Ir.Content;
        if (content.Sounds.Count > 0)
        {
            Write(context, naming, naming.RegistryPackage, naming.SoundsClass, WriteSoundsClass(context, naming, api));
        }

        if (content.Mobs.Count > 0)
        {
            Write(context, naming, naming.RegistryPackage, naming.EntitiesClass, WriteEntitiesClass(context, naming, api));
            Write(context, naming, naming.ClientPackage, naming.EntityRenderersClass, WriteRenderersClass(context, naming, api));
            foreach (IRMob mob in content.Mobs)
            {
                WriteEntityClass(context, naming, api, mob);
            }
        }
    }

    /// <summary>Déclaration du registre des blocs (champ BLOCKS).</summary>
    protected abstract string BlocksRegisterDeclaration(JavaModNaming naming);

    /// <summary>Déclaration du registre des items (champ ITEMS).</summary>
    protected abstract string ItemsRegisterDeclaration(JavaModNaming naming);

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

    // ----- Classe principale -----

    private JavaSourceWriter WriteMainClass(GenerationContext context, JavaModNaming naming)
    {
        IRModInfo mod = context.Mod;
        IRContent content = context.Ir.Content;
        bool hasMobs = content.Mobs.Count > 0;
        bool hasSounds = content.Sounds.Count > 0;

        var imports = new List<string>(MainClassImports)
        {
            $"{naming.RegistryPackage}.{naming.BlocksClass}",
            $"{naming.RegistryPackage}.{naming.CreativeTabsClass}",
            $"{naming.RegistryPackage}.{naming.ItemsClass}",
        };
        if (hasSounds)
        {
            imports.Add($"{naming.RegistryPackage}.{naming.SoundsClass}");
        }

        if (hasMobs)
        {
            imports.Add($"{naming.RegistryPackage}.{naming.EntitiesClass}");
            imports.Add($"{naming.ClientPackage}.{naming.EntityRenderersClass}");
            imports.AddRange(ClientEnvironmentImports);
        }

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

        if (hasSounds)
        {
            w.Line($"{naming.SoundsClass}.SOUND_EVENTS.register(modEventBus);");
        }

        w.Line($"{naming.BlocksClass}.BLOCKS.register(modEventBus);");
        if (hasMobs)
        {
            w.Line($"{naming.EntitiesClass}.ENTITY_TYPES.register(modEventBus);");
        }

        w.Line($"{naming.ItemsClass}.ITEMS.register(modEventBus);")
            .Line($"{naming.CreativeTabsClass}.CREATIVE_MODE_TABS.register(modEventBus);")
            .Line($"modEventBus.addListener({naming.CreativeTabsClass}::addToVanillaTabs);");

        if (hasMobs)
        {
            w.Line($"modEventBus.addListener({naming.EntitiesClass}::registerAttributes);");
            if (content.Mobs.Any(m => m.Spawning is not null))
            {
                w.Line($"modEventBus.addListener({naming.EntitiesClass}::registerSpawnPlacements);");
            }

            // Le rendu n'existe que sur le client : un serveur dédié ne doit pas charger ces classes.
            w.OpenBlock($"if ({ClientEnvironmentCheck})")
                .Line($"modEventBus.addListener({naming.EntityRenderersClass}::registerRenderers);")
                .CloseBlock();
        }

        return w.CloseBlock().CloseBlock();
    }

    // ----- Blocs -----

    private JavaSourceWriter WriteBlocksClass(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        IRContent content = context.Ir.Content;
        string file = naming.SourcePath(naming.RegistryPackage, naming.BlocksClass);

        var imports = new SortedSet<string>(BlocksRegistryImports, StringComparer.Ordinal)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            "net.minecraft.world.level.block.Block",
            "net.minecraft.world.level.block.state.BlockBehaviour",
        };
        if (content.Blocks.Any(b => b.Settings.Sound != BlockSoundType.Stone))
        {
            imports.Add("net.minecraft.world.level.block.SoundType");
        }

        if (content.Blocks.Any(b => b.Settings.Model == BlockModelKind.Column))
        {
            imports.Add("net.minecraft.world.level.block.RotatedPillarBlock");
        }

        if (content.Blocks.Any(b => b.Settings.DropsExperience))
        {
            imports.Add("net.minecraft.world.level.block.DropExperienceBlock");
            imports.Add("net.minecraft.util.valueproviders.UniformInt");
        }

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Blocs du mod.")
            .OpenBlock($"public final class {naming.BlocksClass}")
            .Line(BlocksRegisterDeclaration(naming));

        foreach (IRBlock block in content.Blocks)
        {
            w.Line();
            int firstLine = w.NextLineNumber;
            (string head, string tail) = BlockConstruction(block, api);
            WriteRegistration(
                w, BlockHolderType, naming.ConstantFor(block.Id), "BLOCKS", block.Id.Value,
                head, "BlockBehaviour.Properties.of()", BlockPropertyCalls(block.Settings), tail, overrides: []);
            context.SourceMap.Add(file, firstLine, w.NextLineNumber - 1, block.SourceAssetId, $"Bloc '{block.Id}'");
        }

        return WritePrivateConstructor(w, naming.BlocksClass);
    }

    private static (string Head, string Tail) BlockConstruction(IRBlock block, JavaGameApi api) => block.Settings switch
    {
        { Model: BlockModelKind.Column } => ("new RotatedPillarBlock(", ")"),
        { DropsExperience: true } settings => api.ExperienceBlockConstruction(settings.ExperienceMin, settings.ExperienceMax),
        _ => ("new Block(", ")"),
    };

    private static List<string> BlockPropertyCalls(IRBlockSettings settings)
    {
        var calls = new List<string>
        {
            settings.Unbreakable
                ? ".strength(-1.0f, 3600000.0f)"
                : $".strength({JavaNames.FloatLiteral(settings.Hardness)}, {JavaNames.FloatLiteral(settings.Resistance)})",
        };

        if (settings.Sound != BlockSoundType.Stone)
        {
            calls.Add($".sound({GameDataJava.Sound(settings.Sound)})");
        }

        if (settings.LightLevel > 0)
        {
            calls.Add($".lightLevel(state -> {settings.LightLevel})");
        }

        if (settings.RequiresCorrectTool)
        {
            calls.Add(".requiresCorrectToolForDrops()");
        }

        if (settings.RenderType != BlockRenderType.Solid || settings.Model == BlockModelKind.Cross)
        {
            calls.Add(".noOcclusion()");
        }

        if (settings.Model == BlockModelKind.Cross)
        {
            calls.Add(".noCollission()");
        }

        if (Math.Abs(settings.Friction - 0.6f) > 0.0001f)
        {
            calls.Add($".friction({JavaNames.FloatLiteral(settings.Friction)})");
        }

        if (Math.Abs(settings.SpeedFactor - 1f) > 0.0001f)
        {
            calls.Add($".speedFactor({JavaNames.FloatLiteral(settings.SpeedFactor)})");
        }

        if (Math.Abs(settings.JumpFactor - 1f) > 0.0001f)
        {
            calls.Add($".jumpFactor({JavaNames.FloatLiteral(settings.JumpFactor)})");
        }

        return calls;
    }

    // ----- Items -----

    private JavaSourceWriter WriteItemsClass(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        IRContent content = context.Ir.Content;
        string file = naming.SourcePath(naming.RegistryPackage, naming.ItemsClass);

        var imports = new SortedSet<string>(ItemsRegistryImports, StringComparer.Ordinal)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            "net.minecraft.world.item.Item",
        };

        List<IRItemForm> forms = [.. content.ElementsWithItemForm.Select(e => e.ItemForm!)];
        if (forms.Exists(f => f.Rarity != ItemRarity.Common))
        {
            imports.Add("net.minecraft.world.item.Rarity");
        }

        if (content.Blocks.Any(b => b.ItemForm is not null))
        {
            imports.Add("net.minecraft.world.item.BlockItem");
        }

        if (content.Items.Any(i => i.Food is not null))
        {
            imports.Add("net.minecraft.world.food.FoodProperties");
        }

        if (content.Items.Any(i => i.HasGlint))
        {
            imports.Add("net.minecraft.world.item.ItemStack");
        }

        if (forms.Exists(f => f.TooltipLines.Count > 0))
        {
            imports.UnionWith(api.TooltipImports);
        }

        if (content.MobsWithSpawnEgg.Any())
        {
            imports.Add(SpawnEggItemType);
        }

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Items du mod, y compris les items qui représentent les blocs.")
            .OpenBlock($"public final class {naming.ItemsClass}")
            .Line(ItemsRegisterDeclaration(naming));

        foreach (IRItem item in content.Items)
        {
            w.Line();
            int firstLine = w.NextLineNumber;
            WriteRegistration(
                w, ItemHolderType, naming.ConstantFor(item.Id), "ITEMS", item.Id.Value,
                "new Item(", "new Item.Properties()", ItemPropertyCalls(item.Form, item.Durability, item.Food, api), ")",
                ItemOverrides(item, item.Form, item.HasGlint, context, api));
            context.SourceMap.Add(file, firstLine, w.NextLineNumber - 1, item.SourceAssetId, $"Item '{item.Id}'");
        }

        foreach (IRBlock block in content.Blocks.Where(b => b.ItemForm is not null))
        {
            string constant = naming.ConstantFor(block.Id);
            w.Line();
            int firstLine = w.NextLineNumber;
            WriteRegistration(
                w, ItemHolderType, constant, "ITEMS", block.Id.Value,
                $"new BlockItem({naming.BlocksClass}.{constant}.get(), ", "new Item.Properties()",
                ItemPropertyCalls(block.Form!, durability: 0, food: null, api), ")",
                ItemOverrides(block, block.Form!, hasGlint: false, context, api));
            context.SourceMap.Add(file, firstLine, w.NextLineNumber - 1, block.SourceAssetId, $"Bloc '{block.Id}'");
        }

        string spawnEggClass = SimpleName(SpawnEggItemType);
        foreach (IRMob mob in content.MobsWithSpawnEgg)
        {
            IRSpawnEgg egg = mob.SpawnEgg!;
            w.Line();
            int firstLine = w.NextLineNumber;
            WriteRegistration(
                w, ItemHolderType, naming.ConstantFor(egg.ItemId), "ITEMS", egg.ItemId,
                $"new {spawnEggClass}({naming.EntitiesClass}.{naming.ConstantFor(mob.Id)}, {ColorLiteral(egg.PrimaryColor)}, {ColorLiteral(egg.SecondaryColor)}, ",
                "new Item.Properties()", [], ")", overrides: []);
            context.SourceMap.Add(file, firstLine, w.NextLineNumber - 1, mob.SourceAssetId, $"Œuf du mob '{mob.Id}'");
        }

        return WritePrivateConstructor(w, naming.ItemsClass);
    }

    private static List<string> ItemPropertyCalls(IRItemForm form, int durability, IRFood? food, JavaGameApi api)
    {
        var calls = new List<string>();

        // Minecraft refuse une taille de pile différente de 1 sur un item à durabilité.
        if (durability > 0)
        {
            calls.Add($".durability({durability})");
        }
        else if (form.MaxStackSize != 64)
        {
            calls.Add($".stacksTo({form.MaxStackSize})");
        }

        if (form.Rarity != ItemRarity.Common)
        {
            calls.Add($".rarity({GameDataJava.Rarity(form.Rarity)})");
        }

        if (form.FireResistant)
        {
            calls.Add(".fireResistant()");
        }

        if (food is not null)
        {
            calls.Add($".food({api.FoodProperties(food)})");
        }

        return calls;
    }

    private static List<string> ItemOverrides(IRElement element, IRItemForm form, bool hasGlint, GenerationContext context, JavaGameApi api)
    {
        var lines = new List<string>();
        if (hasGlint)
        {
            lines.Add("@Override");
            lines.Add("public boolean isFoil(ItemStack stack) {");
            lines.Add("    return true;");
            lines.Add("}");
        }

        if (form.TooltipLines.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add("@Override");
            lines.Add(api.TooltipMethodSignature + " {");
            lines.Add("    " + api.TooltipSuperCall);
            for (int i = 0; i < form.TooltipLines.Count; i++)
            {
                string key = context.Paths.TooltipTranslationKey(element.Id, i);
                lines.Add($"    tooltip.add(Component.translatable({JavaNames.StringLiteral(key)}));");
            }

            lines.Add("}");
        }

        return lines;
    }

    // ----- Onglets créatifs -----

    private JavaSourceWriter WriteCreativeTabsClass(GenerationContext context, JavaModNaming naming)
    {
        IRContent content = context.Ir.Content;
        List<IRElement> elements = [.. content.ElementsWithItemForm];

        IEnumerable<string> imports = CreativeTabsRegistryImports.Concat(
        [
            $"{naming.RootPackage}.{naming.MainClass}",
            BuildCreativeTabEventType,
            "net.minecraft.core.registries.Registries",
            "net.minecraft.network.chat.Component",
            "net.minecraft.world.item.CreativeModeTab",
            "net.minecraft.world.item.CreativeModeTabs",
        ]);

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Onglets de l'inventaire créatif : celui du mod et les onglets vanilla.")
            .OpenBlock($"public final class {naming.CreativeTabsClass}")
            .Line("public static final DeferredRegister<CreativeModeTab> CREATIVE_MODE_TABS =")
            .Indent().Indent()
            .Line($"DeferredRegister.create(Registries.CREATIVE_MODE_TAB, {naming.MainClass}.MOD_ID);")
            .Unindent().Unindent();

        List<string> spawnEggs = content.MobsWithSpawnEgg
            .Select(m => $"{naming.ItemsClass}.{naming.ConstantFor(m.SpawnEgg!.ItemId)}")
            .ToList();
        List<string> modTabEntries = elements
            .Where(e => e.ItemForm!.InModCreativeTab)
            .Select(e => $"{naming.ItemsClass}.{naming.ConstantFor(e.Id)}")
            .Concat(spawnEggs)
            .ToList();

        if (modTabEntries.Count > 0)
        {
            w.Line()
                .Line($"public static final {CreativeTabHolderType} MAIN_TAB = CREATIVE_MODE_TABS.register(")
                .Indent().Indent()
                .Line("\"main\", () -> CreativeModeTab.builder()")
                .Indent().Indent()
                .Line($".title(Component.translatable({JavaNames.StringLiteral(context.Paths.CreativeTabTranslationKey)}))")
                .Line($".icon(() -> {modTabEntries[0]}.get().getDefaultInstance())")
                .OpenBlock(".displayItems((parameters, output) ->");

            foreach (string entry in modTabEntries)
            {
                w.Line($"output.accept({entry}.get());");
            }

            w.CloseBlock(")")
                .Line(".build());")
                .Unindent().Unindent().Unindent().Unindent();
        }

        // Ajout aux onglets vanilla choisis dans l'éditeur.
        string eventClass = SimpleName(BuildCreativeTabEventType);
        w.Line().OpenBlock($"public static void addToVanillaTabs({eventClass} event)");
        foreach (CreativeTab tab in Enum.GetValues<CreativeTab>())
        {
            List<string> inTab = elements
                .Where(e => e.ItemForm!.CreativeTabs.Contains(tab))
                .Select(e => $"{naming.ItemsClass}.{naming.ConstantFor(e.Id)}")
                .Concat(tab == CreativeTab.SpawnEggs ? spawnEggs : [])
                .ToList();
            if (inTab.Count == 0)
            {
                continue;
            }

            w.OpenBlock($"if (event.getTabKey() == {GameDataJava.CreativeTab(tab)})");
            foreach (string entry in inTab)
            {
                w.Line($"event.accept({entry}.get());");
            }

            w.CloseBlock();
        }

        w.CloseBlock();
        return WritePrivateConstructor(w, naming.CreativeTabsClass);
    }

    // ----- Écriture commune -----

    /// <summary>
    /// Écrit l'enregistrement d'un élément :
    /// <c>public static final TYPE NOM = REGISTRE.register("id", () -> new X(propriétés...));</c>,
    /// avec une sous-classe anonyme si <paramref name="overrides"/> n'est pas vide.
    /// </summary>
    private static void WriteRegistration(
        JavaSourceWriter w,
        string holderType,
        string constant,
        string register,
        string id,
        string constructionHead,
        string propertiesHead,
        IReadOnlyList<string> propertyCalls,
        string constructionTail,
        IReadOnlyList<string> overrides)
    {
        string ending = overrides.Count > 0 ? " {" : ");";

        w.Line($"public static final {holderType} {constant} = {register}.register(")
            .Indent().Indent();

        string start = $"{JavaNames.StringLiteral(id)}, () -> {constructionHead}{propertiesHead}";
        if (propertyCalls.Count <= InlineChainLimit)
        {
            w.Line(start + string.Concat(propertyCalls) + constructionTail + ending);
        }
        else
        {
            w.Line(start).Indent().Indent();
            for (int i = 0; i < propertyCalls.Count; i++)
            {
                bool last = i == propertyCalls.Count - 1;
                w.Line(last ? propertyCalls[i] + constructionTail + ending : propertyCalls[i]);
            }

            w.Unindent().Unindent();
        }

        if (overrides.Count > 0)
        {
            w.Indent();
            foreach (string line in overrides)
            {
                w.Line(line);
            }

            w.Unindent().Line("});");
        }

        w.Unindent().Unindent();
    }

    private static string SimpleName(string qualifiedName) => qualifiedName[(qualifiedName.LastIndexOf('.') + 1)..];

    private static string ColorLiteral(int rgb) => $"0x{rgb & 0xFFFFFF:X6}";

    private static JavaSourceWriter WritePrivateConstructor(JavaSourceWriter w, string className) =>
        w.Line()
            .OpenBlock($"private {className}()")
            .CloseBlock()
            .CloseBlock();
}
