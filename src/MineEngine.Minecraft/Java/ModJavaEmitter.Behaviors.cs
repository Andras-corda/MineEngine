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

    /// <summary>Lines that start the handler of an event when the loader reads it differently (tick of Forge), or null.</summary>
    protected virtual IReadOnlyList<string>? EventPrologue(GraphNodeKind eventKind) => null;

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
            if (handler.IsDirectCall)
            {
                // Événement d'item, de commande ou de touche : appelé directement, côté serveur, avec le joueur déjà connu.
                imports.UnionWith(["net.minecraft.core.BlockPos", "net.minecraft.world.entity.player.Player", "net.minecraft.world.level.Level"]);
                if (handler.Mob is not null || handler.Block is not null || handler.Item is not null)
                {
                    // Mob event (V0.7.9): the same parameters for every event; each node shows those it gives.
                    imports.UnionWith(["net.minecraft.world.entity.Entity", "net.minecraft.world.entity.LivingEntity"]);
                }

                body.OpenBlock(handler switch
                {
                    { Mob: not null } => $"public static void {method}(LivingEntity mob, Level level, BlockPos position, Player player, Entity other, double amount)",
                    { Block: not null } => $"public static void {method}(Level level, BlockPos position, Player player, Entity other, double amount)",
                    { Item: not null } => $"public static void {method}(Player player, Level level, BlockPos position, Entity other, double amount)",
                    { Event: GraphNodeKind.OnCommand } => $"public static void {method}(Player player, Level level, BlockPos position, int number, String text, Player target)",
                    _ => $"public static void {method}(Player player, Level level, BlockPos position)",
                });
            }
            else
            {
                (string eventImport, string eventType) = GameEventType(handler.Event);
                imports.Add(eventImport);
                registered.Add(method);
                body.OpenBlock($"private static void {method}({eventType} event)");
                // The prologue of BehaviorJava is always read: it brings the imports.
                IReadOnlyList<string> prologue = java.Prologue(handler.Event);
                foreach (string line in EventPrologue(handler.Event) ?? prologue)
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

                case IRLoopStatement loop:
                    WriteLoop(w, java, loop, nodeLines);
                    break;

                case IRSwitchStatement choice:
                    WriteSwitch(w, java, choice, nodeLines);
                    break;

                case IRFlipFlopStatement flipFlop:
                    WriteFlipFlop(w, java, flipFlop, nodeLines);
                    break;

                default:
                    foreach (string line in java.Statement(statement))
                    {
                        w.Line(line);
                    }

                    break;
            }

            nodeLines.Add((first, statement is IRIfStatement or IRLoopStatement or IRSwitchStatement or IRFlipFlopStatement ? first : w.NextLineNumber - 1, statement.NodeId));
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
        IRSwitchStatement choice => choice.Bodies.All(EndsWithReturn) && EndsWithReturn(choice.Default),
        IRFlipFlopStatement flipFlop => EndsWithReturn(flipFlop.A) && EndsWithReturn(flipFlop.B),
        _ => false,
    };

    /// <summary>Switch: the value in a local, then "if / else if" on each case and "else" for Default.</summary>
    private static void WriteSwitch(JavaSourceWriter w, BehaviorJava java, IRSwitchStatement choice, List<(int, int, Guid)> nodeLines)
    {
        bool text = choice.Type.Kind == PinType.Text;
        string value = "switch_" + choice.NodeId.ToString("N")[..8];
        w.Line($"{(text ? "String" : "int")} {value} = {java.Expression(choice.Value)};");
        for (int i = 0; i < choice.Cases.Count; i++)
        {
            string test = text ? $"{java.Expression(choice.Cases[i])}.equals({value})" : $"{value} == {java.Expression(choice.Cases[i])}";
            if (i == 0)
            {
                w.OpenBlock($"if ({test})");
            }
            else
            {
                w.CloseBlock($" else if ({test}) {{").Indent();
            }

            WriteStatements(w, java, choice.Bodies[i], nodeLines);
        }

        if (choice.Cases.Count == 0)
        {
            WriteStatements(w, java, choice.Default, nodeLines);
            return;
        }

        if (choice.Default.Count > 0)
        {
            w.CloseBlock(" else {").Indent();
            WriteStatements(w, java, choice.Default, nodeLines);
        }

        w.CloseBlock();
    }

    /// <summary>Flip Flop: its state is kept by a helper of the class, per node.</summary>
    private static void WriteFlipFlop(JavaSourceWriter w, BehaviorJava java, IRFlipFlopStatement flipFlop, List<(int, int, Guid)> nodeLines)
    {
        string isA = "flip_" + flipFlop.NodeId.ToString("N")[..8];
        w.Line($"boolean {isA} = {java.FlipFlop(flipFlop.NodeId)};")
            .Line($"{flipFlop.Output} = {isA};");
        w.OpenBlock($"if ({isA})");
        WriteStatements(w, java, flipFlop.A, nodeLines);
        if (flipFlop.B.Count > 0)
        {
            w.CloseBlock(" else {").Indent();
            WriteStatements(w, java, flipFlop.B, nodeLines);
        }

        w.CloseBlock();
    }

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

    /// <summary>Most turns of a "While" loop: a loop that never ends must not freeze the server.</summary>
    private const int MaxWhileTurns = 10000;

    /// <summary>Java loop of a Repeat, While or For each player node (V0.6.5).</summary>
    private static void WriteLoop(JavaSourceWriter w, BehaviorJava java, IRLoopStatement loop, List<(int, int, Guid)> nodeLines)
    {
        string suffix = loop.NodeId.ToString("N")[..8];
        switch (loop.Kind)
        {
            case GraphNodeKind.Repeat:
                string turn = loop.Outputs[0];
                w.OpenBlock($"for ({turn} = 1; {turn} <= {java.Expression(loop.Arguments[0])}; {turn}++)");
                break;
            case GraphNodeKind.While:
                w.OpenBlock($"for (int guard_{suffix} = 0; guard_{suffix} < {MaxWhileTurns} && {java.Expression(loop.Arguments[0])}; guard_{suffix}++)");
                break;
            case GraphNodeKind.ForEachInCollection when loop.Arguments[0].Type.Kind == PinType.Map:
                // A copy: the body may change the dictionary.
                w.OpenBlock($"for (var entry_{suffix} : new java.util.ArrayList<>({java.Expression(loop.Arguments[0])}.entrySet()))")
                    .Line($"{loop.Outputs[0]} = entry_{suffix}.getKey();")
                    .Line($"{loop.Outputs[1]} = entry_{suffix}.getValue();");
                break;
            case GraphNodeKind.ForEachInCollection:
                w.OpenBlock($"for (var each_{suffix} : new java.util.ArrayList<>({java.Expression(loop.Arguments[0])}))")
                    .Line($"{loop.Outputs[0]} = each_{suffix};");
                break;
            default:
                w.OpenBlock($"for (Player each_{suffix} : level.players())")
                    .Line($"{loop.Outputs[0]} = each_{suffix};");
                break;
        }

        WriteStatements(w, java, loop.Body, nodeLines);
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
