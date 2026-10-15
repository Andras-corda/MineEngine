using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Graphe nodal. Selon <see cref="Kind"/>, il décrit des réactions aux événements du jeu,
/// une fonction ou une procédure (paramètres, valeurs renvoyées, variables et corps), ou
/// un type : classe (champs, constructeur, héritage, interfaces ; ses méthodes sont des
/// fonctions qui lui appartiennent), structure, interface ou énumération. Chaque propriété
/// est immuable : la remplacer est une seule modification, annulable comme les autres.
/// </summary>
public sealed class GraphAsset : Asset
{
    private NodeGraph _graph = NodeGraph.Empty;
    private GraphKind _kind = GraphKind.Events;
    private AccessLevel _access = AccessLevel.Public;
    private Guid? _ownerId;
    private bool _isStatic;
    private bool _isPure;
    private IReadOnlyList<VariableDefinition> _parameters = [];
    private IReadOnlyList<VariableDefinition> _returns = [];
    private IReadOnlyList<VariableDefinition> _variables = [];
    private Guid? _baseClassId;
    private IReadOnlyList<Guid> _interfaceIds = [];
    private IReadOnlyList<EnumConstant> _constants = [];
    private IReadOnlyList<MethodSignature> _methods = [];

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

    /// <summary>Ce que définit le graphe ; choisi à la création.</summary>
    public GraphKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    public AccessLevel Access
    {
        get => _access;
        set => SetField(ref _access, value);
    }

    /// <summary>Classe dans laquelle la définition est intégrée (méthode, type imbriqué), ou null.</summary>
    public Guid? OwnerId
    {
        get => _ownerId;
        set => SetField(ref _ownerId, value);
    }

    /// <summary>Méthode d'une classe qui ne dépend d'aucun objet (pas de "Moi").</summary>
    public bool IsStatic
    {
        get => _isStatic;
        set => SetField(ref _isStatic, value);
    }

    /// <summary>Fonction sans effet sur le jeu : son nœud d'appel n'a pas de fil d'exécution.</summary>
    public bool IsPure
    {
        get => _isPure;
        set => SetField(ref _isPure, value);
    }

    /// <summary>Paramètres d'une fonction, d'une procédure ou du constructeur d'une classe.</summary>
    public IReadOnlyList<VariableDefinition> Parameters
    {
        get => _parameters;
        set => SetField(ref _parameters, value ?? []);
    }

    /// <summary>Valeurs renvoyées par une fonction.</summary>
    public IReadOnlyList<VariableDefinition> Returns
    {
        get => _returns;
        set => SetField(ref _returns, value ?? []);
    }

    /// <summary>
    /// Variables d'une fonction ou d'une procédure (locales, ou globales si conservées d'un
    /// appel à l'autre), variables d'un graphe d'événements (globales), champs d'une classe
    /// ou d'une structure.
    /// </summary>
    public IReadOnlyList<VariableDefinition> Variables
    {
        get => _variables;
        set => SetField(ref _variables, value ?? []);
    }

    /// <summary>Classe parente d'une classe, ou null.</summary>
    public Guid? BaseClassId
    {
        get => _baseClassId;
        set => SetField(ref _baseClassId, value);
    }

    /// <summary>Interfaces implémentées par une classe.</summary>
    public IReadOnlyList<Guid> InterfaceIds
    {
        get => _interfaceIds;
        set => SetField(ref _interfaceIds, value ?? []);
    }

    /// <summary>Valeurs d'une énumération.</summary>
    public IReadOnlyList<EnumConstant> Constants
    {
        get => _constants;
        set => SetField(ref _constants, value ?? []);
    }

    /// <summary>Méthodes déclarées par une interface.</summary>
    public IReadOnlyList<MethodSignature> Methods
    {
        get => _methods;
        set => SetField(ref _methods, value ?? []);
    }

    /// <summary>Méthode d'une classe qui s'exécute sur un objet (a accès à "Moi" et aux champs).</summary>
    public bool IsInstanceMember => _ownerId is not null && _kind.IsCallable() && !_isStatic;

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (GraphNode node in Graph.Nodes)
        {
            if (node.Target is { } target)
            {
                yield return new AssetReference(target, AssetReferenceKind.Definition, Tr.F("nœud {0}", node));
            }

            if (NodeCatalog.Find(node.Kind) is not { } info)
            {
                continue;
            }

            foreach (PinInfo pin in info.Inputs.Where(p => p.Type.IsContentReference))
            {
                if (Graph.InputConnection(node.Id, pin.Name) is null
                    && ContentReference.TryParseStorageText(node.GetValue(pin.Name), out ContentReference? reference)
                    && reference!.Kind == ContentReferenceKind.Asset)
                {
                    AssetReferenceKind kind = pin.Type.Kind switch
                    {
                        PinType.Block => AssetReferenceKind.Block,
                        PinType.Sound => AssetReferenceKind.Sound,
                        _ => AssetReferenceKind.Item,
                    };
                    yield return new AssetReference(reference.AssetId, kind, Tr.F("nœud « {0} »", info.Title));
                }
            }
        }

        foreach (Guid type in Parameters.Concat(Returns).Concat(Variables).Concat(Methods.SelectMany(m => m.Parameters.Concat(m.Returns)))
                     .Select(v => v.Type.Definition).OfType<Guid>().Distinct())
        {
            yield return new AssetReference(type, AssetReferenceKind.Definition, Tr.T("type d'une variable"));
        }

        if (BaseClassId is { } baseClass)
        {
            yield return new AssetReference(baseClass, AssetReferenceKind.Definition, Tr.T("classe parente"));
        }

        foreach (Guid implemented in InterfaceIds)
        {
            yield return new AssetReference(implemented, AssetReferenceKind.Definition, Tr.T("interface implémentée"));
        }

        if (OwnerId is { } owner)
        {
            yield return new AssetReference(owner, AssetReferenceKind.Definition, Tr.T("classe propriétaire"));
        }
    }

    /// <summary>
    /// Sans les autres définitions du projet, seul un graphe d'événements peut être vérifié ;
    /// le registre utilise <see cref="Validate(DiagnosticBag, ProgramModel)"/>.
    /// </summary>
    public override void Validate(DiagnosticBag diagnostics) => Validate(diagnostics, new ProgramModel([this]));

    public void Validate(DiagnosticBag diagnostics, ProgramModel program)
    {
        ArgumentNullException.ThrowIfNull(program);

        // Le nom affiché d'un graphe ne sert qu'à l'éditeur : pas d'avertissement s'il est vide.
        string source = ToString();
        foreach (GraphIssue issue in ProgramValidator.Validate(this, program))
        {
            diagnostics.Add(new Diagnostic(issue.Severity, issue.Message, source, Id));
        }
    }
}
