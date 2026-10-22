using System.Globalization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;

namespace MineEngine.Assets.Puppeteer;

/// <summary>Problem of a tree; <see cref="ItemId"/> designates a node, a decorator or a service.</summary>
public sealed record BtIssue(DiagnosticSeverity Severity, string Message, Guid? ItemId);

/// <summary>
/// Checks a behavior tree: empty branches, invalid settings, missing or wrongly typed
/// blackboard keys, aborts without effect.
/// </summary>
public static class BehaviorTreeValidator
{
    /// <param name="resolve">Finds another tree of the project (goals run by this one); null to skip those checks.</param>
    public static IReadOnlyList<BtIssue> Validate(BehaviorTreeAsset asset, Func<Guid, BehaviorTreeAsset?>? resolve = null)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var issues = new List<BtIssue>();
        BehaviorTree tree = asset.Tree;
        Dictionary<Guid, BlackboardKey> keys = asset.Keys.DistinctBy(k => k.Id).ToDictionary(k => k.Id);

        ValidateKeys(asset.Keys, issues);

        if (tree.ChildrenOf(tree.Root.Id).Count == 0)
        {
            issues.Add(new BtIssue(DiagnosticSeverity.Warning, Tr.T("The tree is empty: connect the root to a node (selector, sequence or task)."), tree.Root.Id));
        }

