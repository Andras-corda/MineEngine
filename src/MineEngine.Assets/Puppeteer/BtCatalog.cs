using MineEngine.Core.GameData;

namespace MineEngine.Assets.Puppeteer;

/// <summary>
/// Catalog of the Puppeteer nodes: composites, tasks, decorators and services, with their
/// settings. Inspired by Unreal's Behavior Trees (Selector, Sequence, Simple Parallel,
/// Blackboard decorators, services) and by the usual actions of Minecraft mobs.
/// </summary>
public static class BtCatalog
{
    /// <summary>Common speed setting (multiplier of the mob speed).</summary>
    public const string Speed = "speed";

    public const string Target = "target";

    public const string Result = "result";

    public const string Key = "key";

    public const string Value = "value";

    /// <summary>Inverted condition ("is not").</summary>
    public const string Inverse = "inverse";

    /// <summary>Abort mode of a condition (<see cref="BtAbortMode"/>).</summary>
    public const string Abort = "abort";

    /// <summary>Interval of a service, in seconds.</summary>
    public const string Interval = "interval";

    /// <summary>Goal run by a "Run a goal" task.</summary>
    public const string Goal = "goal";

    private static readonly BlackboardKeyType[] EntityOrPosition = [BlackboardKeyType.Entity, BlackboardKeyType.Position];

    // Built when first needed: the choice lists below must exist before.
    private static readonly Lazy<Dictionary<BtNodeKind, BtNodeInfo>> LazyInfos = new(() => Build().ToDictionary(i => i.Kind));

    private static Dictionary<BtNodeKind, BtNodeInfo> Infos => LazyInfos.Value;

    public static IReadOnlyCollection<BtNodeInfo> All => Infos.Values;

    public static BtNodeInfo Get(BtNodeKind kind) =>
        Infos.TryGetValue(kind, out BtNodeInfo? info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, Tr.T("Unknown node."));

    public static BtNodeInfo? Find(BtNodeKind kind) => Infos.GetValueOrDefault(kind);

    public static IEnumerable<BtNodeInfo> InCategory(BtCategory category) => All.Where(i => i.Category == category);

    /// <summary>Label of a blackboard key type.</summary>
    public static string KeyTypeLabel(BlackboardKeyType type) => type switch
    {
        BlackboardKeyType.Entity => Tr.T("Creature"),
        BlackboardKeyType.Position => Tr.T("Position"),
        BlackboardKeyType.Number => Tr.T("Number"),
        _ => Tr.T("Boolean"),
    };

    public static string CategoryLabel(BtCategory category) => category switch
    {
        BtCategory.Root => Tr.T("Root"),
        BtCategory.Composite => Tr.T("Composite"),
        BtCategory.Task => Tr.T("Task"),
        BtCategory.Decorator => Tr.T("Decorator"),
        BtCategory.Annotation => Tr.T("Annotation"),
        _ => Tr.T("Service"),
    };

    public static IReadOnlyList<BtChoice> AbortChoices { get; } =
    [
        new(nameof(BtAbortMode.None), Tr.T("None (checked when starting)")),
        new(nameof(BtAbortMode.Self), Tr.T("Self (stops if it becomes false)")),
        new(nameof(BtAbortMode.LowerPriority), Tr.T("Lower priority (interrupts the nodes on the right)")),
        new(nameof(BtAbortMode.Both), Tr.T("Both")),
    ];

    public static IReadOnlyList<BtChoice> ComparisonChoices { get; } =
    [
        new(nameof(BtComparison.Less), "<"),
        new(nameof(BtComparison.LessOrEqual), "≤"),
        new(nameof(BtComparison.Equal), "="),
        new(nameof(BtComparison.GreaterOrEqual), "≥"),
        new(nameof(BtComparison.Greater), ">"),
    ];

    public static IReadOnlyList<BtChoice> FamilyChoices { get; } =
    [
        new(nameof(MobTarget.Player), Tr.T("Players")),
        new(nameof(MobTarget.Villager), Tr.T("Villagers")),
        new(nameof(MobTarget.IronGolem), Tr.T("Iron Golems")),
        new(nameof(MobTarget.Animal), Tr.T("Animals")),
        new(nameof(MobTarget.Monster), Tr.T("Monsters")),
    ];

