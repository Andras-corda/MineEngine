using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Diagnostics;
using MineEngine.Minecraft.Java;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Inspector d'un graphe nodal : nom, identifiant, ce qu'il définit et résumé (événements,
/// nœuds, problèmes). Le graphe lui-même se modifie dans l'éditeur nodal, ouvert dans un onglet.
/// </summary>
public sealed class GraphAssetViewModel : AssetViewModel
{
    public GraphAssetViewModel(GraphAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
    }

    private GraphAsset Graph => (GraphAsset)Model;

    /// <summary>Le graphe n'existe pas en jeu sous son identifiant : il donne le nom de la classe Java générée.</summary>
    public override bool HasGameId => false;

    public int NodeCount => Graph.Graph.Nodes.Length;

    public int ConnectionCount => Graph.Graph.Connections.Length;

    /// <summary>"Fonction", "Classe"... ; "Graphe d'événements" pour les réactions au jeu.</summary>
    public string KindLabel => Graph.Kind.Label();

    /// <summary>Dans le Dashboard : "Graphe" pour les événements, sinon ce que définit le graphe.</summary>
    public override string TypeLabel => Graph.Kind == GraphKind.Events ? base.TypeLabel : Graph.Kind.Label();

    public override string TypeIcon => Graph.Kind switch
    {
        GraphKind.Function => "FunctionGeometry",
        GraphKind.Procedure => "ProcedureGeometry",
        GraphKind.Class => "ClassGeometry",
        GraphKind.Struct => "StructGeometry",
        GraphKind.Interface => "InterfaceGeometry",
        GraphKind.Enum => "EnumGeometry",
        _ => base.TypeIcon,
    };

    /// <summary>Événements auxquels réagit le graphe ("Clic droit sur un bloc", ...).</summary>
    public IReadOnlyList<string> Events =>
        [.. Graph.Graph.Nodes.Select(n => NodeCatalog.Find(n.Kind)).OfType<NodeInfo>().Where(i => i.Category == NodeCategory.Event).Select(i => i.Title)];

    public bool HasEvents => Events.Count > 0;

    public bool IsEventGraph => Graph.Kind == GraphKind.Events;

    public bool ShowNoEventHint => IsEventGraph && !HasEvents;

    /// <summary>Problèmes du graphe (erreurs et avertissements), tels qu'affichés dans Diagnostics.</summary>
    public IReadOnlyList<string> Problems =>
        [.. ProgramValidator.Validate(Graph, ProgramModel.From(Context.Project.Assets))
            .Where(i => i.Severity != DiagnosticSeverity.Info).Select(i => i.Message)];

    public bool HasProblems => Problems.Count > 0;

    /// <summary>Nom de la classe Java générée ("magic_altar" devient "MagicAltarBehavior", "Coffre maudit" devient "CoffreMaudit").</summary>
    public string JavaClassName => Graph.Kind == GraphKind.Events
        ? JavaModNaming.BehaviorClassName(Model.ResourceId)
        : ProgramNames.Identifier(ProgramModel.Name(Graph), upperFirst: true);

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(GraphAsset.Graph):
                OnPropertyChanged(nameof(NodeCount));
                OnPropertyChanged(nameof(ConnectionCount));
                OnPropertyChanged(nameof(Events));
                OnPropertyChanged(nameof(HasEvents));
                OnPropertyChanged(nameof(ShowNoEventHint));
                OnPropertyChanged(nameof(Problems));
                OnPropertyChanged(nameof(HasProblems));
                break;
            case nameof(GraphAsset.ResourceId) or nameof(GraphAsset.DisplayName):
                OnPropertyChanged(nameof(JavaClassName));
                break;
        }
    }
}
