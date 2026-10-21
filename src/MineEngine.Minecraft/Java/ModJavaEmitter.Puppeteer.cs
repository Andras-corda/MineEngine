using System.Globalization;
using MineEngine.Assets;
using MineEngine.Assets.Puppeteer;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// "Puppeteer" part of the Java code: the behavior tree engine (copied once) and one class
/// per tree, whose build() method creates the nodes for a mob.
/// </summary>
public abstract partial class ModJavaEmitter
{
    private static void WriteBehaviorTrees(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        IReadOnlyList<IRBehaviorTree> trees = context.Ir.Content.BehaviorTrees;
        if (trees.Count == 0)
        {
            return;
        }

        context.Files.WriteText(
            naming.SourcePath(naming.AiPackage, PuppeteerRuntimeJava.ClassName),
            PuppeteerRuntimeJava.Source(naming.AiPackage, api, GeneratedNotice));

        foreach (IRBehaviorTree tree in trees)
        {
            WriteBehaviorTreeClass(context, naming, tree);
        }
    }

    private static void WriteBehaviorTreeClass(GenerationContext context, JavaModNaming naming, IRBehaviorTree tree)
    {
        string className = naming.BehaviorTreeClassFor(tree.Id);
        var builder = new BehaviorTreeJavaBuilder(naming);
        string? root = tree.Root is null ? null : builder.Node(tree.Root);

        JavaSourceWriter w = WriteHeader(naming.AiPackage, builder.Imports, $"Arbre de comportement « {tree.DisplayName} » (Puppeteer).")
            .OpenBlock($"public final class {className}")
            .OpenBlock($"private {className}()")
            .CloseBlock()
            .Line();
        if (tree.Keys.Count > 0)
        {
            w.Line("// Blackboard : " + string.Join(", ", tree.Keys.Select(k => $"{k.Name} ({k.Type})")));
        }

        w.Line("/** Construit l'arbre pour un mob : chaque mob a le sien (ses nœuds gardent leur état). */")
            .OpenBlock("public static Puppeteer.Node build()");
        foreach (string statement in builder.Statements)
        {
            w.Line(statement);
        }

        w.Line(root is null ? "return null;" : $"return {root};")
            .CloseBlock()
            .CloseBlock();

        string path = naming.SourcePath(naming.AiPackage, className);
        context.Files.WriteText(path, w.ToString());
        context.SourceMap.Add(path, 1, w.NextLineNumber - 1, tree.SourceAssetId, $"Arbre '{tree.Id}'");
    }

    /// <summary>Writes the Java statements that create the nodes of a tree, one variable per node.</summary>
    private sealed class BehaviorTreeJavaBuilder(JavaModNaming naming)
    {
        private int _counter;

        public List<string> Statements { get; } = [];

        public SortedSet<string> Imports { get; } = new(StringComparer.Ordinal);

        /// <summary>Creates the node and its children; returns the name of its variable.</summary>
        public string Node(IRBtNode node)
        {
            string name = "n" + ++_counter;
            bool composite = node.Info.Category == BtCategory.Composite;
            Statements.Add($"{(composite ? "Puppeteer.Composite" : "Puppeteer.Node")} {name} = {Create(node)};");
            foreach (IRBtNode decorator in node.Decorators)
            {
                Statements.Add($"{name}.decorate({Create(decorator)});");
            }

            foreach (IRBtNode service in node.Services)
            {
                Statements.Add($"{name}.service({Create(service)});");
            }

            if (composite && node.Children.Count > 0)
            {
                List<string> children = [.. node.Children.Select(Node)];
                Statements.Add($"{name}.add({string.Join(", ", children)});");
            }

            return name;
        }

