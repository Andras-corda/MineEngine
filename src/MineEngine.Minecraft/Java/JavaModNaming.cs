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
        "BLOCKS", "ITEMS", "CREATIVE_MODE_TABS", "MAIN_TAB", "MOD_ID", "ENTITY_TYPES", "SOUND_EVENTS",
    };

    public JavaModNaming(IRModInfo mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        RootPackage = $"{BasePackage}.{JavaNames.ToPackageSegment(mod.ModId)}";
        RegistryPackage = RootPackage + ".registry";
        EntityPackage = RootPackage + ".entity";
        ClientPackage = RootPackage + ".client";
        BehaviorPackage = RootPackage + ".behavior";
        AiPackage = RootPackage + ".ai";

        string typeName = JavaNames.ToTypeName(mod.Name, JavaNames.ToTypeName(mod.ModId.Value, "Generated"));
        // Le suffixe "Mod" évite aussi toute collision avec ModBlocks, ModItems et ModCreativeTabs.
        MainClass = typeName.EndsWith("Mod", StringComparison.Ordinal) ? typeName : typeName + "Mod";
    }

    public string RootPackage { get; }

    public string RegistryPackage { get; }

    /// <summary>Package des classes de mobs.</summary>
    public string EntityPackage { get; }

    /// <summary>Package du code chargé seulement par le client (rendu).</summary>
    public string ClientPackage { get; }

    /// <summary>Package des classes générées à partir des graphes nodaux.</summary>
    public string BehaviorPackage { get; }

    /// <summary>Package des arbres de comportement Puppeteer et de leur moteur.</summary>
    public string AiPackage { get; }

    public string MainClass { get; }

    public string BlocksClass => "ModBlocks";

    public string ItemsClass => "ModItems";

    public string CreativeTabsClass => "ModCreativeTabs";

    public string EntitiesClass => "ModEntities";

    public string SoundsClass => "ModSounds";

    public string EntityRenderersClass => "ModEntityRenderers";

    /// <summary>Package of the Java entity models made from Blockbench models.</summary>
    public string EntityModelPackage => ClientPackage + ".model";

    /// <summary>Java entity model of a 3D model ("cat" becomes "CatModel").</summary>
    public string EntityModelClassFor(ResourceId modelId) => JavaNames.ToTypeName(modelId.Value, "Custom") + "Model";

    /// <summary>Renderer of a mob shown with a 3D model ("goblin" becomes "GoblinRenderer").</summary>
    public string RendererClassFor(ResourceId mobId) => JavaNames.ToTypeName(mobId.Value, "Mob") + "Renderer";

    /// <summary>Classe Java d'un mob ("goblin" devient "GoblinEntity").</summary>
    public string EntityClassFor(ResourceId id) => JavaNames.ToTypeName(id.Value, "Mob") + "Entity";

    /// <summary>Classe Java d'un graphe ("magic_wand" devient "MagicWandBehavior").</summary>
    public string BehaviorClassFor(ResourceId id) => BehaviorClassName(id);

    /// <summary>Même règle que <see cref="BehaviorClassFor"/>, utilisable sans mod (affichage dans l'éditeur).</summary>
    public static string BehaviorClassName(ResourceId id) => JavaNames.ToTypeName(id.Value, "Graph") + "Behavior";

    /// <summary>Classe Java d'un arbre Puppeteer ("goblin_brain" devient "GoblinBrainTree").</summary>
    public string BehaviorTreeClassFor(ResourceId id) => BehaviorTreeClassName(id);

    /// <summary>Même règle que <see cref="BehaviorTreeClassFor"/>, utilisable sans mod (affichage dans l'éditeur).</summary>
    public static string BehaviorTreeClassName(ResourceId id)
    {
        string typeName = JavaNames.ToTypeName(id.Value, "Ai");
        // "Puppeteer" est le moteur ; "goblin_tree" donne "GoblinTree" et non "GoblinTreeTree".
        return typeName.EndsWith("Tree", StringComparison.Ordinal) && typeName != "Tree" ? typeName : typeName + "Tree";
    }

    /// <summary>Nom du champ Java qui porte un élément ("magic_sword" devient "MAGIC_SWORD").</summary>
    public string ConstantFor(ResourceId id) => ConstantFor(id.Value);

    /// <summary>Nom du champ Java d'un identifiant qui n'est pas celui d'un asset (œuf d'apparition).</summary>
    public string ConstantFor(string id)
    {
        string constant = id.ToUpperInvariant();
        return ReservedConstants.Contains(constant) ? constant + "_ENTRY" : constant;
    }

    public string SourcePath(string package, string className) =>
        $"src/main/java/{package.Replace('.', '/')}/{className}.java";
}
