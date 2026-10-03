using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

public sealed class RecipeAssetDefinition : AssetDefinition<RecipeAsset>
{
    public override AssetType Type => AssetType.Recipe;

    public override string Label => "Recette";

    public override string DefaultResourceIdBase => "new_recipe";

    public override string DefaultDisplayName => "Nouvelle recette";

    protected override RecipeAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName);

    protected override void ReadProperties(RecipeAsset asset, JsonPropertyReader reader)
    {
        asset.Kind = reader.GetEnum("kind", RecipeKind.Shaped);

        IReadOnlyList<ContentReference?> grid = reader.GetReferenceList("grid");
        asset.Grid = [.. Enumerable.Range(0, RecipeAsset.GridSize).Select(i => i < grid.Count ? grid[i] : null)];

        asset.SmeltingInput = reader.GetReference("input");
        asset.Result = reader.GetReference("result");
        asset.ResultCount = reader.GetInt32("count", 1);
        asset.Experience = reader.GetSingle("experience", 0.1f);
        asset.CookingTime = reader.GetInt32("cookingTime", RecipeAsset.DefaultCookingTime);
    }

    protected override void WriteProperties(RecipeAsset asset, JsonObject properties)
    {
        properties["kind"] = asset.Kind.ToString();
        properties["grid"] = new JsonArray([.. asset.Grid.Select(r => (JsonNode?)JsonValue.Create(r?.ToStorageText()))]);
        properties["input"] = asset.SmeltingInput?.ToStorageText();
        properties["result"] = asset.Result?.ToStorageText();
        properties["count"] = asset.ResultCount;
        properties["experience"] = asset.Experience;
        properties["cookingTime"] = asset.CookingTime;
    }
}
