using MineEngine.Assets;
using MineEngine.Assets.Puppeteer;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Example Puppeteer tree that uses every kind of node, decorator and service, and a mob
/// that follows it: the full build checks that all the Java code compiles.
/// </summary>
internal static class PuppeteerSample
{
    public static BehaviorTreeAsset AddTo(ModProject project, SoundAsset sound)
    {
        var target = new BlackboardKey(Guid.NewGuid(), "target", BlackboardKeyType.Entity);
        var home = new BlackboardKey(Guid.NewGuid(), "home", BlackboardKeyType.Position);
        var angry = new BlackboardKey(Guid.NewGuid(), "angry", BlackboardKeyType.Boolean);
        var hits = new BlackboardKey(Guid.NewGuid(), "hits", BlackboardKeyType.Number);
        var distance = new BlackboardKey(Guid.NewGuid(), "distance", BlackboardKeyType.Number);
        string T = target.Id.ToString("D");
        string H = home.Id.ToString("D");
        string A = angry.Id.ToString("D");
        string N = hits.Id.ToString("D");
        string D = distance.Id.ToString("D");

        BtNode root = BtNode.Create(BtNodeKind.Root, 0, 0);
        BtNode main = Node(BtNodeKind.Selector, 0, 100) with
        {
            Services =
            [
                BtAttachment.Create(BtNodeKind.FindNearestEntityService).WithValue(BtCatalog.Result, T).WithValue("family", nameof(MobTarget.Villager)),
                BtAttachment.Create(BtNodeKind.RememberAttacker).WithValue(BtCatalog.Result, T),
                BtAttachment.Create(BtNodeKind.UpdateDistance).WithValue(BtCatalog.Target, T).WithValue(BtCatalog.Result, D),
            ],
        };

        // Branch 1: flee when health is low (interrupts the branches on the right).
        BtNode flee = Node(BtNodeKind.Sequence, -600, 250) with
        {
            Decorators = [Condition(BtNodeKind.CheckHealth, BtAbortMode.LowerPriority).WithValue("operator", nameof(BtComparison.Less))],
        };
        BtNode run = Node(BtNodeKind.FleeFrom, -700, 400).WithValue(BtCatalog.Target, T);
        BtNode heal = Node(BtNodeKind.Heal, -600, 400) with { Decorators = [BtAttachment.Create(BtNodeKind.Cooldown)] };
        BtNode blink = Node(BtNodeKind.Teleport, -500, 400) with { Decorators = [BtAttachment.Create(BtNodeKind.RandomChance)] };

        // Branch 2: fight the target.
        BtNode fight = Node(BtNodeKind.SimpleParallel, -200, 250) with
        {
            Decorators =
            [
                Condition(BtNodeKind.IsSet, BtAbortMode.Both).WithValue(BtCatalog.Key, T),
                Condition(BtNodeKind.CanSee, BtAbortMode.Self).WithValue(BtCatalog.Target, T),
                BtAttachment.Create(BtNodeKind.TimeLimit),
            ],
        };
        BtNode attack = Node(BtNodeKind.MeleeAttack, -300, 400).WithValue(BtCatalog.Target, T);
        BtNode ranged = Node(BtNodeKind.RandomSelector, -100, 400);
        BtNode arrow = Node(BtNodeKind.ShootProjectile, -200, 550).WithValue(BtCatalog.Target, T) with
        {
            Decorators = [Condition(BtNodeKind.CheckDistance, BtAbortMode.None).WithValue(BtCatalog.Target, T).WithValue("operator", nameof(BtComparison.Greater))],
        };
        BtNode fireball = Node(BtNodeKind.ShootProjectile, -100, 550).WithValue(BtCatalog.Target, T).WithValue("projectile", nameof(BtProjectile.SmallFireball));
        BtNode snowball = Node(BtNodeKind.ShootProjectile, 0, 550).WithValue(BtCatalog.Target, T).WithValue("projectile", nameof(BtProjectile.Snowball)) with
        {
            Decorators = [BtAttachment.Create(BtNodeKind.InvertResult), BtAttachment.Create(BtNodeKind.ForceSuccess)],
        };
        BtNode leap = Node(BtNodeKind.Leap, 100, 550).WithValue(BtCatalog.Target, T);

        // Branch 3: at night, out of the water, count and get angry.
        BtNode mood = Node(BtNodeKind.Sequence, 300, 250) with
        {
            Decorators =
            [
                Condition(BtNodeKind.CheckTimeOfDay, BtAbortMode.None).WithValue("period", nameof(BtTimeOfDay.Night)),
                Condition(BtNodeKind.IsInWater, BtAbortMode.None).WithValue(BtCatalog.Inverse, "true"),
                Condition(BtNodeKind.CompareNumber, BtAbortMode.None).WithValue(BtCatalog.Key, N).WithValue("operator", nameof(BtComparison.Less)).WithValue(BtCatalog.Value, "5"),
            ],
        };
        BtNode count = Node(BtNodeKind.SetNumber, 200, 400).WithValue(BtCatalog.Key, N).WithValue("operation", nameof(BtNumberOperation.Add));
        BtNode anger = Node(BtNodeKind.SetBoolean, 300, 400).WithValue(BtCatalog.Key, A);
        BtNode growl = Node(BtNodeKind.PlaySound, 400, 400).WithValue("sound", ContentReference.ToAsset(sound.Id).ToStorageText()) with
        {
            Decorators = [Condition(BtNodeKind.CheckBoolean, BtAbortMode.None).WithValue(BtCatalog.Key, A)],
        };

        // Branche 4 : rentrer, regarder autour, se promener.
        BtNode idle = Node(BtNodeKind.Sequence, 700, 250) with { Decorators = [BtAttachment.Create(BtNodeKind.Loop)] };
        BtNode pick = Node(BtNodeKind.FindRandomPosition, 550, 400).WithValue(BtCatalog.Result, H);
        BtNode go = Node(BtNodeKind.MoveTo, 650, 400).WithValue(BtCatalog.Target, H);
        BtNode look = Node(BtNodeKind.LookAt, 750, 400).WithValue(BtCatalog.Target, H);
        BtNode forget = Node(BtNodeKind.ClearKey, 850, 400).WithValue(BtCatalog.Key, H);
        BtNode search = Node(BtNodeKind.FindNearestEntity, 950, 400).WithValue(BtCatalog.Result, T).WithValue("family", nameof(MobTarget.Animal));
        BtNode wander = Node(BtNodeKind.Wander, 1050, 400);
        BtNode wait = Node(BtNodeKind.Wait, 1150, 400).WithValue("deviation", "0.5");

        // Branch 5: a public goal shared with other trees (it uses the same "target" key).
        BehaviorTreeAsset patrol = AddPatrolGoal(project);
        BtNode runGoal = Node(BtNodeKind.RunGoal, 1300, 250).WithValue(BtCatalog.Goal, patrol.Id.ToString("D"));

        var asset = new BehaviorTreeAsset(Guid.NewGuid(), ResourceId.Parse("shadow_brain"), "Cerveau de l'ombre")
        {
            Keys = [target, home, angry, hits, distance],
            Tree = new BehaviorTree(
                [root, main, flee, run, heal, blink, fight, attack, ranged, arrow, fireball, snowball, leap, mood, count, anger, growl, idle, pick, go, look, forget, search, wander, wait, runGoal],
                [
                    new(root.Id, main.Id), new(main.Id, runGoal.Id),
                    new(main.Id, flee.Id), new(flee.Id, run.Id), new(flee.Id, heal.Id), new(flee.Id, blink.Id),
                    new(main.Id, fight.Id), new(fight.Id, attack.Id), new(fight.Id, ranged.Id),
                    new(ranged.Id, arrow.Id), new(ranged.Id, fireball.Id), new(ranged.Id, snowball.Id), new(ranged.Id, leap.Id),
                    new(main.Id, mood.Id), new(mood.Id, count.Id), new(mood.Id, anger.Id), new(mood.Id, growl.Id),
                    new(main.Id, idle.Id), new(idle.Id, pick.Id), new(idle.Id, go.Id), new(idle.Id, look.Id), new(idle.Id, forget.Id),
                    new(idle.Id, search.Id), new(idle.Id, wander.Id), new(idle.Id, wait.Id),
                ]).Arranged(),
        };
        project.Assets.Add(asset);

        project.Assets.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("shadow_puppet"), "Marionnette de l'ombre")
        {
            Model = MobModel.Skeleton,
            Kind = MobKind.Monster,
            BehaviorTreeId = asset.Id,
            HasSpawnEgg = true,
        });

        return asset;
    }

    /// <summary>Public goal: look for a player and walk to it.</summary>
    private static BehaviorTreeAsset AddPatrolGoal(ModProject project)
    {
        var target = new BlackboardKey(Guid.NewGuid(), "target", BlackboardKeyType.Entity);
        string key = target.Id.ToString("D");
        BtNode root = BtNode.Create(BtNodeKind.Root, 0, 0);
        BtNode sequence = Node(BtNodeKind.Sequence, 0, 200);
        BtNode find = Node(BtNodeKind.FindNearestEntity, -150, 400).WithValue(BtCatalog.Result, key);
        BtNode go = Node(BtNodeKind.MoveTo, 150, 400).WithValue(BtCatalog.Target, key);
        var goal = new BehaviorTreeAsset(Guid.NewGuid(), ResourceId.Parse("patrol_goal"), "Patrouille")
        {
            IsPublicGoal = true,
            Keys = [target],
            Tree = new BehaviorTree([root, sequence, find, go], [new(root.Id, sequence.Id), new(sequence.Id, find.Id), new(sequence.Id, go.Id)]),
        };
        project.Assets.Add(goal);
        return goal;
    }

    private static BtNode Node(BtNodeKind kind, double x, double y) => BtNode.Create(kind, x, y);

    private static BtAttachment Condition(BtNodeKind kind, BtAbortMode abort) =>
        BtAttachment.Create(kind).WithValue(BtCatalog.Abort, abort.ToString());
}