    private static IEnumerable<BtNodeInfo> Build()
    {
        yield return new BtNodeInfo
        {
            Kind = BtNodeKind.Root, Category = BtCategory.Root, Title = Tr.T("Root"),
            Description = Tr.T("Starting point of the tree. It runs on every game tick; when its branch ends, it starts again."),
        };

        // ----- Composites -----
        yield return new BtNodeInfo
        {
            Kind = BtNodeKind.Selector, Category = BtCategory.Composite, Title = Tr.T("Selector"),
            Description = Tr.T("Tries its children from left to right and stops at the first one that succeeds. Succeeds if one of them succeeds."),
        };
        yield return new BtNodeInfo
        {
            Kind = BtNodeKind.Sequence, Category = BtCategory.Composite, Title = Tr.T("Sequence"),
            Description = Tr.T("Runs its children from left to right and stops at the first one that fails. Succeeds if all of them succeed."),
        };
        yield return new BtNodeInfo
        {
            Kind = BtNodeKind.RandomSelector, Category = BtCategory.Composite, Title = Tr.T("Random selector"),
            Description = Tr.T("Like the selector, but tries its children in a random order each time it starts."),
        };
        yield return new BtNodeInfo
        {
            Kind = BtNodeKind.SimpleParallel, Category = BtCategory.Composite, Title = Tr.T("Simple parallel"),
            Description = Tr.T("The first child is the main task; the others run in the background while it lasts. Ends with the main task."),
        };

        // ----- Tâches -----
        yield return Task(BtNodeKind.Wait, Tr.T("Wait"), Tr.T("Does nothing for a while, then succeeds."),
            Number("seconds", Tr.T("Duration (seconds)"), 1, 0, 3600),
            Number("deviation", Tr.T("Random deviation (seconds)"), 0, 0, 3600));
        yield return Task(BtNodeKind.MoveTo, Tr.T("Move to"), Tr.T("Walks to a creature or a position of the blackboard; succeeds on arrival."),
            KeyParameter(Target, Tr.T("Destination"), EntityOrPosition),
            SpeedParameter(1),
            Number("acceptance", Tr.T("Arrival distance (blocks)"), 1.5, 0.5, 64));
        yield return Task(BtNodeKind.Wander, Tr.T("Wander"), Tr.T("Walks to a random point around it, avoiding water."),
            Integer("radius", Tr.T("Radius (blocks)"), 10, 1, 64),
            SpeedParameter(1));
        yield return Task(BtNodeKind.MeleeAttack, Tr.T("Melee attack"), Tr.T("Chases the creature and hits it once; succeeds after the hit."),
            KeyParameter(Target, Tr.T("Target"), BlackboardKeyType.Entity),
            SpeedParameter(1.2),
            Number("cooldown", Tr.T("Time between two hits (seconds)"), 1, 0, 60));
        yield return Task(BtNodeKind.LookAt, Tr.T("Look at"), Tr.T("Turns its head towards a creature or a position for a while."),
            KeyParameter(Target, Tr.T("Target"), EntityOrPosition),
            Number("seconds", Tr.T("Duration (seconds)"), 1, 0, 3600));
        yield return Task(BtNodeKind.FleeFrom, Tr.T("Flee"), Tr.T("Moves away from a creature or a position; succeeds once at a safe distance."),
            KeyParameter(Target, Tr.T("Danger"), EntityOrPosition),
            Number("distance", Tr.T("Safe distance (blocks)"), 16, 1, 64),
            SpeedParameter(1.3));
        yield return Task(BtNodeKind.FindNearestEntity, Tr.T("Find the nearest creature"),
            Tr.T("Stores the nearest creature of the chosen family in a key; fails if there is none."),
            FamilyParameter(),
            Number("range", Tr.T("Range (blocks)"), 16, 1, 128),
            Boolean("mustSee", Tr.T("Must see it"), true),
            KeyParameter(Result, Tr.T("Store in"), BlackboardKeyType.Entity));
        yield return Task(BtNodeKind.FindRandomPosition, Tr.T("Choose a random position"),
            Tr.T("Stores a reachable random point around the mob in a key."),
            Integer("radius", Tr.T("Radius (blocks)"), 10, 1, 64),
            KeyParameter(Result, Tr.T("Store in"), BlackboardKeyType.Position));
        yield return Task(BtNodeKind.SetBoolean, Tr.T("Set a boolean"), Tr.T("Gives a true/false value to a blackboard key."),
            KeyParameter(Key, Tr.T("Key"), BlackboardKeyType.Boolean),
            Boolean(Value, Tr.T("Value"), true));
        yield return Task(BtNodeKind.SetNumber, Tr.T("Change a number"), Tr.T("Replaces, increases or decreases the number of a blackboard key."),
            KeyParameter(Key, Tr.T("Key"), BlackboardKeyType.Number),
            Choice("operation", Tr.T("Operation"), nameof(BtNumberOperation.Set),
                new(nameof(BtNumberOperation.Set), Tr.T("Replace with")),
                new(nameof(BtNumberOperation.Add), Tr.T("Add")),
                new(nameof(BtNumberOperation.Subtract), Tr.T("Remove"))),
            Number(Value, Tr.T("Value"), 1, -1_000_000, 1_000_000));
        yield return Task(BtNodeKind.ClearKey, Tr.T("Clear a key"), Tr.T("Erases the value of a blackboard key (\"is set\" becomes false)."),
            KeyParameter(Key, Tr.T("Key")));
        yield return Task(BtNodeKind.PlaySound, Tr.T("Play a sound"), Tr.T("Plays a sound at the position of the mob."),
            new BtParameterInfo("sound", Tr.T("Sound"), BtParameterType.Sound, string.Empty),
            Number("volume", Tr.T("Volume"), 1, 0, 10),
            Number("pitch", Tr.T("Pitch"), 1, 0.5, 2));
        yield return Task(BtNodeKind.Leap, Tr.T("Leap"), Tr.T("Jumps, towards a target if one is chosen (like a spider). Fails if it is not on the ground."),
            KeyParameter(Target, Tr.T("Towards"), EntityOrPosition, optional: true),
            Number("height", Tr.T("Leap height"), 0.4, 0.1, 3));
        yield return Task(BtNodeKind.ShootProjectile, Tr.T("Shoot a projectile"), Tr.T("Shoots an arrow, a snowball or a fireball at the target."),
            Choice("projectile", Tr.T("Projectile"), nameof(BtProjectile.Arrow),
                new(nameof(BtProjectile.Arrow), Tr.T("Arrow")),
                new(nameof(BtProjectile.Snowball), Tr.T("Snowball")),
                new(nameof(BtProjectile.SmallFireball), Tr.T("Small fireball"))),
            KeyParameter(Target, Tr.T("Target"), BlackboardKeyType.Entity),
            Number("velocity", Tr.T("Shot speed"), 1.6, 0.1, 5),
            Number("inaccuracy", Tr.T("Inaccuracy"), 6, 0, 30));
        yield return Task(BtNodeKind.Heal, Tr.T("Heal"), Tr.T("Gives health back to the mob."),
            Number("amount", Tr.T("Health points"), 2, 0, 1024));
        yield return Task(BtNodeKind.RunGoal, Tr.T("Run a goal"),
            Tr.T("Runs a public goal (another behavior tree marked as reusable) as a branch of this tree. It uses the blackboard of the mob: keys with the same name are shared."),
            new BtParameterInfo(Goal, Tr.T("Goal"), BtParameterType.Goal, string.Empty));
        yield return Task(BtNodeKind.Teleport, Tr.T("Teleport"), Tr.T("Teleports to a free random point (like an enderman); fails if it finds none."),
            Integer("range", Tr.T("Range (blocks)"), 16, 1, 64));

        // ----- Decorators: conditions -----
        yield return Condition(BtNodeKind.IsSet, Tr.T("Key is set"), Tr.T("True if the blackboard key has a value (a living creature, a position...)."),
            KeyParameter(Key, Tr.T("Key")));
        yield return Condition(BtNodeKind.CheckBoolean, Tr.T("Boolean is true"), Tr.T("True if the blackboard boolean is true."),
            KeyParameter(Key, Tr.T("Key"), BlackboardKeyType.Boolean));
        yield return Condition(BtNodeKind.CompareNumber, Tr.T("Compare a number"), Tr.T("Compares the number of a blackboard key with a value."),
            KeyParameter(Key, Tr.T("Key"), BlackboardKeyType.Number),
            ComparisonParameter(),
            Number(Value, Tr.T("Value"), 0, -1_000_000, 1_000_000));
        yield return Condition(BtNodeKind.CheckDistance, Tr.T("Distance"), Tr.T("Compares the distance between the mob and a creature or a position."),
            KeyParameter(Target, Tr.T("To"), EntityOrPosition),
            ComparisonParameter(nameof(BtComparison.Less)),
            Number(Value, Tr.T("Distance (blocks)"), 4, 0, 256));
        yield return Condition(BtNodeKind.CheckHealth, Tr.T("Health"), Tr.T("Compares the health of the mob, as a percentage of its maximum health."),
            ComparisonParameter(nameof(BtComparison.Less)),
            Number(Value, Tr.T("Health (%)"), 30, 0, 100));
        yield return Condition(BtNodeKind.CheckTimeOfDay, Tr.T("Time of day"), Tr.T("True during the day, or during the night."),
            Choice("period", Tr.T("Time"), nameof(BtTimeOfDay.Day),
                new(nameof(BtTimeOfDay.Day), Tr.T("Day")),
                new(nameof(BtTimeOfDay.Night), Tr.T("Night"))));
        yield return Condition(BtNodeKind.IsInWater, Tr.T("In water"), Tr.T("True when the mob is in water."));
        yield return Condition(BtNodeKind.CanSee, Tr.T("Sees the creature"), Tr.T("True if nothing hides the creature from the mob."),
            KeyParameter(Target, Tr.T("Creature"), BlackboardKeyType.Entity));
        yield return new BtNodeInfo
        {
            Kind = BtNodeKind.RandomChance, Category = BtCategory.Decorator, Title = Tr.T("Random chance"), IsCondition = true,
            Description = Tr.T("Lets the node run with the chosen probability, drawn each time it starts."),
            Parameters = [Number("percent", Tr.T("Chance (%)"), 50, 0, 100)],
        };

        // ----- Decorators: modifiers -----
        yield return Decorator(BtNodeKind.Cooldown, Tr.T("Cooldown"), Tr.T("Once the node has finished, it cannot start again before this delay."),
            Number("seconds", Tr.T("Delay (seconds)"), 5, 0, 3600));
        yield return Decorator(BtNodeKind.Loop, Tr.T("Loop"), Tr.T("Restarts the node the chosen number of times (0: forever)."),
            Integer("count", Tr.T("Number of times"), 3, 0, 1000));
        yield return Decorator(BtNodeKind.TimeLimit, Tr.T("Time limit"), Tr.T("The node fails if it lasts longer than this delay."),
            Number("seconds", Tr.T("Delay (seconds)"), 10, 0.05, 3600));
        yield return Decorator(BtNodeKind.ForceSuccess, Tr.T("Always succeed"), Tr.T("The node succeeds even if it fails."));
        yield return Decorator(BtNodeKind.InvertResult, Tr.T("Invert the result"), Tr.T("The node succeeds when it fails, and the other way round."));

        // ----- Services -----
        yield return Service(BtNodeKind.FindNearestEntityService, Tr.T("Spot the nearest creature"),
            Tr.T("While the node runs, regularly stores the nearest creature in a key (the key is cleared when there is none left)."),
            FamilyParameter(),
            Number("range", Tr.T("Range (blocks)"), 16, 1, 128),
            Boolean("mustSee", Tr.T("Must see it"), true),
            KeyParameter(Result, Tr.T("Store in"), BlackboardKeyType.Entity));
        yield return Service(BtNodeKind.RememberAttacker, Tr.T("Remember the attacker"),
            Tr.T("While the node runs, stores the last creature that hit the mob in a key."),
            KeyParameter(Result, Tr.T("Store in"), BlackboardKeyType.Entity));
        yield return Service(BtNodeKind.UpdateDistance, Tr.T("Measure a distance"),
            Tr.T("While the node runs, stores the distance between the mob and a creature or a position in a key."),
            KeyParameter(Target, Tr.T("To"), EntityOrPosition),
            KeyParameter(Result, Tr.T("Store in"), BlackboardKeyType.Number));

        // ----- Annotations -----
        yield return new BtNodeInfo
        {
            Kind = BtNodeKind.Comment, Category = BtCategory.Annotation, Title = Tr.T("Comment"),
            Description = Tr.T("Frames nodes with a title, like the comments of Blueprints: moving it moves the nodes inside. It does not change the AI."),
        };
        yield return new BtNodeInfo
        {
            Kind = BtNodeKind.Tooltip, Category = BtCategory.Annotation, Title = Tr.T("Tooltip"),
            Description = Tr.T("Message shown when the mouse is over the bubble or over the nodes it describes. It does not change the AI."),
        };
    }

