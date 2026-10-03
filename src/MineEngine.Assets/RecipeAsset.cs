using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Recette d'artisanat (établi, avec ou sans disposition) ou de cuisson (four).
/// Les ingrédients et le résultat sont des items du projet ou du jeu ; un
/// ingrédient peut aussi être un tag ("#minecraft:planks").
/// </summary>
public sealed class RecipeAsset : Asset
{
    public const int GridSize = 9;
    public const int GridWidth = 3;
    public const int MaxResultCount = 64;
    public const float MaxExperience = 100f;
    public const int DefaultCookingTime = 200;
    public const int MaxCookingTime = 72_000;

    private RecipeKind _kind = RecipeKind.Shaped;
    private IReadOnlyList<ContentReference?> _grid = new ContentReference?[GridSize];
    private ContentReference? _smeltingInput;
    private ContentReference? _result;
    private int _resultCount = 1;
    private float _experience = 0.1f;
    private int _cookingTime = DefaultCookingTime;

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
                throw new ArgumentException($"La grille doit contenir {GridSize} cases.", nameof(value));
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

    /// <summary>Ingrédients effectivement utilisés par la recette.</summary>
    public IEnumerable<ContentReference> Ingredients => Kind == RecipeKind.Smelting
        ? SmeltingInput is { } input ? [input] : []
        : Grid.OfType<ContentReference>();

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
            yield return new AssetReference(ingredient.AssetId, AssetReferenceKind.Item, "ingrédient");
        }

        if (Result is { Kind: ContentReferenceKind.Asset } result)
        {
            yield return new AssetReference(result.AssetId, AssetReferenceKind.Item, "résultat");
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        string source = ToString();
        if (Result is null)
        {
            diagnostics.Error("La recette n'a pas de résultat.", source, Id);
        }
        else if (Result.Kind == ContentReferenceKind.Tag)
        {
            diagnostics.Error("Le résultat d'une recette doit être un item précis, pas un tag.", source, Id);
        }

        if (!Ingredients.Any())
        {
            diagnostics.Error(
                Kind == RecipeKind.Smelting ? "Choisissez l'ingrédient à cuire." : "La grille de l'établi est vide.",
                source,
                Id);
        }
    }
}
