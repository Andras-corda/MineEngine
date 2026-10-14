using MineEngine.Assets.Graphs;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Partie "graphes nodaux" du code Java : une classe par graphe, avec une méthode par
/// événement, inscrite sur le bus des événements du jeu au démarrage du mod.
/// </summary>
public abstract partial class ModJavaEmitter
{
    /// <summary>Bus des événements du jeu (clics, connexions...) et son import.</summary>
    protected abstract (string Expression, string Import) GameEventBus { get; }

    /// <summary>Classe Java de l'événement d'un nœud événement : import et nom tel qu'écrit dans le code.</summary>
    protected abstract (string Import, string TypeName) GameEventType(GraphNodeKind eventKind);

    private void WriteBehaviorClass(GenerationContext context, JavaModNaming naming, JavaGameApi api, JavaProgramNames names, IRBehavior behavior)
    {
        string className = naming.BehaviorClassFor(behavior.Id);
        string path = naming.SourcePath(naming.BehaviorPackage, className);
        var java = new BehaviorJava(api, names);
        (string bus, string busImport) = GameEventBus;

        // Le corps est écrit d'abord : il détermine les imports et les méthodes utilitaires.
        var body = new JavaSourceWriter();
        var nodeLines = new List<(int First, int Last, Guid NodeId)>();
        var registered = new List<string>();
        var imports = new SortedSet<string>(StringComparer.Ordinal) { busImport };
        if (context.Ir.Content.Definitions.Count > 0)
        {
            imports.Add(names.Package + ".*");
        }

        // Variables du graphe : communes à toutes ses réactions, conservées tant que le jeu tourne.
        foreach (IRVariable variable in behavior.Variables)
        {
            imports.UnionWith(JavaProgramNames.ImportsFor(variable.Type));
            body.Line($"private static {names.JavaType(variable.Type)} {names.Variable(variable.Id)} = {java.Expression(variable.Initial)};");
        }

        foreach ((IREventHandler handler, string method) in HandlerMethods(behavior))
        {
            body.Line().Line($"/** {NodeCatalog.Get(handler.Event).Title}. */");
            if (handler.IsItemEvent)
            {
                // Événement d'item : appelé par la classe de l'item, côté serveur, avec le joueur déjà connu.
                imports.UnionWith(["net.minecraft.core.BlockPos", "net.minecraft.world.entity.player.Player", "net.minecraft.world.level.Level"]);
                body.OpenBlock($"public static void {method}(Player player, Level level, BlockPos position)");
            }
            else
            {
                (string eventImport, string eventType) = GameEventType(handler.Event);
                imports.Add(eventImport);
                registered.Add(method);
                body.OpenBlock($"private static void {method}({eventType} event)");
                foreach (string line in java.Prologue(handler.Event))
                {
                    body.Line(line);
                }
            }

            WriteLocals(body, java, names, handler.Locals, imports);
            WriteStatements(body, java, handler.Body, nodeLines);
            body.CloseBlock();
        }

        foreach (IReadOnlyList<string> helper in java.HelperMethods())
        {
            body.Line();
            foreach (string line in helper)
            {
                body.Line(line);
            }
        }

        imports.UnionWith(java.Imports);

        JavaSourceWriter w = WriteHeader(naming.BehaviorPackage, imports, $"Comportements du graphe « {behavior.DisplayName} ».")
            .OpenBlock($"public final class {className}")
            .Line("/** Inscrit les réactions du graphe sur le bus des événements du jeu. */")
            .OpenBlock("public static void register()");
        foreach (string name in registered)
        {
            w.Line($"{bus}.addListener({className}::{name});");
        }

        w.CloseBlock();

        // Le corps a été écrit à part (il fixe les imports) ; ses numéros de ligne sont décalés d'autant.
        int offset = w.NextLineNumber - 1;
        foreach (string line in body.ToString().TrimEnd('\n').Split('\n'))
        {
            w.Line(line);
        }

        WritePrivateConstructor(w, className);
        context.Files.WriteText(path, w.ToString());

        string label = $"Graphe '{behavior.Id}'";
        foreach ((int first, int last, Guid nodeId) in nodeLines)
        {
            context.SourceMap.Add(path, first + offset, last + offset, behavior.SourceAssetId, $"{label}, nœud {nodeId:N}");
        }

        context.SourceMap.Add(path, 1, w.NextLineNumber - 1, behavior.SourceAssetId, label);
    }

