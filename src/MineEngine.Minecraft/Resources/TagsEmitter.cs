using System.Text.Json.Nodes;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Tags vanilla complétés par le mod : outil efficace (mineable/pickaxe...), niveau
/// d'outil requis (needs_iron_tool...), tags d'items choisis dans l'éditeur et, depuis 1.20.5,
/// la viande (minecraft:meat).
/// </summary>
public sealed class TagsEmitter : IFileEmitter
{
    public string Name => "Tags";

    public void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        IRContent content = context.Ir.Content;

        foreach (IGrouping<HarvestTool, IRBlock> group in content.Blocks
                     .Where(b => b.Settings.HarvestTool != HarvestTool.None)
                     .GroupBy(b => b.Settings.HarvestTool))
        {
            WriteTag(context, paths.VanillaBlockTag("mineable/" + group.Key.ToString().ToLowerInvariant()), group);
        }

        foreach (IGrouping<ToolTier, IRBlock> group in content.Blocks
                     .Where(b => b.Settings.HarvestTool != HarvestTool.None && b.Settings.ToolTier != ToolTier.Any)
                     .GroupBy(b => b.Settings.ToolTier))
        {
            WriteTag(context, paths.VanillaBlockTag($"needs_{group.Key.ToString().ToLowerInvariant()}_tool"), group);
        }

        // Tags d'items : ceux choisis dans l'éditeur, plus minecraft:meat depuis 1.20.5. Un même
        // tag peut venir de plusieurs sources : il est écrit une seule fois avec tous ses items.
        var itemTags = new Dictionary<string, List<IRElement>>(StringComparer.Ordinal);
        void AddToTag(string path, IRElement element)
        {
            if (!itemTags.TryGetValue(path, out List<IRElement>? elements))
            {
                itemTags[path] = elements = [];
            }

            if (!elements.Contains(element))
            {
                elements.Add(element);
            }
        }

        foreach (IRItem item in content.Items)
        {
            foreach (NamespacedId tag in item.Tags)
            {
                AddToTag(paths.ItemTag(tag), item);
            }
        }

        if (JavaGameApi.For(context.MinecraftVersion).MeatUsesItemTag)
        {
            foreach (IRItem meat in content.Items.Where(i => i.Food is { IsMeat: true }))
            {
                AddToTag(paths.VanillaItemTag("meat"), meat);
            }
        }

        foreach ((string path, List<IRElement> elements) in itemTags)
        {
            WriteTag(context, path, elements);
        }
    }

    private static void WriteTag(GenerationContext context, string path, IEnumerable<IRElement> elements)
    {
        var values = new JsonArray();
        foreach (IRElement element in elements.OrderBy(e => e.Id))
        {
            values.Add(context.Paths.Reference(element.Id.Value));
        }

        var tag = new JsonObject { ["replace"] = false, ["values"] = values };
        context.Files.WriteText(path, JsonFormatting.ToText(tag));
    }
}
