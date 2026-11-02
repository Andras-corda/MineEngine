using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>Ingrédient : un item précis ou un tag d'items.</summary>
public sealed record IRIngredient(NamespacedId Id, bool IsTag);

/// <summary>Recette du mod (fichier JSON de données).</summary>
public abstract class IRRecipe
{
    protected IRRecipe(Guid sourceAssetId, ResourceId id, NamespacedId result)
    {
        SourceAssetId = sourceAssetId;
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public Guid SourceAssetId { get; }

    public ResourceId Id { get; }

    public NamespacedId Result { get; }

    /// <summary>Namespace of the file, place in the recipe book and items that unlock the recipe.</summary>
    public IRRecipeBook Book { get; init; } = IRRecipeBook.Default;
}

/// <summary>
/// Where a recipe goes: <paramref name="Namespace"/> of its file (null for the mod; "minecraft" replaces a
/// recipe of the game), its <paramref name="Group"/> and <paramref name="Category"/> in the recipe book,
/// and the items or tags that unlock it when the player gets one (an advancement; empty for none).
/// </summary>
public sealed record IRRecipeBook(string? Namespace, string Group, string Category, IReadOnlyList<IRIngredient> UnlockItems)
{
    public static IRRecipeBook Default { get; } = new(null, string.Empty, "misc", []);
}

/// <summary>Établi avec disposition : motif réduit au plus petit rectangle et légende.</summary>
public sealed class IRShapedRecipe : IRRecipe
{
    public IRShapedRecipe(
        Guid sourceAssetId, ResourceId id, IReadOnlyList<string> pattern, IReadOnlyDictionary<char, IRIngredient> keys, NamespacedId result, int count)
        : base(sourceAssetId, id, result)
    {
        Pattern = pattern;
        Keys = keys;
        Count = count;
    }

    /// <summary>Lignes du motif ("AA", " B"), une lettre par ingrédient, espace pour une case vide.</summary>
    public IReadOnlyList<string> Pattern { get; }

    public IReadOnlyDictionary<char, IRIngredient> Keys { get; }

    public int Count { get; }
}

public sealed class IRShapelessRecipe : IRRecipe
{
    public IRShapelessRecipe(Guid sourceAssetId, ResourceId id, IReadOnlyList<IRIngredient> ingredients, NamespacedId result, int count)
        : base(sourceAssetId, id, result)
    {
        Ingredients = ingredients;
        Count = count;
    }

    public IReadOnlyList<IRIngredient> Ingredients { get; }

    public int Count { get; }
}

public sealed class IRSmeltingRecipe : IRRecipe
{
    public IRSmeltingRecipe(Guid sourceAssetId, ResourceId id, IRIngredient input, NamespacedId result, float experience, int cookingTime)
        : base(sourceAssetId, id, result)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Experience = experience;
        CookingTime = cookingTime;
    }

    public IRIngredient Input { get; }

    /// <summary>Station: furnace (Smelting), blast furnace, smoker or campfire.</summary>
    public RecipeKind Kind { get; init; } = RecipeKind.Smelting;

    public float Experience { get; }

    /// <summary>Durée de cuisson en ticks.</summary>
    public int CookingTime { get; }
}

/// <summary>Stonecutter: one ingredient gives the result, in several copies.</summary>
public sealed class IRStonecuttingRecipe : IRRecipe
{
    public IRStonecuttingRecipe(Guid sourceAssetId, ResourceId id, IRIngredient input, NamespacedId result, int count)
        : base(sourceAssetId, id, result)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Count = count;
    }

    public IRIngredient Input { get; }

    public int Count { get; }
}

/// <summary>Smithing table: a template, a base item and a material give the improved item.</summary>
public sealed class IRSmithingRecipe : IRRecipe
{
    public IRSmithingRecipe(Guid sourceAssetId, ResourceId id, IRIngredient template, IRIngredient @base, IRIngredient addition, NamespacedId result)
        : base(sourceAssetId, id, result)
    {
        Template = template;
        Base = @base;
        Addition = addition;
    }

    public IRIngredient Template { get; }

    public IRIngredient Base { get; }

    public IRIngredient Addition { get; }
}

/// <summary>Événement sonore du mod et son fichier OGG.</summary>
public sealed class IRSound
{
    public IRSound(Guid sourceAssetId, ResourceId id, string sourceFile, string subtitle)
    {
        SourceAssetId = sourceAssetId;
        Id = id ?? throw new ArgumentNullException(nameof(id));
        SourceFile = sourceFile ?? throw new ArgumentNullException(nameof(sourceFile));
        Subtitle = subtitle ?? string.Empty;
    }

    public Guid SourceAssetId { get; }

    public ResourceId Id { get; }

    /// <summary>Chemin absolu du fichier OGG.</summary>
    public string SourceFile { get; }

    public string Subtitle { get; }
}
