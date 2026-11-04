using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Recette d'artisanat (établi, avec ou sans disposition), de cuisson (four, haut fourneau,
/// fumoir, feu de camp), du tailleur de pierre ou de la table de forgeron.
/// Les ingrédients et le résultat sont des items du projet ou du jeu ; un
/// ingrédient peut aussi être un tag ("#minecraft:planks"). Le livre de recettes la range
/// dans un groupe et une catégorie, et la débloque quand le joueur obtient un des items choisis.
/// </summary>
public sealed class RecipeAsset : Asset
{
    public const int GridSize = 9;
    public const int GridWidth = 3;
    public const int MaxResultCount = 64;
    public const float MaxExperience = 100f;
    public const int DefaultCookingTime = 200;

    /// <summary>Slots of the grid used by the smithing table: template, base, addition.</summary>
    public const int SmithingTemplate = 0;
    public const int SmithingBase = 1;
    public const int SmithingAddition = 2;
    public const int MaxCookingTime = 72_000;

    private RecipeKind _kind = RecipeKind.Shaped;
    private IReadOnlyList<ContentReference?> _grid = new ContentReference?[GridSize];
    private ContentReference? _smeltingInput;
    private ContentReference? _result;
    private int _resultCount = 1;
    private float _experience = 0.1f;
    private int _cookingTime = DefaultCookingTime;
    private string _group = string.Empty;
    private string _namespace = string.Empty;
    private string _category = "misc";
    private IReadOnlyList<ContentReference> _unlockItems = [];

    public RecipeAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Recipe;

    public RecipeKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    /// <summary>
    /// Grille 3 x 3 de l'établi, ligne par ligne (cases vides : null). Pour une recette
    /// sans disposition, seules les cases remplies comptent, dans n'importe quel ordre.
    /// </summary>
    public IReadOnlyList<ContentReference?> Grid
    {
        get => _grid;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count != GridSize)
            {
                throw new ArgumentException(Tr.F("The grid must contain {0} slots.", GridSize), nameof(value));
            }

