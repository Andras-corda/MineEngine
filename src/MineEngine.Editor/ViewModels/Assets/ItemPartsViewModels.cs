using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Un effet donné en mangeant : effet, durée, niveau, probabilité.</summary>
public sealed class FoodEffectViewModel : ObservableObject
{
    private readonly ItemAssetViewModel _owner;

    public FoodEffectViewModel(ItemAssetViewModel owner, int index)
    {
        _owner = owner;
        Index = index;
        RemoveCommand = new RelayCommand(() => _owner.RemoveFoodEffect(Index));
    }

    public int Index { get; }

    private FoodEffect Effect => _owner.GetFoodEffect(Index);

    public IReadOnlyList<Choice<FoodEffectKind>> EffectChoices => GameDataChoices.FoodEffects;

    public FoodEffectKind Kind
    {
        get => Effect.Effect;
        set
        {
            if (value != Effect.Effect)
            {
                _owner.UpdateFoodEffect(Index, e => new FoodEffect(value, e.DurationSeconds, e.Amplifier, e.Probability));
            }
        }
    }

    public int MaxSeconds => FoodEffect.MaxDurationSeconds;

    public double Seconds
    {
        get => Effect.DurationSeconds;
        set
        {
            if (!double.IsNaN(value) && (int)Math.Round(value) != Effect.DurationSeconds)
            {
                _owner.UpdateFoodEffect(Index, e => new FoodEffect(e.Effect, (int)Math.Round(value), e.Amplifier, e.Probability));
            }
        }
    }

    public int MaxLevel => FoodEffect.MaxAmplifier + 1;

    /// <summary>Niveau affiché comme dans le jeu : I, II... (amplificateur + 1).</summary>
    public double Level
    {
        get => Effect.Amplifier + 1;
        set
        {
            if (!double.IsNaN(value) && (int)Math.Round(value) - 1 != Effect.Amplifier)
            {
                _owner.UpdateFoodEffect(Index, e => new FoodEffect(e.Effect, e.DurationSeconds, (int)Math.Round(value) - 1, e.Probability));
            }
        }
    }

    /// <summary>Probabilité en pourcentage (100 = toujours).</summary>
    public double Percent
    {
        get => Math.Round(Effect.Probability * 100);
        set
        {
            if (!double.IsNaN(value) && Math.Abs(value - Percent) > 0.01)
            {
                _owner.UpdateFoodEffect(Index, e => new FoodEffect(e.Effect, e.DurationSeconds, e.Amplifier, (float)(value / 100)));
            }
        }
    }

    public ICommand RemoveCommand { get; }

    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>
/// Un événement de l'item dans l'onglet Événements : le graphe qui y réagit, ou un bouton
/// pour en créer un (le nœud événement est ajouté au graphe d'événements de l'item).
/// </summary>
public sealed class ItemEventViewModel : EventLinkViewModel
{
    public ItemEventViewModel(ItemAssetViewModel owner, ItemEventKind kind)
        : base(NodeCatalog.ForItemEvent(kind), Tr.T("Item: "), Tr.T("Add this event to the item's event graph"), () => owner.AddItemEvent(kind), owner.OpenItemEvent)
    {
        Kind = kind;
        Method = owner.MethodSlots[ItemMethodSlots.EventKey(kind)];
        Parameters = ["level", .. NodeCatalog.ForItemEvent(kind).Outputs.Select(o => o.Name)];
    }

    /// <summary>The values the event gives to its method and its graphs (level, player, position...).</summary>
    public IReadOnlyList<string> Parameters { get; }

    public ItemEventKind Kind { get; }

    /// <summary>The method of the event in the class of the item (V0.7.13), next to the graphs that react to it.</summary>
    public ElementMethodViewModel Method { get; }
}
