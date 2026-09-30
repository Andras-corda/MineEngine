using System.Text.Json.Nodes;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Blockstate, modèles (bloc et item), texture et loot table de chaque bloc.
/// Chaque bloc est un cube identique sur ses six faces qui se récupère lui-même.
/// </summary>
public sealed class BlockResourcesEmitter : ResourceEmitter
{
    public BlockResourcesEmitter(PlaceholderTexture placeholder)
        : base(placeholder)
    {
    }

    public override string Name => "Ressources des blocs";

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        foreach (IRBlock block in context.Ir.Content.Blocks)
        {
            string blockModel = paths.Reference($"block/{block.Id}");

            WriteJson(context, paths.BlockState(block.Id), new JsonObject
            {
                ["variants"] = new JsonObject { [""] = new JsonObject { ["model"] = blockModel } },
            });

            WriteJson(context, paths.BlockModel(block.Id), new JsonObject
            {
                ["parent"] = "minecraft:block/cube_all",
                ["textures"] = new JsonObject { ["all"] = blockModel },
            });

            WriteJson(context, paths.ItemModel(block.Id), new JsonObject { ["parent"] = blockModel });

            WriteJson(context, paths.BlockLootTable(block.Id), CreateSelfDropLootTable(paths.Reference(block.Id.Value)));

            WriteTexture(context, block.Texture, paths.BlockTexture(block.Id));
        }
    }

    private static JsonObject CreateSelfDropLootTable(string itemReference) => new()
    {
        ["type"] = "minecraft:block",
        ["pools"] = new JsonArray
        {
            new JsonObject
            {
                ["rolls"] = 1,
                ["entries"] = new JsonArray
                {
                    new JsonObject { ["type"] = "minecraft:item", ["name"] = itemReference },
                },
                ["conditions"] = new JsonArray
                {
                    new JsonObject { ["condition"] = "minecraft:survives_explosion" },
                },
            },
        },
    };
}
