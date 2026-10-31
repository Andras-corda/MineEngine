using MineEngine.Core.GameData;

namespace MineEngine.Assets.Graphs;

/// <summary>Famille d'un nœud ; donne sa couleur et son dossier dans le menu d'ajout.</summary>
public enum NodeCategory
{
    /// <summary>Point de départ : réagit à un événement du jeu.</summary>
    Event,

    /// <summary>Choisit la suite selon une condition (sorties Vrai / Faux).</summary>
    Condition,

    /// <summary>Agit sur le jeu, puis continue.</summary>
    Action,

    /// <summary>Calcule une valeur, sans fil d'exécution.</summary>
    Variable,

    /// <summary>Entrée, retour et appel d'une fonction ou d'une procédure.</summary>
    Function,

    /// <summary>Objets : création, champs, valeurs d'énumération, variables du graphe.</summary>
    Object,

    /// <summary>Call of a function or a procedure of a function library (gray).</summary>
    Library,

    /// <summary>Read or change a global variable (Global tab).</summary>
    Global,
}

/// <summary>Sorte de nœud. Le nom est enregistré dans les fichiers : ne pas renommer.</summary>
public enum GraphNodeKind
{
    // Événements
    OnRightClickBlock,
    OnUseItem,
    OnPlayerJoin,
    OnBlockBroken,

    // Conditions
    Branch,
    HasItem,
    HoldsItem,
    IsSneaking,
    Chance,

    // Actions
    SpawnItem,
    GiveItem,
    RemoveItem,
    SendMessage,
    PlaySound,
    SetBlock,
    Explosion,
    CancelEvent,

    // Valeurs
    NumberValue,
    TextValue,
    BooleanValue,
    PlayerPosition,
    PlayerName,
    OffsetPosition,
    RandomNumber,
    JoinText,
    GreaterThan,
    NumbersEqual,
    Not,

    // Événements d'un item du mod (V0.4.1) : générés dans la classe de l'item.
    OnItemRightClick,
    OnItemUseOnBlock,
    OnItemHitEntity,
    OnItemBlockMined,
    OnItemConsumed,
    OnItemCrafted,
    OnItemHeldTick,

    // Programmation (V0.4.3) : nœuds décrits par les définitions du projet (voir ProgramModel).
    /// <summary>Début du corps d'une fonction, d'une procédure ou d'un constructeur : ses sorties sont les paramètres.</summary>
    FunctionEntry,

    /// <summary>Fin d'une fonction ou d'une procédure : ses entrées sont les valeurs renvoyées.</summary>
    FunctionReturn,

    /// <summary>Appel d'une fonction, d'une procédure ou d'une méthode (Target : sa définition ; Member : méthode d'interface).</summary>
    CallFunction,

    /// <summary>Création d'un objet d'une classe ou d'une structure (Target).</summary>
    NewObject,

    /// <summary>Lecture d'une variable du graphe (Member : son identifiant).</summary>
    GetVariable,

    /// <summary>Modification d'une variable du graphe.</summary>
    SetVariable,

    /// <summary>Lecture d'un champ (Target : la classe ou la structure ; Member : le champ).</summary>
    GetField,

    /// <summary>Modification d'un champ.</summary>
    SetField,

    /// <summary>Une valeur d'une énumération (Target), choisie sur le nœud.</summary>
    EnumLiteral,

    /// <summary>Compare deux valeurs d'une énumération (Target).</summary>
    EnumEquals,

    /// <summary>L'objet dont on exécute la méthode ou le constructeur ("this").</summary>
    Self,

    /// <summary>Appel du constructeur de la classe parente (Target), au début d'un constructeur.</summary>
    SuperConstructor,

    // Calcul (V0.4.3)
    Add,
    Subtract,
    Multiply,
    LessThan,

    // Mise en page (V0.5.4)
    /// <summary>Point de passage d'un fil (voir <see cref="Reroutes"/>) : sans effet sur le programme.</summary>
    Reroute,

    // Blocks (V0.6.2): actions
    RemoveBlock,
    ReplaceBlock,
    UpdateBlock,
    BreakBlock,
    SetBlockNbtNumber,
    SetBlockNbtText,
    SetBlockNbtBoolean,
    SetBlockProperty,
    UseBlock,
    SetBlockDirection,

    // Blocks: values
    GetBlockAt,
    GetBlockProperty,
    GetBlockDirection,
    GetBlockOpacity,
    GetBlockHardness,
    GetLightLevel,
    GetEnchantPowerBonus,
    IsBlockSolid,
    GetBlockNbtNumber,
    GetBlockNbtText,
    GetBlockNbtBoolean,

