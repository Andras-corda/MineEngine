using System.Text.Json.Nodes;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// 3D models of the project: models/custom/&lt;id&gt;.json (a Java JSON model with its bound textures,
/// or a Blockbench project converted to a Java model) and their textures. The textures of the
/// model go into textures/block/model/ (the block atlas, used by blocks and items); the variants
/// and layers of mobs into textures/entity/model/.
/// </summary>
public sealed class ModelsEmitter : ResourceEmitter
{
    public ModelsEmitter(PlaceholderTexture placeholder)
        : base(placeholder)
    {
    }

    public override string Name => Tr.T("3D models");

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        foreach (IRModel model in context.Ir.Content.Models)
        {
            foreach (IRModelTexture texture in model.Textures)
            {
                WriteTexture(context, texture.Texture, paths.ModelTexture(model.Id, texture.Key));
            }

            for (int i = 0; i < model.Variants.Count; i++)
            {
                WriteTexture(context, model.Variants[i], paths.EntityModelTexture(model.Id, $"variant_{i + 1}"));
            }

            for (int i = 0; i < model.Layers.Count; i++)
            {
                WriteTexture(context, model.Layers[i].Texture, paths.EntityModelTexture(model.Id, $"layer_{i + 1}"));
            }

            // A Java JSON model without bound textures is copied as it is.
            if (model.Blockbench is null && model.Textures.Count == 0)
            {
                context.Files.CopyFile(model.SourceFile, paths.CustomModel(model.Id));
            }
            else
            {
                WriteJson(context, paths.CustomModel(model.Id), CreateJavaModel(model, paths));
            }
        }
    }

    /// <summary>The Java model of a Blockbench project, or the Java JSON model with its bound textures.</summary>
    private static JsonObject CreateJavaModel(IRModel model, ResourcePaths paths)
    {
        if (model.Blockbench is { } blockbench)
        {
            return blockbench.ToJavaModel(t => paths.ModelTextureReference(model.Id, t.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        JsonObject json = JsonNode.Parse(File.ReadAllText(model.SourceFile)) as JsonObject ?? [];
        if (model.Textures.Count > 0)
        {
            JsonObject textures = json["textures"] as JsonObject ?? [];
            foreach (IRModelTexture texture in model.Textures)
            {
                textures[texture.Key] = paths.ModelTextureReference(model.Id, texture.Key);
            }

            json["textures"] = textures;
        }

        return json;
    }
}
