using MineEngine.Assets.Graphs;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Partie "programmation" du code Java : chaque définition hors de toute classe devient
/// un fichier du package "code" ; les méthodes et les types intégrés à une classe y sont
/// écrits comme membres. Toute fonction, procédure, méthode et tout constructeur reçoit le
/// monde ("Level level") en premier paramètre, pour que les nœuds qui agissent sur le jeu
/// fonctionnent partout (comme le "World Context" des fonctions de l'Unreal Engine).
/// </summary>
public abstract partial class ModJavaEmitter
{
    private void WriteDefinitionFile(GenerationContext context, JavaGameApi api, JavaProgramNames names, IRDefinition definition)
    {
        string path = $"src/main/java/{names.Package.Replace('.', '/')}/{names.SimpleName(definition)}.java";
        var java = new BehaviorJava(api, names);
        var imports = new SortedSet<string>(StringComparer.Ordinal) { "net.minecraft.world.level.Level" };
        var body = new JavaSourceWriter();
        var spans = new List<(int First, int Last, Guid Asset, string Label)>();

        WriteDefinition(body, java, names, definition, isTopLevel: true, imports, spans);
        imports.UnionWith(java.Imports);

        JavaSourceWriter w = WriteHeader(names.Package, imports, $"{KindLabel(definition)} « {definition.Name} ».");
        int offset = w.NextLineNumber - 1;
        foreach (string line in body.ToString().TrimEnd('\n').Split('\n'))
        {
            w.Line(line);
        }

        context.Files.WriteText(path, w.ToString());

        // Les plus petits intervalles d'abord : une erreur dans une méthode est rattachée à la méthode, pas à sa classe.
        foreach ((int first, int last, Guid asset, string label) in spans.OrderBy(s => s.Last - s.First))
        {
            context.SourceMap.Add(path, first + offset, last + offset, asset, label);
        }
    }

    private static string KindLabel(IRDefinition definition) => definition switch
    {
        IRFunction { IsProcedure: true } => "Procédure",
        IRFunction => "Fonction",
        IRClass => "Classe",
        IRStruct => "Structure",
        IRInterface => "Interface",
        _ => "Énumération",
    };

