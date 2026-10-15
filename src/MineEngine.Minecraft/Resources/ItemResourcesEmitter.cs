using System.Text.Json.Nodes;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Minecraft.Resources;

/// <summary>Modèle et texture de chaque item.</summary>
public sealed class ItemResourcesEmitter : ResourceEmitter
{
    public ItemResourcesEmitter(PlaceholderTexture placeholder)
        : base(placeholder)
    {
    }

    public override string Name => Tr.T("Ressources des items");

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        foreach (IRItem item in context.Ir.Content.Items)
        {
            WriteJson(context, paths.ItemModel(item.Id), new JsonObject
            {
                // "handheld" : tenu en diagonale comme un outil ; "generated" : à plat.
                ["parent"] = item.Handheld ? "minecraft:item/handheld" : "minecraft:item/generated",
                ["textures"] = new JsonObject { ["layer0"] = paths.Reference($"item/{item.Id}") },
            });

            WriteTexture(context, item.Texture, paths.ItemTexture(item.Id));
        }
    }
}
