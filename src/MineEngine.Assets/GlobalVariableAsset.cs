using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>Where a global variable lives and how long it is kept (like the global variables of MCreator).</summary>
public enum GlobalScope
{
    /// <summary>One value for the whole save, kept with the world (every dimension shares it).</summary>
    Map,

    /// <summary>One value per dimension (overworld, Nether, End...), kept with the world.</summary>
    World,

    /// <summary>One value while the server runs; back to its default value at each start.</summary>
    Session,

    /// <summary>One value per player, kept with the player but back to its default value when the player dies.</summary>
    PlayerLifetime,

    /// <summary>One value per player, kept with the player, even after death.</summary>
    PlayerPersistent,

    // ----- More scopes (scopeGlobalVariable.md) -----

    /// <summary>One value per player, while the player is connected (back to its default value at the next connection).</summary>
    PlayerSession,

    /// <summary>One value per player and per dimension, kept with the save.</summary>
    PlayerDimension,

    /// <summary>One value per account of player, shared by every save of this game or server.</summary>
    PlayerAccount,

    /// <summary>One value per guild, shared by its players (the guild of a player is set by a node).</summary>
    PlayerGuild,

    /// <summary>One value per team of the scoreboard (/team), shared by its players; a player without team has its own.</summary>
    PlayerTeam,

    /// <summary>One value shared by every entity of the same type (every zombie...).</summary>
    EntityType,

    /// <summary>One value per owner: shared by the tamed animals of a player (an entity without owner has its own).</summary>
    EntityOwner,

    /// <summary>One value per group of entities (the group of an entity is set by a node).</summary>
    EntityGroup,

    /// <summary>One value per entity and per dimension.</summary>
    EntityDimension,

    /// <summary>One value per chunk (16 x 16 blocks of a dimension).</summary>
    GlobalChunk,

    /// <summary>One value for the whole server, shared by every save (kept in the config folder of the game).</summary>
    GlobalServer,

    /// <summary>One value per structure of the world (a village, a temple...): shared by the positions inside it.</summary>
    WorldStructure,

    /// <summary>One value per biome (plains, desert...).</summary>
    WorldBiome,

    /// <summary>One value per dimension, kept with the world (like global_dimension).</summary>
    WorldDimension,
}

/// <summary>What the nodes of a global variable need to find its value.</summary>
public enum GlobalScopeKey
{
    None,
    Player,
    Entity,
    Position,
}

/// <summary>
/// Global variable of the mod (V0.6.7, Global tab): a value of any type (number, text, block
/// state, vector, entity, an object of the project, or a collection of them) readable and
/// changeable from every graph, kept according to its <see cref="Scope"/>.
/// </summary>
public sealed class GlobalVariableAsset : Asset
{
    private DataType _valueType = DataType.Number;
    private GlobalScope _scope = GlobalScope.Map;
    private string _defaultValue = string.Empty;

    public GlobalVariableAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.GlobalVariable;

    /// <summary>Type of the value (a collection gives its element and key types).</summary>
    public DataType ValueType
    {
        get => _valueType;
        set => SetField(ref _valueType, value ?? DataType.Number);
    }

    public GlobalScope Scope
    {
        get => _scope;
        set => SetField(ref _scope, value);
    }

    /// <summary>Starting value of a simple type (number, text, true/false, direction); empty: the default value of the type.</summary>
    public string DefaultValue
    {
        get => _defaultValue;
        set => SetField(ref _defaultValue, value?.Trim() ?? string.Empty);
    }

    /// <summary>Name of the variable in the graphs and in the Java code.</summary>
    public string VariableName => string.IsNullOrWhiteSpace(DisplayName) ? ResourceId.Value : DisplayName;

    /// <summary>The variable has a value per player: its nodes need the player.</summary>
    public bool IsPerPlayer => KeyOf(Scope) == GlobalScopeKey.Player;

    /// <summary>The value is written in the save (every scope but the sessions).</summary>
    public bool IsSaved => Scope is not (GlobalScope.Session or GlobalScope.PlayerSession);

    /// <summary>What the nodes of a variable of this scope need: a player, an entity, a position (the world is always known), or nothing.</summary>
    public static GlobalScopeKey KeyOf(GlobalScope scope) => scope switch
    {
        GlobalScope.PlayerLifetime or GlobalScope.PlayerPersistent or GlobalScope.PlayerSession or GlobalScope.PlayerDimension
            or GlobalScope.PlayerAccount or GlobalScope.PlayerGuild or GlobalScope.PlayerTeam => GlobalScopeKey.Player,
        GlobalScope.EntityType or GlobalScope.EntityOwner or GlobalScope.EntityGroup or GlobalScope.EntityDimension => GlobalScopeKey.Entity,
        GlobalScope.GlobalChunk or GlobalScope.WorldStructure or GlobalScope.WorldBiome => GlobalScopeKey.Position,
        _ => GlobalScopeKey.None,
    };