    private static void WriteDefinition(
        JavaSourceWriter w, BehaviorJava java, JavaProgramNames names, IRDefinition definition, bool isTopLevel,
        ISet<string> imports, List<(int, int, Guid, string)> spans)
    {
        int first = w.NextLineNumber;
        string access = isTopLevel ? "public" : JavaProgramNames.Modifier(definition.Access);
        string name = names.SimpleName(definition);
        string label = $"{KindLabel(definition)} '{definition.Name}'";

        switch (definition)
        {
            case IRFunction function:
                // Hors de toute classe, une fonction est portée par une classe à son nom.
                w.Line($"/** {label}. */").OpenBlock($"public final class {name}");
                WriteGlobals(w, java, names, function, imports);
                WriteMethod(w, java, names, function, imports, spans);
                WriteHelpers(w, java, isTopLevel);
                w.Line().OpenBlock($"private {name}()").CloseBlock();
                w.CloseBlock();
                break;

            case IRClass type:
                string extends = type.BaseClassId is { } parent ? $" extends {names.TypeName(parent)}" : string.Empty;
                string implements = type.InterfaceIds.Count > 0 ? $" implements {string.Join(", ", type.InterfaceIds.Select(names.TypeName))}" : string.Empty;
                w.Line($"/** {label}. */").OpenBlock($"{access}{(isTopLevel ? string.Empty : " static")} class {name}{extends}{implements}");
                foreach (IRVariable field in type.Fields)
                {
                    imports.UnionWith(JavaProgramNames.ImportsFor(field.Type));
                    w.Line($"{JavaProgramNames.Modifier(field.Definition.Access)} {names.JavaType(field.Type)} {names.Variable(field.Id)} = {java.Expression(field.Initial)};");
                }

                foreach (IRFunction method in names.Members(type.Id).OfType<IRFunction>())
                {
                    WriteGlobals(w, java, names, method, imports);
                }

                WriteConstructor(w, java, names, type, imports, spans);
                WriteMembers(w, java, names, type.Id, imports, spans);
                WriteHelpers(w, java, isTopLevel);
                w.CloseBlock();
                break;

            case IRStruct structure:
                w.Line($"/** {label}. */").OpenBlock($"{access}{(isTopLevel ? string.Empty : " static")} final class {name}");
                foreach (IRVariable field in structure.Fields)
                {
                    imports.UnionWith(JavaProgramNames.ImportsFor(field.Type));
                    w.Line($"public {names.JavaType(field.Type)} {names.Variable(field.Id)} = {java.Expression(field.Initial)};");
                }

                w.Line().OpenBlock($"public {name}({names.ParameterList(structure.Fields.Select(f => f.Definition), withLevel: false)})");
                foreach (IRVariable field in structure.Fields)
                {
                    w.Line($"this.{names.Variable(field.Id)} = {names.Variable(field.Id)};");
                }

                w.CloseBlock();
                WriteHelpers(w, java, isTopLevel);
                w.CloseBlock();
                break;

            case IRInterface contract:
                w.Line($"/** {label}. */").OpenBlock($"{access} interface {name}");
                foreach (MethodSignature method in contract.Methods)
                {
                    foreach (VariableDefinition parameter in method.Parameters.Concat(method.Returns))
                    {
                        imports.UnionWith(JavaProgramNames.ImportsFor(parameter.Type));
                    }

                    w.Line($"{names.ReturnType(method)} {names.InterfaceMethod(contract.Id, method.Id)}({names.ParameterList(method.Parameters)});");
                }

                w.CloseBlock();
                break;

            case IREnum enumeration:
                w.Line($"/** {label}. */").OpenBlock($"{access} enum {name}");
                w.Line(enumeration.Constants.Count == 0
                    ? ";"
                    : string.Join(", ", enumeration.Constants.Select(c => ProgramNames.Constant(c.Name))) + ";");
                w.CloseBlock();
                break;
        }

        spans.Add((first, w.NextLineNumber - 1, definition.Id, label));
    }

    /// <summary>Méthodes et types intégrés à une classe, dans l'ordre de leurs identifiants.</summary>
    private static void WriteMembers(
        JavaSourceWriter w, BehaviorJava java, JavaProgramNames names, Guid owner, ISet<string> imports, List<(int, int, Guid, string)> spans)
    {
        foreach (IRDefinition member in names.Members(owner))
        {
            w.Line();
            if (member is IRFunction method)
            {
                WriteMethod(w, java, names, method, imports, spans);
            }
            else
            {
                WriteDefinition(w, java, names, member, isTopLevel: false, imports, spans);
            }
        }
    }

    /// <summary>Variables globales d'une fonction : champs statiques, conservés d'un appel à l'autre.</summary>
    private static void WriteGlobals(JavaSourceWriter w, BehaviorJava java, JavaProgramNames names, IRFunction function, ISet<string> imports)
    {
        foreach (IRVariable global in function.Globals)
        {
            imports.UnionWith(JavaProgramNames.ImportsFor(global.Type));
            w.Line($"private static {names.JavaType(global.Type)} {names.Variable(global.Id)} = {java.Expression(global.Initial)};");
        }
    }

