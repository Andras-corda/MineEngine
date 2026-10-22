using System.Text.Json.Nodes;

namespace MineEngine.Assets.Definitions;

/// <summary>Reading and writing of the appearances of an item, a block or a mob ("appearances": [...]).</summary>
internal static class AppearanceJson
{
    private const string Key = "appearances";

    public static IReadOnlyList<AssetAppearance> Read(JsonPropertyReader reader) =>
        [.. reader.GetObjectList(Key)
            .Select(a => new AssetAppearance(a.GetGuid("id") ?? Guid.NewGuid(), a.GetString("name") ?? string.Empty, a.GetGuid("texture"), a.GetGuid("model")))];

    public static void Write(TexturedAsset asset, JsonObject properties)
    {
        if (asset.Appearances.Count == 0)
        {
            return;
        }

        properties[Key] = new JsonArray([.. asset.Appearances.Select(a => (JsonNode)new JsonObject
        {
            ["id"] = a.Id.ToString("D"),
            ["name"] = a.Name,
            ["texture"] = a.TextureId?.ToString("D"),
            ["model"] = a.ModelId?.ToString("D"),
        })]);
    }
}