            if (!value.SequenceEqual(_grid))
            {
                _grid = [.. value];
                RaiseChanged(nameof(Grid));
            }
        }
    }

    /// <summary>Ingrédient cuit dans le four.</summary>
    public ContentReference? SmeltingInput
    {
        get => _smeltingInput;
        set => SetField(ref _smeltingInput, value);
    }

    public ContentReference? Result
    {
        get => _result;
        set => SetField(ref _result, value);
    }

    /// <summary>Nombre d'items produits par l'établi (le four en produit toujours un).</summary>
    public int ResultCount
    {
        get => _resultCount;
        set => SetField(ref _resultCount, Math.Clamp(value, 1, MaxResultCount));
    }

    /// <summary>Expérience gagnée en retirant le résultat du four.</summary>
    public float Experience
    {
        get => _experience;
        set => SetField(ref _experience, float.IsFinite(value) ? Math.Clamp(value, 0f, MaxExperience) : 0f);
    }

    /// <summary>Durée de cuisson en ticks (20 par seconde ; 200 pour le four).</summary>
    public int CookingTime
    {
        get => _cookingTime;
        set => SetField(ref _cookingTime, Math.Clamp(value, 1, MaxCookingTime));
    }

    /// <summary>Recipes that share a group are shown as one in the recipe book ("wooden_door"); empty for none.</summary>
    public string Group
    {
        get => _group;
        set => SetField(ref _group, CleanPath(value));
    }

    /// <summary>
    /// Namespace of the file of the recipe: empty for the mod; "minecraft" replaces the recipe of the
    /// game with the same name (its registry name).
    /// </summary>
    public string Namespace
    {
        get => _namespace;
        set => SetField(ref _namespace, CleanPath(value).Replace("/", string.Empty, StringComparison.Ordinal));
    }

    /// <summary>Tab of the recipe book: building, redstone, equipment, misc (crafting) or food, blocks, misc (cooking).</summary>
    public string Category
    {
        get => _category;
        set => SetField(ref _category, CleanPath(value) is { Length: > 0 } category ? category : "misc");
    }

    /// <summary>Items or tags: the recipe is unlocked in the recipe book as soon as the player gets one of them.</summary>
    public IReadOnlyList<ContentReference> UnlockItems
    {
        get => _unlockItems;
        set
        {
            IReadOnlyList<ContentReference> list = value is null ? [] : [.. value];
            if (!_unlockItems.SequenceEqual(list))
            {
                _unlockItems = list;
                RaiseChanged(nameof(UnlockItems));
            }
        }
    }

    public bool IsCrafting => Kind is RecipeKind.Shaped or RecipeKind.Shapeless;

    /// <summary>Furnace, blast furnace, smoker or campfire: one ingredient, experience and a cooking time.</summary>
    public bool IsCooking => Kind is RecipeKind.Smelting or RecipeKind.Blasting or RecipeKind.Smoking or RecipeKind.CampfireCooking;

    /// <summary>The recipe uses <see cref="SmeltingInput"/> as its only ingredient (cooking, stonecutter).</summary>
    public bool HasSingleInput => IsCooking || Kind == RecipeKind.Stonecutting;

    /// <summary>The station gives several results (crafting table, stonecutter); the others give one.</summary>
    public bool HasResultCount => IsCrafting || Kind == RecipeKind.Stonecutting;

    /// <summary>Categories of the recipe book for this kind of recipe (none for the stonecutter and the smithing table).</summary>
    public static IReadOnlyList<string> CategoriesOf(RecipeKind kind) => kind switch
    {
        RecipeKind.Shaped or RecipeKind.Shapeless => ["building", "redstone", "equipment", "misc"],
        RecipeKind.Smelting or RecipeKind.Blasting or RecipeKind.Smoking or RecipeKind.CampfireCooking => ["food", "blocks", "misc"],
        _ => [],
    };

    /// <summary>Cooking time of the game for a station, in ticks (furnace 10 s, blast furnace and smoker 5 s, campfire 30 s).</summary>
    public static int DefaultCookingTimeOf(RecipeKind kind) => kind switch
    {
        RecipeKind.Blasting or RecipeKind.Smoking => 100,
        RecipeKind.CampfireCooking => 600,
        _ => DefaultCookingTime,
    };

    /// <summary>Ingrédients effectivement utilisés par la recette.</summary>
    public IEnumerable<ContentReference> Ingredients => Kind switch
    {
        _ when HasSingleInput => SmeltingInput is { } input ? [input] : [],
        RecipeKind.Smithing => Grid.Take(3).OfType<ContentReference>(),
        _ => Grid.OfType<ContentReference>(),
    };

    /// <summary>Renvoie une copie de la grille où une case a été remplacée.</summary>
    public IReadOnlyList<ContentReference?> WithGridSlot(int index, ContentReference? ingredient)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, GridSize);
        var copy = _grid.ToArray();
        copy[index] = ingredient;
        return copy;
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (ContentReference ingredient in Ingredients.Where(i => i.Kind == ContentReferenceKind.Asset))
        {
            yield return new AssetReference(ingredient.AssetId, AssetReferenceKind.Item, Tr.T("ingredient"));
        }

        if (Result is { Kind: ContentReferenceKind.Asset } result)
        {
            yield return new AssetReference(result.AssetId, AssetReferenceKind.Item, Tr.T("result"));
        }

        foreach (ContentReference item in UnlockItems.Where(i => i.Kind == ContentReferenceKind.Asset))
        {
            yield return new AssetReference(item.AssetId, AssetReferenceKind.Item, Tr.T("unlocks the recipe"));
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        string source = ToString();
        if (Result is null)
        {
            diagnostics.Error(Tr.T("The recipe has no result."), source, Id);
        }
        else if (Result.Kind == ContentReferenceKind.Tag)
        {
            diagnostics.Error(Tr.T("The result of a recipe must be a specific item, not a tag."), source, Id);
        }

        if (Kind == RecipeKind.Smithing)
        {
            if (Ingredients.Count() < 3)
            {
                diagnostics.Error(Tr.T("The smithing table needs a template, a base item and a material."), source, Id);
            }
        }
        else if (!Ingredients.Any())
        {
            diagnostics.Error(
                HasSingleInput ? Tr.T("Choose the ingredient to smelt.") : Tr.T("The crafting grid is empty."),
                source,
                Id);
        }

        if (CategoriesOf(Kind) is { Count: > 0 } categories && !categories.Contains(Category))
        {
            diagnostics.Warning(Tr.F("The category \"{0}\" is not one of this station ({1}): misc is used.", Category, string.Join(", ", categories)), source, Id);
        }
    }

    /// <summary>"Wooden Door" gives "wooden_door": lowercase letters, digits, '_', '.', '-' and '/'.</summary>
    private static string CleanPath(string? text) =>
        new([.. (text ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_')
            .Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '.' or '-' or '/')]);
}
