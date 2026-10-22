namespace MineEngine.Assets.Puppeteer;

/// <summary>
/// Kinds of nodes of a Puppeteer behavior tree, like Unreal's Behavior Trees: the root,
/// the composites (which choose the order of their children), the tasks (leaves that act),
/// and what is attached to a node: decorators (conditions or modifiers) and services
/// (regular updates of the blackboard). The names are stored in the files: do not
/// rename them.
/// </summary>
public enum BtNodeKind
{
    Root,

    // ----- Composites -----
    Selector,
    Sequence,
    RandomSelector,
    SimpleParallel,

    // ----- Tâches -----
    Wait,
    MoveTo,
    Wander,
    MeleeAttack,
    LookAt,
    FleeFrom,
    FindNearestEntity,
    FindRandomPosition,
    SetBoolean,
    SetNumber,
    ClearKey,
    PlaySound,
    Leap,
    ShootProjectile,
    Heal,
    Teleport,

    /// <summary>Runs a public goal (another behavior tree) as a branch of this tree.</summary>
    RunGoal,

    // ----- Decorators: conditions -----
    IsSet,
    CheckBoolean,
    CompareNumber,
    CheckDistance,
    CheckHealth,
    CheckTimeOfDay,
    IsInWater,
    CanSee,
    RandomChance,

    // ----- Decorators: modifiers -----
    Cooldown,
    Loop,
    TimeLimit,
    ForceSuccess,
    InvertResult,

    // ----- Services -----
    FindNearestEntityService,
    RememberAttacker,
    UpdateDistance,

    // ----- Annotations: no effect on the AI (see GraphAnnotations for their values) -----
    Comment,
    Tooltip,
}

/// <summary>Role of a kind of node in the tree.</summary>
public enum BtCategory
{
    Root,
    Composite,
    Task,
    Decorator,
    Service,

    /// <summary>Comment or tooltip: not part of the tree.</summary>
    Annotation,
}

/// <summary>Type of a blackboard key (the memory of the mob).</summary>
public enum BlackboardKeyType
{
    /// <summary>A creature (player, mob...); the key is cleared when it dies.</summary>
    Entity,

    /// <summary>A point of the world.</summary>
    Position,

    Number,

    Boolean,
}

/// <summary>When a condition is checked again while the tree runs (Unreal's "Observer aborts").</summary>
public enum BtAbortMode
{
    /// <summary>Checked only when the node starts.</summary>
    None,

    /// <summary>The node stops as soon as the condition is no longer met.</summary>
    Self,

    /// <summary>The node interrupts its siblings on the right (lower priority) as soon as the condition becomes true.</summary>
    LowerPriority,

    /// <summary>Both.</summary>
    Both,
}

public enum BtComparison
{
    Less,
    LessOrEqual,
    Equal,
    GreaterOrEqual,
    Greater,
}

public enum BtNumberOperation
{
    Set,
    Add,
    Subtract,
}

public enum BtProjectile
{
    Arrow,
    Snowball,
    SmallFireball,
}

public enum BtTimeOfDay
{
    Day,
    Night,
}
