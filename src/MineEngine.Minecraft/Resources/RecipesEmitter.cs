using System.Text.Json.Nodes;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Recettes JSON. Depuis 1.20.5, le résultat s'écrit {"id": ..., "count": ...}
/// (au lieu de "item") et le four accepte un objet comme résultat. Une recette débloquée par
/// des items a aussi son avancement (comme celles du jeu), dont le format d'item change en 1.20.5.
/// </summary>
public sealed class RecipesEmitter : IFileEmitter
{
    public string Name => Tr.T("Recipes");

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
                IRStonecuttingRecipe stonecutting => Stonecutting(stonecutting, modern),
                IRSmithingRecipe smithing => Smithing(smithing, modern),
                _ => throw new NotSupportedException(Tr.F("Unsupported recipe: {0}.", recipe.GetType().Name)),
            };

            // The stonecutter and the smithing table have no group nor tab in the recipe book.
            if (recipe.Book.Group.Length > 0 && recipe is not IRStonecuttingRecipe and not IRSmithingRecipe)
            {
                json.Insert(1, "group", recipe.Book.Group);
            }

            context.Files.WriteText(context.Paths.Recipe(recipe.Book.Namespace, recipe.Id), JsonFormatting.ToText(json));
            if (recipe.Book.UnlockItems.Count > 0)
            {
                context.Files.WriteText(context.Paths.RecipeAdvancement(recipe.Book.Namespace, recipe.Id),
                    JsonFormatting.ToText(Advancement(recipe, $"{recipe.Book.Namespace ?? context.Paths.Namespace}:{recipe.Id}", modern)));
            }
        }
    }

    /// <summary>
    /// Advancement that gives the recipe as soon as the player has one of the items (or an item of a tag),
    /// or already knows the recipe; it is hidden, like the recipe advancements of the game.
    /// </summary>
    private static JsonObject Advancement(IRRecipe recipe, string recipeId, bool modern)
    {
        var criteria = new JsonObject
        {
            ["has_the_recipe"] = new JsonObject
            {
                ["trigger"] = "minecraft:recipe_unlocked",
                ["conditions"] = new JsonObject { ["recipe"] = recipeId },
            },
        };
        var names = new JsonArray { "has_the_recipe" };
        for (int i = 0; i < recipe.Book.UnlockItems.Count; i++)
        {
            string name = $"has_item_{i}";
            criteria[name] = new JsonObject
            {
                ["trigger"] = "minecraft:inventory_changed",
                ["conditions"] = new JsonObject { ["items"] = new JsonArray { ItemPredicate(recipe.Book.UnlockItems[i], modern) } },
            };
            names.Add(name);
        }

        return new JsonObject
        {
            ["parent"] = "minecraft:recipes/root",
            ["criteria"] = criteria,
            ["requirements"] = new JsonArray { names },
            ["rewards"] = new JsonObject { ["recipes"] = new JsonArray { recipeId } },
        };
    }

    /// <summary>An item or a tag in a condition: {"items": ["minecraft:diamond"]} / {"tag": ...} before 1.20.5, {"items": "#..."} since.</summary>
    private static JsonObject ItemPredicate(IRIngredient item, bool modern) => modern
        ? new JsonObject { ["items"] = (item.IsTag ? "#" : string.Empty) + item.Id }
        : item.IsTag
            ? new JsonObject { ["tag"] = item.Id.ToString() }
            : new JsonObject { ["items"] = new JsonArray { item.Id.ToString() } };

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
            ["category"] = recipe.Book.Category,
            ["pattern"] = new JsonArray([.. recipe.Pattern.Select(l => (JsonNode?)JsonValue.Create(l))]),
            ["key"] = key,
            ["result"] = Result(recipe.Result, recipe.Count, modern),
        };
    }

    private static JsonObject Shapeless(IRShapelessRecipe recipe, bool modern) => new()
    {
        ["type"] = "minecraft:crafting_shapeless",
        ["category"] = recipe.Book.Category,
        ["ingredients"] = new JsonArray([.. recipe.Ingredients.Select(i => (JsonNode?)Ingredient(i))]),
        ["result"] = Result(recipe.Result, recipe.Count, modern),
    };

    private static JsonObject Smelting(IRSmeltingRecipe recipe, bool modern) => new()
    {
        ["type"] = recipe.Kind switch
        {
            RecipeKind.Blasting => "minecraft:blasting",
            RecipeKind.Smoking => "minecraft:smoking",
            RecipeKind.CampfireCooking => "minecraft:campfire_cooking",
            _ => "minecraft:smelting",
        },
        ["category"] = recipe.Book.Category,
        ["ingredient"] = Ingredient(recipe.Input),
        ["result"] = modern ? new JsonObject { ["id"] = recipe.Result.ToString() } : JsonValue.Create(recipe.Result.ToString()),
        ["experience"] = recipe.Experience,
        ["cookingtime"] = recipe.CookingTime,
    };

    /// <summary>Stonecutter: the count is beside the result before 1.20.5, inside it since.</summary>
    private static JsonObject Stonecutting(IRStonecuttingRecipe recipe, bool modern)
    {
        var json = new JsonObject
        {
            ["type"] = "minecraft:stonecutting",
            ["ingredient"] = Ingredient(recipe.Input),
            ["result"] = modern ? Result(recipe.Result, recipe.Count, modern) : JsonValue.Create(recipe.Result.ToString()),
        };
        if (!modern)
        {
            json["count"] = recipe.Count;
        }

        return json;
    }

    private static JsonObject Smithing(IRSmithingRecipe recipe, bool modern) => new()
    {
        ["type"] = "minecraft:smithing_transform",
        ["template"] = Ingredient(recipe.Template),
        ["base"] = Ingredient(recipe.Base),
        ["addition"] = Ingredient(recipe.Addition),
        ["result"] = new JsonObject { [modern ? "id" : "item"] = recipe.Result.ToString() },
    };

    private static JsonObject Ingredient(IRIngredient ingredient) =>
        new() { [ingredient.IsTag ? "tag" : "item"] = ingredient.Id.ToString() };

    private static JsonObject Result(NamespacedId item, int count, bool modern) =>
        new() { [modern ? "id" : "item"] = item.ToString(), ["count"] = count };
}
