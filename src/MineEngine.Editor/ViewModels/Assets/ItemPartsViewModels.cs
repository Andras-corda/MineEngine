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
public sealed class ItemEventViewModel : ObservableObject
{
    private readonly ItemAssetViewModel _owner;
    private IReadOnlyList<(GraphAsset Graph, GraphNode Node)> _links = [];

    public ItemEventViewModel(ItemAssetViewModel owner, ItemEventKind kind)
    {
        _owner = owner;
        Kind = kind;
        NodeInfo info = NodeCatalog.ForItemEvent(kind);
        Title = info.Title.Replace("Item : ", string.Empty, StringComparison.Ordinal);
        Description = info.Description;
        AddCommand = new RelayCommand(() => _owner.AddItemEvent(Kind));
        OpenCommand = new RelayCommand(() => _owner.OpenItemEvent(_links[0].Graph, _links[0].Node), () => _links.Count > 0);
    }

    public ItemEventKind Kind { get; }

    public string Title { get; }

    public string Description { get; }

    public bool IsLinked => _links.Count > 0;

    /// <summary>"Relié au graphe « Événements de l'épée »" ou "Aucun graphe".</summary>
    public string Status => _links.Count switch
    {
        0 => "Aucun graphe ne réagit à cet événement.",
        1 => $"Relié au graphe « {Header(_links[0].Graph)} ».",
        _ => $"Relié à {_links.Count} nœuds ({string.Join(", ", _links.Select(l => Header(l.Graph)).Distinct())}).",
    };

    public ICommand AddCommand { get; }

    public ICommand OpenCommand { get; }

    internal void SetLinks(IReadOnlyList<(GraphAsset Graph, GraphNode Node)> links)
    {
        _links = links;
        OnPropertyChanged(nameof(IsLinked));
        OnPropertyChanged(nameof(Status));
        CommandManager.InvalidateRequerySuggested();
    }

    private static string Header(GraphAsset graph) =>
        string.IsNullOrWhiteSpace(graph.DisplayName) ? graph.ResourceId.Value : graph.DisplayName;
}
