using MineEngine.Core.Diagnostics;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Vérifie une définition de programme avec le reste du projet : noms des paramètres, des
/// variables, des champs et des constantes ; classe propriétaire ; héritage (pas de boucle,
/// parente qui est une classe) ; interfaces implémentées entièrement ; redéfinitions de
/// même signature ; types utilisés existants et visibles ; puis le corps du graphe.
/// </summary>
public static class ProgramValidator
{
    /// <summary>
    /// Noms des types déjà utilisés par le code généré (et par Java) : une classe du projet
    /// qui porterait l'un d'eux serait confondue avec lui dans le code Java.
    /// </summary>
    public static readonly IReadOnlySet<string> ReservedTypeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "Object", "String", "Math", "Integer", "Boolean", "System", "Override", "Record", "Enum", "List", "Map",
        "Player", "Level", "BlockPos", "Item", "Items", "ItemStack", "Block", "Blocks", "SoundEvent", "SoundSource",
        "Component", "ItemEntity", "InteractionHand", "BuiltInRegistries", "ResourceLocation", "MinecraftForge", "NeoForge",
        "ModItems", "ModBlocks", "ModCreativeTabs", "ModEntities", "ModSounds", "ModEntityRenderers",
    };

    public static IReadOnlyList<GraphIssue> Validate(GraphAsset graph, ProgramModel program)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(program);
        var issues = new List<GraphIssue>();
        void Error(string message) => issues.Add(new GraphIssue(DiagnosticSeverity.Error, message, null));
        void Warning(string message) => issues.Add(new GraphIssue(DiagnosticSeverity.Warning, message, null));

        ValidateOwner(graph, program, Error);

        if (graph.Kind.IsType() && ProgramNames.Identifier(ProgramModel.Name(graph), upperFirst: true) is { } typeName
            && (typeName.Length == 0 || ReservedTypeNames.Contains(typeName)))
        {
            Error(typeName.Length == 0
                ? "Donnez un nom à ce type (lettres ou chiffres)."
                : $"« {typeName} » est déjà le nom d'un type de Minecraft ou du code généré : choisissez un autre nom.");
        }

        if (graph.Kind.IsCallable() && ProgramModel.MemberName(graph).Length == 0)
        {
            Error($"Donnez un nom à cette {graph.Kind.Label().ToLowerInvariant()} (lettres ou chiffres).");
        }

        ValidateNames("paramètre", graph.Parameters.Select(p => p.Name), Error);
        if (graph.Kind.IsCallable() || graph.Kind == GraphKind.Events)
        {
            // Paramètres et variables d'une fonction sont tous des variables du code : pas deux fois le même nom.
            ValidateNames("paramètre ou de variable", graph.Parameters.Concat(graph.Variables).Select(p => p.Name), Error);
        }

        ValidateNames("valeur renvoyée", graph.Returns.Select(p => p.Name), Error);
        ValidateNames(graph.Kind is GraphKind.Class or GraphKind.Struct ? "champ" : "variable", graph.Variables.Select(p => p.Name), Error);
        ValidateNames("constante", graph.Constants.Select(c => c.Name), Error, constant: true);
        foreach (MethodSignature method in graph.Methods)
        {
            ValidateNames($"paramètre de « {method.Name} »", method.Parameters.Select(p => p.Name), Error);
        }

        ValidateNames("méthode", graph.Methods.Select(m => m.Name), Error);
        foreach (MethodSignature method in graph.Methods.Where(m => m.Returns.Count > 1))
        {
            Error($"« {method.Name} » : une méthode d'interface renvoie au plus une valeur.");
        }

        foreach (VariableDefinition variable in graph.Parameters.Concat(graph.Returns).Concat(graph.Variables)
                     .Concat(graph.Methods.SelectMany(m => m.Parameters.Concat(m.Returns))))
        {
            ValidateType(variable, graph, program, Error);
        }

        switch (graph.Kind)
        {
            case GraphKind.Function when graph.Returns.Count == 0:
                Warning("Cette fonction ne renvoie aucune valeur : ajoutez-en une, ou faites-en une procédure.");
                break;
            case GraphKind.Class:
                ValidateClass(graph, program, Error);
                break;
            case GraphKind.Enum when graph.Constants.Count == 0:
                Error("Une énumération a besoin d'au moins une valeur.");
                break;
            case GraphKind.Struct when graph.Variables.Count == 0:
                Warning("Cette structure n'a aucun champ.");
                break;
        }

        if (graph.Kind.IsCallable() && program.Find(graph.OwnerId) is { Kind: GraphKind.Class } owner && !graph.IsStatic)
        {
            ValidateOverride(graph, owner, program, Error);
        }

        if (graph.Kind.HasBody())
        {
            issues.AddRange(GraphValidator.Validate(graph.Graph, program.ContextFor(graph)));
            if (graph.Kind == GraphKind.Class)
            {
                ValidateSuperCall(graph, program, issues);
            }
        }

        return issues;
    }

    private static void ValidateOwner(GraphAsset graph, ProgramModel program, Action<string> error)
    {
        if (graph.OwnerId is not { } ownerId)
        {
            if (graph.IsStatic)
            {
                error("« Statique » ne concerne que les méthodes d'une classe.");
            }

            return;
        }

        if (program.Find(ownerId) is not { } owner)
        {
            error("La classe à laquelle cette définition était intégrée a été supprimée.");
        }
        else if (owner.Kind != GraphKind.Class || graph.Kind == GraphKind.Events)
        {
            error("Seules les fonctions, les procédures et les types peuvent être intégrés, et seulement à une classe.");
        }
        else if (program.IsInside(owner, graph))
        {
            error("Une définition ne peut pas être intégrée à elle-même.");
        }
    }

    private static void ValidateNames(string role, IEnumerable<string> names, Action<string> error, bool constant = false)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            string identifier = constant ? ProgramNames.Constant(name) : ProgramNames.Identifier(name);
            if (identifier.Length == 0)
            {
                error($"Un nom de {role} est vide ou sans lettre ni chiffre.");
            }
            else if (!seen.Add(identifier))
            {
                error($"Deux noms de {role} désignent la même chose : « {name} ».");
            }
        }
    }

    private static void ValidateType(VariableDefinition variable, GraphAsset graph, ProgramModel program, Action<string> error)
    {
        if (variable.Type.Definition is not { } id)
        {
            return;
        }

        if (program.Find(id) is not { } type || !type.Kind.IsType())
        {
            error($"« {variable.Name} » utilise un type qui a été supprimé.");
        }
        else if (!program.IsVisible(type, graph))
        {
            error($"« {variable.Name} » utilise le type {ProgramModel.Name(type)}, qui n'est pas accessible ici (privé).");
        }
        else if (variable.Type.Kind == PinType.Enum && !string.IsNullOrEmpty(variable.DefaultValue)
                 && !type.Constants.Any(c => c.Id.ToString("D") == variable.DefaultValue))
        {
            error($"« {variable.Name} » : la valeur par défaut n'existe plus dans {ProgramModel.Name(type)}.");
        }
    }

    private static void ValidateClass(GraphAsset graph, ProgramModel program, Action<string> error)
    {
        if (graph.BaseClassId is { } baseId)
        {
            if (program.Find(baseId) is not { } parent)
            {
                error("La classe parente a été supprimée.");
            }
            else if (parent.Kind != GraphKind.Class)
            {
                error($"{ProgramModel.Name(parent)} n'est pas une classe : on ne peut hériter que d'une classe (une interface s'implémente).");
            }
            else if (program.Ancestors(parent).Any(a => a.Id == graph.Id) || parent.Id == graph.Id)
            {
                error("Héritage circulaire : la classe hérite d'elle-même.");
            }
            else if (!program.IsVisible(parent, graph))
            {
                error($"La classe parente {ProgramModel.Name(parent)} n'est pas accessible ici (privée).");
            }
        }

        List<GraphAsset> methods = [.. program.Methods(graph).Where(m => !m.IsStatic)];
        foreach (Guid interfaceId in graph.InterfaceIds.Distinct())
        {
            if (program.Find(interfaceId) is not { Kind: GraphKind.Interface } contract)
            {
                error("Une interface implémentée a été supprimée ou n'est pas une interface.");
                continue;
            }

            foreach (MethodSignature signature in contract.Methods)
            {
                GraphAsset? implementation = methods.FirstOrDefault(m => signature.Matches(ProgramModel.MemberName(m), m.Parameters, m.Returns));
                if (implementation is null)
                {
                    error($"La classe doit avoir une méthode « {signature.Name} » (interface {ProgramModel.Name(contract)}), " +
                          "avec les mêmes types de paramètres et de valeurs renvoyées.");
                }
                else if (implementation.Access != AccessLevel.Public)
                {
                    error($"« {ProgramModel.Name(implementation)} » implémente l'interface {ProgramModel.Name(contract)} : elle doit être publique.");
                }
            }
        }
    }

    /// <summary>Une méthode qui porte le nom d'une méthode d'une classe parente la redéfinit : même signature exigée.</summary>
    private static void ValidateOverride(GraphAsset method, GraphAsset owner, ProgramModel program, Action<string> error)
    {
        string name = ProgramModel.MemberName(method);
        foreach (GraphAsset parent in program.Ancestors(owner))
        {
            if (program.Members(parent).FirstOrDefault(m => m.Kind.IsCallable() && !m.IsStatic && ProgramModel.MemberName(m) == name) is not { } overridden)
            {
                continue;
            }

            bool sameSignature = overridden.Kind == method.Kind
                                 && overridden.Parameters.Select(p => p.Type).SequenceEqual(method.Parameters.Select(p => p.Type))
                                 && overridden.Returns.Select(p => p.Type).SequenceEqual(method.Returns.Select(p => p.Type));
            if (!sameSignature)
            {
                error($"« {ProgramModel.Name(method)} » porte le nom d'une méthode de {ProgramModel.Name(parent)} : " +
                      "pour la redéfinir, gardez les mêmes paramètres et valeurs renvoyées (sinon, changez de nom).");
            }
            else if (overridden.Access == AccessLevel.Private)
            {
                error($"« {ProgramModel.Name(method)} » redéfinit une méthode privée de {ProgramModel.Name(parent)} : rendez-la protégée ou publique.");
            }
            else if (method.Access > overridden.Access)
            {
                error($"« {ProgramModel.Name(method)} » redéfinit une méthode {overridden.Access.Label().ToLowerInvariant()} : elle ne peut pas être moins accessible.");
            }

            return;
        }
    }

    /// <summary>Le constructeur parent, s'il a des paramètres, doit être appelé juste après l'entrée.</summary>
    private static void ValidateSuperCall(GraphAsset graph, ProgramModel program, List<GraphIssue> issues)
    {
        NodeGraph body = graph.Graph;
        GraphNode? entry = body.Nodes.FirstOrDefault(n => n.Kind == GraphNodeKind.FunctionEntry);
        GraphNode? first = entry is null ? null : body.NextNode(entry.Id, NodeInfo.ExecThen);
        foreach (GraphNode super in body.Nodes.Where(n => n.Kind == GraphNodeKind.SuperConstructor && n.Id != first?.Id))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error, "Le constructeur parent doit suivre directement l'entrée du constructeur.", super.Id));
        }

        if (program.Find(graph.BaseClassId) is { Kind: GraphKind.Class, Parameters.Count: > 0 } parent
            && first?.Kind != GraphNodeKind.SuperConstructor)
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error,
                $"Le constructeur de {ProgramModel.Name(parent)} a des paramètres : reliez l'entrée à « Constructeur parent » en premier.",
                entry?.Id));
        }
    }
}
