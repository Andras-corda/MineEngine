using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Graphe nodal : des comportements déclenchés par des événements du jeu (clic droit,
/// bloc cassé...), décrits par des nœuds reliés entre eux. Le graphe est immuable ; le
/// remplacer est une seule modification, annulable comme les autres.
/// </summary>
public sealed class GraphAsset : Asset
{
    private NodeGraph _graph = NodeGraph.Empty;

    public GraphAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Graph;

    public NodeGraph Graph
    {
        get => _graph;
        set => SetField(ref _graph, value ?? throw new ArgumentNullException(nameof(value)));
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (GraphNode node in Graph.Nodes)
        {
            foreach (PinInfo pin in node.Info.Inputs.Where(p => p.Type.IsContentReference()))
            {
                if (Graph.InputConnection(node.Id, pin.Name) is null
                    && ContentReference.TryParseStorageText(node.GetValue(pin.Name), out ContentReference? reference)
                    && reference!.Kind == ContentReferenceKind.Asset)
                {
                    AssetReferenceKind kind = pin.Type switch
                    {
                        PinType.Block => AssetReferenceKind.Block,
                        PinType.Sound => AssetReferenceKind.Sound,
                        _ => AssetReferenceKind.Item,
                    };
                    yield return new AssetReference(reference.AssetId, kind, $"nœud « {node.Info.Title} »");
                }
            }
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        // Le nom affiché d'un graphe ne sert qu'à l'éditeur : pas d'avertissement s'il est vide.
        string source = ToString();
        foreach (GraphIssue issue in GraphValidator.Validate(Graph))
        {
            diagnostics.Add(new Diagnostic(issue.Severity, issue.Message, source, Id));
        }
    }
}