    private static BtNodeInfo Task(BtNodeKind kind, string title, string description, params BtParameterInfo[] parameters) =>
        new() { Kind = kind, Category = BtCategory.Task, Title = title, Description = description, Parameters = parameters };

    private static BtNodeInfo Decorator(BtNodeKind kind, string title, string description, params BtParameterInfo[] parameters) =>
        new() { Kind = kind, Category = BtCategory.Decorator, Title = title, Description = description, Parameters = parameters };

    /// <summary>Condition: its settings, then the inversion and the abort mode.</summary>
    private static BtNodeInfo Condition(BtNodeKind kind, string title, string description, params BtParameterInfo[] parameters) =>
        new()
        {
            Kind = kind, Category = BtCategory.Decorator, Title = title, Description = description, IsCondition = true,
            Parameters =
            [
                .. parameters,
                Boolean(Inverse, Tr.T("Invert (\"is not\")"), false),
                new BtParameterInfo(Abort, Tr.T("Check again"), BtParameterType.Choice, nameof(BtAbortMode.None))
                {
                    Choices = AbortChoices,
                    Hint = Tr.T("Unreal's \"Observer aborts\": the condition can be checked again on every tick while the tree runs."),
                },
            ],
        };

    /// <summary>Service: its settings, then the update interval.</summary>
    private static BtNodeInfo Service(BtNodeKind kind, string title, string description, params BtParameterInfo[] parameters) =>
        new()
        {
            Kind = kind, Category = BtCategory.Service, Title = title, Description = description,
            Parameters = [.. parameters, Number(Interval, Tr.T("Every (seconds)"), 0.5, 0.05, 60)],
        };

