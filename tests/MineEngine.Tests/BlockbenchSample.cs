using System.Text.Json.Nodes;
using MineEngine.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Minecraft.Resources.Imaging;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// A Blockbench model of a cat (modded entity format): body, head with ears, four legs and a
/// tail turned by a group, a whisker turned by its own cube rotation, and an embedded texture.
/// The sample binds it to a mob (two coat variants, a collar layer and glowing eyes), an item
/// and a block, so that the end-to-end builds compile all of it.
/// </summary>
internal static class BlockbenchSample
{
    public static string CreateFile(string directory, string name = "cat")
    {
        string png = Convert.ToBase64String(new PlaceholderTexture(new PngEncoder()).GetPngBytes());
        static JsonObject Cube(string uuid, string name, double[] from, double[] to, int[] uv, double[]? rotation = null, double[]? origin = null) => new()
        {
            ["name"] = name,
            ["type"] = "cube",
            ["uuid"] = uuid,
            ["box_uv"] = true,
            ["from"] = new JsonArray([.. from.Select(v => (JsonNode)v)]),
            ["to"] = new JsonArray([.. to.Select(v => (JsonNode)v)]),
            ["uv_offset"] = new JsonArray([.. uv.Select(v => (JsonNode)v)]),
            ["origin"] = new JsonArray([.. (origin ?? [0, 0, 0]).Select(v => (JsonNode)v)]),
            ["rotation"] = new JsonArray([.. (rotation ?? [0, 0, 0]).Select(v => (JsonNode)v)]),
        };
        static JsonObject Group(string name, double[] origin, JsonArray children, double[]? rotation = null) => new()
        {
            ["name"] = name,
            ["origin"] = new JsonArray([.. origin.Select(v => (JsonNode)v)]),
            ["rotation"] = new JsonArray([.. (rotation ?? [0, 0, 0]).Select(v => (JsonNode)v)]),
            ["children"] = children,
        };

        var model = new JsonObject
        {
            ["meta"] = new JsonObject { ["format_version"] = "4.10", ["model_format"] = "modded_entity", ["box_uv"] = true },
            ["name"] = name,
            ["resolution"] = new JsonObject { ["width"] = 64, ["height"] = 32 },
            ["elements"] = new JsonArray(
                Cube("c-body", "body", [-2, 6, -8], [2, 10, 8], [20, 0]),
                Cube("c-head", "head", [-2.5, 8, -12], [2.5, 12, -7], [0, 0]),
                Cube("c-ear", "ear", [-2, 12, -10], [-1, 13, -8], [0, 10]),
                Cube("c-whisker", "whisker", [2.5, 9, -12], [4.5, 10, -12], [0, 14], [0, 22.5, 0], [2.5, 9, -12]),
                Cube("c-leg0", "leg0", [1, 0, -6], [3, 6, -4], [40, 0]),
                Cube("c-leg1", "leg1", [-3, 0, -6], [-1, 6, -4], [40, 0]),
                Cube("c-leg2", "leg2", [1, 0, 4], [3, 6, 6], [8, 13]),
                Cube("c-leg3", "leg3", [-3, 0, 4], [-1, 6, 6], [8, 13]),
                Cube("c-tail", "tail", [-0.5, 9, 8], [0.5, 10, 16], [0, 15]),
                new JsonObject { ["name"] = "marker", ["type"] = "locator", ["uuid"] = "l-1" }),
            ["outliner"] = new JsonArray(
                Group("body", [0, 8, 0], ["c-body",
                    Group("head", [0, 10, -7], ["c-head", "c-ear", "c-whisker"]),
                    Group("tail", [0, 9.5, 8], ["c-tail"], [-45, 0, 0])]),
                Group("leg0", [2, 6, -5], ["c-leg0"]),
                Group("leg1", [-2, 6, -5], ["c-leg1"]),
                Group("leg2", [2, 6, 5], ["c-leg2"]),
                Group("leg3", [-2, 6, 5], ["c-leg3"])),
            ["textures"] = new JsonArray(new JsonObject
            {
                ["name"] = "cat_coat.png",
                ["id"] = "0",
                ["uv_width"] = 64,
                ["uv_height"] = 32,
                ["source"] = "data:image/png;base64," + png,
            }),
            ["display"] = new JsonObject { ["gui"] = new JsonObject { ["rotation"] = new JsonArray(30, 225, 0), ["scale"] = new JsonArray(0.6, 0.6, 0.6) } },
        };

        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, name + ".bbmodel");
        File.WriteAllText(file, model.ToJsonString());
        return file;
    }

    /// <summary>Imports the model with its textures bound, and adds a mob, an item and a block shown with it.</summary>
    public static ModelAsset AddTo(ModProject project, IReadOnlyList<TextureAsset> textures)
    {
        string file = CreateFile(Path.Combine(project.Layout.RootDirectory, "imports"));
        ModelAsset model = new AssetFileImporter().ImportModel(project, file);
        model.Variants = [new ModelTextureVariant("Black", textures[0].Id), new ModelTextureVariant("Orange", textures[1].Id)];
        model.Layers = [new ModelTextureLayer("Collar", textures[0].Id, false), new ModelTextureLayer("Eyes", textures[1].Id, true)];
        project.Assets.Add(model);

        project.Assets.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("alley_cat"), "Chat de gouttière")
        {
            Model = MobModel.Custom,
            CustomModelId = model.Id,
            Kind = MobKind.Animal,
        });
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("cat_figurine"), "Figurine de chat") { CustomModelId = model.Id });
        project.Assets.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("cat_statue"), "Statue de chat")
        {
            Model = BlockModelKind.Custom,
            CustomModelId = model.Id,
        });
        return model;
    }
}
