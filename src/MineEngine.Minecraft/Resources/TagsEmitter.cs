using System.Text.Json.Nodes;
using MineEngine.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Tags of the mod: those of the Tags tab (V0.7.1), and the vanilla tags completed by the mod:
/// effective tool (mineable/pickaxe...), tool level (needs_iron_tool...), ladders, soils of plants,
/// small flowers, biomes of the overworld, item tags chosen in the editor and, since 1.20.5, the
/// meat (minecraft:meat). A tag that comes from several sources is written once with every entry.
/// </summary>
public sealed class TagsEmitter : IFileEmitter
{
    public string Name => Tr.T("Tags");

    public void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        IRContent content = context.Ir.Content;
        bool singular = context.MinecraftVersion.UsesSingularDataFolders;
        var tags = new SortedDictionary<string, (List<string> Values, bool Replace)>(StringComparer.Ordinal);

        void Add(TagRegistry registry, string tagNamespace, string tagPath, IEnumerable<string> values, bool replace = false)
        {
            string path = $"{ResourcePaths.ResourcesRoot}/data/{tagNamespace}/tags/{TagAsset.Folder(registry, singular)}/{tagPath}.json";
            if (!tags.TryGetValue(path, out var tag))
            {
                tags[path] = tag = ([], false);
            }

            foreach (string value in values.Where(v => !tag.Values.Contains(v)))
            {
                tag.Values.Add(value);
            }

            if (replace)
            {
                tags[path] = (tag.Values, true);
            }
        }

        IEnumerable<string> Ids(IEnumerable<IRElement> elements) => elements.OrderBy(e => e.Id).Select(e => paths.Reference(e.Id.Value));

        foreach (IGrouping<HarvestTool, IRBlock> group in content.Blocks.Where(b => b.Settings.HarvestTool != HarvestTool.None).GroupBy(b => b.Settings.HarvestTool))
        {
            Add(TagRegistry.Blocks, "minecraft", "mineable/" + group.Key.ToString().ToLowerInvariant(), Ids(group));
        }

        foreach (IGrouping<ToolTier, IRBlock> group in content.Blocks
                     .Where(b => b.Settings.HarvestTool != HarvestTool.None && b.Settings.ToolTier != ToolTier.Any)
                     .GroupBy(b => b.Settings.ToolTier))
        {
            Add(TagRegistry.Blocks, "minecraft", $"needs_{group.Key.ToString().ToLowerInvariant()}_tool", Ids(group));
        }

        // Ladders (entities climb them), soils of the plants, small flowers (bees, bone meal, flower pots).
        Add(TagRegistry.Blocks, "minecraft", "climbable", Ids(content.Blocks.Where(b => b.Options.Ladder)));
        Add(TagRegistry.Blocks, "minecraft", "dirt", Ids(content.Blocks.Where(b => b.Options.PlantsCanSustain)));
        List<IRBlock> flowers = [.. content.Blocks.Where(b => b.Plant is { Kind: PlantKind.Flower })];
        Add(TagRegistry.Blocks, "minecraft", "small_flowers", Ids(flowers));
        Add(TagRegistry.Items, "minecraft", "small_flowers", Ids(flowers.Where(f => f.ItemForm is not null)));
        Add(TagRegistry.Blocks, "minecraft", "crops", Ids(content.Blocks.Where(b => b.Plant is { Kind: PlantKind.Crop })));

        // The biomes of the overworld get its ores, structures and mobs that use the tag.
        Add(TagRegistry.Biomes, "minecraft", "is_overworld",
            content.Biomes.Where(b => b.GeneratesInOverworld).OrderBy(b => b.Id).Select(b => paths.Reference(b.Id.Value)));

        foreach (IRItem item in content.Items)
        {
            foreach (NamespacedId tag in item.Tags)
            {
                Add(TagRegistry.Items, tag.Namespace, tag.Path, [paths.Reference(item.Id.Value)]);
            }
        }

        if (JavaGameApi.For(context.MinecraftVersion).MeatUsesItemTag)
        {
            Add(TagRegistry.Items, "minecraft", "meat", Ids(content.Items.Where(i => i.Food is { IsMeat: true })));
        }

        foreach (IRTag tag in content.Tags)
        {
            Add(tag.Registry, tag.Namespace, tag.Path, tag.Values, tag.Replace);
        }

        foreach ((string path, (List<string> values, bool replace)) in tags.Where(t => t.Value.Values.Count > 0 || t.Value.Replace))
        {
            var json = new JsonArray();
            foreach (string value in values)
            {
                json.Add(value.EndsWith('?')
                    ? new JsonObject { ["id"] = value[..^1], ["required"] = false }
                    : JsonValue.Create(value));
            }

            context.Files.WriteText(path, JsonFormatting.ToText(new JsonObject { ["replace"] = replace, ["values"] = json }));
        }
    }
}
