using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

public sealed class RecipeLowering : AssetLowering<RecipeAsset>
{
    private const string KeyLetters = "ABCDEFGHI";

    public override AssetType AssetType => AssetType.Recipe;

    protected override void Lower(RecipeAsset asset, LoweringContext context)
    {
        if (context.ResolveItem(asset, asset.Result, "Résultat") is not { } result)
        {
            return;
        }

        IRRecipe? recipe = asset.Kind switch
        {
            RecipeKind.Shaped => LowerShaped(asset, result, context),
            RecipeKind.Shapeless => LowerShapeless(asset, result, context),
            _ => LowerSmelting(asset, result, context),
        };

        if (recipe is not null)
        {
            context.AddRecipe(recipe);
        }
    }

    /// <summary>
    /// Réduit la grille au plus petit rectangle qui contient les ingrédients (comme le
    /// jeu, qui accepte alors le motif n'importe où dans la grille) et attribue une
    /// lettre à chaque ingrédient distinct.
    /// </summary>
    private static IRShapedRecipe? LowerShaped(RecipeAsset asset, NamespacedId result, LoweringContext context)
    {
        IReadOnlyList<ContentReference?> grid = asset.Grid;
        int width = RecipeAsset.GridWidth;
        List<int> filled = Enumerable.Range(0, grid.Count).Where(i => grid[i] is not null).ToList();
        if (filled.Count == 0)
        {
            return null;
        }

        int top = filled.Min(i => i / width), bottom = filled.Max(i => i / width);
        int left = filled.Min(i => i % width), right = filled.Max(i => i % width);

        var letters = new Dictionary<ContentReference, char>();
        var keys = new Dictionary<char, IRIngredient>();
        var pattern = new List<string>();
        for (int row = top; row <= bottom; row++)
        {
            var line = new char[right - left + 1];
            for (int column = left; column <= right; column++)
            {
                ContentReference? reference = grid[(row * width) + column];
                if (reference is null)
                {
                    line[column - left] = ' ';
                    continue;
                }

                if (!letters.TryGetValue(reference, out char letter))
                {
                    if (context.ResolveIngredient(asset, reference, "Ingrédient") is not { } ingredient)
                    {
                        return null;
                    }

                    letter = KeyLetters[letters.Count];
                    letters[reference] = letter;
                    keys[letter] = ingredient;
                }

                line[column - left] = letter;
            }

            pattern.Add(new string(line));
        }

        return new IRShapedRecipe(asset.Id, asset.ResourceId, pattern, keys, result, asset.ResultCount);
    }

    private static IRShapelessRecipe? LowerShapeless(RecipeAsset asset, NamespacedId result, LoweringContext context)
    {
        var ingredients = new List<IRIngredient>();
        foreach (ContentReference reference in asset.Grid.OfType<ContentReference>())
        {
            if (context.ResolveIngredient(asset, reference, "Ingrédient") is not { } ingredient)
            {
                return null;
            }

            ingredients.Add(ingredient);
        }

        return ingredients.Count == 0
            ? null
            : new IRShapelessRecipe(asset.Id, asset.ResourceId, ingredients, result, asset.ResultCount);
    }

    private static IRSmeltingRecipe? LowerSmelting(RecipeAsset asset, NamespacedId result, LoweringContext context) =>
        asset.SmeltingInput is { } input && context.ResolveIngredient(asset, input, "Ingrédient") is { } ingredient
            ? new IRSmeltingRecipe(asset.Id, asset.ResourceId, ingredient, result, asset.Experience, asset.CookingTime)
            : null;
}