    // Items (V0.6.2): actions
    SetItemCooldown,
    AddEnchantment,
    RemoveEnchantment,
    RemoveAllEnchantments,
    SetItemDisplayName,

    // Items: values
    MainHandItem,
    NewItemStack,
    ItemOfStack,
    ItemCooldownPercent,
    IsItemOnCooldown,
    CopyItemStack,
    ItemMaxStackSize,
    ItemCount,
    IsItemStackable,
    ItemDamage,
    ItemMaxDamage,
    IsItemDamageable,
    IsItemDamaged,
    EnchantmentLevel,
    HasEnchantment,
    IsItemEnchantable,
    IsItemEnchanted,
    ItemFoodValue,
    ItemSaturation,
    IsItemFood,
    CanHarvestBlock,
    ItemHasRarity,
    ItemRarity,
    ItemDisplayName,
    ItemRegistryName,
    CanBeSmelted,

    // ----- V0.6.2: commands and keybinds -----
    OnCommand,
    OnKeyPressed,

    // ----- V0.6.3: game rules and effects -----
    GetGameRuleBoolean,
    GetGameRuleNumber,
    SetGameRuleBoolean,
    SetGameRuleNumber,
    GiveEffect,
    RemoveEffect,
    HasEffect,

    // ----- V0.6.5: logic -----
    And,
    Or,
    NumbersNotEqual,
    GreaterOrEqual,
    LessOrEqual,
    TextEquals,

    // ----- V0.6.5: loops -----
    Repeat,
    While,
    ForEachPlayer,

    // ----- V0.6.5: math -----
    Divide,
    Modulo,
    Power,
    SquareRoot,
    Minimum,
    Maximum,
    Absolute,

    // ----- V0.6.5: text -----
    TextLength,
    TextContains,
    ReplaceText,
    TextToNumber,
    NumberToText,

    // ----- V0.6.5: world -----
    Dimension,
    WorldTime,
    IsDay,
    IsRaining,
    IsThundering,
    SetWorldTime,
    SetWeather,
    SpawnEntity,
    SpawnParticles,

    // ----- V0.6.5: players -----
    PlayerHealth,
    PlayerMaxHealth,
    PlayerFood,
    IsPlayerAlive,
    SetPlayerHealth,
    HealPlayer,
    DamagePlayer,
    TeleportPlayer,
    ArmorItem,
    OffhandItem,
    LookedAtBlock,

    // ----- V0.6.5: inventory and item data -----
    ItemInSlot,
    ClearSlot,
    CountItemInInventory,
    SetItemNbtNumber,
    SetItemNbtText,
    GetItemNbtNumber,
    GetItemNbtText,

    // ----- V0.6.5: positions -----
    PositionX,
    PositionY,
    PositionZ,
    MakePosition,
    Distance,
    PositionsEqual,
    IsInArea,
    OffsetInDirection,

    // ----- V0.6.5: events -----
    OnPlayerLeave,
    OnPlayerTick,
    OnPlayerHurt,
    OnEntitySpawn,
    OnEntityDeath,

    // ----- Annotations (see GraphAnnotations): no effect on the program -----
    Comment,
    Tooltip,

    // ----- Flow nodes with several execution outputs (see FlowNodes) -----
    SwitchOnNumber,
    SwitchOnText,
    Sequence,
    FlipFlop,

    // ----- Commands, ray casts, physics, sounds and music -----
    RunCommandAsPlayer,
    RunCommandAsServer,
    RaycastHitsBlock,
    RaycastBlockPosition,
    LooksAtCreature,
    CreatureLookedAt,
    SetVelocity,
    AddVelocity,
    LaunchForward,
    SetGravity,
    IsOnGround,
    FallDistance,
    PlaySoundAdvanced,
    PlaySoundToPlayer,
    StopSounds,
    PlayMusic,
    StopMusic,

    // ----- Appearances: textures and 3D models switched while the game runs -----
    SetItemAppearance,
    ItemAppearance,
    SetBlockAppearance,
    BlockAppearance,
    SetCreatureAppearance,
    CreatureAppearance,

    // ----- Global variables (V0.6.7): the node targets the variable (Target) -----
    GetGlobal,
    SetGlobal,

    // ----- Collections (typed by their collection, see GenericNodes): keep this range together -----
    CollectionSize,
    CollectionClear,
    CollectionContains,
    CollectionRemove,
    ListGet,
    ListSet,
    ListAdd,
    ListRemoveAt,
    MapGet,
    MapPut,
    ArrayResize,
    ForEachInCollection,

