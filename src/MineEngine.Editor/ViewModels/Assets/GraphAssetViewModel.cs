using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Diagnostics;
using MineEngine.Minecraft.Java;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Inspector d'un graphe nodal : nom, identifiant et résumé (événements, nœuds,
/// problèmes). Le graphe lui-même se modifie dans l'éditeur nodal, ouvert dans un onglet.
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

    /// <summary>Événements auxquels réagit le graphe ("Clic droit sur un bloc", ...).</summary>
    public IReadOnlyList<string> Events =>
        [.. Graph.Graph.Nodes.Where(n => n.Info.Category == NodeCategory.Event).Select(n => n.Info.Title)];

    public bool HasEvents => Events.Count > 0;

    /// <summary>Problèmes du graphe (erreurs et avertissements), tels qu'affichés dans Diagnostics.</summary>
    public IReadOnlyList<string> Problems =>
        [.. GraphValidator.Validate(Graph.Graph).Where(i => i.Severity != DiagnosticSeverity.Info).Select(i => i.Message)];

    public bool HasProblems => Problems.Count > 0;

    /// <summary>Nom de la classe Java générée ("magic_altar" devient "MagicAltarBehavior").</summary>
    public string JavaClassName => JavaModNaming.BehaviorClassName(Model.ResourceId);

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
                OnPropertyChanged(nameof(Problems));
                OnPropertyChanged(nameof(HasProblems));
                break;
            case nameof(GraphAsset.ResourceId):
                OnPropertyChanged(nameof(JavaClassName));
                break;
        }
    }
}
