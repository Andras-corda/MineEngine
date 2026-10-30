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

    public override string Name => Tr.T("Item resources");

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        foreach (IRItem item in context.Ir.Content.Items)
        {
            JsonObject model = ItemModel(item, item.CustomModel, item.Id, paths);
            if (item.CustomModel is null)
            {
                WriteTexture(context, item.Texture, paths.ItemTexture(item.Id));
            }

            WriteAppearances(context, item, model);
            WriteJson(context, paths.ItemModel(item.Id), GuiTexture(context, item, model));
        }
    }

    /// <summary>Model of an item: its 3D model (see ModelsEmitter), or its texture flat or held like a tool.</summary>
    private static JsonObject ItemModel(IRItem item, MineEngine.Core.Identifiers.ResourceId? customModel, MineEngine.Core.Identifiers.ResourceId textureId, ResourcePaths paths) =>
        customModel is { } model
            ? new JsonObject { ["parent"] = paths.CustomModelReference(model) }
            : new JsonObject
            {
                // "handheld" : tenu en diagonale comme un outil ; "generated" : à plat.
                ["parent"] = item.Handheld ? "minecraft:item/handheld" : "minecraft:item/generated",
                ["textures"] = new JsonObject { ["layer0"] = paths.Reference($"item/{textureId}") },
            };

    /// <summary>
    /// Appearance N is a model of its own, chosen when the custom model data of the stack is N
    /// (the appearance nodes set it): the base model lists them in its "overrides".
    /// </summary>
    private void WriteAppearances(GenerationContext context, IRItem item, JsonObject baseModel)
    {
        if (item.Appearances.Count == 0)
        {
            return;
        }

        ResourcePaths paths = context.Paths;
        var overrides = new JsonArray();
        for (int number = 1; number <= item.Appearances.Count; number++)
        {
            IRAppearance appearance = item.Appearances[number - 1];
            MineEngine.Core.Identifiers.ResourceId fileId = IRAppearance.FileId(item.Id, number);
            JsonObject model = appearance switch
            {
                { Model: { } custom } => ItemModel(item, custom, fileId, paths),
                { Texture: { } texture } => WriteThen(ItemModel(item, null, fileId, paths), () => WriteTexture(context, texture, paths.ItemTexture(fileId))),
                _ => (JsonObject)baseModel.DeepClone(),
            };
            WriteJson(context, paths.ItemModel(fileId), model);

            // The last override that matches wins: numbers go up, so N only shows appearance N.
            overrides.Add(new JsonObject
            {
                ["predicate"] = new JsonObject { ["custom_model_data"] = number },
                ["model"] = paths.Reference($"item/{fileId}"),
            });
        }

        baseModel["overrides"] = overrides;
    }

    /// <summary>
    /// A 3D item with a texture for the inventories (V0.7.12): the model of the loader that changes
    /// with the place the item is shown, flat in the inventories and the 3D model elsewhere.
    /// </summary>
    private JsonObject GuiTexture(GenerationContext context, IRItem item, JsonObject model)
    {
        if (item.CustomModel is null || item.GuiTexture is not { } texture)
        {
            return model;
        }

        var guiId = MineEngine.Core.Identifiers.ResourceId.Parse(item.Id.Value + "_gui");
        WriteTexture(context, texture, context.Paths.ItemTexture(guiId));
        string loader = context.Ir.Mod.LoaderId.Equals("neoforge", StringComparison.OrdinalIgnoreCase) ? "neoforge" : "forge";
        var separate = new JsonObject
        {
            ["parent"] = model["parent"]!.DeepClone(),
            ["loader"] = $"{loader}:separate_transforms",
            ["base"] = new JsonObject { ["parent"] = model["parent"]!.DeepClone() },
            ["perspectives"] = new JsonObject
            {
                ["gui"] = new JsonObject
                {
                    ["parent"] = "minecraft:item/generated",
                    ["textures"] = new JsonObject { ["layer0"] = context.Paths.Reference($"item/{guiId}") },
                },
            },
        };
        if (model["overrides"] is { } overrides)
        {
            separate["overrides"] = overrides.DeepClone();
        }

        return separate;
    }

    private static JsonObject WriteThen(JsonObject model, Action write)
    {
        write();
        return model;
    }
}