    /// <summary>Scopes in the order they are offered: players, entities, global, world.</summary>
    public static IReadOnlyList<GlobalScope> OrderedScopes { get; } =
    [
        GlobalScope.PlayerPersistent, GlobalScope.PlayerSession, GlobalScope.PlayerLifetime, GlobalScope.PlayerDimension,
        GlobalScope.PlayerAccount, GlobalScope.PlayerGuild, GlobalScope.PlayerTeam,
        GlobalScope.EntityType, GlobalScope.EntityOwner, GlobalScope.EntityGroup, GlobalScope.EntityDimension,
        GlobalScope.Map, GlobalScope.World, GlobalScope.Session, GlobalScope.GlobalChunk, GlobalScope.GlobalServer,
        GlobalScope.WorldStructure, GlobalScope.WorldBiome, GlobalScope.WorldDimension,
    ];

    /// <summary>Types that cannot be written in a save: an entity, a player, an object of the project.</summary>
    public static bool CanBeSaved(DataType type) =>
        !type.Parts().Any(p => p.Kind is PinType.Entity or PinType.Player or PinType.Object or PinType.LivingEntity
            or PinType.DamageSource or PinType.EffectInstance or PinType.World);

    /// <summary>Name of the scope, never translated: "player_persistent", "global_map"...</summary>
    public static string ScopeLabel(GlobalScope scope) => scope switch
    {
        GlobalScope.Map => "global_map",
        GlobalScope.World => "global_dimension",
        GlobalScope.Session => "global_session",
        GlobalScope.PlayerLifetime => "player_lifetime",
        GlobalScope.PlayerPersistent => "player_persistent",
        GlobalScope.PlayerSession => "player_session",
        GlobalScope.PlayerDimension => "player_dimension",
        GlobalScope.PlayerAccount => "player_account",
        GlobalScope.PlayerGuild => "player_guild",
        GlobalScope.PlayerTeam => "player_team",
        GlobalScope.EntityType => "entity_type",
        GlobalScope.EntityOwner => "entity_owner",
        GlobalScope.EntityGroup => "entity_group",
        GlobalScope.EntityDimension => "entity_dimension",
        GlobalScope.GlobalChunk => "global_chunk",
        GlobalScope.GlobalServer => "global_server",
        GlobalScope.WorldStructure => "world_structure",
        GlobalScope.WorldBiome => "world_biome",
        _ => "world_dimension",
    };

    /// <summary>What the scope means (tooltip of the Global tab, descriptions of the nodes).</summary>
    public static string ScopeDescription(GlobalScope scope) => scope switch
    {
        GlobalScope.Map => Tr.T("A value shared by the whole save."),
        GlobalScope.World => Tr.T("A value tied to a dimension."),
        GlobalScope.Session => Tr.T("A temporary value, lost when the game closes."),
        GlobalScope.PlayerLifetime => Tr.T("A value of the player, reset at death."),
        GlobalScope.PlayerPersistent => Tr.T("A value of the player, kept after death."),
        GlobalScope.PlayerSession => Tr.T("A value of the player, kept only while the player is connected."),
        GlobalScope.PlayerDimension => Tr.T("A value of the player, in a dimension."),
        GlobalScope.PlayerAccount => Tr.T("A value of the account of the player, shared by every save."),
        GlobalScope.PlayerGuild => Tr.T("A value of the player, shared by a guild."),
        GlobalScope.PlayerTeam => Tr.T("A value shared by the players of a team."),
        GlobalScope.EntityType => Tr.T("A value shared by every mob of the same type."),
        GlobalScope.EntityOwner => Tr.T("A value tied to the owner of an entity."),
        GlobalScope.EntityGroup => Tr.T("A value shared by a group of entities."),
        GlobalScope.EntityDimension => Tr.T("A value of an entity, in a dimension."),
        GlobalScope.GlobalChunk => Tr.T("A value tied to a chunk."),
        GlobalScope.GlobalServer => Tr.T("A value shared by the whole server."),
        GlobalScope.WorldStructure => Tr.T("A value shared by a structure."),
        GlobalScope.WorldBiome => Tr.T("A value tied to a biome."),
        _ => Tr.T("A value tied to a dimension."),
    };

    public override IEnumerable<AssetReference> GetReferences() =>
        ValueType.Parts().Select(p => p.Definition).OfType<Guid>().Distinct()
            .Select(id => new AssetReference(id, AssetReferenceKind.Definition, Tr.T("variable type")));

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();
        if (ProgramNames.Identifier(VariableName).Length == 0)
        {
            diagnostics.Error(Tr.T("Give the variable a name (letters or digits)."), source, Id);
        }

        if (ValueType.Kind == PinType.Exec || (ValueType.IsCollection && ValueType.Element is null))
        {
            diagnostics.Error(Tr.T("The type of the variable is incomplete."), source, Id);
        }

        if (ValueType.HasInlineValue && !ValueType.IsContentReference && DefaultValue.Length > 0
            && GraphValidator.DescribeValueProblem(new PinInfo("default", Tr.T("Default value"), ValueType), DefaultValue) is { } problem)
        {
            diagnostics.Error(Tr.F("Default value: {0}", problem), source, Id);
        }

        if (IsSaved && !CanBeSaved(ValueType))
        {
            diagnostics.Warning(
                Tr.T("An entity, a player or an object of the project cannot be written in the save: the value is kept only until the server stops."),
                source,
                Id);
        }
    }
}