        private string Create(IRBtNode n) => n.Kind switch
        {
            BtNodeKind.Selector => "new Puppeteer.Selector(false)",
            BtNodeKind.RandomSelector => "new Puppeteer.Selector(true)",
            BtNodeKind.Sequence => "new Puppeteer.Sequence()",
            BtNodeKind.SimpleParallel => "new Puppeteer.SimpleParallel()",

            BtNodeKind.Wait => $"new Puppeteer.Wait({D(n, "seconds")}, {D(n, "deviation")})",
            BtNodeKind.MoveTo => $"new Puppeteer.MoveTo({K(n, BtCatalog.Target)}, {D(n, BtCatalog.Speed)}, {D(n, "acceptance")})",
            BtNodeKind.Wander => $"new Puppeteer.Wander({I(n, "radius")}, {D(n, BtCatalog.Speed)})",
            BtNodeKind.MeleeAttack => $"new Puppeteer.MeleeAttack({K(n, BtCatalog.Target)}, {D(n, BtCatalog.Speed)}, {D(n, "cooldown")})",
            BtNodeKind.LookAt => $"new Puppeteer.LookAt({K(n, BtCatalog.Target)}, {D(n, "seconds")})",
            BtNodeKind.FleeFrom => $"new Puppeteer.FleeFrom({K(n, BtCatalog.Target)}, {D(n, "distance")}, {D(n, BtCatalog.Speed)})",
            BtNodeKind.FindNearestEntity =>
                $"new Puppeteer.FindNearestEntity({Family(n)}, {D(n, "range")}, {B(n, "mustSee")}, {K(n, BtCatalog.Result)})",
            BtNodeKind.FindRandomPosition => $"new Puppeteer.FindRandomPosition({I(n, "radius")}, {K(n, BtCatalog.Result)})",
            BtNodeKind.SetBoolean => $"new Puppeteer.SetBoolean({K(n, BtCatalog.Key)}, {B(n, BtCatalog.Value)})",
            BtNodeKind.SetNumber =>
                $"new Puppeteer.SetNumber({K(n, BtCatalog.Key)}, {(int)n.Choice("operation", BtNumberOperation.Set)}, {D(n, BtCatalog.Value)})",
            BtNodeKind.ClearKey => $"new Puppeteer.ClearKey({K(n, BtCatalog.Key)})",
            BtNodeKind.PlaySound => $"new Puppeteer.PlaySound({S(n.Text("sound"))}, {D(n, "volume")}, {D(n, "pitch")})",
            BtNodeKind.Leap => $"new Puppeteer.Leap({(n.Text(BtCatalog.Target) is { } target ? S(target) : "null")}, {D(n, "height")})",
            BtNodeKind.ShootProjectile =>
                $"new Puppeteer.ShootProjectile({(int)n.Choice("projectile", BtProjectile.Arrow)}, {K(n, BtCatalog.Target)}, {D(n, "velocity")}, {D(n, "inaccuracy")})",
            BtNodeKind.Heal => $"new Puppeteer.Heal({D(n, "amount")})",
            BtNodeKind.Teleport => $"new Puppeteer.Teleport({I(n, "range")})",
            BtNodeKind.RunGoal => $"Puppeteer.goal({naming.BehaviorTreeClassFor(ResourceId.Parse(n.Text(BtCatalog.Goal)!))}.build())",

            BtNodeKind.IsSet => Observe(n, $"new Puppeteer.IsSet({K(n, BtCatalog.Key)})"),
            BtNodeKind.CheckBoolean => Observe(n, $"new Puppeteer.CheckBoolean({K(n, BtCatalog.Key)})"),
            BtNodeKind.CompareNumber => Observe(n, $"new Puppeteer.CompareNumber({K(n, BtCatalog.Key)}, {Operator(n)}, {D(n, BtCatalog.Value)})"),
            BtNodeKind.CheckDistance => Observe(n, $"new Puppeteer.CheckDistance({K(n, BtCatalog.Target)}, {Operator(n)}, {D(n, BtCatalog.Value)})"),
            BtNodeKind.CheckHealth => Observe(n, $"new Puppeteer.CheckHealth({Operator(n)}, {D(n, BtCatalog.Value)})"),
            BtNodeKind.CheckTimeOfDay =>
                Observe(n, $"new Puppeteer.CheckTimeOfDay({(n.Choice("period", BtTimeOfDay.Day) == BtTimeOfDay.Day ? "true" : "false")})"),
            BtNodeKind.IsInWater => Observe(n, "new Puppeteer.IsInWater()"),
            BtNodeKind.CanSee => Observe(n, $"new Puppeteer.CanSee({K(n, BtCatalog.Target)})"),
            BtNodeKind.RandomChance => $"new Puppeteer.RandomChance({D(n, "percent")})",
            BtNodeKind.Cooldown => $"new Puppeteer.Cooldown({D(n, "seconds")})",
            BtNodeKind.Loop => $"new Puppeteer.Loop({I(n, "count")})",
            BtNodeKind.TimeLimit => $"new Puppeteer.TimeLimit({D(n, "seconds")})",
            BtNodeKind.ForceSuccess => "new Puppeteer.ForceSuccess()",
            BtNodeKind.InvertResult => "new Puppeteer.InvertResult()",

            BtNodeKind.FindNearestEntityService =>
                $"new Puppeteer.FindNearestEntityService({D(n, BtCatalog.Interval)}, {Family(n)}, {D(n, "range")}, {B(n, "mustSee")}, {K(n, BtCatalog.Result)})",
            BtNodeKind.RememberAttacker => $"new Puppeteer.RememberAttacker({D(n, BtCatalog.Interval)}, {K(n, BtCatalog.Result)})",
            BtNodeKind.UpdateDistance =>
                $"new Puppeteer.UpdateDistance({D(n, BtCatalog.Interval)}, {K(n, BtCatalog.Target)}, {K(n, BtCatalog.Result)})",
            _ => throw new ArgumentOutOfRangeException(nameof(n), n.Kind, Tr.T("Unknown node.")),
        };

        /// <summary>Inversion and abort mode of a condition, only when they differ from the default values.</summary>
        private static string Observe(IRBtNode n, string expression)
        {
            bool inverse = n.Flag(BtCatalog.Inverse);
            int abort = (int)n.Choice(BtCatalog.Abort, BtAbortMode.None);
            return inverse || abort != 0 ? $"{expression}.observe({(inverse ? "true" : "false")}, {abort})" : expression;
        }

        private string Family(IRBtNode n)
        {
            (string javaClass, string import) = MobJava.Target(BehaviorTreeAsset.ParseFamily(n.Text("family") ?? nameof(MobTarget.Player)));
            Imports.Add(import);
            return javaClass + ".class";
        }

        private static int Operator(IRBtNode n) => (int)n.Choice("operator", BtComparison.GreaterOrEqual);

        private static string D(IRBtNode n, string name) => MobJava.Double(n.Number(name));

        private static string I(IRBtNode n, string name) => ((int)n.Number(name)).ToString(CultureInfo.InvariantCulture);

        private static string B(IRBtNode n, string name) => n.Flag(name) ? "true" : "false";

        private static string K(IRBtNode n, string name) => S(n.Text(name) ?? string.Empty);

        private static string S(string? text) => JavaNames.StringLiteral(text ?? string.Empty);
    }
}