    // ----- Vectors (typed by their vector): keep this range together -----
    MakeVector,
    VectorX,
    VectorY,
    VectorZ,
    VectorLength,
    AddVectors,
    VectorToPosition,

    // ----- Decimal numbers, directions, block states, entities -----
    PositionToVector,
    RoundDecimal,
    FloorDecimal,
    DecimalAdd,
    DecimalSubtract,
    DecimalMultiply,
    DecimalDivide,
    DecimalGreater,
    DecimalLess,
    RandomDecimal,
    PlayerFacing,
    OppositeDirection,
    OffsetByDirection,
    DirectionName,
    BlockStateAt,
    SetBlockState,
    BlockOfState,
    DefaultBlockState,
    StateProperty,
    StateWithProperty,
    EntityPosition,
    EntityTypeId,
    IsEntityAlive,
    RemoveEntity,
    NearestEntity,
    EntityLookedAt,
    TeleportEntity,
    DamageEntity,
    EntityHealth,
    EntityIsPlayer,

    // ----- Casts (typed by the wire connected to them, see GenericNodes): keep this range together -----
    CastToNumber,
    CastToLong,
    CastToFloat,
    CastToDecimal,
    CastToByte,
    CastToChar,
    CastToText,
    CastToComponent,
    CastToIdentifier,
    CastToUuid,
    CastToLiving,
    CastToPlayer,

    // ----- Nodes of the types of TypesVar.md -----
    CharAt,
    EntityUuid,
    EntityByUuid,
    RandomUuid,
    StyledText,
    SendRichMessage,
    IdentifierNamespace,
    IdentifierPath,
    EquipmentItem,
    ItemInHand,
    SwingHand,
    StackRarity,
    FluidAt,
    PlaceFluid,
    EntityBox,
    MakeBox,
    EntitiesInBox,
    BoxContains,
    GenericDamage,
    DamageWithSource,
    MakeEffect,
    ApplyEffect,
    NewNbt,
    NbtPutNumber,
    NbtPutText,
    NbtGetNumber,
    NbtGetText,
    CurrentWorld,
    WorldOf,
    WorldDimension,
    GameTime,

    // ----- Guilds and groups (scopes player_guild and entity_group) -----
    SetPlayerGuild,
    PlayerGuild,
    SetEntityGroup,
    EntityGroup,

    // ----- Tags (V0.7.1) -----
    IsBlockInTag,
    IsItemInTag,
    IsEntityInTag,
    IsBiomeInTag,

    // ----- Camera of the player (V0.7.4): keep the actions together, from SetCameraRotation to ResetCamera -----
    SetCameraRotation,
    TurnCamera,
    CameraLookAt,
    ViewFromEntity,
    SetCameraPerspective,
    SetCameraZoom,
    ShakeCamera,
    TiltCamera,
    SetHudVisible,
    ResetCamera,
    CameraYaw,
    CameraPitch,
    CameraDirection,
    CameraPosition,

    // ----- Sparkle particle systems (V0.7.5) -----
    PlaySparkle,
    PlaySparkleOnEntity,

    // ----- Sparkle in the graphs (V0.7.7) -----
    SetSparkleNumber,
    SetSparkleVector,
    PlaySparkleBetween,
    StopSparkleOnEntity,
    StopSparklesAt,

    // ----- Events of a mob of the mod (V0.7.9) -----
    OnMobStruckByLightning,
    OnMobFalls,
    OnMobSpawns,
    OnMobDies,
    OnMobPlayerCollides,
    OnMobTick,
    OnMobHurt,
    OnMobRightClicked,
    OnMobKillsEntity,
    OnMobRideStarts,
    OnMobRideStops,
    OnMobRiddenTick,
    OnMobTamed,
    OnMobEntityCollides,

    // ----- Events of a block of the mod (V0.7.11) -----
    OnBlockRightClicked,
    OnBlockAdded,
    OnBlockNeighborChanged,
    OnBlockTick,
    OnBlockDestroyed,
    OnBlockDestroyedByPlayer,
    OnBlockDestroyedByExplosion,
    OnBlockStartDestroy,
    OnBlockEntityCollides,
    OnBlockPlayerCollides,
    OnBlockEntityWalksOn,
    OnBlockPlayerWalksOn,
    OnBlockEntityFallsOn,
    OnBlockHitByProjectile,
    OnBlockPlaced,
    OnBlockRedstoneOn,
    OnBlockRedstoneOff,

    // Events of an item of the mod (V0.7.12, item.md).
    OnItemUseOnEntity,
    OnItemBroken,
    OnItemDropped,
    OnItemPickedUp,
    OnItemEatingStarted,
    OnItemEatingStopped,
    OnItemEatingTick,
    OnItemToolSwingTick,
    OnItemToolSwung,
    OnItemChargeStarted,
    OnItemCharged,
    OnItemChargingTick,
    OnItemShot,