    private static BtParameterInfo Number(string name, string label, double defaultValue, double min, double max) =>
        new(name, label, BtParameterType.Number, Format(defaultValue)) { Min = min, Max = max };

    private static BtParameterInfo Integer(string name, string label, int defaultValue, int min, int max) =>
        new(name, label, BtParameterType.Integer, Format(defaultValue)) { Min = min, Max = max };

    private static BtParameterInfo Boolean(string name, string label, bool defaultValue) =>
        new(name, label, BtParameterType.Boolean, defaultValue ? "true" : "false");

    private static BtParameterInfo Choice(string name, string label, string defaultValue, params BtChoice[] choices) =>
        new(name, label, BtParameterType.Choice, defaultValue) { Choices = choices };

    private static BtParameterInfo SpeedParameter(double defaultValue) =>
        Number(Speed, Tr.T("Speed (× mob speed)"), defaultValue, 0.1, 5);

    private static BtParameterInfo FamilyParameter() =>
        new("family", Tr.T("Family"), BtParameterType.Choice, nameof(MobTarget.Player)) { Choices = FamilyChoices };

    private static BtParameterInfo ComparisonParameter(string defaultValue = nameof(BtComparison.GreaterOrEqual)) =>
        new("operator", Tr.T("Comparison"), BtParameterType.Choice, defaultValue) { Choices = ComparisonChoices };

    private static BtParameterInfo KeyParameter(string name, string label, params BlackboardKeyType[] types) =>
        new(name, label, BtParameterType.Key, string.Empty) { KeyTypes = types };

    private static BtParameterInfo KeyParameter(string name, string label, BlackboardKeyType[] types, bool optional) =>
        new(name, label, BtParameterType.Key, string.Empty) { KeyTypes = types, Optional = optional };

    /// <summary>Number written as in the files (invariant culture).</summary>
    public static string Format(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
