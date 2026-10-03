using System.Text.Json.Nodes;
using MineEngine.Core.GameData;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Tags vanilla complétés par le mod : outil efficace (mineable/pickaxe...), niveau
/// d'outil requis (needs_iron_tool...) et, depuis 1.20.5, la viande (minecraft:meat).
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

        if (JavaGameApi.For(context.MinecraftVersion).MeatUsesItemTag)
        {
            List<IRItem> meat = content.Items.Where(i => i.Food is { IsMeat: true }).ToList();
            if (meat.Count > 0)
            {
                WriteTag(context, paths.VanillaItemTag("meat"), meat);
            }
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