    // Damage types and drop tables (V0.7.10).
    DamageOfType,
    DropLootTable,
}

/// <summary>Values offered by a text input of a node, taken from the project (V0.7.7).</summary>
public enum PinSuggestion
{
    None,

    /// <summary>Identifiers of the Sparkle particle systems of the project.</summary>
    SparkleSystem,

    /// <summary>User parameters of the system chosen on the "system" input of the node.</summary>
    SparkleParameter,

    /// <summary>Identifiers of the mobs of the project (V0.7.9).</summary>
    Mob,

    /// <summary>Identifiers of the blocks of the project (V0.7.11).</summary>
    Block,

    /// <summary>Identifiers of the damage types of the project and of the game (V0.7.10).</summary>
    DamageType,

    /// <summary>Identifiers of the drop tables of the project (V0.7.10).</summary>
    LootTable,
}

/// <summary>Domain of a node, used to sort the Nodes window like MCreator's procedure blocks.</summary>
public enum NodeGroup
{
    Events,
    Logic,
    Math,
    Text,
    Player,
    Camera,
    Item,
    Block,
    World,
    Program,
    Collections,
    Entities,
    Globals,
    Conversions,
}

/// <summary>
/// Entrée ou sortie d'un nœud. Une entrée dont le type a une valeur en ligne (nombre,
/// texte, item...) prend <see cref="DefaultValue"/> tant qu'elle n'est pas reliée ;
/// une entrée non reliable (<see cref="Connectable"/> faux) n'est qu'un champ du nœud.
/// Une entrée facultative (<see cref="Optional"/>) peut rester vide : l'objet d'une méthode
/// appelée depuis sa propre classe vaut alors "this".
/// </summary>
public sealed record PinInfo(string Name, string Label, DataType Type, string? DefaultValue = null, bool Connectable = true)
{
    public bool Optional { get; init; }

    /// <summary>Valeurs proposées pour une entrée d'énumération (identifiant, nom), vide sinon.</summary>
    public IReadOnlyList<EnumConstant> Choices { get; init; } = [];

    /// <summary>Values of the project offered by a text input (it can still be typed).</summary>
    public PinSuggestion Suggest { get; init; }
}

/// <summary>Description d'une sorte de nœud : titre, famille, entrées et sorties.</summary>
public sealed class NodeInfo
{
    /// <summary>Entrée d'exécution de tous les nœuds qui en ont une.</summary>
    public const string ExecInput = "exec";

    /// <summary>Sortie d'exécution "la suite" des événements et des actions.</summary>
    public const string ExecThen = "then";

    public const string ExecTrue = "true";

    public const string ExecFalse = "false";

    /// <summary>Execution output of a loop: run at each turn (V0.6.5).</summary>
    public const string ExecBody = "body";

    /// <summary>Entrée "objet" d'une méthode ou d'un champ.</summary>
    public const string TargetInput = "target";

    private static readonly PinInfo ThenPin = new(ExecThen, string.Empty, DataType.Exec);

    /// <param name="flow">
    /// Fil d'exécution du nœud ; par défaut celui de sa famille : sortie seule pour un événement,
    /// entrée et sortie pour une action, Vrai / Faux pour une condition, aucun pour une valeur.
    /// </param>
    /// <param name="execOutputs">Execution outputs of a <see cref="NodeFlow.Multi"/> node.</param>
    public NodeInfo(
        GraphNodeKind kind,
        NodeCategory category,
        string title,
        string description,
        IReadOnlyList<PinInfo>? inputs = null,
        IReadOnlyList<PinInfo>? outputs = null,
        NodeFlow? flow = null,
        IReadOnlyList<PinInfo>? execOutputs = null)
    {
        Kind = kind;
        Category = category;
        Title = title;
        Description = description;
        Inputs = inputs ?? [];
        Outputs = outputs ?? [];
        Flow = flow ?? category switch
        {
            NodeCategory.Event => NodeFlow.Start,
            NodeCategory.Condition => NodeFlow.Branch,
            NodeCategory.Action => NodeFlow.Step,
            _ => NodeFlow.Pure,
        };
        ExecOutputs = Flow switch
        {
            NodeFlow.Start or NodeFlow.Step => [ThenPin],
            NodeFlow.Branch => [new PinInfo(ExecTrue, Tr.T("True"), DataType.Exec), new PinInfo(ExecFalse, Tr.T("False"), DataType.Exec)],
            NodeFlow.Loop => [new PinInfo(ExecBody, Tr.T("Loop body"), DataType.Exec), new PinInfo(ExecThen, Tr.T("Completed"), DataType.Exec)],
            NodeFlow.Multi => execOutputs ?? [],
            _ => [],
        };
    }