    private static void WriteMethod(
        JavaSourceWriter w, BehaviorJava java, JavaProgramNames names, IRFunction function, ISet<string> imports, List<(int, int, Guid, string)> spans)
    {
        int first = w.NextLineNumber;
        foreach (VariableDefinition variable in function.Parameters.Concat(function.Returns))
        {
            imports.UnionWith(JavaProgramNames.ImportsFor(variable.Type));
        }

        if (function.Returns.Count > 1 && names.DeclaresResultRecord(function))
        {
            string components = string.Join(", ", function.Returns.Select(r => $"{names.JavaType(r.Type)} {names.Variable(r.Id)}"));
            w.Line($"/** Valeurs renvoyées par « {function.Name} ». */")
                .Line($"public record {names.ResultRecordName(function)}({components}) {{")
                .Line("}")
                .Line();
        }

        bool topLevel = function.OwnerId is null || names.Find(function.OwnerId.Value) is null;
        string access = topLevel ? "public" : JavaProgramNames.Modifier(function.Access);
        string isStatic = topLevel || function.IsStatic ? " static" : string.Empty;
        w.Line($"/** {KindLabel(function)} « {function.Name} ». */");
        if (names.Overridden(function) is not null || names.ImplementsInterface(function))
        {
            w.Line("@Override");
        }

        w.OpenBlock($"{access}{isStatic} {names.ReturnType(function)} {names.MethodName(function)}({names.ParameterList(function.Parameters)})");
        java.CurrentFunction = function;
        WriteBody(w, java, names, function.Locals, function.Body, function.Id, imports, spans);
        if (function.Returns.Count > 0 && !EndsWithReturn(function.Body.Statements))
        {
            // Un chemin sans nœud Retour renvoie les valeurs par défaut.
            w.Line(java.Return([]));
        }

        java.CurrentFunction = null;
        w.CloseBlock();
        spans.Add((first, w.NextLineNumber - 1, function.Id, $"{KindLabel(function)} '{function.Name}'"));
    }

    private static void WriteConstructor(
        JavaSourceWriter w, BehaviorJava java, JavaProgramNames names, IRClass type, ISet<string> imports, List<(int, int, Guid, string)> spans)
    {
        foreach (VariableDefinition parameter in type.ConstructorParameters)
        {
            imports.UnionWith(JavaProgramNames.ImportsFor(parameter.Type));
        }

        w.Line().OpenBlock($"public {names.SimpleName(type)}({names.ParameterList(type.ConstructorParameters)})");
        IReadOnlyList<IRStatement> statements = type.Constructor.Statements;
        if (statements is [IRSuperStatement super, ..])
        {
            w.Line(java.Statement(super)[0]);
            statements = [.. statements.Skip(1)];
        }
        else if (type.BaseClassId is not null)
        {
            w.Line($"super({JavaProgramNames.LevelParameter});");
        }

        WriteBody(w, java, names, [], type.Constructor with { Statements = statements }, type.Id, imports, spans);
        w.CloseBlock();
    }

    private static void WriteBody(
        JavaSourceWriter w, BehaviorJava java, JavaProgramNames names, IReadOnlyList<IRVariable> variables, IRBody body, Guid asset,
        ISet<string> imports, List<(int, int, Guid, string)> spans)
    {
        foreach (IRVariable variable in variables)
        {
            imports.UnionWith(JavaProgramNames.ImportsFor(variable.Type));
            w.Line($"{names.JavaType(variable.Type)} {names.Variable(variable.Id)} = {java.Expression(variable.Initial)};");
        }

        WriteLocals(w, java, names, body.Locals, imports);
        var nodeLines = new List<(int First, int Last, Guid NodeId)>();
        WriteStatements(w, java, body.Statements, nodeLines);
        foreach ((int first, int last, Guid nodeId) in nodeLines)
        {
            spans.Add((first, last, asset, $"nœud {nodeId:N}"));
        }
    }

    /// <summary>Méthodes utilitaires (donner un item...) écrites une fois, dans la classe du fichier.</summary>
    private static void WriteHelpers(JavaSourceWriter w, BehaviorJava java, bool isTopLevel)
    {
        if (!isTopLevel)
        {
            return;
        }

        foreach (IReadOnlyList<string> helper in java.HelperMethods())
        {
            w.Line();
            foreach (string line in helper)
            {
                w.Line(line);
            }
        }
    }
}
