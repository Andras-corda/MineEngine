using System.Windows.Input;
using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Case à cocher reliée à une valeur de l'asset (biome d'apparition...).</summary>
public sealed class ToggleOptionViewModel : ObservableObject
{
    private readonly Func<bool> _getter;
    private readonly Action<bool> _setter;

    public ToggleOptionViewModel(string label, Func<bool> getter, Action<bool> setter)
    {
        Label = label;
        _getter = getter ?? throw new ArgumentNullException(nameof(getter));
        _setter = setter ?? throw new ArgumentNullException(nameof(setter));
    }

    public string Label { get; }

    public bool IsChecked
    {
        get => _getter();
        set => _setter(value);
    }

    public void Refresh() => OnPropertyChanged(nameof(IsChecked));
}

/// <summary>
/// Un comportement de l'IA dans la liste : réglages utiles à son type, déplacement
/// (priorité) et suppression. Les modifications remplacent le comportement dans la
/// liste de l'asset, au travers de l'historique.
/// </summary>
public sealed class MobGoalViewModel : ObservableObject
{
    private readonly MobAssetViewModel _owner;

    public MobGoalViewModel(MobAssetViewModel owner, AssetEditingContext context, int index)
    {
        _owner = owner;
        Index = index;
        ItemPicker = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => Goal.Item, v => _owner.UpdateGoal(Index, g => g with { Item = v }, seal: false));
        MoveUpCommand = new RelayCommand(() => _owner.MoveGoal(Index, -1), () => Index > 0);
        MoveDownCommand = new RelayCommand(() => _owner.MoveGoal(Index, +1), () => Index < _owner.GoalCount - 1);
        RemoveCommand = new RelayCommand(() => _owner.RemoveGoal(Index));
    }

    public int Index { get; }

    public MobGoal Goal => _owner.GetGoal(Index);

    public MobGoalInfo Info => Goal.Info;

    public string Title => $"{Index + 1}. {Info.Label}";

    public string SelectorLabel => Info.Selector == MobGoalSelector.Target ? "Choix de la cible" : "Action";

    public string Description => Info.Description;

    /// <summary>Signale un comportement réservé aux animaux sur un mob d'une autre famille.</summary>
    public bool IsInvalidForKind => Info.RequiresAnimal && _owner.Kind != MobKind.Animal;

    public IReadOnlyList<Choice<MobTarget>> TargetChoices => GameDataChoices.MobTargets;

    public double MaxSpeed => MobGoal.MaxSpeed;

    public double MaxDistance => MobGoal.MaxDistance;

    public double Speed
    {
        get => Goal.Speed;
        set
        {
            if (!double.IsNaN(value) && Math.Abs(value - Goal.Speed) > 1e-9)
            {
                _owner.UpdateGoal(Index, g => g with { Speed = Math.Clamp(value, 0, MobGoal.MaxSpeed) }, seal: false);
            }
        }
    }

    public double Distance
    {
        get => Goal.Distance;
        set
        {
            if (!double.IsNaN(value) && Math.Abs(value - Goal.Distance) > 1e-9)
            {
                _owner.UpdateGoal(Index, g => g with { Distance = Math.Clamp(value, 0, MobGoal.MaxDistance) }, seal: false);
            }
        }
    }

    public MobTarget Target
    {
        get => Goal.Target;
        set
        {
            if (value != Goal.Target)
            {
                _owner.UpdateGoal(Index, g => g with { Target = value }, seal: true);
            }
        }
    }

    public bool Flag
    {
        get => Goal.Flag;
        set
        {
            if (value != Goal.Flag)
            {
                _owner.UpdateGoal(Index, g => g with { Flag = value }, seal: true);
            }
        }
    }

    public ReferencePickerViewModel ItemPicker { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    public ICommand RemoveCommand { get; }

    /// <summary>Réaffiche les réglages après une modification (saisie, annulation).</summary>
    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
        ItemPicker.Refresh();
    }
}

/// <summary>Un item du butin : item, quantité minimale et maximale.</summary>
public sealed class MobDropViewModel : ObservableObject
{
    private readonly MobAssetViewModel _owner;

    public MobDropViewModel(MobAssetViewModel owner, AssetEditingContext context, int index)
    {
        _owner = owner;
        Index = index;
        ItemPicker = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => Drop.Item,
            v =>
            {
                if (v is not null)
                {
                    _owner.UpdateDrop(Index, d => d with { Item = v });
                }
            });
        RemoveCommand = new RelayCommand(() => _owner.RemoveDrop(Index));
    }

    public int Index { get; }

    public MobDrop Drop => _owner.GetDrop(Index);

    public int MaxCount => MobDrop.MaxCount;

    public double Min
    {
        get => Drop.Min;
        set
        {
            int count = double.IsNaN(value) ? Drop.Min : Math.Clamp((int)Math.Round(value), 0, MobDrop.MaxCount);
            if (count != Drop.Min)
            {
                _owner.UpdateDrop(Index, d => d with { Min = count });
            }
        }
    }

    public double Max
    {
        get => Drop.Max;
        set
        {
            int count = double.IsNaN(value) ? Drop.Max : Math.Clamp((int)Math.Round(value), 0, MobDrop.MaxCount);
            if (count != Drop.Max)
            {
                _owner.UpdateDrop(Index, d => d with { Max = count });
            }
        }
    }

    public ReferencePickerViewModel ItemPicker { get; }

    public ICommand RemoveCommand { get; }

    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
        ItemPicker.Refresh();
    }
}

/// <summary>Type de comportement proposé dans la liste "Ajouter un comportement".</summary>
/// <param name="Kind">Comportement.</param>
/// <param name="Label">Nom, avec la mention "(Animal)" si réservé aux animaux.</param>
public sealed record GoalKindOption(MobGoalKind Kind, string Label);

/// <summary>Valeur par défaut d'une référence absente : utile au butin, qui exige un item.</summary>
internal static class DefaultReferences
{
    public static ContentReference Bone { get; } =
        ContentReference.TryParseStorageText("minecraft:bone", out ContentReference? bone) ? bone! : throw new InvalidOperationException();
}