        var reachable = tree.ExecutionOrder().Select(n => n.Id).ToHashSet();
        foreach (BtNode node in tree.Nodes.Where(n => n.Kind != BtNodeKind.Root && !n.Info.IsAnnotation))
        {
            BtNodeInfo info = node.Info;
            if (!reachable.Contains(node.Id))
            {
                issues.Add(new BtIssue(DiagnosticSeverity.Warning, Tr.F("\"{0}\" is not connected to the root: it will never run.", info.Title), node.Id));
                continue;
            }

            int children = tree.ChildrenOf(node.Id).Count;
            if (info.Category == BtCategory.Composite && children == 0)
            {
                issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\" has no child.", info.Title), node.Id));
            }

            if (node.Kind == BtNodeKind.SimpleParallel && children == 1)
            {
                issues.Add(new BtIssue(DiagnosticSeverity.Warning, Tr.T("The simple parallel has only one child: nothing runs in the background."), node.Id));
            }

            ValidateValues(info, node.GetValue, node.Id, keys, issues);
            if (node.Kind == BtNodeKind.RunGoal)
            {
                ValidateGoal(asset, node, resolve, issues);
            }

            BtNode? parent = tree.ParentOf(node.Id);
            foreach (BtAttachment attachment in node.Attachments)
            {
                ValidateValues(attachment.Info, attachment.GetValue, attachment.Id, keys, issues);
                if (attachment.Info.IsCondition && attachment.Kind != BtNodeKind.RandomChance
                    && Enum.TryParse(attachment.GetValue(BtCatalog.Abort), out BtAbortMode abort)
                    && abort is BtAbortMode.LowerPriority or BtAbortMode.Both
                    && parent?.Kind is not (BtNodeKind.Selector or BtNodeKind.RandomSelector))
                {
                    issues.Add(new BtIssue(
                        DiagnosticSeverity.Warning,
                        Tr.F("\"{0}\": interrupting lower-priority nodes only works under a selector.", attachment.Info.Title),
                        attachment.Id));
                }
            }
        }

        return issues;
    }

    /// <summary>A goal must be another public tree, must not lead back to this tree, and should agree on the key types.</summary>
    private static void ValidateGoal(BehaviorTreeAsset asset, BtNode node, Func<Guid, BehaviorTreeAsset?>? resolve, List<BtIssue> issues)
    {
        if (!Guid.TryParse(node.GetValue(BtCatalog.Goal), out Guid goalId))
        {
            return;
        }

        if (goalId == asset.Id)
        {
            issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.T("A tree cannot run itself as a goal."), node.Id));
            return;
        }

        if (resolve?.Invoke(goalId) is not { } goal)
        {
            return;
        }

        if (!goal.IsPublicGoal)
        {
            issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\" is not a public goal: tick \"Public goal\" in its settings.", Name(goal)), node.Id));
        }

        if (LeadsTo(goal, asset.Id, resolve, []))
        {
            issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("Goal loop: \"{0}\" runs this tree again.", Name(goal)), node.Id));
        }

        foreach (BlackboardKey key in goal.Keys)
        {
            if (asset.Keys.FirstOrDefault(k => string.Equals(k.Name.Trim(), key.Name.Trim(), StringComparison.OrdinalIgnoreCase)) is { } mine && mine.Type != key.Type)
            {
                issues.Add(new BtIssue(
                    DiagnosticSeverity.Warning,
                    Tr.F("The key \"{0}\" is a {1} here but a {2} in the goal \"{3}\": they share the same memory.",
                        key.Name, BtCatalog.KeyTypeLabel(mine.Type).ToLower(CultureInfo.CurrentCulture), BtCatalog.KeyTypeLabel(key.Type).ToLower(CultureInfo.CurrentCulture), Name(goal)),
                    node.Id));
            }
        }
    }

    /// <summary>True if <paramref name="tree"/> runs, directly or through other goals, the tree <paramref name="target"/>.</summary>
    private static bool LeadsTo(BehaviorTreeAsset tree, Guid target, Func<Guid, BehaviorTreeAsset?> resolve, HashSet<Guid> visited)
    {
        if (!visited.Add(tree.Id))
        {
            return false;
        }

        foreach (Guid id in tree.GoalIds)
        {
            if (id == target || (resolve(id) is { } next && LeadsTo(next, target, resolve, visited)))
            {
                return true;
            }
        }

        return false;
    }

    private static string Name(BehaviorTreeAsset tree) => string.IsNullOrWhiteSpace(tree.DisplayName) ? tree.ResourceId.Value : tree.DisplayName;

    private static void ValidateKeys(IReadOnlyList<BlackboardKey> keys, List<BtIssue> issues)
    {
        foreach (BlackboardKey key in keys)
        {
            if (string.IsNullOrWhiteSpace(key.Name))
            {
                issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.T("A blackboard key has no name."), null));
            }
            else if (key.Name.Length > BlackboardKey.MaxNameLength)
            {
                issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("The name of the key \"{0}\" is too long.", key.Name), null));
            }
        }

        foreach (string duplicate in keys.Where(k => !string.IsNullOrWhiteSpace(k.Name))
                     .GroupBy(k => k.Name.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("Several blackboard keys are named \"{0}\".", duplicate), null));
        }
    }

    private static void ValidateValues(BtNodeInfo info, Func<string, string> value, Guid id, Dictionary<Guid, BlackboardKey> keys, List<BtIssue> issues)
    {
        foreach (BtParameterInfo parameter in info.Parameters)
        {
            string text = value(parameter.Name);
            switch (parameter.Type)
            {
                case BtParameterType.Number or BtParameterType.Integer when parameter.ParseNumber(text) is null:
                    issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\": \"{1}\" is not a valid number.", info.Title, parameter.Label), id));
                    break;

                case BtParameterType.Choice when parameter.Choices.All(c => c.Value != text):
                    issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\": choose \"{1}\".", info.Title, parameter.Label), id));
                    break;

                case BtParameterType.Key:
                    ValidateKey(info, parameter, text, id, keys, issues);
                    break;

                case BtParameterType.Goal when !Guid.TryParse(text, out _):
                    issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\": choose the goal to run.", info.Title), id));
                    break;

                case BtParameterType.Sound:
                    if (!ContentReference.TryParseStorageText(text, out ContentReference? sound))
                    {
                        issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\": choose a sound.", info.Title), id));
                    }
                    else if (sound!.Kind == ContentReferenceKind.Tag)
                    {
                        issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\": a sound cannot be a tag.", info.Title), id));
                    }

                    break;
            }
        }
    }

    private static void ValidateKey(BtNodeInfo info, BtParameterInfo parameter, string text, Guid id, Dictionary<Guid, BlackboardKey> keys, List<BtIssue> issues)
    {
        if (string.IsNullOrEmpty(text))
        {
            if (!parameter.Optional)
            {
                issues.Add(new BtIssue(
                    DiagnosticSeverity.Error,
                    Tr.F("\"{0}\": choose a blackboard key for \"{1}\" ({2}).", info.Title, parameter.Label, KeyTypesLabel(parameter)),
                    id));
            }

            return;
        }

        if (!Guid.TryParse(text, out Guid keyId) || !keys.TryGetValue(keyId, out BlackboardKey? key))
        {
            issues.Add(new BtIssue(DiagnosticSeverity.Error, Tr.F("\"{0}\": the key of \"{1}\" no longer exists in the blackboard.", info.Title, parameter.Label), id));
        }
        else if (!parameter.AcceptsKey(key.Type))
        {
            issues.Add(new BtIssue(
                DiagnosticSeverity.Error,
                Tr.F("\"{0}\": the key \"{1}\" is of type {2}, {3} is required.", info.Title, key.Name, BtCatalog.KeyTypeLabel(key.Type).ToLower(CultureInfo.CurrentCulture), KeyTypesLabel(parameter)),
                id));
        }
    }

    private static string KeyTypesLabel(BtParameterInfo parameter) =>
        parameter.KeyTypes.Count == 0
            ? Tr.T("any type")
            : string.Join(Tr.T(" or "), parameter.KeyTypes.Select(t => BtCatalog.KeyTypeLabel(t).ToLower(CultureInfo.CurrentCulture)));
}
