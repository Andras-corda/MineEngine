namespace MineEngine.Assets.Puppeteer;

/// <summary>Starting trees offered when a behavior tree is created (V0.7.14).</summary>
public enum BehaviorTreeTemplate
{
    /// <summary>A monster: spots the nearest player and attacks it, otherwise wanders.</summary>
    Aggressive,

    /// <summary>A peaceful creature: wanders and waits.</summary>
    Wanderer,

    /// <summary>A shy creature: runs away from the players that come near, otherwise wanders.</summary>
    Fearful,

    /// <summary>A neutral creature: attacks only the one that hit it, otherwise wanders.</summary>
    Retaliating,

    /// <summary>A curious creature: looks at the players nearby, otherwise wanders.</summary>
    Watcher,

    /// <summary>Only the root.</summary>
    Empty,
}

/// <summary>Description of a template, for the creation window.</summary>
public sealed record BehaviorTreeTemplateInfo(BehaviorTreeTemplate Template, string Title, string Description);

/// <summary>Builds the starting tree and blackboard of a template.</summary>
public static class BehaviorTreeTemplates
{
    public static IReadOnlyList<BehaviorTreeTemplateInfo> All { get; } =
    [
        new(BehaviorTreeTemplate.Aggressive, Tr.T("Aggressive"), Tr.T("Spots the nearest player and attacks it; otherwise wanders and waits.")),
        new(BehaviorTreeTemplate.Wanderer, Tr.T("Wanderer"), Tr.T("Walks around at random and waits a little between two walks.")),
        new(BehaviorTreeTemplate.Fearful, Tr.T("Fearful"), Tr.T("Runs away from the players that come near; otherwise wanders.")),
        new(BehaviorTreeTemplate.Retaliating, Tr.T("Retaliating"), Tr.T("Attacks only the creature that hit it; otherwise wanders.")),
        new(BehaviorTreeTemplate.Watcher, Tr.T("Watcher"), Tr.T("Looks at the players nearby; otherwise wanders.")),
        new(BehaviorTreeTemplate.Empty, Tr.T("Empty tree"), Tr.T("Only the root, to build the tree yourself.")),
    ];

    public static BehaviorTreeTemplateInfo Info(BehaviorTreeTemplate template) => All.First(t => t.Template == template);

    /// <summary>Replaces the tree and the blackboard of <paramref name="asset"/> with those of the template.</summary>
    public static void Apply(BehaviorTreeAsset asset, BehaviorTreeTemplate template)
    {
        ArgumentNullException.ThrowIfNull(asset);
        BtNode root = BtNode.Create(BtNodeKind.Root, 0, 0);
        if (template == BehaviorTreeTemplate.Empty)
        {
            asset.Keys = [];
            asset.Tree = new BehaviorTree([root], []);
            return;
        }

        BtNode idle = BtNode.Create(BtNodeKind.Sequence, 0, 0);
        BtNode wander = BtNode.Create(BtNodeKind.Wander, 0, 0);
        BtNode wait = BtNode.Create(BtNodeKind.Wait, 0, 0).WithValue("seconds", "2").WithValue("deviation", "1");
        List<BtNode> nodes = [root, idle, wander, wait];
        List<BtLink> links = [new(idle.Id, wander.Id), new(idle.Id, wait.Id)];
        if (template == BehaviorTreeTemplate.Wanderer)
        {
            asset.Keys = [];
            asset.Tree = new BehaviorTree(nodes, [new(root.Id, idle.Id), .. links]).Arranged();
            return;
        }

        // The others: a selector that does the action while a creature is in the key, otherwise wanders.
        var target = new BlackboardKey(Guid.NewGuid(), "target", BlackboardKeyType.Entity);
        string key = target.Id.ToString("D");
        BtAttachment service = template == BehaviorTreeTemplate.Retaliating
            ? BtAttachment.Create(BtNodeKind.RememberAttacker).WithValue(BtCatalog.Result, key)
            : BtAttachment.Create(BtNodeKind.FindNearestEntityService).WithValue(BtCatalog.Result, key)
                .WithValue("range", template == BehaviorTreeTemplate.Fearful ? "8" : "16");
        BtNode selector = BtNode.Create(BtNodeKind.Selector, 0, 0) with { Services = [service] };
        BtNode action = template switch
        {
            BehaviorTreeTemplate.Fearful => BtNode.Create(BtNodeKind.FleeFrom, 0, 0).WithValue(BtCatalog.Target, key),
            BehaviorTreeTemplate.Watcher => BtNode.Create(BtNodeKind.LookAt, 0, 0).WithValue(BtCatalog.Target, key).WithValue("seconds", "2"),
            _ => BtNode.Create(BtNodeKind.MeleeAttack, 0, 0).WithValue(BtCatalog.Target, key),
        };
        action = action with
        {
            Decorators = [BtAttachment.Create(BtNodeKind.IsSet).WithValue(BtCatalog.Key, key).WithValue(BtCatalog.Abort, nameof(BtAbortMode.Both))],
        };

        asset.Keys = [target];
        asset.Tree = new BehaviorTree(
            [.. nodes, selector, action],
            [new(root.Id, selector.Id), new(selector.Id, action.Id), new(selector.Id, idle.Id), .. links])
            .Arranged();
    }
}
