using System.Text;
using System.Text.RegularExpressions;
using MineEngine.Assets;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Java of the Global and Tags tabs (V0.7.14): the lines of each global variable in MineEngineGlobals are
/// mapped to it, and the tags get a class MineEngineTags of TagKey constants that Java code can use
/// (stack.is(MineEngineTags.MOON_ORES)).
/// </summary>
public abstract partial class ModJavaEmitter
{
    /// <summary>Class of the tags, in the package "code" next to MineEngineGlobals.</summary>
    public const string TagsClass = "MineEngineTags";

    /// <summary>Registry and Java class of the values of a tag; null for the tags that Java cannot hold (functions...).</summary>
    public static (string Registry, string Type, string Import)? TagRegistryJava(TagRegistry registry) => registry switch
    {
        TagRegistry.Blocks => ("BLOCK", "Block", "net.minecraft.world.level.block.Block"),
        TagRegistry.Items => ("ITEM", "Item", "net.minecraft.world.item.Item"),
        TagRegistry.EntityTypes => ("ENTITY_TYPE", "EntityType<?>", "net.minecraft.world.entity.EntityType"),
        TagRegistry.Biomes => ("BIOME", "Biome", "net.minecraft.world.level.biome.Biome"),
        TagRegistry.Fluids => ("FLUID", "Fluid", "net.minecraft.world.level.material.Fluid"),
        TagRegistry.DamageTypes => ("DAMAGE_TYPE", "DamageType", "net.minecraft.world.damagesource.DamageType"),
        TagRegistry.Enchantments => ("ENCHANTMENT", "Enchantment", "net.minecraft.world.item.enchantment.Enchantment"),
        TagRegistry.Structures => ("STRUCTURE", "Structure", "net.minecraft.world.level.levelgen.structure.Structure"),
        TagRegistry.GameEvents => ("GAME_EVENT", "GameEvent", "net.minecraft.world.level.gameevent.GameEvent"),
        _ => null,
    };

    /// <summary>Name of the constant of a tag: "MOON_ORES", or "MINECRAFT_LOGS" for a tag of another namespace.</summary>
    public static string TagConstant(IRTag tag, string modId)
    {
        string text = tag.Namespace == modId ? tag.Path : tag.Namespace + "_" + tag.Path;
        var builder = new StringBuilder();
        foreach (char c in text.ToUpperInvariant())
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        }

        string name = Regex.Replace(builder.ToString(), "_+", "_").Trim('_');
        return name.Length == 0 || char.IsDigit(name[0]) ? "TAG_" + name : name;
    }

    private static void WriteTagsClass(GenerationContext context, JavaModNaming naming, JavaProgramNames names, JavaGameApi api)
    {
        string modId = context.Mod.ModId.Value;
        List<(IRTag Tag, (string Registry, string Type, string Import) Java)> tags =
            [.. context.Ir.Content.Tags.Where(t => TagRegistryJava(t.Registry) is not null).Select(t => (t, TagRegistryJava(t.Registry)!.Value))];
        if (tags.Count == 0)
        {
            return;
        }

        string file = naming.SourcePath(names.Package, TagsClass);
        JavaSourceWriter w = WriteHeader(names.Package,
            [
                "net.minecraft.core.registries.Registries", "net.minecraft.resources.ResourceLocation", "net.minecraft.tags.TagKey",
                .. tags.Select(t => t.Java.Import),
            ],
            "Tags du mod (onglet Tags), utilisables dans le code Java : stack.is(MineEngineTags.NOM).")
            .OpenBlock($"public final class {TagsClass}");

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach ((IRTag tag, (string registry, string type, _)) in tags)
        {
            string constant = TagConstant(tag, modId);
            string unique = constant;
            for (int i = 2; !used.Add(unique); i++)
            {
                unique = constant + "_" + i;
            }

            int line = w.NextLineNumber;
            string location = api.ParseResourceLocation(JavaNames.StringLiteral($"{tag.Namespace}:{tag.Path}"));
            w.Line($"public static final TagKey<{type}> {unique} = TagKey.create(Registries.{registry}, {location});");
            context.SourceMap.Add(file, line, line, tag.SourceAssetId, $"Tag '{tag.Namespace}:{tag.Path}'");
        }

        w.Line()
            .Line($"private {TagsClass}() {{")
            .Line("}")
            .CloseBlock();
        context.Files.WriteText(file, w.ToString());
    }

    /// <summary>Maps each global variable to the first line of MineEngineGlobals that names it (its field or its accessor).</summary>
    private static void MapGlobals(GenerationContext context, string file, string source, JavaProgramNames names)
    {
        string[] lines = source.Split('\n');
        foreach ((IRGlobalVariable global, string name) in names.Globals)
        {
            var word = new Regex($@"\b(get|set)?{Regex.Escape(JavaProgramNames.Accessor(name))}\b|\b{Regex.Escape(name)}\b");
            int index = Array.FindIndex(lines, l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal) && word.IsMatch(l));
            if (index >= 0)
            {
                context.SourceMap.Add(file, index + 1, index + 1, global.Id, $"Variable globale '{global.Name}'");
            }
        }
    }
}
