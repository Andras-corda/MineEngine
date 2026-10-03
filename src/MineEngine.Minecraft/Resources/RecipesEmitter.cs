using System.Text.Json.Nodes;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Recettes JSON. Depuis 1.20.5, le résultat s'écrit {"id": ..., "count": ...}
/// (au lieu de "item") et le four accepte un objet comme résultat.
/// </summary>
public sealed class RecipesEmitter : IFileEmitter
{
    public string Name => "Recettes";

    public void Emit(GenerationContext context)
    {
        bool modern = context.MinecraftVersion.IsAtLeast("1.20.5");
        foreach (IRRecipe recipe in context.Ir.Content.Recipes)
        {
            JsonObject json = recipe switch
            {
                IRShapedRecipe shaped => Shaped(shaped, modern),
                IRShapelessRecipe shapeless => Shapeless(shapeless, modern),
                IRSmeltingRecipe smelting => Smelting(smelting, modern),
                _ => throw new NotSupportedException($"Recette non prise en charge : {recipe.GetType().Name}."),
            };

            context.Files.WriteText(context.Paths.Recipe(recipe.Id), JsonFormatting.ToText(json));
        }
    }

    private static JsonObject Shaped(IRShapedRecipe recipe, bool modern)
    {
        var key = new JsonObject();
        foreach ((char letter, IRIngredient ingredient) in recipe.Keys.OrderBy(k => k.Key))
        {
            key[letter.ToString()] = Ingredient(ingredient);
        }

        return new JsonObject
        {
            ["type"] = "minecraft:crafting_shaped",
            ["category"] = "misc",
            ["pattern"] = new JsonArray([.. recipe.Pattern.Select(l => (JsonNode?)JsonValue.Create(l))]),
            ["key"] = key,
            ["result"] = Result(recipe.Result, recipe.Count, modern),
        };
    }

    private static JsonObject Shapeless(IRShapelessRecipe recipe, bool modern) => new()
    {
        ["type"] = "minecraft:crafting_shapeless",
        ["category"] = "misc",
        ["ingredients"] = new JsonArray([.. recipe.Ingredients.Select(i => (JsonNode?)Ingredient(i))]),
        ["result"] = Result(recipe.Result, recipe.Count, modern),
    };

    private static JsonObject Smelting(IRSmeltingRecipe recipe, bool modern) => new()
    {
        ["type"] = "minecraft:smelting",
        ["category"] = "misc",
        ["ingredient"] = Ingredient(recipe.Input),
        ["result"] = modern ? new JsonObject { ["id"] = recipe.Result.ToString() } : JsonValue.Create(recipe.Result.ToString()),
        ["experience"] = recipe.Experience,
        ["cookingtime"] = recipe.CookingTime,
    };

    private static JsonObject Ingredient(IRIngredient ingredient) =>
        new() { [ingredient.IsTag ? "tag" : "item"] = ingredient.Id.ToString() };

    private static JsonObject Result(NamespacedId item, int count, bool modern) =>
        new() { [modern ? "id" : "item"] = item.ToString(), ["count"] = count };
}
