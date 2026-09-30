using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Java;

/// <summary>Noms des packages, classes et champs Java générés pour un mod.</summary>
public sealed class JavaModNaming
{
    public const string BasePackage = "com.mineengine.mods";

    /// <summary>Noms de champs déjà utilisés par les classes générées.</summary>
    private static readonly HashSet<string> ReservedConstants = new(StringComparer.Ordinal)
    {
        "BLOCKS", "ITEMS", "CREATIVE_MODE_TABS", "MAIN_TAB", "MOD_ID",
    };

    public JavaModNaming(IRModInfo mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        RootPackage = $"{BasePackage}.{JavaNames.ToPackageSegment(mod.ModId)}";
        RegistryPackage = RootPackage + ".registry";

        string typeName = JavaNames.ToTypeName(mod.Name, JavaNames.ToTypeName(mod.ModId.Value, "Generated"));
        // Le suffixe "Mod" évite aussi toute collision avec ModBlocks, ModItems et ModCreativeTabs.
        MainClass = typeName.EndsWith("Mod", StringComparison.Ordinal) ? typeName : typeName + "Mod";
    }

    public string RootPackage { get; }

    public string RegistryPackage { get; }

    public string MainClass { get; }

    public string BlocksClass => "ModBlocks";

    public string ItemsClass => "ModItems";

    public string CreativeTabsClass => "ModCreativeTabs";

    /// <summary>Nom du champ Java qui porte un élément ("magic_sword" devient "MAGIC_SWORD").</summary>
    public string ConstantFor(ResourceId id)
    {
        string constant = JavaNames.ToConstantName(id);
        return ReservedConstants.Contains(constant) ? constant + "_ENTRY" : constant;
    }

    public string SourcePath(string package, string className) =>
        $"src/main/java/{package.Replace('.', '/')}/{className}.java";
}
