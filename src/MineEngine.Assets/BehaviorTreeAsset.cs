using MineEngine.Assets.Puppeteer;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Puppeteer behavior tree: the intelligence of a mob described as nodes, like Unreal's
/// Behavior Trees. The tree (<see cref="Tree"/>) chooses on every tick what the mob does;
/// the blackboard (<see cref="Keys"/>) is its memory. A mob that uses the tree replaces
/// its list of behaviors with it. Every property is immutable: replacing it is a single
/// change, undoable like the others.
/// </summary>
public sealed class BehaviorTreeAsset : Asset
{
    private BehaviorTree _tree = BehaviorTree.Empty;
    private IReadOnlyList<BlackboardKey> _keys = [];
    private bool _isPublicGoal;

    public BehaviorTreeAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.BehaviorTree;

    public BehaviorTree Tree
    {
        get => _tree;
        set => SetField(ref _tree, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>Blackboard keys, in the order they are displayed.</summary>
    public IReadOnlyList<BlackboardKey> Keys
    {
        get => _keys;
        set => SetField(ref _keys, value ?? []);
    }

    /// <summary>
    /// Public goal: the tree can be run by other trees with the "Run a goal" task, like a reusable
    /// sub-behavior (Unreal's Run Behavior). It shares the blackboard of the mob that runs it.
    /// </summary>
    public bool IsPublicGoal
    {
        get => _isPublicGoal;
        set => SetField(ref _isPublicGoal, value);
    }

    public BlackboardKey? FindKey(Guid id) => _keys.FirstOrDefault(k => k.Id == id);

    /// <summary>Goals run by this tree ("Run a goal" tasks), as asset identifiers.</summary>
    public IEnumerable<Guid> GoalIds =>
        Tree.Nodes.Where(n => n.Kind == BtNodeKind.RunGoal)
            .Select(n => Guid.TryParse(n.GetValue(BtCatalog.Goal), out Guid id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct();

    /// <summary>
    /// Starting tree of a new asset, for a monster: spot the nearest player, attack it if
    /// there is one, otherwise wander and wait a little.
    /// </summary>
    public void ApplyStarterTree()
    {
        var target = new BlackboardKey(Guid.NewGuid(), "target", BlackboardKeyType.Entity);
        string key = target.Id.ToString("D");

        BtNode root = BtNode.Create(BtNodeKind.Root, 0, 0);
        BtNode selector = BtNode.Create(BtNodeKind.Selector, 0, 120) with
        {
            Services = [BtAttachment.Create(BtNodeKind.FindNearestEntityService).WithValue(BtCatalog.Result, key)],
        };
        BtNode attack = BtNode.Create(BtNodeKind.MeleeAttack, -220, 300).WithValue(BtCatalog.Target, key) with
        {
            Decorators = [BtAttachment.Create(BtNodeKind.IsSet).WithValue(BtCatalog.Key, key).WithValue(BtCatalog.Abort, nameof(BtAbortMode.Both))],
        };
        BtNode idle = BtNode.Create(BtNodeKind.Sequence, 220, 300);
        BtNode wander = BtNode.Create(BtNodeKind.Wander, 100, 460);
        BtNode wait = BtNode.Create(BtNodeKind.Wait, 340, 460).WithValue("seconds", "2").WithValue("deviation", "1");

        Keys = [target];
        Tree = new BehaviorTree(
            [root, selector, attack, idle, wander, wait],
            [new(root.Id, selector.Id), new(selector.Id, attack.Id), new(selector.Id, idle.Id), new(idle.Id, wander.Id), new(idle.Id, wait.Id)])
            .Arranged();
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (Guid goal in GoalIds)
        {
            yield return new AssetReference(goal, AssetReferenceKind.BehaviorTree, Tr.T("goal run by the tree"));
        }

        foreach (BtNode node in Tree.Nodes)
        {
            foreach ((BtNodeInfo info, Func<string, string> value) in node.Attachments.Select(a => (a.Info, (Func<string, string>)a.GetValue)).Prepend((node.Info, node.GetValue)))
            {
                foreach (BtParameterInfo parameter in info.Parameters.Where(p => p.Type == BtParameterType.Sound))
                {
                    if (ContentReference.TryParseStorageText(value(parameter.Name), out ContentReference? sound) && sound!.Kind == ContentReferenceKind.Asset)
                    {
                        yield return new AssetReference(sound.AssetId, AssetReferenceKind.Sound, Tr.F("node \"{0}\"", info.Title));
                    }
                }
            }
        }
    }

    public override void Validate(DiagnosticBag diagnostics) => Validate(diagnostics, null);

    /// <summary>Checks the tree; with <paramref name="resolve"/>, the goals it runs are checked too (public, no loop).</summary>
    public void Validate(DiagnosticBag diagnostics, Func<Guid, BehaviorTreeAsset?>? resolve)
    {
        base.Validate(diagnostics);
        string source = ToString();
        foreach (BtIssue issue in BehaviorTreeValidator.Validate(this, resolve))
        {
            diagnostics.Add(new Diagnostic(issue.Severity, issue.Message, source, Id));
        }
    }

    /// <summary>Family of creatures chosen in a setting (players by default).</summary>
    public static MobTarget ParseFamily(string text) => Enum.TryParse(text, out MobTarget family) ? family : MobTarget.Player;

    /// <summary>Deep copy of a node with new identifiers (node and attached pieces).</summary>
    public static BtNode CloneNode(BtNode node, double x, double y) => node with
    {
        Id = Guid.NewGuid(),
        X = x,
        Y = y,
        Decorators = [.. node.Decorators.Select(a => a with { Id = Guid.NewGuid() })],
        Services = [.. node.Services.Select(a => a with { Id = Guid.NewGuid() })],
    };
}
