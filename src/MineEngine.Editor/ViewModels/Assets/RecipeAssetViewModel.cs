using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Inspector d'une recette, comme l'éditeur de recettes de MCreator : grille 3 x 3 de
/// l'établi (ou ingrédient du four) et résultat.
/// </summary>
public sealed class RecipeAssetViewModel : AssetViewModel
{
    private readonly RecipeAsset _recipe;

    public RecipeAssetViewModel(RecipeAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _recipe = model;
        GridSlots =
        [
            .. Enumerable.Range(0, RecipeAsset.GridSize).Select(index => new ReferencePickerViewModel(
                context, ReferenceDomain.Ingredient, () => _recipe.Grid[index],
                v => Change("Modifier la grille", nameof(RecipeAsset.Grid), () => _recipe.Grid, g => _recipe.Grid = g, _recipe.WithGridSlot(index, v)))),
        ];
        SmeltingInput = new ReferencePickerViewModel(
            context, ReferenceDomain.Ingredient, () => _recipe.SmeltingInput,
            v => Change("Modifier l'ingrédient", nameof(RecipeAsset.SmeltingInput), () => _recipe.SmeltingInput, r => _recipe.SmeltingInput = r, v));
        Result = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => _recipe.Result,
            v => Change("Modifier le résultat", nameof(RecipeAsset.Result), () => _recipe.Result, r => _recipe.Result = r, v));
    }

    public IReadOnlyList<Choice<RecipeKind>> KindChoices => GameDataChoices.RecipeKinds;

    public RecipeKind Kind
    {
        get => _recipe.Kind;
        set
        {
            if (value != _recipe.Kind)
            {
                Change("Modifier le type de recette", nameof(RecipeAsset.Kind), () => _recipe.Kind, v => _recipe.Kind = v, value);
                Context.History.Seal();
            }
        }
    }

    public bool IsCrafting => _recipe.Kind != RecipeKind.Smelting;

    public bool IsSmelting => _recipe.Kind == RecipeKind.Smelting;

    public string GridHint => _recipe.Kind == RecipeKind.Shaped
        ? "Disposez les ingrédients comme sur l'établi. Le motif peut être placé n'importe où dans la grille du jeu."
        : "Les ingrédients peuvent être placés dans n'importe quel ordre.";

    /// <summary>Cases de la grille, ligne par ligne.</summary>
    public IReadOnlyList<ReferencePickerViewModel> GridSlots { get; }

    public ReferencePickerViewModel SmeltingInput { get; }

    public ReferencePickerViewModel Result { get; }

    public int MaxResultCount => RecipeAsset.MaxResultCount;

    public double ResultCount
    {
        get => _recipe.ResultCount;
        set => SetNumber("Modifier la quantité produite", nameof(RecipeAsset.ResultCount), value, () => _recipe.ResultCount,
            v => _recipe.ResultCount = v, v => Math.Clamp((int)Math.Round(v), 1, RecipeAsset.MaxResultCount));
    }

    public double Experience
    {
        get => _recipe.Experience;
        set => SetNumber("Modifier l'expérience", nameof(RecipeAsset.Experience), value, () => _recipe.Experience,
            v => _recipe.Experience = v, v => (float)Math.Clamp(v, 0, RecipeAsset.MaxExperience));
    }

    /// <summary>Durée de cuisson affichée en secondes (20 ticks par seconde).</summary>
    public double CookingSeconds
    {
        get => _recipe.CookingTime / 20.0;
        set => SetNumber("Modifier la durée de cuisson", nameof(RecipeAsset.CookingTime), value, () => _recipe.CookingTime,
            v => _recipe.CookingTime = v, v => Math.Clamp((int)Math.Round(v * 20), 1, RecipeAsset.MaxCookingTime));
    }

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        foreach (ReferencePickerViewModel slot in GridSlots)
        {
            slot.Refresh();
        }

        SmeltingInput.Refresh();
        Result.Refresh();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(RecipeAsset.Kind):
                OnPropertyChanged(nameof(Kind));
                OnPropertyChanged(nameof(IsCrafting));
                OnPropertyChanged(nameof(IsSmelting));
                OnPropertyChanged(nameof(GridHint));
                break;

            case nameof(RecipeAsset.Grid):
                foreach (ReferencePickerViewModel slot in GridSlots)
                {
                    slot.Refresh();
                }

                break;

            case nameof(RecipeAsset.SmeltingInput):
                SmeltingInput.Refresh();
                break;

            case nameof(RecipeAsset.Result):
                Result.Refresh();
                break;

            case nameof(RecipeAsset.ResultCount):
                OnPropertyChanged(nameof(ResultCount));
                break;

            case nameof(RecipeAsset.Experience):
                OnPropertyChanged(nameof(Experience));
                break;

            case nameof(RecipeAsset.CookingTime):
                OnPropertyChanged(nameof(CookingSeconds));
                break;
        }
    }

    private void SetNumber<T>(string description, string propertyName, double value, Func<T> getter, Action<T> setter, Func<double, T> convert)
    {
        if (double.IsNaN(value))
        {
            // Champ vidé : on réaffiche la valeur actuelle.
            OnPropertyChanged(string.Empty);
            return;
        }

        T converted = convert(value);
        if (!EqualityComparer<T>.Default.Equals(getter(), converted))
        {
            Change(description, propertyName, getter, setter, converted);
        }
    }
}
