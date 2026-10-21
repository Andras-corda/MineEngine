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
                ? Tr.T("Give this type a name (letters or digits).")
                : Tr.F("\"{0}\" is already the name of a Minecraft type or of generated code: choose another name.", typeName));
        }

        if (graph.Kind.IsCallable() && ProgramModel.MemberName(graph).Length == 0)
        {
            Error(Tr.F("Give this {0} a name (letters or digits).", graph.Kind.Label().ToLowerInvariant()));
        }

        ValidateNames(Tr.T("parameter"), graph.Parameters.Select(p => p.Name), Error);
        if (graph.Kind.IsCallable() || graph.Kind == GraphKind.Events)
        {
            // Paramètres et variables d'une fonction sont tous des variables du code : pas deux fois le même nom.
            ValidateNames(Tr.T("parameter or variable"), graph.Parameters.Concat(graph.Variables).Select(p => p.Name), Error);
        }

        ValidateNames(Tr.T("return value"), graph.Returns.Select(p => p.Name), Error);
        ValidateNames(graph.Kind is GraphKind.Class or GraphKind.Struct ? "champ" : "variable", graph.Variables.Select(p => p.Name), Error);
        ValidateNames("constante", graph.Constants.Select(c => c.Name), Error, constant: true);
        foreach (MethodSignature method in graph.Methods)
        {
            ValidateNames(Tr.F("\"{0}\" parameter", method.Name), method.Parameters.Select(p => p.Name), Error);
        }

        ValidateNames(Tr.T("method"), graph.Methods.Select(m => m.Name), Error);
        foreach (MethodSignature method in graph.Methods.Where(m => m.Returns.Count > 1))
        {
            Error(Tr.F("\"{0}\": an interface method returns at most one value.", method.Name));
        }

        foreach (VariableDefinition variable in graph.Parameters.Concat(graph.Returns).Concat(graph.Variables)
                     .Concat(graph.Methods.SelectMany(m => m.Parameters.Concat(m.Returns))))
        {
            ValidateType(variable, graph, program, Error);
        }

        switch (graph.Kind)
        {
            case GraphKind.Function when graph.Returns.Count == 0:
                Warning(Tr.T("This function returns no value: add one, or make it a procedure."));
                break;
            case GraphKind.Class:
                ValidateClass(graph, program, Error);
                break;
            case GraphKind.Enum when graph.Constants.Count == 0:
                Error(Tr.T("An enumeration needs at least one value."));
                break;
            case GraphKind.Struct when graph.Variables.Count == 0:
                Warning(Tr.T("This struct has no field."));
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
                error(Tr.T("\"Static\" only applies to the methods of a class."));
            }

            return;
        }

        if (program.Find(ownerId) is not { } owner)
        {
            error(Tr.T("The class this definition was nested in was deleted."));
        }
        else if (owner.Kind == GraphKind.Library)
        {
            // A library only holds functions and procedures, public and static (see ProjectViewModel.AddGraph).
            if (!graph.Kind.IsCallable())
            {
                error(Tr.T("A function library only holds functions and procedures."));
            }
            else if (graph.Access != AccessLevel.Public || !graph.IsStatic)
            {
                error(Tr.T("The functions of a library are public and static."));
            }
        }
        else if (graph.Kind == GraphKind.Library)
        {
            error(Tr.T("A function library cannot be nested in a class."));
        }
        else if (owner.Kind != GraphKind.Class || graph.Kind == GraphKind.Events)
        {
            error(Tr.T("Only functions, procedures and types can be nested, and only in a class."));
        }
        else if (program.IsInside(owner, graph))
        {
            error(Tr.T("A definition cannot be nested in itself."));
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
                error(Tr.F("A {0} name is empty or has no letter or digit.", role));
            }
            else if (!seen.Add(identifier))
            {
                error(Tr.F("Two {0} names refer to the same thing: \"{1}\".", role, name));
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
            error(Tr.F("\"{0}\" uses a type that was deleted.", variable.Name));
        }
        else if (!program.IsVisible(type, graph))
        {
            error(Tr.F("\"{0}\" uses the type {1}, which is not accessible here (private).", variable.Name, ProgramModel.Name(type)));
        }
        else if (variable.Type.Kind == PinType.Enum && !string.IsNullOrEmpty(variable.DefaultValue)
                 && !type.Constants.Any(c => c.Id.ToString("D") == variable.DefaultValue))
        {
            error(Tr.F("\"{0}\": the default value no longer exists in {1}.", variable.Name, ProgramModel.Name(type)));
        }
    }

    private static void ValidateClass(GraphAsset graph, ProgramModel program, Action<string> error)
    {
        if (graph.BaseClassId is { } baseId)
        {
            if (program.Find(baseId) is not { } parent)
            {
                error(Tr.T("The parent class was deleted."));
            }
            else if (parent.Kind != GraphKind.Class)
            {
                error(Tr.F("{0} is not a class: you can only inherit from a class (an interface is implemented).", ProgramModel.Name(parent)));
            }
            else if (program.Ancestors(parent).Any(a => a.Id == graph.Id) || parent.Id == graph.Id)
            {
                error(Tr.T("Circular inheritance: the class inherits from itself."));
            }
            else if (!program.IsVisible(parent, graph))
            {
                error(Tr.F("The parent class {0} is not accessible here (private).", ProgramModel.Name(parent)));
            }
        }

        List<GraphAsset> methods = [.. program.Methods(graph).Where(m => !m.IsStatic)];
        foreach (Guid interfaceId in graph.InterfaceIds.Distinct())
        {
            if (program.Find(interfaceId) is not { Kind: GraphKind.Interface } contract)
            {
                error(Tr.T("An implemented interface was deleted or is not an interface."));
                continue;
            }

            foreach (MethodSignature signature in contract.Methods)
            {
                GraphAsset? implementation = methods.FirstOrDefault(m => signature.Matches(ProgramModel.MemberName(m), m.Parameters, m.Returns));
                if (implementation is null)
                {
                    error(Tr.F("The class must have a method \"{0}\" (interface {1}), ", signature.Name, ProgramModel.Name(contract)) +
                          Tr.T("with the same parameter and return types."));
                }
                else if (implementation.Access != AccessLevel.Public)
                {
                    error(Tr.F("\"{0}\" implements interface {1}: it must be public.", ProgramModel.Name(implementation), ProgramModel.Name(contract)));
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
                error(Tr.F("\"{0}\" has the name of a method of {1}: ", ProgramModel.Name(method), ProgramModel.Name(parent)) +
                      Tr.T("to override it, keep the same parameters and return values (otherwise, change its name)."));
            }
            else if (overridden.Access == AccessLevel.Private)
            {
                error(Tr.F("\"{0}\" overrides a private method of {1}: make it protected or public.", ProgramModel.Name(method), ProgramModel.Name(parent)));
            }
            else if (method.Access > overridden.Access)
            {
                error(Tr.F("\"{0}\" overrides a {1} method: it cannot be less accessible.", ProgramModel.Name(method), overridden.Access.Label().ToLowerInvariant()));
            }

            return;
        }
    }

    /// <summary>Le constructeur parent, s'il a des paramètres, doit être appelé juste après l'entrée.</summary>
    private static void ValidateSuperCall(GraphAsset graph, ProgramModel program, List<GraphIssue> issues)
    {
        NodeGraph body = Reroutes.Collapse(graph.Graph);
        GraphNode? entry = body.Nodes.FirstOrDefault(n => n.Kind == GraphNodeKind.FunctionEntry);
        GraphNode? first = entry is null ? null : body.NextNode(entry.Id, NodeInfo.ExecThen);
        foreach (GraphNode super in body.Nodes.Where(n => n.Kind == GraphNodeKind.SuperConstructor && n.Id != first?.Id))
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error, Tr.T("The parent constructor must directly follow the constructor entry."), super.Id));
        }

        if (program.Find(graph.BaseClassId) is { Kind: GraphKind.Class, Parameters.Count: > 0 } parent
            && first?.Kind != GraphNodeKind.SuperConstructor)
        {
            issues.Add(new GraphIssue(DiagnosticSeverity.Error,
                Tr.F("The {0} constructor has parameters: connect the entry to \"Parent constructor\" first.", ProgramModel.Name(parent)),
                entry?.Id));
        }
    }
}
