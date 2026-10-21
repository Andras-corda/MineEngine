using MineEngine.Assets;
using MineEngine.Assets.Models;
using MineEngine.Core.Assets;
using MineEngine.IR.Model;
using MineEngine.Project;

namespace MineEngine.IR.Lowering;

/// <summary>Blockbench and Minecraft Java JSON models go into the mod with their textures; the other formats stay in the project.</summary>
public sealed class ModelLowering : AssetLowering<ModelAsset>
{
    public override AssetType AssetType => AssetType.Model;

    protected override void Lower(ModelAsset asset, LoweringContext context)
    {
        if (!asset.IsUsedByTheGame)
        {
            return;
        }

        string? file = AssetFileImporter.GetExistingFile(context.Project, asset);
        if (file is null)
        {
            context.Diagnostics.Error(Tr.F("The model file ({0}) was not found.", asset.FilePath), asset.ToString(), asset.Id);
            return;
        }

        BlockbenchModel? blockbench = null;
        var textures = new List<IRModelTexture>();
        if (asset.Format == ModelFormat.Blockbench)
        {
            try
            {
                blockbench = BlockbenchModel.Load(file);
            }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidDataException)
            {
                context.Diagnostics.Error(Tr.F("The Blockbench model cannot be read: {0}", exception.Message), asset.ToString(), asset.Id);
                return;
            }

            if (blockbench.SkippedElements.Count > 0)
            {
                context.Diagnostics.Warning(
                    Tr.F("Only cubes are used; left out: {0}.", string.Join(", ", blockbench.SkippedElements)), asset.ToString(), asset.Id);
            }

            // A texture of the model takes the texture bound to it, or else the image embedded in the file.
            foreach (BlockbenchTexture texture in blockbench.Textures)
            {
                string key = texture.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Guid? bound = asset.Slots.FirstOrDefault(s => s.Key == key)?.TextureId;
                IRTexture resolved = bound is not null ? context.ResolveTexture(asset, bound)
                    : texture.Png is { } png ? IRTexture.FromPng(png)
                    : context.ResolveTexture(asset, null);
                textures.Add(new IRModelTexture(key, resolved));
            }
        }
        else
        {
            textures.AddRange(asset.Slots.Where(s => s.TextureId is not null).Select(s => new IRModelTexture(s.Key, context.ResolveTexture(asset, s.TextureId))));
        }

        if (blockbench is not null)
        {
            ReportConversionProblems(asset, blockbench, context);
        }

        context.AddModel(new IRModel(asset.Id, asset.ResourceId, file)
        {
            Format = asset.Format,
            Blockbench = blockbench,
            Textures = textures,
            Variants = [.. asset.Variants.Where(v => v.TextureId is not null).Select(v => context.ResolveTexture(asset, v.TextureId))],
            Layers = [.. asset.Layers.Where(l => l.TextureId is not null).Select(l => new IRModelLayer(context.ResolveTexture(asset, l.TextureId), l.Emissive))],
        });
    }

    /// <summary>What is lost when the model becomes a Java block or item model, or a Java entity model, for the elements that use it.</summary>
    private static void ReportConversionProblems(ModelAsset asset, BlockbenchModel blockbench, LoweringContext context)
    {
        List<TexturedAsset> users = [.. context.Project.Assets.OfType<TexturedAsset>().Where(a => a.UsesCustomModel && a.CustomModelId == asset.Id)];
        if (users.Any(u => u is not MobAsset))
        {
            var problems = new List<string>();
            blockbench.ToJavaModel(_ => string.Empty, problems);
            foreach (string problem in problems.Distinct())
            {
                context.Diagnostics.Warning(problem, asset.ToString(), asset.Id);
            }
        }

        if (users.Any(u => u is MobAsset) && !blockbench.UsesBoxUv)
        {
            context.Diagnostics.Warning(
                Tr.T("A mob model is mapped with box UV (Minecraft entity models): cubes mapped per face take the box UV of their UV offset."),
                asset.ToString(),
                asset.Id);
        }
    }
}
