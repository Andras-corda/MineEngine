using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.ViewModels.Graphs;

/// <summary>
/// Un nœud affiché sur le canevas. Sa position suit la souris pendant un déplacement
/// et n'est écrite dans le graphe (étape annulable) qu'au relâchement.
/// </summary>
public sealed class GraphNodeViewModel : ObservableObject
{
    private GraphNode _model;
    private double _x;
    private double _y;
    private bool _isSelected;
    private DiagnosticSeverity? _issueSeverity;
    private string? _issueText;

    /// <param name="info">Description du nœud dans ce graphe (les broches d'un appel suivent la fonction appelée).</param>
    public GraphNodeViewModel(GraphDocumentViewModel document, GraphNode model, NodeInfo info, AssetEditingContext context)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _x = model.X;
        _y = model.Y;

        if (info.HasExecInput)
        {
            ExecInput = new GraphPinViewModel(this, new PinInfo(NodeInfo.ExecInput, string.Empty, DataType.Exec), PinDirection.Input, null);
        }

        if (info.Flow is NodeFlow.Start or NodeFlow.Step)
        {
            ExecOutput = new GraphPinViewModel(this, info.ExecOutputs[0], PinDirection.Output, null);
        }

        Inputs = [.. info.Inputs.Select(p => new GraphPinViewModel(this, p, PinDirection.Input, CreatePicker(p, context)))];
        Outputs =
        [
            .. info.Flow == NodeFlow.Branch ? info.ExecOutputs.Select(p => new GraphPinViewModel(this, p, PinDirection.Output, null)) : [],
            .. info.Outputs.Select(p => new GraphPinViewModel(this, p, PinDirection.Output, null)),
        ];
    }

    public GraphDocumentViewModel Document { get; }

    public GraphNode Model => _model;

    public Guid Id => _model.Id;

    public NodeInfo Info { get; }

    /// <summary>Le nœud d'entrée d'une fonction ne se supprime pas : le graphe n'aurait plus de début.</summary>
    public bool IsEntry => _model.Kind == GraphNodeKind.FunctionEntry;

    public string Title => Info.Title;

    public NodeCategory Category => Info.Category;

    public string Description => Info.Description;

    /// <summary>Entrée d'exécution, dans l'en-tête (conditions et actions).</summary>
    public GraphPinViewModel? ExecInput { get; }

    /// <summary>Sortie d'exécution "la suite", dans l'en-tête (événements et actions).</summary>
    public GraphPinViewModel? ExecOutput { get; }

    public IReadOnlyList<GraphPinViewModel> Inputs { get; }

    /// <summary>Sorties Vrai / Faux d'une condition, puis sorties de valeur.</summary>
    public IReadOnlyList<GraphPinViewModel> Outputs { get; }

    public IEnumerable<GraphPinViewModel> AllPins =>
        new[] { ExecInput, ExecOutput }.OfType<GraphPinViewModel>().Concat(Inputs).Concat(Outputs);

    public double X
    {
        get => _x;
        set
        {
            if (SetProperty(ref _x, value))
            {
                MoveAnchors();
            }
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (SetProperty(ref _y, value))
            {
                MoveAnchors();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasError => _issueSeverity == DiagnosticSeverity.Error;

    public bool HasWarning => _issueSeverity == DiagnosticSeverity.Warning;

    /// <summary>Problèmes de ce nœud (info-bulle de son contour orange ou rouge).</summary>
    public string? IssueText => _issueText;

    public GraphPinViewModel? FindPin(string name, bool input) =>
        AllPins.FirstOrDefault(p => p.Name == name && p.IsInput == input);

    /// <summary>Le graphe a changé (modification, annulation) : position et valeurs suivent.</summary>
    internal void Update(GraphNode model)
    {
        bool valuesChanged = !ReferenceEquals(model.Values, _model.Values);
        _model = model;
        X = model.X;
        Y = model.Y;
        if (valuesChanged)
        {
            foreach (GraphPinViewModel pin in Inputs)
            {
                pin.Refresh();
            }
        }
    }

    internal void SetIssues(IReadOnlyList<GraphIssue> issues)
    {
        // Une simple information ("ne déclenche encore rien") n'entoure pas le nœud.
        DiagnosticSeverity? severity = issues.Count == 0 ? null : issues.Max(i => i.Severity);
        severity = severity == DiagnosticSeverity.Info ? null : severity;
        string? text = issues.Count == 0 ? null : string.Join("\n", issues.Select(i => i.Message));
        if (severity == _issueSeverity && text == _issueText)
        {
            return;
        }

        _issueSeverity = severity;
        _issueText = text;
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(IssueText));
    }

    private void MoveAnchors()
    {
        foreach (GraphPinViewModel pin in AllPins)
        {
            pin.NotifyAnchorMoved();
        }
    }

    private ReferencePickerViewModel? CreatePicker(PinInfo pin, AssetEditingContext context)
    {
        if (!pin.Type.IsContentReference)
        {
            return null;
        }

        ReferenceDomain domain = pin.Type.Kind switch
        {
            PinType.Block => ReferenceDomain.Block,
            PinType.Sound => ReferenceDomain.Sound,
            _ => ReferenceDomain.Item,
        };
        return new ReferencePickerViewModel(
            context,
            domain,
            () => ContentReference.TryParseStorageText(_model.GetValue(pin.Name), out ContentReference? reference) ? reference : null,
            reference => Document.SetValue(this, pin.Name, reference?.ToStorageText() ?? string.Empty));
    }

    public override string ToString() => Title;
}