    public GraphNodeKind Kind { get; }

    public NodeCategory Category { get; }

    public string Title { get; }

    public string Description { get; }

    public NodeFlow Flow { get; }

    /// <summary>Entrées de valeur (sans l'entrée d'exécution).</summary>
    public IReadOnlyList<PinInfo> Inputs { get; }

    /// <summary>Sorties de valeur (sans les sorties d'exécution).</summary>
    public IReadOnlyList<PinInfo> Outputs { get; }

    /// <summary>Sorties d'exécution : "then", ou "true" et "false" pour une condition.</summary>
    public IReadOnlyList<PinInfo> ExecOutputs { get; }

    public bool HasExecInput => Flow is NodeFlow.Branch or NodeFlow.Step or NodeFlow.End or NodeFlow.Loop or NodeFlow.Multi;

    /// <summary>Execution outputs shown on the side of the node, with the value outputs (all but the single "then").</summary>
    public bool HasSideExecOutputs => Flow is NodeFlow.Branch or NodeFlow.Loop or NodeFlow.Multi;

    /// <summary>Point de départ d'une exécution : événement, ou entrée d'une fonction.</summary>
    public bool IsEntryPoint => Flow == NodeFlow.Start;

    /// <summary>Vrai pour un nœud de valeur : il est calculé là où sa sortie est utilisée.</summary>
    public bool IsPure => Flow == NodeFlow.Pure;

    /// <summary>Événement que Minecraft permet d'annuler (le nœud "Annuler l'événement" y est permis).</summary>
    public bool IsCancellable { get; init; }

    /// <summary>Événement propre à un item du mod (son entrée "item" désigne l'item), sinon null.</summary>
    public ItemEventKind? ItemEvent { get; init; }

    /// <summary>Event of a mob of the mod (its input "mob" names the mob, V0.7.9), or null.</summary>
    public MobEventKind? MobEvent { get; init; }

    /// <summary>Event of a block of the mod (its input "block" names the block, V0.7.11), or null.</summary>
    public BlockEventKind? BlockEvent { get; init; }

    /// <summary>
    /// Event run by a command or a keybind of the mod (V0.6.2), called directly by the generated code
    /// rather than by the event bus; its first input names the command or the keybind.
    /// </summary>
    public DirectEventKind? DirectEvent { get; init; }

    /// <summary>Vrai si la définition visée par le nœud n'existe plus (le nœud est alors en erreur).</summary>
    public bool IsMissing { get; init; }

    /// <summary>Name of the function library of a called function: its domain in the Nodes window.</summary>
    public string? Library { get; init; }

    public PinInfo? FindInput(string name) => Inputs.FirstOrDefault(p => p.Name == name);

    public PinInfo? FindOutput(string name) => Outputs.FirstOrDefault(p => p.Name == name);

    public PinInfo? FindExecOutput(string name) => ExecOutputs.FirstOrDefault(p => p.Name == name);

    /// <summary>Décrit les broches : deux descriptions égales donnent le même nœud à l'écran.</summary>
    public string Signature => string.Join("|",
        new[] { Title, Flow.ToString() }
            .Concat(Inputs.Select(p => $"i:{p.Name}:{p.Label}:{p.Type}:{p.Optional}:{string.Join(',', p.Choices.Select(c => c.Name))}"))
            .Concat(Outputs.Select(p => $"o:{p.Name}:{p.Label}:{p.Type}"))
            .Concat(ExecOutputs.Select(p => $"x:{p.Name}:{p.Label}")));

    public override string ToString() => Title;
}

/// <summary>Place d'un nœud dans le fil d'exécution.</summary>
public enum NodeFlow
{
    /// <summary>Point de départ (événement, entrée d'une fonction) : une sortie, pas d'entrée.</summary>
    Start,

    /// <summary>Instruction : une entrée, une sortie "la suite".</summary>
    Step,

    /// <summary>Condition : une entrée, sorties Vrai et Faux.</summary>
    Branch,

    /// <summary>Fin (retour d'une fonction) : une entrée, pas de suite.</summary>
    End,

    /// <summary>Valeur calculée à la demande, sans fil d'exécution.</summary>
    Pure,

    /// <summary>Loop (V0.6.5): an input, outputs "Loop body" (each turn) and "Completed".</summary>
    Loop,

    /// <summary>An input and the execution outputs given to the node (Switch, Sequence, Flip Flop).</summary>
    Multi,
}
