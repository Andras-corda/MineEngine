using System.Windows;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.ViewModels.Graphs;

public enum PinDirection
{
    Input,
    Output,
}

/// <summary>Éditeur affiché sur une entrée non reliée.</summary>
public enum PinEditorKind
{
    None,
    Number,
    Text,
    Boolean,
    Reference,

    /// <summary>Liste des valeurs d'une énumération.</summary>
    Choice,

    /// <summary>Text typed or chosen among values of the project (particle systems, their parameters).</summary>
    Suggest,
}

/// <summary>
/// Une broche d'un nœud. Une entrée non reliée montre un champ pour sa valeur ; la
/// position de la broche sur le canevas (mesurée par la vue) sert à dessiner les fils.
/// </summary>
public sealed class GraphPinViewModel : ObservableObject
{
    private bool _isConnected;
    private Point _offset;
    private string _numberText;

    public GraphPinViewModel(GraphNodeViewModel node, PinInfo info, PinDirection direction, ReferencePickerViewModel? picker)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Info = info ?? throw new ArgumentNullException(nameof(info));
        Direction = direction;
        Picker = picker;
        _numberText = node.Model.GetValue(info.Name) ?? string.Empty;
    }

    public GraphNodeViewModel Node { get; }

    public PinInfo Info { get; }

    public PinDirection Direction { get; }

    public string Name => Info.Name;

    public string Label => Info.Label;

    public DataType Type => Info.Type;

    public bool IsExec => Info.Type.IsExec;

    public bool IsInput => Direction == PinDirection.Input;

    /// <summary>Faux pour un champ du nœud qui n'accepte pas de fil (valeur d'une constante).</summary>
    public bool CanConnect => Info.Connectable;

    public string ToolTipText => IsExec ? Tr.T("Execution") : $"{Label} ({Node.Document.TypeLabel(Type)})";

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(Editor));
                OnPropertyChanged(nameof(HasEditor));
            }
        }
    }

    public PinEditorKind Editor => !IsInput || IsConnected || !Type.HasInlineValue
        ? PinEditorKind.None
        : Type.Kind switch
        {
            PinType.Number => PinEditorKind.Number,
            PinType.Text when Info.Suggest != PinSuggestion.None => PinEditorKind.Suggest,
            PinType.Text or PinType.Decimal or PinType.Direction or PinType.Char or PinType.Long or PinType.Float or PinType.Byte
                or PinType.Identifier or PinType.Fluid or PinType.EquipmentSlot or PinType.Hand or PinType.Rarity => PinEditorKind.Text,
            PinType.Boolean => PinEditorKind.Boolean,
            PinType.Enum => PinEditorKind.Choice,
            _ => PinEditorKind.Reference,
        };

    /// <summary>Valeurs d'une entrée d'énumération.</summary>
    public IReadOnlyList<EnumConstant> Choices => Info.Choices;

    /// <summary>Valeur d'énumération choisie sur le nœud.</summary>
    public EnumConstant? ChoiceValue
    {
        get => Guid.TryParse(Node.Model.GetValue(Name) ?? Info.DefaultValue, out Guid id) ? Choices.FirstOrDefault(c => c.Id == id) : null;
        set
        {
            if (value is not null)
            {
                Node.Document.SetValue(Node, Name, value.Id.ToString("D"));
            }
        }
    }

    public bool HasEditor => Editor != PinEditorKind.None;

    /// <summary>Choix d'un item, d'un bloc ou d'un son (entrées de ces types).</summary>
    public ReferencePickerViewModel? Picker { get; }

    /// <summary>Texte saisi pour un nombre ; appliqué seulement s'il est valide.</summary>
    public string NumberText
    {
        get => _numberText;
        set
        {
            if (SetProperty(ref _numberText, value ?? string.Empty) && GraphValidator.TryParseNumber(_numberText, out int number))
            {
                Node.Document.SetValue(Node, Name, number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    public string TextValue
    {
        get => Node.Model.GetValue(Name) ?? string.Empty;
        set => Node.Document.SetValue(Node, Name, value ?? string.Empty);
    }

    /// <summary>
    /// Values offered by a text input (<see cref="PinInfo.Suggest"/>): the identifiers of the particle
    /// systems or of the mobs of the project, or the user parameters of the system chosen on the node.
    /// </summary>
    public IReadOnlyList<string> Suggestions
    {
        get
        {
            if (Info.Suggest == PinSuggestion.Mob)
            {
                return [.. Node.Document.Assets.OfType<MobAsset>().Select(m => m.ResourceId.Value).Order(StringComparer.Ordinal)];
            }

            if (Info.Suggest == PinSuggestion.Block)
            {
                return [.. Node.Document.Assets.OfType<BlockAsset>().Select(b => b.ResourceId.Value).Order(StringComparer.Ordinal)];
            }

            IEnumerable<SparkleAsset> systems = Node.Document.Assets.OfType<SparkleAsset>();
            if (Info.Suggest == PinSuggestion.SparkleSystem)
            {
                return [.. systems.Select(s => s.ResourceId.Value).Order(StringComparer.Ordinal)];
            }

            string chosen = (Node.Model.GetValue("system") ?? string.Empty).Trim();
            chosen = chosen[(chosen.IndexOf(':', StringComparison.Ordinal) + 1)..];
            // The parameters of the chosen system; of all the systems while none is chosen.
            SparkleAsset? system = systems.FirstOrDefault(s => s.ResourceId.Value == chosen);
            IEnumerable<SparkleAsset> source = system is null ? systems : [system];
            return [.. source.SelectMany(s => s.UserParameters).Select(p => p.Name).Distinct().Order(StringComparer.Ordinal)];
        }
    }

    public bool BoolValue
    {
        get => bool.TryParse(Node.Model.GetValue(Name), out bool flag) && flag;
        set => Node.Document.SetValue(Node, Name, value ? "true" : "false");
    }

    /// <summary>Position du centre de la broche par rapport au coin du nœud (mesurée par la vue).</summary>
    public Point Offset
    {
        get => _offset;
        set
        {
            if (SetProperty(ref _offset, value))
            {
                OnPropertyChanged(nameof(Anchor));
            }
        }
    }

    /// <summary>Position du centre de la broche sur le canevas, là où s'accroche le fil.</summary>
    public Point Anchor => new(Node.X + _offset.X, Node.Y + _offset.Y);

    /// <summary>Le nœud a bougé : les fils accrochés doivent suivre.</summary>
    internal void NotifyAnchorMoved() => OnPropertyChanged(nameof(Anchor));

    /// <summary>La valeur du nœud a changé (saisie, annulation) : les champs se remettent à jour.</summary>
    internal void Refresh()
    {
        string current = Node.Model.GetValue(Name) ?? string.Empty;
        if (!GraphValidator.TryParseNumber(_numberText, out int typed) || typed.ToString(System.Globalization.CultureInfo.InvariantCulture) != current)
        {
            _numberText = current;
            OnPropertyChanged(nameof(NumberText));
        }

        OnPropertyChanged(nameof(TextValue));
        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(ChoiceValue));
        if (Info.Suggest == PinSuggestion.SparkleParameter)
        {
            // The parameters offered follow the system chosen on the node.
            OnPropertyChanged(nameof(Suggestions));
        }

        Picker?.Refresh();
    }

    public override string ToString() => $"{Node.Title}.{Label}";
}