    /// <summary>Nom de la méthode Java de chaque réaction ("onRightClickBlock1"...), partagé avec la classe des items.</summary>
    private static IReadOnlyList<(IREventHandler Handler, string Method)> HandlerMethods(IRBehavior behavior)
    {
        var counters = new Dictionary<GraphNodeKind, int>();
        var methods = new List<(IREventHandler, string)>();
        foreach (IREventHandler handler in behavior.Handlers)
        {
            counters[handler.Event] = counters.GetValueOrDefault(handler.Event) + 1;
            string name = handler.Event.ToString();
            methods.Add((handler, $"{char.ToLowerInvariant(name[0])}{name[1..]}{counters[handler.Event]}"));
        }

        return methods;
    }

    /// <summary>Variables locales qui reçoivent les résultats des nœuds exécutés, initialisées à leur valeur par défaut.</summary>
    private static void WriteLocals(JavaSourceWriter w, BehaviorJava java, JavaProgramNames names, IReadOnlyList<IRLocal> locals, ISet<string> imports)
    {
        foreach (IRLocal local in locals)
        {
            imports.UnionWith(JavaProgramNames.ImportsFor(local.Type));
            w.Line($"{names.JavaType(local.Type)} {local.Name} = {java.DefaultValue(local.Type)};");
        }
    }

    private static void WriteStatements(
        JavaSourceWriter w, BehaviorJava java, IReadOnlyList<IRStatement> statements, List<(int, int, Guid)> nodeLines)
    {
        foreach (IRStatement statement in statements)
        {
            int first = w.NextLineNumber;
            switch (statement)
            {
                // Une condition sans suite ni d'un côté ni de l'autre n'a aucun effet.
                case IRIfStatement { Then.Count: 0, Else.Count: 0 }:
                    continue;

                case IRIfStatement branch:
                    WriteIf(w, java, branch, "if", nodeLines);
                    break;

                default:
                    foreach (string line in java.Statement(statement))
                    {
                        w.Line(line);
                    }

                    break;
            }

            nodeLines.Add((first, statement is IRIfStatement ? first : w.NextLineNumber - 1, statement.NodeId));
        }
    }

    /// <summary>
    /// Vrai si l'exécution ne peut pas dépasser ces instructions (elles finissent par un retour,
    /// ou par une condition dont les deux branches finissent par un retour) : Java refuse
    /// alors toute instruction qui suivrait.
    /// </summary>
    private static bool EndsWithReturn(IReadOnlyList<IRStatement> statements) => statements.Count > 0 && statements[^1] switch
    {
        IRReturnStatement => true,
        IRIfStatement branch => EndsWithReturn(branch.Then) && EndsWithReturn(branch.Else),
        _ => false,
    };

    /// <summary>"if (...) { } else { }", en enchaînant "else if" quand la branche Faux n'est qu'une autre condition.</summary>
    private static void WriteIf(JavaSourceWriter w, BehaviorJava java, IRIfStatement branch, string keyword, List<(int, int, Guid)> nodeLines)
    {
        w.OpenBlock($"{keyword} ({WithoutOuterParentheses(java.Expression(branch.Condition))})");
        WriteStatements(w, java, branch.Then, nodeLines);
        if (branch.Else is [IRIfStatement nested] && (nested.Then.Count > 0 || nested.Else.Count > 0))
        {
            w.Unindent();
            int first = w.NextLineNumber;
            WriteIf(w, java, nested, "} else if", nodeLines);
            nodeLines.Add((first, first, nested.NodeId));
            return;
        }

        if (branch.Else.Count > 0)
        {
            w.CloseBlock(" else {").Indent();
            WriteStatements(w, java, branch.Else, nodeLines);
        }

        w.CloseBlock();
    }

    /// <summary>"(a > b)" devient "a > b" : la condition d'un if a déjà ses parenthèses.</summary>
    private static string WithoutOuterParentheses(string expression)
    {
        if (expression.Length < 2 || expression[0] != '(' || expression[^1] != ')')
        {
            return expression;
        }

        int depth = 0;
        for (int i = 0; i < expression.Length - 1; i++)
        {
            depth += expression[i] switch { '(' => 1, ')' => -1, _ => 0 };
            if (depth == 0)
            {
                return expression;
            }
        }

        return expression[1..^1];
    }
}
